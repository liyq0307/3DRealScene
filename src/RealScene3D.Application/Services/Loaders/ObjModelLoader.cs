using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Geometry;
using RealScene3D.Domain.Materials;
using System.Globalization;
using System.Text;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// OBJ模型加载器 - 加载和解析Wavefront OBJ格式的3D模型
/// 完整支持顶点(v)、法线(vn)、纹理坐标(vt)和面片(f)数据的解析
/// 支持MTL材质文件加载和材质关联
/// 直接构建索引化的MeshT网格，避免中间转换开销
/// </summary>
public class ObjModelLoader : ModelLoader
{
    private readonly ILogger<ObjModelLoader> _logger;
    private static readonly string[] SupportedFormats = { ".obj" };

    public ObjModelLoader(ILogger<ObjModelLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 加载OBJ模型文件并构建索引网格（MeshT）
    /// 算法:逐行解析OBJ文件,提取v(顶点)、vn(法线)、vt(纹理坐标)和f(面片)数据
    /// </summary>
    public override async Task<(MeshT Mesh, Box3 BoundingBox)> LoadModelAsync(
        string modelPath,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("开始加载OBJ模型: {Path}", modelPath);

        try
        {
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"模型文件不存在: {modelPath}");
            }

            var vertices = new List<Vertex3>();
            var normals = new List<Vertex3>();
            var texCoords = new List<Vertex2>();
            var faces = new List<FaceT>();

            // 材质相关
            var materials = new List<Material>();
            var materialNameToIndex = new Dictionary<string, int>();
            int currentMaterialIndex = 0;
            string? basePath = Path.GetDirectoryName(modelPath);

            // 初始化包围盒为极值
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            int lineNumber = 0;
            int vertexCount = 0;
            int normalCount = 0;
            int texCoordCount = 0;
            int faceCount = 0;

            // 使用UTF-8编码读取文件
            using (var reader = new StreamReader(modelPath, Encoding.UTF8))
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    lineNumber++;

                    // 跳过空行和注释
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0)
                        continue;

                    try
                    {
                        switch (parts[0])
                        {
                            case "v": // 顶点坐标
                                if (parts.Length >= 4)
                                {
                                    var vertex = ParseVertex(parts);
                                    vertices.Add(vertex);
                                    vertexCount++;

                                    // 更新包围盒
                                    minX = Math.Min(minX, vertex.X);
                                    minY = Math.Min(minY, vertex.Y);
                                    minZ = Math.Min(minZ, vertex.Z);
                                    maxX = Math.Max(maxX, vertex.X);
                                    maxY = Math.Max(maxY, vertex.Y);
                                    maxZ = Math.Max(maxZ, vertex.Z);
                                }
                                break;

                            case "vn": // 法线向量
                                if (parts.Length >= 4)
                                {
                                    var normal = ParseNormal(parts);
                                    normals.Add(normal);
                                    normalCount++;
                                }
                                break;

                            case "vt": // 纹理坐标
                                if (parts.Length >= 3)
                                {
                                    var texCoord = ParseTexCoord(parts);
                                    texCoords.Add(texCoord);
                                    texCoordCount++;
                                }
                                break;

                            case "f": // 面片(三角形或多边形)
                                if (parts.Length >= 4)
                                {
                                    var facesFromPoly = ParseFace(parts, vertices.Count, texCoords.Count, normals.Count, currentMaterialIndex);
                                    faces.AddRange(facesFromPoly);
                                    faceCount++;
                                }
                                break;

                            case "mtllib": // MTL材质库文件
                                if (parts.Length >= 2 && !string.IsNullOrEmpty(basePath))
                                {
                                    var mtlFileName = string.Join(" ", parts.Skip(1));
                                    var mtlPath = Path.Combine(basePath, mtlFileName);
                                    try
                                    {
                                        _logger.LogDebug("加载MTL文件: {Path}", mtlPath);
                                        var loadedMaterialsEx = await MaterialEx.ReadMtlAsync(mtlPath, basePath, cancellationToken);

                                        // 转换 MaterialEx 为 Material
                                        foreach (var kvp in loadedMaterialsEx)
                                        {
                                            var materialEx = kvp.Value;
                                            var material = new Material(
                                                materialEx.Name,
                                                materialEx.DiffuseTexture?.FilePath,
                                                materialEx.NormalTexture?.FilePath,
                                                materialEx.AmbientColor,
                                                materialEx.DiffuseColor,
                                                materialEx.SpecularColor,
                                                materialEx.SpecularExponent,
                                                materialEx.Dissolve,
                                                materialEx.IlluminationModel
                                            );

                                            materialNameToIndex[kvp.Key] = materials.Count;
                                            materials.Add(material);
                                        }
                                        _logger.LogInformation("MTL文件加载成功: {Count}个材质", loadedMaterialsEx.Count);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning("加载MTL文件失败: {Path}, 错误: {Error}", mtlPath, ex.Message);
                                    }
                                }
                                break;

                            case "usemtl": // 使用材质
                                if (parts.Length >= 2)
                                {
                                    var materialName = string.Join(" ", parts.Skip(1));
                                    if (materialNameToIndex.TryGetValue(materialName, out int matIndex))
                                    {
                                        currentMaterialIndex = matIndex;
                                    }
                                    _logger.LogDebug("切换材质: {Material}", materialName);
                                }
                                break;

                            // 其他类型暂时忽略(o, g, s等)
                            default:
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("解析第{LineNumber}行时出错: {Error}, 行内容: {Line}",
                            lineNumber, ex.Message, line);
                    }
                }
            }

            // 如果没有纹理坐标，添加默认值
            if (texCoords.Count == 0)
            {
                texCoords.Add(new Vertex2(0, 0));
                _logger.LogInformation("模型未包含UV坐标，使用默认值(0,0)");
            }

            // 如果没有材质，创建默认材质
            if (materials.Count == 0)
            {
                materials.Add(CreateDefaultMaterial("default"));
                _logger.LogInformation("模型未包含材质，使用默认材质");
            }

            // 构建包围盒
            var boundingBox = new Box3(
                minX != double.MaxValue ? minX : 0,
                minY != double.MaxValue ? minY : 0,
                minZ != double.MaxValue ? minZ : 0,
                maxX != double.MinValue ? maxX : 0,
                maxY != double.MinValue ? maxY : 0,
                maxZ != double.MinValue ? maxZ : 0);

            // 构建 MeshT
            var mesh = new MeshT(vertices, texCoords, faces, materials)
            {
                Name = Path.GetFileNameWithoutExtension(modelPath)
            };

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "OBJ模型加载完成: 顶点={VertexCount}, 法线={NormalCount}, 纹理坐标={TexCoordCount}, " +
                "面片={FaceCount}, 三角形={TriangleCount}, 材质={MaterialCount}, " +
                "包围盒=[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}], 耗时={Elapsed:F2}秒",
                vertexCount, normalCount, texCoordCount, faceCount, faces.Count, materials.Count,
                boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Min.Z,
                boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Max.Z,
                elapsed.TotalSeconds);

            return (mesh, boundingBox);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载OBJ模型失败: {Path}", modelPath);
            throw;
        }
    }

    /// <summary>
    /// 解析顶点坐标行
    /// 格式: v x y z [w]
    /// </summary>
    private Vertex3 ParseVertex(string[] parts)
    {
        return new Vertex3(
            double.Parse(parts[1], CultureInfo.InvariantCulture),
            double.Parse(parts[2], CultureInfo.InvariantCulture),
            double.Parse(parts[3], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 解析法线向量行
    /// 格式: vn x y z
    /// </summary>
    private Vertex3 ParseNormal(string[] parts)
    {
        return new Vertex3(
            double.Parse(parts[1], CultureInfo.InvariantCulture),
            double.Parse(parts[2], CultureInfo.InvariantCulture),
            double.Parse(parts[3], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 解析纹理坐标行
    /// 格式: vt u v [w]
    /// </summary>
    private Vertex2 ParseTexCoord(string[] parts)
    {
        return new Vertex2(
            double.Parse(parts[1], CultureInfo.InvariantCulture),
            parts.Length > 2 ? double.Parse(parts[2], CultureInfo.InvariantCulture) : 0.0);
    }

    /// <summary>
    /// 解析面片行并转换为三角形面
    /// 格式: f v1[/vt1][/vn1] v2[/vt2][/vn2] v3[/vt3][/vn3] [v4[/vt4][/vn4] ...]
    /// 支持多种格式:
    /// - f v1 v2 v3 (仅顶点)
    /// - f v1/vt1 v2/vt2 v3/vt3 (顶点和纹理)
    /// - f v1//vn1 v2//vn2 v3//vn3 (顶点和法线)
    /// - f v1/vt1/vn1 v2/vt2/vn2 v3/vt3/vn3 (完整)
    /// 支持三角形和多边形(自动三角化)
    /// </summary>
    private List<FaceT> ParseFace(
        string[] parts,
        int vertexCount,
        int texCoordCount,
        int normalCount,
        int materialIndex)
    {
        var faces = new List<FaceT>();

        // 解析顶点索引
        var faceVertices = new List<(int v, int vt)>();
        for (int i = 1; i < parts.Length; i++)
        {
            var (vIdx, vtIdx, _) = ParseFaceVertex(parts[i], vertexCount, texCoordCount, normalCount);
            faceVertices.Add((vIdx, vtIdx));
        }

        // 三角化:扇形三角化算法
        // 对于n边形,生成(n-2)个三角形
        if (faceVertices.Count >= 3)
        {
            for (int i = 1; i < faceVertices.Count - 1; i++)
            {
                var (v0, vt0) = faceVertices[0];
                var (v1, vt1) = faceVertices[i];
                var (v2, vt2) = faceVertices[i + 1];

                faces.Add(new FaceT(v0, v1, v2, vt0, vt1, vt2, materialIndex));
            }
        }

        return faces;
    }

    /// <summary>
    /// 解析单个面片顶点
    /// 格式: v[/vt][/vn]
    /// 返回: (顶点索引, 纹理坐标索引, 法线索引)
    /// OBJ 索引从1开始，转换为从0开始
    /// </summary>
    private (int vIdx, int vtIdx, int vnIdx) ParseFaceVertex(
        string vertexStr,
        int vertexCount,
        int texCoordCount,
        int normalCount)
    {
        var parts = vertexStr.Split('/');
        int vIdx = 0, vtIdx = 0, vnIdx = 0;

        // 解析顶点索引
        if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
        {
            vIdx = int.Parse(parts[0]);
            vIdx = vIdx < 0 ? vertexCount + vIdx : vIdx - 1; // 转换为从0开始
        }

        // 解析纹理坐标索引
        if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) && texCoordCount > 0)
        {
            vtIdx = int.Parse(parts[1]);
            vtIdx = vtIdx < 0 ? texCoordCount + vtIdx : vtIdx - 1;
        }

        // 解析法线索引
        if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]) && normalCount > 0)
        {
            vnIdx = int.Parse(parts[2]);
            vnIdx = vnIdx < 0 ? normalCount + vnIdx : vnIdx - 1;
        }

        return (vIdx, vtIdx, vnIdx);
    }

    /// <summary>
    /// 检查是否支持指定的文件格式
    /// </summary>
    public override bool SupportsFormat(string extension)
    {
        return SupportedFormats.Contains(extension.ToLowerInvariant());
    }

    /// <summary>
    /// 获取支持的所有文件格式
    /// </summary>
    public override IEnumerable<string> GetSupportedFormats()
    {
        return SupportedFormats;
    }
}
