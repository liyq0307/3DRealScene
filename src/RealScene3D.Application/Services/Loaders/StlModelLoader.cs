using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Geometry;
using RealScene3D.Domain.Materials;
using System.Text;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// STL模型加载器 - 加载和解析STL (Stereolithography) 格式的3D模型
/// 支持ASCII和二进制两种STL格式
/// 用于3D打印、CAD等领域的模型数据提取
/// </summary>
public class StlModelLoader : ModelLoader
{
    private readonly ILogger<StlModelLoader> _logger;
    private static readonly string[] SupportedFormats = [".stl"];

    public StlModelLoader(ILogger<StlModelLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 加载STL模型文件并构建索引网格（IMesh）
    /// </summary>
    public override async Task<(IMesh Mesh, Box3 BoundingBox)> LoadModelAsync(
        string modelPath,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("开始加载STL模型: {Path}", modelPath);

        try
        {
            ValidateFileExists(modelPath);
            ValidateFileExtension(modelPath);

            // 检测是ASCII还是二进制格式
            var isAscii = await IsAsciiStlAsync(modelPath, cancellationToken);

            IMesh mesh;
            if (isAscii)
            {
                _logger.LogDebug("检测到ASCII STL格式");
                mesh = await LoadAsciiStlAsync(modelPath, cancellationToken);
            }
            else
            {
                _logger.LogDebug("检测到二进制STL格式");
                mesh = await LoadBinaryStlAsync(modelPath, cancellationToken);
            }

            // 计算包围盒
            var boundingBox = CalculateBoundingBox(mesh);

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("STL模型加载完成: {Path}, 三角形数量: {Count}, 耗时: {Elapsed}ms",
                modelPath, mesh.Faces.Count, elapsed.TotalMilliseconds);

            return (mesh, boundingBox);
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
    private static async Task<bool> IsAsciiStlAsync(string filePath, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(filePath, Encoding.ASCII);
        var firstLine = await reader.ReadLineAsync(cancellationToken);
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
    private async Task<Mesh> LoadAsciiStlAsync(string filePath, CancellationToken cancellationToken)
    {
        var vertices = new List<Vertex3>();
        var faces = new List<Face>();

        using var reader = new StreamReader(filePath, Encoding.ASCII);
        string? line;
        Vector3d? normal = null;
        var tempVertices = new List<Vector3d>();

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            line = line.Trim();

            if (line.StartsWith("facet normal", StringComparison.OrdinalIgnoreCase))
            {
                // 解析法线
                var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5)
                {
                    normal = new Vector3d(
                        double.Parse(parts[2]),
                        double.Parse(parts[3]),
                        double.Parse(parts[4])
                    );
                }
            }
            else if (line.StartsWith("vertex", StringComparison.OrdinalIgnoreCase))
            {
                // 解析顶点
                var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    tempVertices.Add(new Vector3d(
                        double.Parse(parts[1]),
                        double.Parse(parts[2]),
                        double.Parse(parts[3])
                    ));
                }
            }
            else if (line.StartsWith("endfacet", StringComparison.OrdinalIgnoreCase))
            {
                // 创建三角形面
                if (tempVertices.Count == 3)
                {
                    // 添加顶点并创建面
                    var v1Idx = vertices.Count;
                    vertices.Add(new Vertex3(tempVertices[0].x, tempVertices[0].y, tempVertices[0].z));
                    vertices.Add(new Vertex3(tempVertices[1].x, tempVertices[1].y, tempVertices[1].z));
                    vertices.Add(new Vertex3(tempVertices[2].x, tempVertices[2].y, tempVertices[2].z));

                    // 创建无纹理的面 (YAGNI原则 - STL不支持纹理)
                    faces.Add(new Face(v1Idx, v1Idx + 1, v1Idx + 2));
                }
                tempVertices.Clear();
                normal = null;
            }
        }

        return new Mesh(vertices, faces);
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
#pragma warning disable CS1998 // 异步方法缺少 await 运算符
    private Task<Mesh> LoadBinaryStlAsync(string filePath, CancellationToken cancellationToken)
#pragma warning restore CS1998
    {
        var vertices = new List<Vertex3>();
        var faces = new List<Face>();

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

            // 读取法线（暂不使用）
            var normalX = reader.ReadSingle();
            var normalY = reader.ReadSingle();
            var normalZ = reader.ReadSingle();

            // 读取3个顶点并添加到网格
            var v1Idx = vertices.Count;
            for (int j = 0; j < 3; j++)
            {
                var x = reader.ReadSingle();
                var y = reader.ReadSingle();
                var z = reader.ReadSingle();
                vertices.Add(new Vertex3(x, y, z));
            }

            // 跳过属性字节
            reader.ReadUInt16();

            // 创建无纹理的面 (YAGNI原则 - STL不支持纹理)
            faces.Add(new Face(v1Idx, v1Idx + 1, v1Idx + 2));
        }

        return Task.FromResult(new Mesh(vertices, faces));
    }

    /// <summary>
    /// 计算网格的包围盒
    /// </summary>
    private static Box3 CalculateBoundingBox(IMesh mesh)
    {
        if (mesh.Vertices.Count == 0)
        {
            return new Box3(new Vertex3(0, 0, 0), new Vertex3(0, 0, 0));
        }

        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

        foreach (var vertex in mesh.Vertices)
        {
            minX = Math.Min(minX, vertex.X);
            minY = Math.Min(minY, vertex.Y);
            minZ = Math.Min(minZ, vertex.Z);
            maxX = Math.Max(maxX, vertex.X);
            maxY = Math.Max(maxY, vertex.Y);
            maxZ = Math.Max(maxZ, vertex.Z);
        }

        return new Box3(new Vertex3(minX, minY, minZ), new Vertex3(maxX, maxY, maxZ));
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
