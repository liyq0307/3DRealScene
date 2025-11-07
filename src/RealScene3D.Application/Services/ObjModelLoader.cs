using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using System.Globalization;
using System.Text;

namespace RealScene3D.Application.Services;

/// <summary>
/// OBJ模型加载器 - 加载和解析Wavefront OBJ格式的3D模型
/// 支持顶点、法线、纹理坐标和面片数据的解析
/// 用于提取三角形网格数据供切片处理使用
/// </summary>
public class ObjModelLoader : IModelLoader
{
    private readonly ILogger<ObjModelLoader> _logger;
    private static readonly string[] SupportedFormats = { ".obj" };

    public ObjModelLoader(ILogger<ObjModelLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 加载OBJ模型文件并提取三角形网格数据
    /// 算法:逐行解析OBJ文件,提取v(顶点)和f(面片)数据
    /// </summary>
    public async Task<(List<Triangle> Triangles, BoundingBox3D BoundingBox)> LoadModelAsync(
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
            var triangles = new List<Triangle>();

            // 初始化包围盒为极值
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            int lineNumber = 0;
            int vertexCount = 0;
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

                            case "f": // 面片(三角形或多边形)
                                if (parts.Length >= 4)
                                {
                                    var faceTriangles = ParseFace(parts, vertices);
                                    triangles.AddRange(faceTriangles);
                                    faceCount++;
                                }
                                break;

                            // 暂时忽略其他类型(vt, vn, o, g, s, mtllib, usemtl等)
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

            // 构建包围盒
            var boundingBox = new BoundingBox3D
            {
                MinX = minX,
                MinY = minY,
                MinZ = minZ,
                MaxX = maxX,
                MaxY = maxY,
                MaxZ = maxZ
            };

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("OBJ模型加载完成: 顶点={VertexCount}, 面片={FaceCount}, 三角形={TriangleCount}, " +
                "包围盒=[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}], 耗时={Elapsed:F2}秒",
                vertexCount, faceCount, triangles.Count,
                minX, minY, minZ, maxX, maxY, maxZ, elapsed.TotalSeconds);

            return (triangles, boundingBox);
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
    /// 解析面片行并转换为三角形
    /// 格式: f v1[/vt1][/vn1] v2[/vt2][/vn2] v3[/vt3][/vn3] [v4[/vt4][/vn4] ...]
    /// 支持三角形和多边形(自动三角化)
    /// </summary>
    private List<Triangle> ParseFace(string[] parts, List<Vector3D> vertices)
    {
        var triangles = new List<Triangle>();

        // 提取顶点索引
        var vertexIndices = new List<int>();
        for (int i = 1; i < parts.Length; i++)
        {
            var indexStr = parts[i].Split('/')[0]; // 只取顶点索引,忽略纹理和法线索引
            var index = int.Parse(indexStr);

            // OBJ索引从1开始,转换为0基索引
            // 负索引表示相对于当前顶点列表末尾的偏移
            if (index < 0)
                index = vertices.Count + index + 1;

            vertexIndices.Add(index - 1);
        }

        // 三角化:扇形三角化算法
        // 对于n边形,生成(n-2)个三角形
        if (vertexIndices.Count >= 3)
        {
            for (int i = 1; i < vertexIndices.Count - 1; i++)
            {
                var v0 = vertices[vertexIndices[0]];
                var v1 = vertices[vertexIndices[i]];
                var v2 = vertices[vertexIndices[i + 1]];

                triangles.Add(new Triangle
                {
                    Vertices = new[] { v0, v1, v2 }
                });
            }
        }

        return triangles;
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
}
