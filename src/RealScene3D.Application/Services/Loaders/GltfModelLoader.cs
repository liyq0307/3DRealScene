using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Enums;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using SharpGLTF.Schema2;
using System.Numerics;
using Material = RealScene3D.Domain.Entities.Material;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// GLTF/GLB 模型加载器 - 实现 IModelLoader 接口
/// 支持 glTF 2.0 格式的二进制 (.glb) 和文本 (.gltf) 文件
/// 完整支持顶点位置、法线、纹理坐标和PBR材质
/// </summary>
public class GltfModelLoader : IModelLoader
{
    private readonly ILogger<GltfModelLoader> _logger;

    // 支持的文件扩展名
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".glb",
        ".gltf"
    };

    // 材质缓存（从glTF材质转换而来）
    private readonly Dictionary<string, Material> _materials = new();

    public GltfModelLoader(ILogger<GltfModelLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 检查是否支持指定的文件格式
    /// </summary>
    public bool SupportsFormat(string extension)
    {
        return SupportedExtensions.Contains(extension);
    }

    /// <summary>
    /// 获取支持的文件格式列表
    /// </summary>
    public IEnumerable<string> GetSupportedFormats()
    {
        return SupportedExtensions;
    }

    /// <summary>
    /// 加载 GLTF/GLB 模型并提取三角形网格、材质信息（包含法线和纹理坐标）
    /// </summary>
    /// <param name="modelPath">模型文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>三角形列表、包围盒和材质字典</returns>
    public async Task<(List<Triangle> Triangles, BoundingBox3D BoundingBox, Dictionary<string, RealScene3D.Domain.Entities.Material> Materials)> LoadModelAsync(
        string modelPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始加载 GLTF/GLB 模型: {Path}", modelPath);

            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"模型文件不存在: {modelPath}");
            }

            var extension = Path.GetExtension(modelPath);
            if (!SupportsFormat(extension))
            {
                throw new NotSupportedException($"不支持的文件格式: {extension}");
            }

            // 使用 SharpGLTF 加载模型
            var model = await Task.Run(() => ModelRoot.Load(modelPath), cancellationToken);

            _logger.LogInformation("已加载 GLTF 模型: {Meshes} 个网格, {Nodes} 个节点, {Materials} 个材质",
                model.LogicalMeshes.Count, model.LogicalNodes.Count, model.LogicalMaterials.Count);

            // 清空材质缓存并提取所有材质
            _materials.Clear();
            ExtractMaterials(model, modelPath);

            // 提取所有三角形
            var triangles = new List<Triangle>();
            var boundingBox = new BoundingBox3D
            {
                MinX = double.MaxValue,
                MinY = double.MaxValue,
                MinZ = double.MaxValue,
                MaxX = double.MinValue,
                MaxY = double.MinValue,
                MaxZ = double.MinValue
            };

            // 遍历所有网格
            foreach (var mesh in model.LogicalMeshes)
            {
                foreach (var primitive in mesh.Primitives)
                {
                    // 确保是三角形网格
                    if (primitive.DrawPrimitiveType != SharpGLTF.Schema2.PrimitiveType.TRIANGLES)
                    {
                        _logger.LogWarning("跳过非三角形图元类型: {Type}", primitive.DrawPrimitiveType);
                        continue;
                    }

                    // 提取三角形（包含法线和纹理坐标）
                    ExtractTrianglesFromPrimitive(primitive, triangles, boundingBox);
                }
            }

            // 如果没有法线数据，自动计算平面法线
            bool hasNormals = triangles.Any(t => t.HasVertexNormals());
            if (!hasNormals)
            {
                _logger.LogInformation("模型未包含法线数据，自动计算平面法线");
                foreach (var triangle in triangles)
                {
                    var normal = triangle.ComputeNormal();
                    triangle.Normal1 = normal;
                    triangle.Normal2 = normal;
                    triangle.Normal3 = normal;
                }
            }

            // 验证包围盒
            if (boundingBox.MinX == double.MaxValue)
            {
                boundingBox = new BoundingBox3D { MinX = 0, MinY = 0, MinZ = 0, MaxX = 0, MaxY = 0, MaxZ = 0 };
            }

            _logger.LogInformation("提取完成: {TriangleCount} 个三角形, 法线={HasNormals}, 纹理坐标={HasTexCoords}, 材质={MaterialCount}",
                triangles.Count,
                triangles.Any(t => t.HasVertexNormals()),
                triangles.Any(t => t.HasUVCoordinates()),
                _materials.Count);
            _logger.LogInformation("包围盒: [{MinX:F2},{MinY:F2},{MinZ:F2}] - [{MaxX:F2},{MaxY:F2},{MaxZ:F2}]",
                boundingBox.MinX, boundingBox.MinY, boundingBox.MinZ,
                boundingBox.MaxX, boundingBox.MaxY, boundingBox.MaxZ);

            return (triangles, boundingBox, _materials);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 GLTF/GLB 模型失败: {Path}", modelPath);
            throw;
        }
    }

    /// <summary>
    /// 从图元中提取三角形（包含法线、纹理坐标和材质）
    /// </summary>
    private void ExtractTrianglesFromPrimitive(
        MeshPrimitive primitive,
        List<Triangle> triangles,
        BoundingBox3D boundingBox)
    {
        // 获取材质名称
        string? materialName = null;
        if (primitive.Material != null)
        {
            materialName = primitive.Material.Name ?? $"Material_{primitive.Material.LogicalIndex}";
        }

        // 获取顶点位置访问器
        var positionAccessor = primitive.GetVertexAccessor("POSITION");
        if (positionAccessor == null)
        {
            _logger.LogWarning("图元缺少 POSITION 属性，跳过");
            return;
        }

        // 获取顶点位置数据
        var positions = positionAccessor.AsVector3Array().ToArray();

        // 获取法线数据（可选）
        Vector3[]? normals = null;
        var normalAccessor = primitive.GetVertexAccessor("NORMAL");
        if (normalAccessor != null)
        {
            normals = normalAccessor.AsVector3Array().ToArray();
            _logger.LogDebug("找到法线数据: {Count} 个法线", normals.Length);
        }

        // 获取纹理坐标数据（可选）
        Vector2[]? texCoords = null;
        var texCoordAccessor = primitive.GetVertexAccessor("TEXCOORD_0");
        if (texCoordAccessor != null)
        {
            texCoords = texCoordAccessor.AsVector2Array().ToArray();
            _logger.LogDebug("找到纹理坐标: {Count} 个UV", texCoords.Length);
        }

        // 获取索引
        var indices = primitive.GetIndices();
        if (indices == null || !indices.Any())
        {
            _logger.LogWarning("图元缺少索引数据，跳过");
            return;
        }

        // 将索引转换为三角形
        var indexArray = indices.ToArray();
        for (int i = 0; i < indexArray.Length; i += 3)
        {
            if (i + 2 >= indexArray.Length)
                break;

            var idx0 = (int)indexArray[i];
            var idx1 = (int)indexArray[i + 1];
            var idx2 = (int)indexArray[i + 2];

            if (idx0 >= positions.Length || idx1 >= positions.Length || idx2 >= positions.Length)
            {
                _logger.LogWarning("索引超出范围: {Idx0}, {Idx1}, {Idx2}", idx0, idx1, idx2);
                continue;
            }

            var v0 = positions[idx0];
            var v1 = positions[idx1];
            var v2 = positions[idx2];

            // 创建三角形
            var triangle = new Triangle(
                new Vector3D(v0.X, v0.Y, v0.Z),
                new Vector3D(v1.X, v1.Y, v1.Z),
                new Vector3D(v2.X, v2.Y, v2.Z)
            );

            // 添加法线（如果有）
            if (normals != null && idx0 < normals.Length && idx1 < normals.Length && idx2 < normals.Length)
            {
                var n0 = normals[idx0];
                var n1 = normals[idx1];
                var n2 = normals[idx2];

                triangle.Normal1 = new Vector3D(n0.X, n0.Y, n0.Z);
                triangle.Normal2 = new Vector3D(n1.X, n1.Y, n1.Z);
                triangle.Normal3 = new Vector3D(n2.X, n2.Y, n2.Z);
            }

            // 添加纹理坐标（如果有）
            if (texCoords != null && idx0 < texCoords.Length && idx1 < texCoords.Length && idx2 < texCoords.Length)
            {
                var uv0 = texCoords[idx0];
                var uv1 = texCoords[idx1];
                var uv2 = texCoords[idx2];

                triangle.UV1 = new Vector2D(uv0.X, uv0.Y);
                triangle.UV2 = new Vector2D(uv1.X, uv1.Y);
                triangle.UV3 = new Vector2D(uv2.X, uv2.Y);
            }

            // 分配材质
            triangle.MaterialName = materialName;

            triangles.Add(triangle);

            // 更新包围盒
            UpdateBoundingBox(boundingBox, v0);
            UpdateBoundingBox(boundingBox, v1);
            UpdateBoundingBox(boundingBox, v2);
        }
    }

    /// <summary>
    /// 更新包围盒
    /// </summary>
    private void UpdateBoundingBox(BoundingBox3D boundingBox, Vector3 point)
    {
        boundingBox.MinX = Math.Min(boundingBox.MinX, point.X);
        boundingBox.MinY = Math.Min(boundingBox.MinY, point.Y);
        boundingBox.MinZ = Math.Min(boundingBox.MinZ, point.Z);

        boundingBox.MaxX = Math.Max(boundingBox.MaxX, point.X);
        boundingBox.MaxY = Math.Max(boundingBox.MaxY, point.Y);
        boundingBox.MaxZ = Math.Max(boundingBox.MaxZ, point.Z);
    }

    /// <summary>
    /// 提取glTF模型中的所有材质
    /// </summary>
    private void ExtractMaterials(ModelRoot model, string modelPath)
    {
        if (model.LogicalMaterials.Count == 0)
        {
            _logger.LogDebug("模型不包含材质定义");
            return;
        }

        var basePath = Path.GetDirectoryName(modelPath) ?? string.Empty;

        foreach (var gltfMaterial in model.LogicalMaterials)
        {
            var materialName = gltfMaterial.Name ?? $"Material_{gltfMaterial.LogicalIndex}";
            var material = ConvertGltfMaterial(gltfMaterial, basePath);

            _materials[materialName] = material;
            _logger.LogDebug("提取材质: {Name}, 纹理数={TextureCount}",
                materialName, material.GetAllTextures().Count());
        }

        _logger.LogInformation("提取了 {Count} 个材质", _materials.Count);
    }

    /// <summary>
    /// 将glTF材质转换为通用Material对象
    /// 完整提取PBR Metallic-Roughness工作流的所有属性
    /// </summary>
    private Material ConvertGltfMaterial(SharpGLTF.Schema2.Material gltfMaterial, string basePath)
    {
        var material = new Material
        {
            Name = gltfMaterial.Name ?? $"Material_{gltfMaterial.LogicalIndex}"
        };

        try
        {
            // 1. 提取透明度（Alpha模式）
            material.Opacity = gltfMaterial.Alpha switch
            {
                SharpGLTF.Schema2.AlphaMode.OPAQUE => 1.0,
                SharpGLTF.Schema2.AlphaMode.MASK => gltfMaterial.AlphaCutoff,
                SharpGLTF.Schema2.AlphaMode.BLEND => 0.5, // 默认半透明
                _ => 1.0
            };

            // 2. 使用FindChannel API提取各种通道
            var baseColorChannel = gltfMaterial.FindChannel("BaseColor");
            var normalChannel = gltfMaterial.FindChannel("Normal");
            var emissiveChannel = gltfMaterial.FindChannel("Emissive");
            var occlusionChannel = gltfMaterial.FindChannel("Occlusion");
            var metallicRoughnessChannel = gltfMaterial.FindChannel("MetallicRoughness");

            // 3. 提取BaseColor（漫反射）
            if (baseColorChannel.HasValue)
            {
                var channel = baseColorChannel.Value;

                // 颜色因子
                var parameters = channel.Parameters;
                if (parameters != null && parameters.Count >= 3)
                {
                    // 提取RGB（注意：Parameters是IReadOnlyList<IMaterialParameter>）
                    var r = GetParameterValue(parameters, 0);
                    var g = GetParameterValue(parameters, 1);
                    var b = GetParameterValue(parameters, 2);

                    material.DiffuseColor = new Color3D(r, g, b);

                    // 透明度（如果有第4个参数）
                    if (parameters.Count >= 4)
                    {
                        var a = GetParameterValue(parameters, 3);
                        if (a < 1.0)
                        {
                            material.Opacity = a;
                        }
                    }
                }

                // 基础颜色纹理
                if (channel.Texture != null)
                {
                    var texturePath = GetTexturePathFromChannel(channel, basePath);
                    if (!string.IsNullOrEmpty(texturePath))
                    {
                        material.DiffuseTexture = new Domain.Entities.TextureInfo
                        {
                            Type = TextureType.Diffuse,
                            FilePath = texturePath
                        };
                    }
                }
            }

            // 4. 提取Normal（法线）
            if (normalChannel.HasValue && normalChannel.Value.Texture != null)
            {
                var texturePath = GetTexturePathFromChannel(normalChannel.Value, basePath);
                if (!string.IsNullOrEmpty(texturePath))
                {
                    material.NormalTexture = new Domain.Entities.TextureInfo
                    {
                        Type = TextureType.Normal,
                        FilePath = texturePath
                    };
                }
            }

            // 5. 提取Emissive（自发光）
            if (emissiveChannel.HasValue)
            {
                var channel = emissiveChannel.Value;

                // 自发光因子
                var parameters = channel.Parameters;
                if (parameters != null && parameters.Count >= 3)
                {
                    var r = GetParameterValue(parameters, 0);
                    var g = GetParameterValue(parameters, 1);
                    var b = GetParameterValue(parameters, 2);

                    if (r > 0 || g > 0 || b > 0)
                    {
                        material.EmissiveColor = new Color3D(r, g, b);
                    }
                }

                // 自发光纹理
                if (channel.Texture != null)
                {
                    var texturePath = GetTexturePathFromChannel(channel, basePath);
                    if (!string.IsNullOrEmpty(texturePath))
                    {
                        material.EmissiveTexture = new Domain.Entities.TextureInfo
                        {
                            Type = TextureType.Emissive,
                            FilePath = texturePath
                        };
                    }
                }
            }

            // 6. 提取MetallicRoughness（金属度-粗糙度组合纹理）
            if (metallicRoughnessChannel.HasValue && metallicRoughnessChannel.Value.Texture != null)
            {
                var texturePath = GetTexturePathFromChannel(metallicRoughnessChannel.Value, basePath);
                if (!string.IsNullOrEmpty(texturePath))
                {
                    // glTF规范: B通道=Metallic, G通道=Roughness
                    material.MetallicTexture = new Domain.Entities.TextureInfo
                    {
                        Type = TextureType.Metallic,
                        FilePath = texturePath
                    };

                    material.RoughnessTexture = new Domain.Entities.TextureInfo
                    {
                        Type = TextureType.Roughness,
                        FilePath = texturePath // 同一张纹理，不同通道
                    };
                }
            }

            // 7. 提取Occlusion（环境光遮蔽）
            if (occlusionChannel.HasValue && occlusionChannel.Value.Texture != null)
            {
                var texturePath = GetTexturePathFromChannel(occlusionChannel.Value, basePath);
                if (!string.IsNullOrEmpty(texturePath))
                {
                    _logger.LogDebug("发现AO贴图: {Path}", texturePath);
                    // TODO: 考虑在Material类中添加OcclusionTexture属性
                }
            }

            _logger.LogDebug("材质提取完成: {Name}, 纹理={TextureCount}, 透明={Opacity:F2}",
                material.Name, material.GetAllTextures().Count(), material.Opacity);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("提取材质属性时出错: {Error}，使用基础信息", ex.Message);
        }

        return material;
    }

    /// <summary>
    /// 从MaterialChannel提取纹理路径
    /// 支持外部纹理引用和GLB内嵌纹理导出
    /// </summary>
    private string? GetTexturePathFromChannel(SharpGLTF.Schema2.MaterialChannel channel, string basePath)
    {
        try
        {
            if (channel.Texture == null)
                return null;

            var image = channel.Texture.PrimaryImage;
            if (image == null)
                return null;

            // 尝试获取图像名称作为文件路径（.gltf格式的外部纹理）
            if (!string.IsNullOrEmpty(image.Name))
            {
                var imageName = image.Name;

                // 如果名称看起来像文件路径，使用它
                if (imageName.Contains(".") || imageName.Contains("/") || imageName.Contains("\\"))
                {
                    if (!Path.IsPathRooted(imageName))
                    {
                        return Path.Combine(basePath, imageName);
                    }
                    return imageName;
                }
            }

            // GLB内嵌纹理 - 导出到临时目录
            if (image.Content.Content.Length > 0)
            {
                _logger.LogDebug("检测到GLB内嵌纹理: Index={Index}, Name={Name}",
                    channel.Texture.LogicalIndex, image.Name ?? "未命名");

                var exportedPath = ExportEmbeddedTexture(image, basePath, channel.Texture.LogicalIndex);
                if (!string.IsNullOrEmpty(exportedPath))
                {
                    _logger.LogInformation("GLB内嵌纹理已导出: {Path}", exportedPath);
                    return exportedPath;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("获取纹理路径失败: {Error}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 导出GLB内嵌纹理到文件系统
    /// </summary>
    /// <param name="image">glTF图像对象</param>
    /// <param name="basePath">基础路径</param>
    /// <param name="textureIndex">纹理索引</param>
    /// <returns>导出的纹理文件路径，失败返回null</returns>
    private string? ExportEmbeddedTexture(SharpGLTF.Schema2.Image image, string basePath, int textureIndex)
    {
        try
        {
            if (image.Content.Content.Length == 0)
            {
                _logger.LogWarning("GLB纹理内容为空: Index={Index}", textureIndex);
                return null;
            }

            // 创建纹理导出目录
            var texturesDir = Path.Combine(basePath, "textures");
            Directory.CreateDirectory(texturesDir);

            // 确定文件扩展名
            var extension = image.Content.MimeType switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/webp" => ".webp",
                "image/bmp" => ".bmp",
                _ => ".bin" // 未知格式使用二进制扩展名
            };

            // 生成文件名（优先使用图像名称，否则使用索引）
            var fileName = !string.IsNullOrEmpty(image.Name)
                ? $"{SanitizeFileName(image.Name)}{extension}"
                : $"texture_{textureIndex}{extension}";

            var exportPath = Path.Combine(texturesDir, fileName);

            // 如果文件已存在，跳过导出
            if (File.Exists(exportPath))
            {
                _logger.LogDebug("纹理文件已存在，跳过导出: {Path}", exportPath);
                return exportPath;
            }

            // 写入二进制数据
            var imageData = image.Content.Content.ToArray();
            File.WriteAllBytes(exportPath, imageData);

            _logger.LogInformation("GLB内嵌纹理导出成功: {Path}, 大小={Size} bytes, 格式={MimeType}",
                exportPath, imageData.Length, image.Content.MimeType);

            return exportPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出GLB内嵌纹理失败: Index={Index}", textureIndex);
            return null;
        }
    }

    /// <summary>
    /// 清理文件名中的非法字符
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());

        // 移除扩展名（如果已存在）
        var extensionIndex = sanitized.LastIndexOf('.');
        if (extensionIndex > 0)
        {
            sanitized = sanitized.Substring(0, extensionIndex);
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "texture" : sanitized;
    }

    /// <summary>
    /// 从IMaterialParameter提取数值
    /// </summary>
    private double GetParameterValue(IReadOnlyList<SharpGLTF.Schema2.IMaterialParameter> parameters, int index)
    {
        if (index >= parameters.Count)
            return 0.0;

        try
        {
            var param = parameters[index];

            // 尝试直接访问Value属性（通过dynamic）
            dynamic dynamicParam = param;
            var value = dynamicParam.Value;

            // 转换为double
            if (value is float floatValue)
                return floatValue;
            else if (value is double doubleValue)
                return doubleValue;
            else if (value is int intValue)
                return intValue;

            return Convert.ToDouble(value);
        }
        catch
        {
            return 0.0;
        }
    }
}