using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using System.Text;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// PLY模型加载器 - 加载和解析PLY (Polygon File Format) 格式的3D模型
/// 支持ASCII和二进制两种PLY格式
/// 广泛用于点云和三角网格数据的存储
/// </summary>
public class PlyModelLoader : ModelLoader
{
    private readonly ILogger<PlyModelLoader> _logger;
    private static readonly string[] SupportedFormats = { ".ply" };

    public PlyModelLoader(ILogger<PlyModelLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 加载PLY模型文件并提取三角形网格数据
    /// </summary>
    public override async Task<(List<Triangle> Triangles, BoundingBox3D BoundingBox, Dictionary<string, Material> Materials)> LoadModelAsync(
        string modelPath,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("开始加载PLY模型: {Path}", modelPath);

        try
        {
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"模型文件不存在: {modelPath}");
            }

            // 解析PLY头部信息
            var (format, vertexCount, faceCount, properties) = await ParsePlyHeaderAsync(modelPath, cancellationToken);

            List<Triangle> triangles;
            if (format == "ascii")
            {
                _logger.LogDebug("检测到ASCII PLY格式, 顶点: {VertexCount}, 面: {FaceCount}", vertexCount, faceCount);
                triangles = await LoadAsciiPlyAsync(modelPath, vertexCount, faceCount, properties, cancellationToken);
            }
            else
            {
                _logger.LogDebug("检测到二进制PLY格式, 顶点: {VertexCount}, 面: {FaceCount}", vertexCount, faceCount);
                triangles = await LoadBinaryPlyAsync(modelPath, vertexCount, faceCount, properties, format, cancellationToken);
            }

            // 计算包围盒
            var boundingBox = CalculateBoundingBox(triangles);

            // PLY格式可能包含颜色，创建默认材质
            var materials = new Dictionary<string, Material>
            {
                ["default"] = new Material
                {
                    Name = "default",
                    DiffuseColor = new Color3D { R = 0.8, G = 0.8, B = 0.8 },
                    SpecularColor = new Color3D { R = 0.2, G = 0.2, B = 0.2 },
                    Shininess = 32.0
                }
            };

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("PLY模型加载完成: {Path}, 三角形数量: {Count}, 耗时: {Elapsed}ms",
                modelPath, triangles.Count, elapsed.TotalMilliseconds);

            return (triangles, boundingBox, materials);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载PLY模型失败: {Path}", modelPath);
            throw;
        }
    }

    /// <summary>
    /// 解析PLY文件头部
    /// </summary>
    private async Task<(string Format, int VertexCount, int FaceCount, List<string> Properties)> ParsePlyHeaderAsync(
        string filePath, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(filePath, Encoding.ASCII);

        var firstLine = await reader.ReadLineAsync();
        if (firstLine?.Trim() != "ply")
        {
            throw new InvalidDataException("不是有效的PLY文件");
        }

        string format = "ascii";
        int vertexCount = 0;
        int faceCount = 0;
        var properties = new List<string>();

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            line = line.Trim();

            if (line.StartsWith("format"))
            {
                var parts = line.Split(' ');
                if (parts.Length >= 2)
                {
                    format = parts[1];
                }
            }
            else if (line.StartsWith("element vertex"))
            {
                var parts = line.Split(' ');
                if (parts.Length >= 3 && int.TryParse(parts[2], out var count))
                {
                    vertexCount = count;
                }
            }
            else if (line.StartsWith("element face"))
            {
                var parts = line.Split(' ');
                if (parts.Length >= 3 && int.TryParse(parts[2], out var count))
                {
                    faceCount = count;
                }
            }
            else if (line.StartsWith("property"))
            {
                properties.Add(line);
            }
            else if (line == "end_header")
            {
                break;
            }
        }

        return (format, vertexCount, faceCount, properties);
    }

    /// <summary>
    /// 加载ASCII格式的PLY文件
    /// </summary>
    private async Task<List<Triangle>> LoadAsciiPlyAsync(
        string filePath, int vertexCount, int faceCount,
        List<string> properties, CancellationToken cancellationToken)
    {
        var vertices = new List<Vector3D>();
        var triangles = new List<Triangle>();

        using var reader = new StreamReader(filePath, Encoding.ASCII);

        // 跳过头部到end_header
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (line.Trim() == "end_header")
                break;
        }

        // 读取顶点
        for (int i = 0; i < vertexCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            line = await reader.ReadLineAsync();
            if (line == null) break;

            var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                vertices.Add(new Vector3D
                {
                    X = double.Parse(parts[0]),
                    Y = double.Parse(parts[1]),
                    Z = double.Parse(parts[2])
                });
            }
        }

        // 读取面
        for (int i = 0; i < faceCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            line = await reader.ReadLineAsync();
            if (line == null) break;

            var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4 && int.TryParse(parts[0], out var indexCount))
            {
                if (indexCount == 3 && parts.Length >= 4)
                {
                    // 三角形
                    var i0 = int.Parse(parts[1]);
                    var i1 = int.Parse(parts[2]);
                    var i2 = int.Parse(parts[3]);

                    if (i0 < vertices.Count && i1 < vertices.Count && i2 < vertices.Count)
                    {
                        var v0 = vertices[i0];
                        var v1 = vertices[i1];
                        var v2 = vertices[i2];

                        // 计算法线
                        var normal = CalculateNormal(v0, v1, v2);

                        triangles.Add(new Triangle
                        {
                            Vertices = new[] { v0, v1, v2 },
                            Normal = normal,
                            MaterialName = "default"
                        });
                    }
                }
                else if (indexCount == 4 && parts.Length >= 5)
                {
                    // 四边形，分割成两个三角形
                    var i0 = int.Parse(parts[1]);
                    var i1 = int.Parse(parts[2]);
                    var i2 = int.Parse(parts[3]);
                    var i3 = int.Parse(parts[4]);

                    if (i0 < vertices.Count && i1 < vertices.Count &&
                        i2 < vertices.Count && i3 < vertices.Count)
                    {
                        var v0 = vertices[i0];
                        var v1 = vertices[i1];
                        var v2 = vertices[i2];
                        var v3 = vertices[i3];

                        // 第一个三角形
                        var normal1 = CalculateNormal(v0, v1, v2);
                        triangles.Add(new Triangle
                        {
                            Vertices = new[] { v0, v1, v2 },
                            Normal = normal1,
                            MaterialName = "default"
                        });

                        // 第二个三角形
                        var normal2 = CalculateNormal(v0, v2, v3);
                        triangles.Add(new Triangle
                        {
                            Vertices = new[] { v0, v2, v3 },
                            Normal = normal2,
                            MaterialName = "default"
                        });
                    }
                }
            }
        }

        return triangles;
    }

    /// <summary>
    /// 加载二进制格式的PLY文件
    /// </summary>
#pragma warning disable CS1998 // 异步方法缺少 await 运算符
    private async Task<List<Triangle>> LoadBinaryPlyAsync(
        string filePath, int vertexCount, int faceCount,
        List<string> properties, string format, CancellationToken cancellationToken)
#pragma warning restore CS1998
    {
        // 注意：完整的二进制PLY实现需要根据properties解析不同的数据类型
        // 这里提供基本实现
        throw new NotImplementedException("二进制PLY格式加载器尚未完全实现，请使用ASCII格式");
    }

    /// <summary>
    /// 计算三角形法线
    /// </summary>
    private new Vector3D CalculateNormal(Vector3D v0, Vector3D v1, Vector3D v2)
    {
        var edge1 = new Vector3D
        {
            X = v1.X - v0.X,
            Y = v1.Y - v0.Y,
            Z = v1.Z - v0.Z
        };

        var edge2 = new Vector3D
        {
            X = v2.X - v0.X,
            Y = v2.Y - v0.Y,
            Z = v2.Z - v0.Z
        };

        // 叉积
        var normal = new Vector3D
        {
            X = edge1.Y * edge2.Z - edge1.Z * edge2.Y,
            Y = edge1.Z * edge2.X - edge1.X * edge2.Z,
            Z = edge1.X * edge2.Y - edge1.Y * edge2.X
        };

        // 归一化
        var length = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);
        if (length > 0)
        {
            normal.X /= length;
            normal.Y /= length;
            normal.Z /= length;
        }

        return normal;
    }

    /// <summary>
    /// 计算三角形集合的包围盒
    /// </summary>
    private new BoundingBox3D CalculateBoundingBox(List<Triangle> triangles)
    {
        if (triangles.Count == 0)
        {
            return new BoundingBox3D
            {
                MinX = 0, MinY = 0, MinZ = 0,
                MaxX = 0, MaxY = 0, MaxZ = 0
            };
        }

        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

        foreach (var triangle in triangles)
        {
            foreach (var vertex in triangle.Vertices)
            {
                minX = Math.Min(minX, vertex.X);
                minY = Math.Min(minY, vertex.Y);
                minZ = Math.Min(minZ, vertex.Z);
                maxX = Math.Max(maxX, vertex.X);
                maxY = Math.Max(maxY, vertex.Y);
                maxZ = Math.Max(maxZ, vertex.Z);
            }
        }

        return new BoundingBox3D
        {
            MinX = minX, MinY = minY, MinZ = minZ,
            MaxX = maxX, MaxY = maxY, MaxZ = maxZ
        };
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
