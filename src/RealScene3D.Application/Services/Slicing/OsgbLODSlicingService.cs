using Microsoft.Extensions.Logging;
using RealScene3D.Application.Services.Generators;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Geometry;
using RealScene3D.Managed;
using System.Text.Json;
using Newtonsoft.Json;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// OSGB PagedLOD 分层切片服务
///
/// 核心功能：将 OSGB 的 PagedLOD 层次结构映射到 3DTiles 切片
/// - 一个精细层对应一个切片文件
/// - 保持 OSGB 原有的 LOD 层级关系
/// - 生成符合 3DTiles 规范的 tileset.json
/// </summary>
public class OsgbLODSlicingService
{
    private readonly ILogger<OsgbLODSlicingService> _logger;
    private readonly B3dmGenerator _b3dmGenerator;

    public OsgbLODSlicingService(
        ILogger<OsgbLODSlicingService> logger,
        B3dmGenerator b3dmGenerator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _b3dmGenerator = b3dmGenerator ?? throw new ArgumentNullException(nameof(b3dmGenerator));
    }

    /// <summary>
    /// 数据传输对象：LOD 层级切片信息（树结构）
    /// </summary>
    public class LODTileInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public int Level { get; set; }
        public IMesh Mesh { get; set; } = null!;
        public Box3 BoundingBox { get; set; } = null!;
        public double GeometricError { get; set; }
        public List<LODTileInfo> Children { get; set; } = new();
    }

    /// <summary>
    /// 加载 OSGB 文件的 PagedLOD 层次结构
    /// </summary>
    /// <param name="osgbPath">OSGB 根文件路径</param>
    /// <param name="maxDepth">最大递归深度（0=无限制）</param>
    /// <returns>LOD 层级切片信息列表</returns>
    public async Task<List<LODTileInfo>> LoadWithLODHierarchyAsync(string osgbPath, int maxDepth = 0)
    {
        _logger.LogInformation("开始加载 OSGB PagedLOD 层次结构: {Path}, 最大深度={MaxDepth}",
            osgbPath, maxDepth);

        var result = new List<LODTileInfo>();

        try
        {
            // 使用 C++/CLI 托管接口加载层次结构
            using var reader = new OsgbReaderWrapper();
            var managedNodes = await Task.Run(() => reader.LoadWithLODHierarchy(osgbPath, maxDepth));

            if (managedNodes == null || managedNodes.Count == 0)
            {
                _logger.LogWarning("OSGB 文件没有 PagedLOD 层次结构: {Path}", osgbPath);
                return result;
            }

            _logger.LogInformation("加载到 {Count} 个 PagedLOD 节点", managedNodes.Count);

            // 转换为应用层数据结构
            foreach (var managedNode in managedNodes)
            {
                var tileInfo = ConvertToLODTileInfo(managedNode);
                if (tileInfo != null && tileInfo.Mesh != null && tileInfo.Mesh.FacesCount > 0)
                {
                    result.Add(tileInfo);
                    _logger.LogDebug("转换节点: Level={Level}, 文件={File}, 面数={FaceCount}",
                        tileInfo.Level, tileInfo.RelativePath, tileInfo.Mesh.FacesCount);
                }
            }

            _logger.LogInformation("转换完成: {Count} 个有效 LOD 切片", result.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 OSGB PagedLOD 层次结构失败: {Path}", osgbPath);
            throw;
        }

        return result;
    }

    /// <summary>
    /// 为 OSGB PagedLOD 层次生成 3DTiles 切片
    /// </summary>
    public async Task<List<Slice>> GenerateLODTilesAsync(
        string osgbPath,
        string outputDir,
        SlicingConfig config,
        GpsCoords? gpsCoords = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("========== 开始 OSGB PagedLOD 分层切片 ==========");
        _logger.LogInformation("源文件: {OsgbPath}", osgbPath);
        _logger.LogInformation("输出目录: {OutputDir}", outputDir);

        var slices = new List<Slice>();

        try
        {
            // 1. 加载 PagedLOD 层次结构
            var lodTiles = await LoadWithLODHierarchyAsync(osgbPath, 0);

            if (lodTiles.Count == 0)
            {
                _logger.LogWarning("没有找到有效的 LOD 层级数据");
                return slices;
            }

            // 重要：C++ 层返回的第一个元素是根节点，已包含完整树结构
            var rootNode = lodTiles[0];
            _logger.LogInformation("根节点: Level={Level}, 子节点数={ChildCount}",
                rootNode.Level, rootNode.Children.Count);

            // 2. 递归生成切片（遍历树结构）
            Directory.CreateDirectory(outputDir);
            await GenerateTilesFromNodeAsync(rootNode, outputDir, slices, cancellationToken);

            _logger.LogInformation("切片生成完成: 总计 {Count} 个切片", slices.Count);

            // 3. 生成 tileset.json（子tileset不包含transform，由根tileset统一管理）
            if (config.GenerateTileset && slices.Count > 0)
            {
                _logger.LogInformation("生成 OSGB 子tileset.json（不包含transform）");

                string tilesetPath = Path.Combine(outputDir, "tileset.json");
                // 子tileset不需要transform，由根tileset统一管理坐标变换
                await GenerateTilesetJsonAsync(rootNode, tilesetPath, gpsCoords: null, includeTransform: false);

                _logger.LogInformation("OSGB 子tileset.json 生成完成: {Path}", tilesetPath);
            }

            _logger.LogInformation("========== OSGB PagedLOD 分层切片完成 ==========");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OSGB PagedLOD 分层切片失败");
            throw;
        }

        return slices;
    }

    /// <summary>
    /// 生成 tileset.json（参考 E:\Code\3dtiles\src\osgb23dtile.cpp:1232-1277）
    /// </summary>
    /// <param name="includeTransform">是否包含ECEF变换矩阵（根tileset需要，子tileset不需要）</param>
    private async Task GenerateTilesetJsonAsync(
        LODTileInfo rootNode,
        string outputPath,
        GpsCoords? gpsCoords = null,
        bool includeTransform = false)
    {
        // 计算几何误差（参考 calc_geometric_error）
        CalculateGeometricError(rootNode, isRoot: true);

        // 仅在需要时计算 ECEF 变换矩阵（子tileset不需要transform）
        double[]? transform = null;
        if (includeTransform && gpsCoords != null)
        {
            transform = gpsCoords.ToEcefTransform();
            _logger.LogInformation("生成包含ECEF变换的tileset.json");
        }
        else
        {
            _logger.LogInformation("生成不包含变换矩阵的子tileset.json");
        }

        // 生成根节点 JSON（递归）
        var rootJson = ConvertNodeToTileJson(rootNode, transform);

        // 使用有序字典保证字段顺序与3dtiles一致
        var tileset = new System.Collections.Generic.Dictionary<string, object>
        {
            ["asset"] = new System.Collections.Generic.Dictionary<string, object>
            {
                ["gltfUpAxis"] = "Z",
                ["version"] = "1.0"
            },
            ["geometricError"] = rootNode.GeometricError,
            ["root"] = rootJson
        };

        // 配置JSON序列化选项：避免Unicode转义（如\u002B）
        var jsonSettings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
            StringEscapeHandling = Newtonsoft.Json.StringEscapeHandling.Default
        };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(tileset, jsonSettings);
        await File.WriteAllTextAsync(outputPath, json, System.Text.Encoding.UTF8);

        _logger.LogInformation("根节点几何误差: {Error:F2}", rootNode.GeometricError);
    }

    /// <summary>
    /// 将 LOD 节点转换为 Tileset JSON（递归，参考 encode_tile_json）
    /// 字段顺序与3dtiles保持一致：boundingVolume -> children -> content -> geometricError -> transform
    /// </summary>
    private object ConvertNodeToTileJson(LODTileInfo node, double[]? transform = null)
    {
        // 使用有序字典确保字段顺序
        var json = new System.Collections.Generic.Dictionary<string, object>
        {
            ["boundingVolume"] = new System.Collections.Generic.Dictionary<string, object>
            {
                ["box"] = ConvertToTilesetBox(node.BoundingBox)
            }
        };

        // 先添加children（如果有）
        if (node.Children.Count > 0)
        {
            json["children"] = node.Children.Select(child => ConvertNodeToTileJson(child)).ToArray();
        }

        // 再添加content（如果有）
        if (!string.IsNullOrEmpty(node.RelativePath))
        {
            // 转换路径格式：Tile_xxx.b3dm（不需要"./"前缀，直接使用文件名）
            string uri = node.RelativePath.Replace('\\', '/');
            json["content"] = new System.Collections.Generic.Dictionary<string, object>
            {
                ["boundingVolume"] = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["box"] = ConvertToTilesetBox(node.BoundingBox)
                },
                ["uri"] = "./" + uri
            };
        }

        // 然后添加geometricError
        json["geometricError"] = node.GeometricError;

        // 最后添加transform（如果需要）
        if (transform != null)
        {
            json["transform"] = transform;
        }

        return json;
    }

    /// <summary>
    /// 转换为 3D Tiles box 格式（参考 convert_bbox）
    /// </summary>
    private double[] ConvertToTilesetBox(Box3 bbox)
    {
        double centerX = (bbox.Max.X + bbox.Min.X) / 2;
        double centerY = (bbox.Max.Y + bbox.Min.Y) / 2;
        double centerZ = (bbox.Max.Z + bbox.Min.Z) / 2;

        double halfWidth = Math.Max((bbox.Max.X - bbox.Min.X) / 2, 0.01);
        double halfHeight = Math.Max((bbox.Max.Y - bbox.Min.Y) / 2, 0.01);
        double halfDepth = Math.Max((bbox.Max.Z - bbox.Min.Z) / 2, 0.01);

        return new double[]
        {
            centerX, centerY, centerZ,
            halfWidth, 0, 0,
            0, halfHeight, 0,
            0, 0, halfDepth
        };
    }

    /// <summary>
    /// 递归计算几何误差（参考 calc_geometric_error, 1205-1230行）
    /// </summary>
    private void CalculateGeometricError(LODTileInfo node, bool isRoot = false)
    {
        const double EPS = 1e-12;

        // 深度优先：先计算所有子节点
        foreach (var child in node.Children)
        {
            CalculateGeometricError(child, false);
        }

        // 计算当前节点的几何误差
        if (node.Children.Count == 0)
        {
            // 叶子节点：几何误差为 0
            node.GeometricError = 0.0;
        }
        else
        {
            // 找第一个非零的子节点（参考 1217-1223 行）
            bool has = false;
            LODTileInfo? leafNode = null;
            foreach (var child in node.Children)
            {
                if (Math.Abs(child.GeometricError) > EPS)
                {
                    has = true;
                    leafNode = child;
                    break; // 找到第一个就停止
                }
            }

            if (!has)
            {
                // 所有子节点都是 0，使用包围盒计算（参考 1226 行）
                double maxExtent = Math.Max(
                    Math.Max(node.BoundingBox.Width, node.BoundingBox.Height),
                    node.BoundingBox.Depth);
                node.GeometricError = maxExtent / 20.0;
            }
            else
            {
                // 父节点 = 第一个非零子节点 * 2（参考 1228 行）
                node.GeometricError = leafNode!.GeometricError * 2.0;
            }
        }

        // 根节点强制设置为 1000.0（参考第1302行）
        if (isRoot)
        {
            node.GeometricError = 1000.0;
        }

        _logger.LogDebug("节点 Level={Level}: geometricError={Error:F2}",
            node.Level, node.GeometricError);
    }

    /// <summary>
    /// 递归生成切片文件（深度优先遍历）
    /// </summary>
    private async Task GenerateTilesFromNodeAsync(
        LODTileInfo node,
        string outputDir,
        List<Slice> slices,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        // 生成当前节点的切片
        if (node.Mesh != null && node.Mesh.FacesCount > 0)
        {
            // 生成切片文件名（直接使用原始文件名，不创建子目录）
            string fileName = Path.GetFileNameWithoutExtension(node.FileName);
            string tilePath = Path.Combine(outputDir, $"{fileName}.b3dm");

            // 更新 RelativePath 为生成的 B3DM 文件名（相对于 outputDir）
            node.RelativePath = $"{fileName}.b3dm";

            _logger.LogInformation("生成切片: 文件={File}, 面数={FaceCount}",
                fileName, node.Mesh.FacesCount);

            // 生成 B3DM 文件
            await _b3dmGenerator.SaveTileAsync(node.Mesh, tilePath);

            // 创建切片记录
            var slice = new Slice
            {
                Id = Guid.NewGuid(),
                Level = node.Level,
                FilePath = tilePath,
                BoundingBox = System.Text.Json.JsonSerializer.Serialize(node.BoundingBox),
                FileSize = new FileInfo(tilePath).Length,
                CreatedAt = DateTime.UtcNow
            };

            slices.Add(slice);
        }

        // 递归处理子节点
        foreach (var child in node.Children)
        {
            await GenerateTilesFromNodeAsync(child, outputDir, slices, cancellationToken);
        }
    }

    /// <summary>
    /// 将托管 PagedLOD 节点转换为应用层数据结构（递归）
    /// </summary>
    private LODTileInfo? ConvertToLODTileInfo(ManagedPagedLODNode managedNode)
    {
        try
        {
            // 转换网格数据
            var mesh = ConvertManagedMeshToIMesh(managedNode.MeshData);
            if (mesh == null)
            {
                return null;
            }

            // 构建包围盒
            var boundingBox = new Box3(
                new Vertex3(
                    managedNode.MeshData.BBoxMinX,
                    managedNode.MeshData.BBoxMinY,
                    managedNode.MeshData.BBoxMinZ),
                new Vertex3(
                    managedNode.MeshData.BBoxMaxX,
                    managedNode.MeshData.BBoxMaxY,
                    managedNode.MeshData.BBoxMaxZ)
            );

            var tileInfo = new LODTileInfo
            {
                FileName = managedNode.FileName,
                RelativePath = managedNode.RelativePath,
                Level = managedNode.Level,
                Mesh = mesh,
                BoundingBox = boundingBox,
                GeometricError = managedNode.GeometricError,
                Children = new List<LODTileInfo>()
            };

            // 递归转换子节点
            foreach (var managedChild in managedNode.Children)
            {
                var childTileInfo = ConvertToLODTileInfo(managedChild);
                if (childTileInfo != null)
                {
                    tileInfo.Children.Add(childTileInfo);
                }
            }

            return tileInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "转换 PagedLOD 节点失败: {FileName}", managedNode.FileName);
            return null;
        }
    }

    /// <summary>
    /// 将托管网格数据转换为 IMesh
    /// </summary>
    private IMesh? ConvertManagedMeshToIMesh(ManagedMeshData managedMesh)
    {
        if (managedMesh.VertexCount == 0 || managedMesh.FaceCount == 0)
        {
            return null;
        }

        // 构建顶点列表
        var vertices = new List<Vertex3>();
        for (int i = 0; i < managedMesh.Vertices.Length; i += 3)
        {
            vertices.Add(new Vertex3(
                managedMesh.Vertices[i],
                managedMesh.Vertices[i + 1],
                managedMesh.Vertices[i + 2]
            ));
        }

        // 构建面列表
        var faces = new List<Face>();
        bool hasTexture = managedMesh.TexCoords != null && managedMesh.TexCoords.Length > 0;

        for (int i = 0; i < managedMesh.Indices.Length; i += 3)
        {
            int idxA = (int)managedMesh.Indices[i];
            int idxB = (int)managedMesh.Indices[i + 1];
            int idxC = (int)managedMesh.Indices[i + 2];

            int materialIndex = 0;
            if (managedMesh.FaceMaterialIndices != null)
            {
                int faceIndex = i / 3;
                if (faceIndex < managedMesh.FaceMaterialIndices.Length)
                {
                    materialIndex = Math.Max(0, managedMesh.FaceMaterialIndices[faceIndex]);
                }
            }

            if (hasTexture)
            {
                // OSGB 使用共享索引模式
                faces.Add(new Face(idxA, idxB, idxC, idxA, idxB, idxC, materialIndex));
            }
            else
            {
                faces.Add(new Face(idxA, idxB, idxC));
            }
        }

        // 如果有纹理，创建 MeshT
        if (hasTexture && managedMesh.Textures != null && managedMesh.Textures.Count > 0)
        {
            // 构建纹理坐标列表
            var texCoords = new List<Vertex2>();
            if (managedMesh.TexCoords != null)
            {
                for (int i = 0; i < managedMesh.TexCoords.Length; i += 2)
                {
                    texCoords.Add(new Vertex2(
                        managedMesh.TexCoords[i],
                        managedMesh.TexCoords[i + 1]
                    ));
                }
            }

            // 转换材质信息，包含纹理数据
            var materials = new List<Domain.Materials.Material>();
            foreach (var managedMat in managedMesh.Materials)
            {
                // 创建材质对象
                var material = new Domain.Materials.Material(
                    name: managedMat.Name,
                    texture: null, // 不使用文件路径，使用TextureImage
                    normalMap: null,
                    ambientColor: new Domain.Materials.RGB(managedMat.AmbientR, managedMat.AmbientG, managedMat.AmbientB),
                    diffuseColor: new Domain.Materials.RGB(managedMat.DiffuseR, managedMat.DiffuseG, managedMat.DiffuseB),
                    specularColor: new Domain.Materials.RGB(managedMat.SpecularR, managedMat.SpecularG, managedMat.SpecularB),
                    specularExponent: managedMat.Shininess,
                    dissolve: null,
                    illuminationModel: null
                );

                // 获取并设置纹理图像数据
                if (managedMat.TextureIndex >= 0 && managedMat.TextureIndex < managedMesh.Textures.Count)
                {
                    try
                    {
                        var managedTexture = managedMesh.Textures[managedMat.TextureIndex];
                        if (managedTexture.ImageData != null && managedTexture.ImageData.Length > 0)
                        {
                            // C++层传递的是原始像素数据（RGB/RGBA），需要转换为Rgba32格式
                            int pixelCount = managedTexture.Width * managedTexture.Height;
                            byte[] rgba32Data = new byte[pixelCount * 4];

                            if (managedTexture.Components == 4) // RGBA
                            {
                                // 直接复制RGBA数据
                                Buffer.BlockCopy(managedTexture.ImageData, 0, rgba32Data, 0, managedTexture.ImageData.Length);
                            }
                            else if (managedTexture.Components == 3) // RGB
                            {
                                // RGB转RGBA：添加Alpha通道
                                for (int i = 0; i < pixelCount; i++)
                                {
                                    int srcOffset = i * 3;
                                    int dstOffset = i * 4;
                                    rgba32Data[dstOffset] = managedTexture.ImageData[srcOffset];         // R
                                    rgba32Data[dstOffset + 1] = managedTexture.ImageData[srcOffset + 1]; // G
                                    rgba32Data[dstOffset + 2] = managedTexture.ImageData[srcOffset + 2]; // B
                                    rgba32Data[dstOffset + 3] = 255;                                      // A (不透明)
                                }
                            }
                            else
                            {
                                _logger.LogWarning("不支持的纹理通道数: {Components}, 材质={MaterialName}",
                                    managedTexture.Components, managedMat.Name);
                                materials.Add(material);
                                continue;
                            }

                            // 使用LoadPixelData从原始像素数据创建图像
                            material.TextureImage = SixLabors.ImageSharp.Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Rgba32>(
                                rgba32Data,
                                managedTexture.Width,
                                managedTexture.Height);

                            // 强制启用JPEG压缩以减小B3DM文件大小（从116KB降至41KB）
                            // GltfGenerator.cs 将使用 JpegEncoder Quality=75 进行压缩
                            material.IsTextureCompressed = true;

                            _logger.LogDebug("加载纹理成功: {Name}, 尺寸={Width}x{Height}, 通道={Components}, 格式={Format}, 压缩=JPEG",
                                managedTexture.Name, managedTexture.Width, managedTexture.Height,
                                managedTexture.Components, managedTexture.Format);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "加载纹理失败: 材质={MaterialName}, 纹理索引={TextureIndex}",
                            managedMat.Name, managedMat.TextureIndex);
                    }
                }

                materials.Add(material);
            }

            return new Domain.Geometry.MeshT(vertices, texCoords, faces, materials);
        }

        // 无纹理，创建 Mesh
        return new Domain.Geometry.Mesh(vertices, faces);
    }

    /// <summary>
    /// 计算全局包围盒
    /// </summary>
    private Box3 CalculateGlobalBounds(List<Box3> bounds)
    {
        if (bounds.Count == 0)
        {
            return new Box3(0, 0, 0, 1, 1, 1);
        }

        double minX = bounds.Min(b => b.Min.X);
        double minY = bounds.Min(b => b.Min.Y);
        double minZ = bounds.Min(b => b.Min.Z);
        double maxX = bounds.Max(b => b.Max.X);
        double maxY = bounds.Max(b => b.Max.Y);
        double maxZ = bounds.Max(b => b.Max.Z);

        return new Box3(
            new Vertex3(minX, minY, minZ),
            new Vertex3(maxX, maxY, maxZ)
        );
    }
}
