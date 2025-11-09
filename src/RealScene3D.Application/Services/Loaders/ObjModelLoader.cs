using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using System.Globalization;
using System.Text;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// OBJ模型加载器 - 加载和解析Wavefront OBJ格式的3D模型
/// 完整支持顶点(v)、法线(vn)、纹理坐标(vt)和面片(f)数据的解析
/// 支持MTL材质文件加载和材质关联
/// 用于提取三角形网格数据供切片处理使用
/// </summary>
public class ObjModelLoader : IModelLoader
{
    private readonly ILogger<ObjModelLoader> _logger;
    private readonly MtlParser _mtlParser;
    private static readonly string[] SupportedFormats = { ".obj" };

    public ObjModelLoader(ILogger<ObjModelLoader> logger, MtlParser mtlParser)
    {
        _logger = logger;
        _mtlParser = mtlParser;
    }

    /// <summary>
    /// 加载OBJ模型文件并提取三角形网格数据、材质信息
    /// 算法:逐行解析OBJ文件,提取v(顶点)、vn(法线)、vt(纹理坐标)和f(面片)数据
    /// </summary>
    public async Task<(List<Triangle> Triangles, BoundingBox3D BoundingBox, Dictionary<string, Material> Materials)> LoadModelAsync(
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

            var vertices = new List<Vector3D>();
            var normals = new List<Vector3D>();
            var texCoords = new List<Vector2D>();
            var triangles = new List<Triangle>();

            // 材质相关
            var materials = new Dictionary<string, Material>();
            string? currentMaterialName = null;
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
                                    var faceTriangles = ParseFace(parts, vertices, texCoords, normals);
                                    // 为三角形分配当前材质
                                    foreach (var triangle in faceTriangles)
                                    {
                                        triangle.MaterialName = currentMaterialName;
                                    }
                                    triangles.AddRange(faceTriangles);
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
                                        var loadedMaterials = await _mtlParser.ParseMtlFileAsync(mtlPath, basePath, cancellationToken);
                                        foreach (var kvp in loadedMaterials)
                                        {
                                            materials[kvp.Key] = kvp.Value;
                                        }
                                        _logger.LogInformation("MTL文件加载成功: {Count}个材质", loadedMaterials.Count);
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
                                    currentMaterialName = string.Join(" ", parts.Skip(1));
                                    _logger.LogDebug("切换材质: {Material}", currentMaterialName);
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

            // 构建包围盒
            var boundingBox = new BoundingBox3D
            {
                MinX = minX != double.MaxValue ? minX : 0,
                MinY = minY != double.MaxValue ? minY : 0,
                MinZ = minZ != double.MaxValue ? minZ : 0,
                MaxX = maxX != double.MinValue ? maxX : 0,
                MaxY = maxY != double.MinValue ? maxY : 0,
                MaxZ = maxZ != double.MinValue ? maxZ : 0
            };

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "OBJ模型加载完成: 顶点={VertexCount}, 法线={NormalCount}, 纹理坐标={TexCoordCount}, " +
                "面片={FaceCount}, 三角形={TriangleCount}, 材质={MaterialCount}, " +
                "包围盒=[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}], 耗时={Elapsed:F2}秒",
                vertexCount, normalCount, texCoordCount, faceCount, triangles.Count, materials.Count,
                boundingBox.MinX, boundingBox.MinY, boundingBox.MinZ,
                boundingBox.MaxX, boundingBox.MaxY, boundingBox.MaxZ,
                elapsed.TotalSeconds);

            return (triangles, boundingBox, materials);
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
    private Vector3D ParseVertex(string[] parts)
    {
        return new Vector3D
        {
            X = double.Parse(parts[1], CultureInfo.InvariantCulture),
            Y = double.Parse(parts[2], CultureInfo.InvariantCulture),
            Z = double.Parse(parts[3], CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// 解析法线向量行
    /// 格式: vn x y z
    /// </summary>
    private Vector3D ParseNormal(string[] parts)
    {
        return new Vector3D
        {
            X = double.Parse(parts[1], CultureInfo.InvariantCulture),
            Y = double.Parse(parts[2], CultureInfo.InvariantCulture),
            Z = double.Parse(parts[3], CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// 解析纹理坐标行
    /// 格式: vt u v [w]
    /// </summary>
    private Vector2D ParseTexCoord(string[] parts)
    {
        return new Vector2D
        {
            U = double.Parse(parts[1], CultureInfo.InvariantCulture),
            V = parts.Length > 2 ? double.Parse(parts[2], CultureInfo.InvariantCulture) : 0.0
        };
    }

    /// <summary>
    /// 解析面片行并转换为三角形
    /// 格式: f v1[/vt1][/vn1] v2[/vt2][/vn2] v3[/vt3][/vn3] [v4[/vt4][/vn4] ...]
    /// 支持多种格式:
    /// - f v1 v2 v3 (仅顶点)
    /// - f v1/vt1 v2/vt2 v3/vt3 (顶点和纹理)
    /// - f v1//vn1 v2//vn2 v3//vn3 (顶点和法线)
    /// - f v1/vt1/vn1 v2/vt2/vn2 v3/vt3/vn3 (完整)
    /// 支持三角形和多边形(自动三角化)
    /// </summary>
    private List<Triangle> ParseFace(
        string[] parts,
        List<Vector3D> vertices,
        List<Vector2D> texCoords,
        List<Vector3D> normals)
    {
        var triangles = new List<Triangle>();

        // 解析顶点数据结构
        var faceVertices = new List<FaceVertex>();
        for (int i = 1; i < parts.Length; i++)
        {
            var faceVertex = ParseFaceVertex(parts[i], vertices, texCoords, normals);
            faceVertices.Add(faceVertex);
        }

        // 三角化:扇形三角化算法
        // 对于n边形,生成(n-2)个三角形
        if (faceVertices.Count >= 3)
        {
            for (int i = 1; i < faceVertices.Count - 1; i++)
            {
                var fv0 = faceVertices[0];
                var fv1 = faceVertices[i];
                var fv2 = faceVertices[i + 1];

                var triangle = new Triangle(fv0.Vertex, fv1.Vertex, fv2.Vertex);

                // 设置纹理坐标（如果有）
                if (fv0.TexCoord != null && fv1.TexCoord != null && fv2.TexCoord != null)
                {
                    triangle.UV1 = fv0.TexCoord;
                    triangle.UV2 = fv1.TexCoord;
                    triangle.UV3 = fv2.TexCoord;
                }

                // 设置顶点法线（如果有）
                if (fv0.Normal != null && fv1.Normal != null && fv2.Normal != null)
                {
                    triangle.Normal1 = fv0.Normal;
                    triangle.Normal2 = fv1.Normal;
                    triangle.Normal3 = fv2.Normal;
                }

                triangles.Add(triangle);
            }
        }

        return triangles;
    }

    /// <summary>
    /// 解析单个面片顶点
    /// 格式: v[/vt][/vn]
    /// </summary>
    private FaceVertex ParseFaceVertex(
        string vertexStr,
        List<Vector3D> vertices,
        List<Vector2D> texCoords,
        List<Vector3D> normals)
    {
        var parts = vertexStr.Split('/');
        var faceVertex = new FaceVertex();

        // 解析顶点索引
        if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
        {
            var vIndex = int.Parse(parts[0]);
            vIndex = vIndex < 0 ? vertices.Count + vIndex + 1 : vIndex;
            faceVertex.Vertex = vertices[vIndex - 1];
        }

        // 解析纹理坐标索引
        if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) && texCoords.Count > 0)
        {
            var vtIndex = int.Parse(parts[1]);
            vtIndex = vtIndex < 0 ? texCoords.Count + vtIndex + 1 : vtIndex;
            faceVertex.TexCoord = texCoords[vtIndex - 1];
        }

        // 解析法线索引
        if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]) && normals.Count > 0)
        {
            var vnIndex = int.Parse(parts[2]);
            vnIndex = vnIndex < 0 ? normals.Count + vnIndex + 1 : vnIndex;
            faceVertex.Normal = normals[vnIndex - 1];
        }

        return faceVertex;
    }

    /// <summary>
    /// 检查是否支持指定的文件格式
    /// </summary>
    public bool SupportsFormat(string extension)
    {
        return SupportedFormats.Contains(extension.ToLowerInvariant());
    }

    /// <summary>
    /// 获取支持的所有文件格式
    /// </summary>
    public IEnumerable<string> GetSupportedFormats()
    {
        return SupportedFormats;
    }

    /// <summary>
    /// 面片顶点数据结构 - 用于临时存储顶点、纹理坐标和法线
    /// </summary>
    private class FaceVertex
    {
        public Vector3D Vertex { get; set; } = new Vector3D();
        public Vector2D? TexCoord { get; set; }
        public Vector3D? Normal { get; set; }
    }
}
