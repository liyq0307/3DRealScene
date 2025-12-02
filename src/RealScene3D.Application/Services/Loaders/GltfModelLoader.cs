using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Geometry;
using RealScene3D.Domain.Materials;
using SharpGLTF.Schema2;
using Material = RealScene3D.Domain.Materials.Material;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// GLTF/GLB 模型加载器 - 实现 IModelLoader 接口
/// 支持 glTF 2.0 格式的二进制 (.glb) 和文本 (.gltf) 文件
/// 完整支持顶点位置、法线、纹理坐标和PBR材质
/// 直接构建索引化的MeshT网格，避免中间转换开销
/// </summary>
public class GltfModelLoader : ModelLoader
{
    private readonly ILogger<GltfModelLoader> _logger;

    // 支持的文件扩展名
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".glb",
        ".gltf"
    };

    public GltfModelLoader(ILogger<GltfModelLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 检查是否支持指定的文件格式
    /// </summary>
    public override bool SupportsFormat(string extension)
    {
        return SupportedExtensions.Contains(extension);
    }

    /// <summary>
    /// 获取支持的文件格式列表
    /// </summary>
    public override IEnumerable<string> GetSupportedFormats()
    {
        return SupportedExtensions;
    }

    /// <summary>
    /// 加载 GLTF/GLB 模型并构建索引网格（IMesh）
    /// 根据是否有纹理坐标返回 Mesh 或 MeshT
    /// </summary>
    /// <param name="modelPath">模型文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>IMesh 对象和包围盒</returns>
    public override async Task<(IMesh Mesh, Box3 BoundingBox)> LoadModelAsync(
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

            var basePath = Path.GetDirectoryName(modelPath) ?? string.Empty;

            // 提取材质
            var materials = ExtractMaterials(model, basePath);

            // 提取网格数据
            var (vertices, texCoords, faces, boundingBox) = ExtractMeshData(model, materials);

            // 根据是否有纹理数据决定返回 Mesh 还是 MeshT
            IMesh mesh;
            bool hasTexture = texCoords.Count > 0 && materials.Count > 0;

            if (hasTexture)
            {
                // 有纹理: 返回 MeshT
                mesh = new MeshT(vertices, texCoords, faces, materials)
                {
                    Name = Path.GetFileNameWithoutExtension(modelPath)
                };

                _logger.LogInformation("提取完成: 类型=MeshT, 顶点={VertexCount}, 面={FaceCount}, 纹理坐标={TexCoordCount}, 材质={MaterialCount}",
                    vertices.Count, faces.Count, texCoords.Count, materials.Count);
            }
            else
            {
                // 无纹理: 返回 Mesh (YAGNI原则)
                var simpleFaces = faces.Select(f => new Face(f.IndexA, f.IndexB, f.IndexC)).ToList();
                mesh = new Domain.Geometry.Mesh(vertices, simpleFaces)
                {
                    Name = Path.GetFileNameWithoutExtension(modelPath)
                };
                
                _logger.LogInformation("提取完成: 类型=Mesh, 顶点={VertexCount}, 面={FaceCount}",
                    vertices.Count, faces.Count);
            }

            _logger.LogInformation("包围盒: [{MinX:F2},{MinY:F2},{MinZ:F2}] - [{MaxX:F2},{MaxY:F2},{MaxZ:F2}]",
                boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Min.Z,
                boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Max.Z);

            return (mesh, boundingBox);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 GLTF/GLB 模型失败: {Path}", modelPath);
            throw;
        }
    }

    /// <summary>
    /// 提取网格数据
    /// </summary>
    private (List<Vertex3> vertices, List<Vertex2> texCoords, List<Face> faces, Box3 boundingBox) ExtractMeshData(
        ModelRoot model,
        List<Material> materials)
    {
        var vertices = new List<Vertex3>();
        var texCoords = new List<Vertex2>();
        var faces = new List<Face>();

        // 初始化包围盒
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

        // 材质名称到索引的映射
        var materialNameToIndex = materials
            .Select((m, i) => new { m.Name, Index = i })
            .ToDictionary(x => x.Name, x => x.Index);

        // 遍历所有网格
        foreach (var mesh in model.LogicalMeshes)
        {
            foreach (var primitive in mesh.Primitives)
            {
                // 确保是三角形网格
                if (primitive.DrawPrimitiveType != PrimitiveType.TRIANGLES)
                {
                    _logger.LogWarning("跳过非三角形图元类型: {Type}", primitive.DrawPrimitiveType);
                    continue;
                }

                // 获取材质索引
                int materialIndex = 0;
                if (primitive.Material != null)
                {
                    var materialName = primitive.Material.Name ?? $"Material_{primitive.Material.LogicalIndex}";
                    if (materialNameToIndex.TryGetValue(materialName, out int matIdx))
                    {
                        materialIndex = matIdx;
                    }
                }

                // 获取顶点位置访问器
                var positionAccessor = primitive.GetVertexAccessor("POSITION");
                if (positionAccessor == null)
                {
                    _logger.LogWarning("图元缺少 POSITION 属性，跳过");
                    continue;
                }

                // 获取顶点位置数据
                var positions = positionAccessor.AsVector3Array().ToArray();

                // 当前图元的顶点起始索引
                int vertexOffset = vertices.Count;

                // 添加顶点并更新包围盒
                foreach (var pos in positions)
                {
                    vertices.Add(new Vertex3(pos.X, pos.Y, pos.Z));
                    minX = Math.Min(minX, pos.X);
                    minY = Math.Min(minY, pos.Y);
                    minZ = Math.Min(minZ, pos.Z);
                    maxX = Math.Max(maxX, pos.X);
                    maxY = Math.Max(maxY, pos.Y);
                    maxZ = Math.Max(maxZ, pos.Z);
                }

                // 获取纹理坐标数据（可选）
                System.Numerics.Vector2[]? uvs = null;
                var texCoordAccessor = primitive.GetVertexAccessor("TEXCOORD_0");
                if (texCoordAccessor != null)
                {
                    uvs = texCoordAccessor.AsVector2Array().ToArray();
                }

                // 当前图元的UV起始索引
                int texCoordOffset = texCoords.Count;

                // 添加UV坐标（如果有）
                if (uvs != null)
                {
                    foreach (var uv in uvs)
                    {
                        texCoords.Add(new Vertex2(uv.X, uv.Y));
                    }
                }
                else
                {
                    // 如果没有UV，添加默认值
                    for (int i = 0; i < positions.Length; i++)
                    {
                        texCoords.Add(new Vertex2(0, 0));
                    }
                }

                // 获取索引
                var indices = primitive.GetIndices();
                if (indices == null || !indices.Any())
                {
                    _logger.LogWarning("图元缺少索引数据，跳过");
                    continue;
                }

                // 将索引转换为三角形面
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

                    // 创建面（索引需要加上偏移）
                    faces.Add(new Face(
                        vertexOffset + idx0,
                        vertexOffset + idx1,
                        vertexOffset + idx2,
                        texCoordOffset + idx0,
                        texCoordOffset + idx1,
                        texCoordOffset + idx2,
                        materialIndex));
                }
            }
        }

        // 如果没有顶点，使用默认包围盒
        var boundingBox = vertices.Count > 0
            ? new Box3(minX, minY, minZ, maxX, maxY, maxZ)
            : new Box3(0, 0, 0, 0, 0, 0);

        return (vertices, texCoords, faces, boundingBox);
    }

    /// <summary>
    /// 提取glTF模型中的所有材质
    /// </summary>
    private List<Material> ExtractMaterials(ModelRoot model, string basePath)
    {
        var materials = new List<Material>();

        if (model.LogicalMaterials.Count == 0)
        {
            _logger.LogDebug("模型不包含材质定义，使用默认材质");
            materials.Add(CreateDefaultMaterial("default"));
            return materials;
        }

        foreach (var gltfMaterial in model.LogicalMaterials)
        {
            var materialName = gltfMaterial.Name ?? $"Material_{gltfMaterial.LogicalIndex}";
            var material = ConvertGltfMaterial(gltfMaterial, basePath);
            materials.Add(material);
            _logger.LogDebug("提取材质: {Name}", materialName);
        }

        _logger.LogInformation("提取了 {Count} 个材质", materials.Count);
        return materials;
    }

    /// <summary>
    /// 将glTF材质转换为通用Material对象
    /// 完整提取PBR Metallic-Roughness工作流的所有属性
    /// </summary>
    private Material ConvertGltfMaterial(SharpGLTF.Schema2.Material gltfMaterial, string basePath)
    {
        string materialName = gltfMaterial.Name ?? $"Material_{gltfMaterial.LogicalIndex}";
        string? diffuseTexture = null;
        string? normalTexture = null;
        RGB? diffuseColor = null;
        double? dissolve = null;

        try
        {
            // 1. 提取透明度（Alpha模式）
            dissolve = gltfMaterial.Alpha switch
            {
                AlphaMode.OPAQUE => 1.0,
                AlphaMode.MASK => gltfMaterial.AlphaCutoff,
                AlphaMode.BLEND => 0.5, // 默认半透明
                _ => 1.0
            };

            // 2. 使用FindChannel API提取各种通道
            var baseColorChannel = gltfMaterial.FindChannel("BaseColor");
            var normalChannel = gltfMaterial.FindChannel("Normal");

            // 3. 提取BaseColor（漫反射）
            if (baseColorChannel.HasValue)
            {
                var channel = baseColorChannel.Value;

                // 颜色因子
                var parameters = channel.Parameters;
                if (parameters != null && parameters.Count >= 3)
                {
                    var r = GetParameterValue(parameters, 0);
                    var g = GetParameterValue(parameters, 1);
                    var b = GetParameterValue(parameters, 2);
                    diffuseColor = new RGB(r, g, b);

                    // 透明度（如果有第4个参数）
                    if (parameters.Count >= 4)
                    {
                        var a = GetParameterValue(parameters, 3);
                        if (a < 1.0)
                        {
                            dissolve = a;
                        }
                    }
                }

                // 基础颜色纹理
                if (channel.Texture != null)
                {
                    diffuseTexture = GetTexturePathFromChannel(channel, basePath);
                }
            }

            // 4. 提取Normal（法线）
            if (normalChannel.HasValue && normalChannel.Value.Texture != null)
            {
                normalTexture = GetTexturePathFromChannel(normalChannel.Value, basePath);
            }

            _logger.LogDebug("材质提取完成: {Name}, 透明={Opacity:F2}", materialName, dissolve ?? 1.0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("提取材质属性时出错: {Error}，使用基础信息", ex.Message);
        }

        return new Material(
            materialName,
            diffuseTexture,
            normalTexture,
            diffuseColor,    // ambientColor
            diffuseColor,    // diffuseColor
            null,            // specularColor
            null,            // specularExponent
            dissolve);
    }

    /// <summary>
    /// 从MaterialChannel提取纹理路径
    /// 支持外部纹理引用和GLB内嵌纹理导出
    /// </summary>
    private string? GetTexturePathFromChannel(MaterialChannel channel, string basePath)
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
    private string? ExportEmbeddedTexture(Image image, string basePath, int textureIndex)
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
    private double GetParameterValue(IReadOnlyList<IMaterialParameter> parameters, int index)
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