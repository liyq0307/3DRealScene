using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using System.Text;
using System.Text.Json;

namespace RealScene3D.Application.Services.Generators;

/// <summary>
/// PNTS生成器 - 生成Cesium 3D Tiles的Point Cloud格式
/// 将三维点云数据转换为PNTS瓦片文件
/// 支持点位置、颜色和法线数据
/// 参考: Cesium 3D Tiles Specification - PNTS Format
/// 适用场景：激光扫描数据、大规模点云可视化、地形点采样
/// </summary>
public class PntsGenerator : TileGenerator
{
    /// <summary>
    /// 点云采样策略枚举
    /// </summary>
    public enum SamplingStrategy
    {
        /// <summary>
        /// 仅使用顶点 - 最快，点数最少
        /// </summary>
        VerticesOnly,

        /// <summary>
        /// 表面均匀采样 - 质量好，点数适中
        /// </summary>
        UniformSampling,

        /// <summary>
        /// 密集采样 - 最高质量，点数最多
        /// </summary>
        DenseSampling
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public PntsGenerator(ILogger<PntsGenerator> logger) : base(logger)
    {
    }

    /// <summary>
    /// 生成瓦片文件数据 - 实现抽象方法
    /// 使用默认的顶点采样策略
    /// </summary>
    /// <param name="triangles">三角形网格数据</param>
    /// <param name="bounds">空间包围盒</param>
    /// <param name="materials">材质字典（点云暂不使用）</param>
    /// <returns>PNTS瓦片文件的二进制数据</returns>
    public override byte[] GenerateTile(List<Triangle> triangles, BoundingBox3D bounds, Dictionary<string, Material>? materials = null)
    {
        return GeneratePNTS(triangles, bounds, SamplingStrategy.VerticesOnly);
    }

    /// <summary>
    /// 生成PNTS文件数据 - 支持多种采样策略
    /// 算法流程: 三角形 → 点采样 → PNTS
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="bounds">包围盒</param>
    /// <param name="strategy">采样策略</param>
    /// <param name="samplingDensity">采样密度（仅用于表面采样，每个三角形的采样点数）</param>
    /// <returns>PNTS文件的二进制数据</returns>
    public byte[] GeneratePNTS(
        List<Triangle> triangles,
        BoundingBox3D bounds,
        SamplingStrategy strategy = SamplingStrategy.VerticesOnly,
        int samplingDensity = 10)
    {
        ValidateInput(triangles, bounds);

        _logger.LogDebug("开始生成PNTS: 三角形数={TriangleCount}, 采样策略={Strategy}",
            triangles.Count, strategy);

        try
        {
            // 1. 根据策略生成点云（包含法线）
            var pointCloud = GeneratePointCloud(triangles, strategy, samplingDensity);

            _logger.LogInformation("点云生成完成: 点数={PointCount}, 有法线={HasNormals}",
                pointCloud.Points.Length, pointCloud.HasNormals);

            // 2. 生成点颜色（基于高度的渐变色）
            var colors = GenerateColors(pointCloud.Points, bounds);

            // 3. 构建Feature Table
            var (featureTableJson, featureTableBinary) = CreateFeatureTable(pointCloud, colors);
            var featureTableJsonBytes = Encoding.UTF8.GetBytes(featureTableJson);
            var featureTableJsonPadded = PadTo4ByteBoundary(featureTableJsonBytes);
            var featureTableBinaryPadded = PadTo8ByteBoundary(featureTableBinary);

            // 4. 构建Batch Table (可选，当前为空)
            var batchTableJson = "{}";
            var batchTableJsonBytes = Encoding.UTF8.GetBytes(batchTableJson);
            var batchTableJsonPadded = PadTo4ByteBoundary(batchTableJsonBytes);
            var batchTableBinary = Array.Empty<byte>();

            // 5. 计算总长度
            int headerLength = 28; // PNTS header固定28字节
            int featureTableJsonLength = featureTableJsonPadded.Length;
            int featureTableBinaryLength = featureTableBinaryPadded.Length;
            int batchTableJsonLength = batchTableJsonPadded.Length;
            int batchTableBinaryLength = batchTableBinary.Length;

            int totalLength = headerLength +
                            featureTableJsonLength +
                            featureTableBinaryLength +
                            batchTableJsonLength +
                            batchTableBinaryLength;

            // 6. 写入PNTS数据
            using var ms = new MemoryStream(totalLength);
            using var writer = new BinaryWriter(ms);

            // PNTS Header (28 bytes)
            writer.Write(Encoding.UTF8.GetBytes("pnts")); // magic (4 bytes)
            writer.Write((uint)1);                         // version (4 bytes)
            writer.Write((uint)totalLength);               // byteLength (4 bytes)
            writer.Write((uint)featureTableJsonLength);    // featureTableJSONByteLength (4 bytes)
            writer.Write((uint)featureTableBinaryLength);  // featureTableBinaryByteLength (4 bytes)
            writer.Write((uint)batchTableJsonLength);      // batchTableJSONByteLength (4 bytes)
            writer.Write((uint)batchTableBinaryLength);    // batchTableBinaryByteLength (4 bytes)

            // Feature Table
            writer.Write(featureTableJsonPadded);
            writer.Write(featureTableBinaryPadded);

            // Batch Table
            writer.Write(batchTableJsonPadded);
            writer.Write(batchTableBinary);

            var result = ms.ToArray();
            LogGenerationStats(triangles.Count, pointCloud.Points.Length, result.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成PNTS失败");
            throw;
        }
    }

    /// <summary>
    /// 根据策略生成点云（包含法线）
    /// </summary>
    private PointCloud GeneratePointCloud(List<Triangle> triangles, SamplingStrategy strategy, int density)
    {
        return strategy switch
        {
            SamplingStrategy.VerticesOnly => GeneratePointCloudFromVertices(triangles),
            SamplingStrategy.UniformSampling => GeneratePointCloudUniformSampling(triangles, density),
            SamplingStrategy.DenseSampling => GeneratePointCloudUniformSampling(triangles, density * 2),
            _ => GeneratePointCloudFromVertices(triangles)
        };
    }

    /// <summary>
    /// 从三角形顶点生成点云 - 最简单的策略
    /// 提取所有不重复的顶点及其法线
    /// </summary>
    private PointCloud GeneratePointCloudFromVertices(List<Triangle> triangles)
    {
        var pointSet = new HashSet<string>();
        var points = new List<Vector3D>();
        var normals = new List<Vector3D>();

        bool hasNormals = triangles.Any(t => t.HasVertexNormals());

        foreach (var triangle in triangles)
        {
            var vertices = new[] {
                (triangle.V1, triangle.Normal1),
                (triangle.V2, triangle.Normal2),
                (triangle.V3, triangle.Normal3)
            };

            foreach (var (vertex, normal) in vertices)
            {
                var key = $"{vertex.X:F6}_{vertex.Y:F6}_{vertex.Z:F6}";
                if (pointSet.Add(key))
                {
                    points.Add(vertex);

                    // 添加法线（如果有）
                    if (hasNormals && normal != null)
                    {
                        normals.Add(normal);
                    }
                }
            }
        }

        return new PointCloud
        {
            Points = points.ToArray(),
            Normals = hasNormals ? normals.ToArray() : null
        };
    }

    /// <summary>
    /// 均匀采样三角形表面生成点云
    /// 算法：重心坐标插值（位置和法线）
    /// </summary>
    private PointCloud GeneratePointCloudUniformSampling(List<Triangle> triangles, int samplesPerTriangle)
    {
        var points = new List<Vector3D>();
        var normals = new List<Vector3D>();

        bool hasNormals = triangles.Any(t => t.HasVertexNormals());

        foreach (var triangle in triangles)
        {
            var v0 = triangle.V1;
            var v1 = triangle.V2;
            var v2 = triangle.V3;

            var n0 = triangle.Normal1;
            var n1 = triangle.Normal2;
            var n2 = triangle.Normal3;

            // 在三角形表面采样
            for (int i = 0; i < samplesPerTriangle; i++)
            {
                // 生成随机重心坐标
                var r1 = Random.Shared.NextDouble();
                var r2 = Random.Shared.NextDouble();

                // 确保点在三角形内
                if (r1 + r2 > 1.0)
                {
                    r1 = 1.0 - r1;
                    r2 = 1.0 - r2;
                }

                var r3 = 1.0 - r1 - r2;

                // 重心坐标插值 - 位置
                var point = new Vector3D
                {
                    X = v0.X * r1 + v1.X * r2 + v2.X * r3,
                    Y = v0.Y * r1 + v1.Y * r2 + v2.Y * r3,
                    Z = v0.Z * r1 + v1.Z * r2 + v2.Z * r3
                };
                points.Add(point);

                // 重心坐标插值 - 法线
                if (hasNormals && n0 != null && n1 != null && n2 != null)
                {
                    var normal = new Vector3D
                    {
                        X = n0.X * r1 + n1.X * r2 + n2.X * r3,
                        Y = n0.Y * r1 + n1.Y * r2 + n2.Y * r3,
                        Z = n0.Z * r1 + n1.Z * r2 + n2.Z * r3
                    };
                    // 归一化法线
                    normal = normal.Normalize();
                    normals.Add(normal);
                }
            }
        }

        return new PointCloud
        {
            Points = points.ToArray(),
            Normals = hasNormals ? normals.ToArray() : null
        };
    }

    /// <summary>
    /// 生成点颜色 - 基于高度的渐变色（蓝→绿→红）
    /// </summary>
    private byte[] GenerateColors(Vector3D[] points, BoundingBox3D bounds)
    {
        var colors = new byte[points.Length * 3]; // RGB
        var minZ = bounds.MinZ;
        var maxZ = bounds.MaxZ;
        var range = maxZ - minZ;

        for (int i = 0; i < points.Length; i++)
        {
            // 归一化高度 [0, 1]
            var normalizedHeight = range > 0 ? (points[i].Z - minZ) / range : 0.5;

            // 渐变色映射
            byte r, g, b;
            if (normalizedHeight < 0.5)
            {
                // 蓝 → 绿
                var t = normalizedHeight * 2.0;
                r = 0;
                g = (byte)(t * 255);
                b = (byte)((1.0 - t) * 255);
            }
            else
            {
                // 绿 → 红
                var t = (normalizedHeight - 0.5) * 2.0;
                r = (byte)(t * 255);
                g = (byte)((1.0 - t) * 255);
                b = 0;
            }

            colors[i * 3] = r;
            colors[i * 3 + 1] = g;
            colors[i * 3 + 2] = b;
        }

        return colors;
    }

    /// <summary>
    /// 创建Feature Table - 包含点位置、颜色和法线
    /// </summary>
    private (string json, byte[] binary) CreateFeatureTable(PointCloud pointCloud, byte[] colors)
    {
        int pointCount = pointCloud.Points.Length;
        int positionsByteLength = pointCount * 3 * sizeof(float);
        int colorsByteLength = colors.Length;
        int normalsByteLength = pointCloud.HasNormals ? pointCount * 3 * sizeof(float) : 0;

        // Feature Table JSON - 动态构建
        var featureTableDict = new Dictionary<string, object>
        {
            { "POINTS_LENGTH", pointCount },
            { "POSITION", new { byteOffset = 0 } },
            { "RGB", new { byteOffset = positionsByteLength } }
        };

        int currentOffset = positionsByteLength + colorsByteLength;

        // 添加法线属性（如果有）
        if (pointCloud.HasNormals)
        {
            featureTableDict["NORMAL"] = new { byteOffset = currentOffset };
        }

        var json = JsonSerializer.Serialize(featureTableDict, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = null // 使用大写属性名
        });

        // Feature Table Binary
        int totalBinaryLength = positionsByteLength + colorsByteLength + normalsByteLength;
        var binary = new byte[totalBinaryLength];
        int offset = 0;

        // 写入位置数据
        foreach (var point in pointCloud.Points)
        {
            Buffer.BlockCopy(BitConverter.GetBytes((float)point.X), 0, binary, offset, sizeof(float));
            offset += sizeof(float);
            Buffer.BlockCopy(BitConverter.GetBytes((float)point.Y), 0, binary, offset, sizeof(float));
            offset += sizeof(float);
            Buffer.BlockCopy(BitConverter.GetBytes((float)point.Z), 0, binary, offset, sizeof(float));
            offset += sizeof(float);
        }

        // 写入颜色数据
        Buffer.BlockCopy(colors, 0, binary, offset, colorsByteLength);
        offset += colorsByteLength;

        // 写入法线数据（如果有）
        if (pointCloud.HasNormals && pointCloud.Normals != null)
        {
            foreach (var normal in pointCloud.Normals)
            {
                Buffer.BlockCopy(BitConverter.GetBytes((float)normal.X), 0, binary, offset, sizeof(float));
                offset += sizeof(float);
                Buffer.BlockCopy(BitConverter.GetBytes((float)normal.Y), 0, binary, offset, sizeof(float));
                offset += sizeof(float);
                Buffer.BlockCopy(BitConverter.GetBytes((float)normal.Z), 0, binary, offset, sizeof(float));
                offset += sizeof(float);
            }
        }

        return (json, binary);
    }

    /// <summary>
    /// 获取瓦片格式名称
    /// </summary>
    protected override string GetFormatName() => "PNTS";

    /// <summary>
    /// 保存瓦片文件到磁盘 - 实现抽象方法
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="bounds">空间包围盒</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="materials">材质字典（可选）</param>
    public override async Task SaveTileAsync(List<Triangle> triangles, BoundingBox3D bounds, string outputPath, Dictionary<string, Material>? materials = null)
    {
        await SavePNTSFileAsync(triangles, bounds, outputPath, SamplingStrategy.VerticesOnly, 10);
    }

    /// <summary>
    /// 保存PNTS文件到磁盘
    /// </summary>
    public async Task SavePNTSFileAsync(
        List<Triangle> triangles,
        BoundingBox3D bounds,
        string outputPath,
        SamplingStrategy strategy = SamplingStrategy.VerticesOnly,
        int samplingDensity = 10)
    {
        _logger.LogInformation("保存PNTS文件: {Path}, 策略={Strategy}", outputPath, strategy);

        try
        {
            var pntsData = GeneratePNTS(triangles, bounds, strategy, samplingDensity);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(outputPath, pntsData);

            _logger.LogInformation("PNTS文件保存成功: {Path}, 大小={Size}字节",
                outputPath, pntsData.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存PNTS文件失败: {Path}", outputPath);
            throw;
        }
    }

    /// <summary>
    /// 点云数据结构 - 包含点位置和法线
    /// </summary>
    private class PointCloud
    {
        public Vector3D[] Points { get; set; } = Array.Empty<Vector3D>();
        public Vector3D[]? Normals { get; set; }
        public bool HasNormals => Normals != null && Normals.Length > 0;
    }
}
