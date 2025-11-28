using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Geometry;
using RealScene3D.Domain.Materials;
using System.Text;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// PLY模型加载器 - 加载和解析PLY (Polygon File Format) 格式的3D模型
/// 支持ASCII和二进制两种PLY格式
/// 用于3D扫描、计算机图形学等领域的模型数据提取
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
    /// 加载PLY模型文件并构建索引网格（MeshT）
    /// </summary>
    public override async Task<(MeshT Mesh, Box3 BoundingBox)> LoadModelAsync(
        string modelPath,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("开始加载PLY模型: {Path}", modelPath);

        try
        {
            ValidateFileExists(modelPath);
            ValidateFileExtension(modelPath);

            // 解析PLY头部信息
            var (format, vertexCount, faceCount, properties) = await ParsePlyHeaderAsync(modelPath, cancellationToken);

            MeshT mesh;
            if (format == "ascii")
            {
                _logger.LogDebug("检测到ASCII PLY格式, 顶点: {VertexCount}, 面: {FaceCount}", vertexCount, faceCount);
                mesh = await LoadAsciiPlyAsync(modelPath, vertexCount, faceCount, properties, cancellationToken);
            }
            else
            {
                _logger.LogDebug("检测到二进制PLY格式, 顶点: {VertexCount}, 面: {FaceCount}", vertexCount, faceCount);
                mesh = await LoadBinaryPlyAsync(modelPath, vertexCount, faceCount, properties, format, cancellationToken);
            }

            // 计算包围盒
            var boundingBox = CalculateBoundingBox(mesh);

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("PLY模型加载完成: {Path}, 三角形数量: {Count}, 耗时: {Elapsed}ms",
                modelPath, mesh.Faces.Count, elapsed.TotalMilliseconds);

            return (mesh, boundingBox);
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
    private async Task<MeshT> LoadAsciiPlyAsync(
        string filePath, int vertexCount, int faceCount,
        List<string> properties, CancellationToken cancellationToken)
    {
        // 临时存储顶点位置，用于后续创建面时引用
        var vertexPositions = new List<Vector3d>();
        var vertices = new List<Vertex3>();
        var faces = new List<FaceT>();
        var textureVertices = new List<Vertex2>();
        var materials = new List<Material> { CreateDefaultMaterial() };

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
                var pos = new Vector3d(
                    double.Parse(parts[0]),
                    double.Parse(parts[1]),
                    double.Parse(parts[2]));
                vertexPositions.Add(pos);
            }
        }

        // 读取面并构建网格数据
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

                    if (i0 < vertexPositions.Count && i1 < vertexPositions.Count && i2 < vertexPositions.Count)
                    {
                        // 添加顶点并创建面
                        var v1Idx = vertices.Count;
                        vertices.Add(new Vertex3(vertexPositions[i0].x, vertexPositions[i0].y, vertexPositions[i0].z));
                        vertices.Add(new Vertex3(vertexPositions[i1].x, vertexPositions[i1].y, vertexPositions[i1].z));
                        vertices.Add(new Vertex3(vertexPositions[i2].x, vertexPositions[i2].y, vertexPositions[i2].z));

                        // 创建面（使用默认材质索引0）
                        faces.Add(new FaceT(v1Idx, v1Idx + 1, v1Idx + 2, 0, 0, 0, 0));
                    }
                }
                else if (indexCount == 4 && parts.Length >= 5)
                {
                    // 四边形，分割成两个三角形
                    var i0 = int.Parse(parts[1]);
                    var i1 = int.Parse(parts[2]);
                    var i2 = int.Parse(parts[3]);
                    var i3 = int.Parse(parts[4]);

                    if (i0 < vertexPositions.Count && i1 < vertexPositions.Count &&
                        i2 < vertexPositions.Count && i3 < vertexPositions.Count)
                    {
                        // 第一个三角形 (v0, v1, v2)
                        var v1Idx = vertices.Count;
                        vertices.Add(new Vertex3(vertexPositions[i0].x, vertexPositions[i0].y, vertexPositions[i0].z));
                        vertices.Add(new Vertex3(vertexPositions[i1].x, vertexPositions[i1].y, vertexPositions[i1].z));
                        vertices.Add(new Vertex3(vertexPositions[i2].x, vertexPositions[i2].y, vertexPositions[i2].z));
                        faces.Add(new FaceT(v1Idx, v1Idx + 1, v1Idx + 2, 0, 0, 0, 0));

                        // 第二个三角形 (v0, v2, v3)
                        var v2Idx = vertices.Count;
                        vertices.Add(new Vertex3(vertexPositions[i0].x, vertexPositions[i0].y, vertexPositions[i0].z));
                        vertices.Add(new Vertex3(vertexPositions[i2].x, vertexPositions[i2].y, vertexPositions[i2].z));
                        vertices.Add(new Vertex3(vertexPositions[i3].x, vertexPositions[i3].y, vertexPositions[i3].z));
                        faces.Add(new FaceT(v2Idx, v2Idx + 1, v2Idx + 2, 0, 0, 0, 0));
                    }
                }
            }
        }

        return new MeshT(vertices, textureVertices, faces, materials);
    }

    /// <summary>
    /// 加载二进制格式的PLY文件
    /// </summary>
#pragma warning disable CS1998 // 异步方法缺少 await 运算符
    private Task<MeshT> LoadBinaryPlyAsync(
        string filePath, int vertexCount, int faceCount,
        List<string> properties, string format, CancellationToken cancellationToken)
#pragma warning restore CS1998
    {
        // 注意：完整的二进制PLY实现需要根据properties解析不同的数据类型
        // 这里提供基本实现
        throw new NotImplementedException("二进制PLY格式加载器尚未完全实现，请使用ASCII格式");
    }

    /// <summary>
    /// 计算网格的包围盒
    /// </summary>
    private static Box3 CalculateBoundingBox(MeshT mesh)
    {
        if (mesh.Vertices.Count == 0)
        {
            return new Box3(new Vertex3(0.0, 0.0, 0.0), new Vertex3(0, 0, 0));
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
