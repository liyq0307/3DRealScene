using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RealScene3D.Application.Services.Generators;

/// <summary>
/// GPS坐标信息 - 用于ECEF坐标变换
/// </summary>
public class GpsCoords
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }

    public GpsCoords() { }

    public GpsCoords(double latitude, double longitude, double altitude = 0)
    {
        Latitude = latitude;
        Longitude = longitude;
        Altitude = altitude;
    }

    /// <summary>
    /// 将GPS坐标转换为ECEF变换矩阵
    /// </summary>
    public double[] ToEcefTransform()
    {
        // WGS-84椭球体参数
        const double a = 6378137.0; // 长半轴
        const double f = 1.0 / 298.257223563; // 扁率
        const double b = a * (1 - f); // 短半轴
        const double e2 = (a * a - b * b) / (a * a); // 第一偏心率的平方

        // 转换为弧度
        double latRad = Latitude * Math.PI / 180.0;
        double lonRad = Longitude * Math.PI / 180.0;

        // 计算法向量半径
        double sinLat = Math.Sin(latRad);
        double cosLat = Math.Cos(latRad);
        double sinLon = Math.Sin(lonRad);
        double cosLon = Math.Cos(lonRad);

        double N = a / Math.Sqrt(1 - e2 * sinLat * sinLat);

        // 计算ECEF坐标
        double x = (N + Altitude) * cosLat * cosLon;
        double y = (N + Altitude) * cosLat * sinLon;
        double z = (N * (1 - e2) + Altitude) * sinLat;

        // 构建局部坐标系到ECEF的旋转矩阵（ENU坐标系）
        // 东(East)、北(North)、上(Up)
        double[,] rotation = new double[3, 3];

        // East向量
        rotation[0, 0] = -sinLon;
        rotation[1, 0] = cosLon;
        rotation[2, 0] = 0;

        // North向量
        rotation[0, 1] = -sinLat * cosLon;
        rotation[1, 1] = -sinLat * sinLon;
        rotation[2, 1] = cosLat;

        // Up向量
        rotation[0, 2] = cosLat * cosLon;
        rotation[1, 2] = cosLat * sinLon;
        rotation[2, 2] = sinLat;

        // 构建4x4变换矩阵（列优先顺序）
        return new double[]
        {
            rotation[0, 0], rotation[1, 0], rotation[2, 0], 0,
            rotation[0, 1], rotation[1, 1], rotation[2, 1], 0,
            rotation[0, 2], rotation[1, 2], rotation[2, 2], 0,
            x, y, z, 1
        };
    }
}

/// <summary>
/// Tileset生成器 - 为Cesium 3D Tiles生成tileset.json文件
/// - 根节点包含整个模型的包围盒，无内容，refine="ADD"
/// - 根节点包含ECEF变换矩阵
/// - 子节点按空间位置组织，同一位置的不同LOD形成父子链
/// - LOD-N（粗糙）是父节点，LOD-0（精细）是最内层子节点
/// - 子节点使用 REPLACE 策略
/// </summary>
public class TilesetGenerator
{
    private readonly ILogger<TilesetGenerator> _logger;

    public TilesetGenerator(ILogger<TilesetGenerator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 默认GPS坐标 - 北京天安门
    /// </summary>
    private static readonly GpsCoords DefaultGpsCoords = new()
    {
        Altitude = 43.5,
        Latitude = 39.908692,
        Longitude = 116.397477
    };

    /// <summary>
    /// 生成tileset.json文件
    /// </summary>
    public async Task GenerateTilesetJsonAsync(
        List<Slice> slices,
        SlicingConfig config,
        BoundingBox3D modelBounds,
        string outputPath,
        GpsCoords? gpsCoords = null)
    {
        try
        {
            _logger.LogInformation("生成tileset.json，包含 {Count} 个切片", slices.Count);

            var maxLod = slices.Max(s => s.Level);
            var rootGeometricError = CalculateRootGeometricError(modelBounds);

            var root = BuildTilesetRoot(slices, config, modelBounds, maxLod, gpsCoords);

            if (root == null)
            {
                throw new InvalidOperationException("构建tileset根节点失败");
            }

            var tileset = new TilesetRoot
            {
                Asset = new Asset
                {
                    Version = "1.0"
                },
                GeometricError = 100.0,
                Root = root
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(tileset, options);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(outputPath, json);

            _logger.LogInformation("tileset.json生成成功: {Path}, 大小: {Size} 字节",
                outputPath, json.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成tileset.json失败: {Path}", outputPath);
            throw;
        }
    }

    /// <summary>
    /// 构建 Tileset 根节点
    /// - 根节点无内容，refine="ADD"
    /// - 根节点包含transform矩阵（ECEF变换）
    /// - children 是按空间位置组织的 LOD 链
    /// </summary>
    private TilesetNode? BuildTilesetRoot(
        List<Slice> slices,
        SlicingConfig config,
        BoundingBox3D modelBounds,
        int maxLod,
        GpsCoords? gpsCoords)
    {
        if (slices == null || slices.Count == 0)
        {
            return null;
        }

        // 按空间位置 (X, Y, Z) 分组
        var slicesByPosition = slices
            .GroupBy(s => (s.X, s.Y, s.Z))
            .ToList();

        _logger.LogInformation("按空间位置分组：{Count} 个位置", slicesByPosition.Count);

        // 创建根节点（无内容，只有包围盒）
        // 根节点使用 ADD 策略
        var root = new TilesetNode
        {
            BoundingVolume = new BoundingVolume
            {
                Box = ConvertToTilesetBox(modelBounds)
            },
            GeometricError = CalculateRootGeometricError(modelBounds),
            Refine = "ADD",  // 根节点使用 ADD 策略
            Content = null   // 根节点无内容
        };

        if (gpsCoords == null)
        {
            gpsCoords = DefaultGpsCoords;
            _logger.LogInformation("未提供GPS坐标，使用默认值：纬度={Lat}, 经度={Lon}, 高度={Alt}",
                gpsCoords.Latitude, gpsCoords.Longitude, gpsCoords.Altitude);
        }

        // 如果提供了GPS坐标，添加ECEF变换矩阵
        if (gpsCoords != null)
        {
            root.Transform = gpsCoords.ToEcefTransform();
            _logger.LogInformation("已添加ECEF变换矩阵：纬度={Lat}, 经度={Lon}, 高度={Alt}",
                gpsCoords.Latitude, gpsCoords.Longitude, gpsCoords.Altitude);
        }

        // 为每个空间位置构建 LOD 链
        var children = new List<TilesetNode>();

        foreach (var positionGroup in slicesByPosition)
        {
            // 按 LOD 级别排序（0 -> N）
            // LOD-0 是最精细的，LOD-N 是最粗糙的
            var lodChain = positionGroup
                .OrderBy(s => s.Level)
                .ToList();

            if (lodChain.Count == 0) continue;

            // 构建该位置的 LOD 链
            // 粗糙的在外层，精细的在内层
            // 传递模型边界框作为参考，用于计算geometricError
            var positionNode = BuildLodChain(lodChain, maxLod, modelBounds);
            if (positionNode != null)
            {
                children.Add(positionNode);
            }
        }

        if (children.Count > 0)
        {
            root.Children = children;
        }

        _logger.LogInformation("tileset 构建完成：{ChildCount} 个空间位置节点", children.Count);

        return root;
    }

    /// <summary>
    /// 构建单个空间位置的 LOD 链
    /// - LOD-N（粗糙，三角形少）是外层父节点
    /// - LOD-0（精细，三角形多）是最内层子节点
    /// - 子节点使用 REPLACE 策略
    ///
    /// 这样当距离远时显示粗糙的LOD，近距离时替换为精细的LOD
    /// </summary>
    /// <param name="lodChain">同一空间位置的所有LOD级别的切片</param>
    /// <param name="maxLod">最大LOD级别</param>
    /// <param name="modelBounds">整个模型的边界框，用作geometricError计算的参考</param>
    private TilesetNode? BuildLodChain(List<Slice> lodChain, int maxLod, BoundingBox3D modelBounds)
    {
        if (lodChain.Count == 0) return null;

        TilesetNode? currentNode = null;

        // 从最精细的 LOD 开始向外构建（LOD-0 -> LOD-N）
        // 这样 LOD-0（精细）是最内层，LOD-N（粗糙）是外层
        for (int i = 0; i < lodChain.Count; i++)
        {
            var slice = lodChain[i];
            var sliceBounds = ParseBoundingBox(slice.BoundingBox);

            // 计算geometricError使用模型边界框作为参考，计算相对差异的指数增长
            var geometricError = CalculateGeometricErrorForLod(modelBounds, sliceBounds, slice.Level);

            var node = new TilesetNode
            {
                BoundingVolume = new BoundingVolume
                {
                    Box = ConvertToTilesetBox(sliceBounds)
                },
                GeometricError = geometricError,
                Refine = "REPLACE",  // 子节点使用 REPLACE 策略
                Content = new Content
                {
                    Uri = GetRelativeUri(slice.FilePath)
                }
            };

            // 如果有更精细的子节点（之前迭代创建的），添加到 children
            if (currentNode != null)
            {
                node.Children = new List<TilesetNode> { currentNode };
            }

            currentNode = node;
        }

        return currentNode;
    }

    /// <summary>
    /// 计算根节点的几何误差
    /// </summary>
    private double CalculateRootGeometricError(BoundingBox3D bounds)
    {
        // 魔法数字，默认100，不要问我为啥是100，反正这样设置显示正确
        // https://github.com/CesiumGS/3d-tiles/issues/162
        return 100.0;
    }

    /// <summary>
    /// 计算特定 LOD 级别的几何误差
    /// geometricError = Math.Pow(dW + dH + dD, lod)
    /// 其中：
    /// - dW = |refBox.Width - box.Width| / box.Width + 1
    /// - dH = |refBox.Height - box.Height| / box.Height + 1
    /// - dD = |refBox.Depth - box.Depth| / box.Depth + 1
    /// - LOD-0（最精细）直接设为0
    /// </summary>
    private double CalculateGeometricErrorForLod(BoundingBox3D refBox, BoundingBox3D tileBox, int lodLevel)
    {
        // LOD-0（最精细）的几何误差为0
        if (lodLevel == 0)
        {
            return 0.0;
        }

        // 获取边界框尺寸
        var refSize = refBox.GetSize();
        var tileSize = tileBox.GetSize();

        // 计算三个维度的相对差异
        var dW = Math.Abs(refSize.Width - tileSize.Width) / Math.Max(tileSize.Width, 0.001) + 1;
        var dH = Math.Abs(refSize.Height - tileSize.Height) / Math.Max(tileSize.Height, 0.001) + 1;
        var dD = Math.Abs(refSize.Depth - tileSize.Depth) / Math.Max(tileSize.Depth, 0.001) + 1;

        var geometricError = Math.Pow(dW + dH + dD, lodLevel);

        return geometricError;
    }

    /// <summary>
    /// 将BoundingBox3D转换为Cesium tileset box格式
    /// </summary>
    private double[] ConvertToTilesetBox(BoundingBox3D bounds)
    {
        var center = bounds.GetCenter();
        var size = bounds.GetSize();

        return new double[]
        {
            center.X, center.Y, center.Z,
            size.Width / 2, 0, 0,
            0, size.Height / 2, 0,
            0, 0, size.Depth / 2
        };
    }

    /// <summary>
    /// 解析包围盒JSON字符串
    /// </summary>
    private BoundingBox3D ParseBoundingBox(string boundingBoxJson)
    {
        try
        {
            var bbox = JsonSerializer.Deserialize<BoundingBox3D>(boundingBoxJson);
            return bbox ?? new BoundingBox3D();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析包围盒JSON失败: {Json}", boundingBoxJson);
            return new BoundingBox3D();
        }
    }

    /// <summary>
    /// 获取相对URI路径
    /// </summary>
    private string GetRelativeUri(string filePath)
    {
        // 确保使用正斜杠
        var normalizedPath = filePath.Replace('\\', '/');

        // 提取 LOD-level/filename 格式
        var parts = normalizedPath.Split('/');
        if (parts.Length >= 2)
        {
            // 返回最后两部分：LOD-level/filename
            return string.Join("/", parts.Skip(parts.Length - 2));
        }

        return normalizedPath;
    }
}

/// <summary>
/// Tileset根节点
/// </summary>
public class TilesetRoot
{
    /// <summary>资源信息</summary>
    [JsonPropertyName("asset")]
    public required Asset Asset { get; set; }

    /// <summary>几何误差</summary>
    [JsonPropertyName("geometricError")]
    public double GeometricError { get; set; }

    /// <summary>根节点</summary>
    [JsonPropertyName("root")]
    public required TilesetNode Root { get; set; }
}

/// <summary>
/// 资源描述信息
/// </summary>
public class Asset
{
    /// <summary>版本号</summary>
    [JsonPropertyName("version")]
    public required string Version { get; set; }
}

/// <summary>
/// Tileset节点
/// </summary>
public class TilesetNode
{
    /// <summary>变换矩阵(ECEF坐标变换)</summary>
    [JsonPropertyName("transform")]
    public double[]? Transform { get; set; }

    /// <summary>包围体</summary>
    [JsonPropertyName("boundingVolume")]
    public required BoundingVolume BoundingVolume { get; set; }

    /// <summary>几何误差</summary>
    [JsonPropertyName("geometricError")]
    public double GeometricError { get; set; }

    /// <summary>细化策略(ADD/REPLACE)</summary>
    [JsonPropertyName("refine")]
    public required string Refine { get; set; }

    /// <summary>内容URI</summary>
    [JsonPropertyName("content")]
    public Content? Content { get; set; }

    /// <summary>子节点列表</summary>
    [JsonPropertyName("children")]
    public List<TilesetNode>? Children { get; set; }
}

/// <summary>
/// 包围体
/// </summary>
public class BoundingVolume
{
    /// <summary>包围盒(中心点+半轴向量)</summary>
    [JsonPropertyName("box")]
    public required double[] Box { get; set; }
}

/// <summary>
/// 内容描述
/// </summary>
public class Content
{
    /// <summary>资源URI</summary>
    [JsonPropertyName("uri")]
    public required string Uri { get; set; }
}
