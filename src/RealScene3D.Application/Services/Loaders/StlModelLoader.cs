using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using System.Text;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// STL模型加载器 - 加载和解析STL (Stereolithography) 格式的3D模型
/// 支持ASCII和二进制两种STL格式
/// 用于3D打印、CAD等领域的模型数据提取
/// </summary>
public class StlModelLoader : IModelLoader
{
    private readonly ILogger<StlModelLoader> _logger;
    private static readonly string[] SupportedFormats = { ".stl" };

    public StlModelLoader(ILogger<StlModelLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 加载STL模型文件并提取三角形网格数据
    /// </summary>
    public async Task<(List<Triangle> Triangles, BoundingBox3D BoundingBox, Dictionary<string, Material> Materials)> LoadModelAsync(
        string modelPath,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("开始加载STL模型: {Path}", modelPath);

        try
        {
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"模型文件不存在: {modelPath}");
            }

            // 检测是ASCII还是二进制格式
            var isAscii = await IsAsciiStlAsync(modelPath);

            List<Triangle> triangles;
            if (isAscii)
            {
                _logger.LogDebug("检测到ASCII STL格式");
                triangles = await LoadAsciiStlAsync(modelPath, cancellationToken);
            }
            else
            {
                _logger.LogDebug("检测到二进制STL格式");
                triangles = await LoadBinaryStlAsync(modelPath, cancellationToken);
            }

            // 计算包围盒
            var boundingBox = CalculateBoundingBox(triangles);

            // STL格式没有材质信息，创建默认材质
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
            _logger.LogInformation("STL模型加载完成: {Path}, 三角形数量: {Count}, 耗时: {Elapsed}ms",
                modelPath, triangles.Count, elapsed.TotalMilliseconds);

            return (triangles, boundingBox, materials);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载STL模型失败: {Path}", modelPath);
            throw;
        }
    }

    /// <summary>
    /// 检测STL文件是ASCII还是二进制格式
    /// </summary>
    private async Task<bool> IsAsciiStlAsync(string filePath)
    {
        using var reader = new StreamReader(filePath, Encoding.ASCII);
        var firstLine = await reader.ReadLineAsync();
        return firstLine?.Trim().StartsWith("solid", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    /// <summary>
    /// 加载ASCII格式的STL文件
    /// 格式:
    /// solid name
    ///   facet normal nx ny nz
    ///     outer loop
    ///       vertex x1 y1 z1
    ///       vertex x2 y2 z2
    ///       vertex x3 y3 z3
    ///     endloop
    ///   endfacet
    /// endsolid name
    /// </summary>
    private async Task<List<Triangle>> LoadAsciiStlAsync(string filePath, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        using var reader = new StreamReader(filePath, Encoding.ASCII);
        string? line;
        Vector3D? normal = null;
        var vertices = new List<Vector3D>();

        while ((line = await reader.ReadLineAsync()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            line = line.Trim();

            if (line.StartsWith("facet normal"))
            {
                // 解析法线
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5)
                {
                    normal = new Vector3D
                    {
                        X = double.Parse(parts[2]),
                        Y = double.Parse(parts[3]),
                        Z = double.Parse(parts[4])
                    };
                }
            }
            else if (line.StartsWith("vertex"))
            {
                // 解析顶点
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    vertices.Add(new Vector3D
                    {
                        X = double.Parse(parts[1]),
                        Y = double.Parse(parts[2]),
                        Z = double.Parse(parts[3])
                    });
                }
            }
            else if (line.StartsWith("endfacet"))
            {
                // 创建三角形
                if (vertices.Count == 3 && normal != null)
                {
                    triangles.Add(new Triangle
                    {
                        Vertices = vertices.ToArray(),
                        Normal = normal,
                        MaterialName = "default"
                    });
                }
                vertices.Clear();
                normal = null;
            }
        }

        return triangles;
    }

    /// <summary>
    /// 加载二进制格式的STL文件
    /// 格式:
    /// - 80字节头部
    /// - 4字节三角形数量(uint32)
    /// - 每个三角形50字节:
    ///   - 12字节法线(3个float32)
    ///   - 36字节顶点(3个顶点,每个12字节)
    ///   - 2字节属性(uint16)
    /// </summary>
    private async Task<List<Triangle>> LoadBinaryStlAsync(string filePath, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);

        // 跳过80字节头部
        reader.ReadBytes(80);

        // 读取三角形数量
        var triangleCount = reader.ReadUInt32();
        _logger.LogDebug("二进制STL包含 {Count} 个三角形", triangleCount);

        for (uint i = 0; i < triangleCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 读取法线
            var normal = new Vector3D
            {
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Z = reader.ReadSingle()
            };

            // 读取3个顶点
            var vertices = new Vector3D[3];
            for (int j = 0; j < 3; j++)
            {
                vertices[j] = new Vector3D
                {
                    X = reader.ReadSingle(),
                    Y = reader.ReadSingle(),
                    Z = reader.ReadSingle()
                };
            }

            // 跳过属性字节
            reader.ReadUInt16();

            triangles.Add(new Triangle
            {
                Vertices = vertices,
                Normal = normal,
                MaterialName = "default"
            });
        }

        return triangles;
    }

    /// <summary>
    /// 计算三角形集合的包围盒
    /// </summary>
    private BoundingBox3D CalculateBoundingBox(List<Triangle> triangles)
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
