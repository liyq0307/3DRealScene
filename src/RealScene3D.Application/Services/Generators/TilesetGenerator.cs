using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Geometry;

namespace RealScene3D.Application.Services.Generators;

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
    public async Task GenerateTilesetAsync(
        List<Slice> slices,
        SlicingConfig config,
        Box3 modelBounds,
        string outputPath,
        GpsCoords? gpsCoords = null)
    {
        try
        {
            _logger.LogInformation("生成tileset.json，包含 {Count} 个切片", slices.Count);

            var maxLod = slices.Max(s => s.Level);
            var rootGeometricError = CalculateRootGeometricError(modelBounds);

            if (gpsCoords == null)
            {
                gpsCoords = DefaultGpsCoords;
                _logger.LogInformation("未提供GPS坐标，使用默认值：纬度={Lat}, 经度={Lon}, 高度={Alt}",
                    gpsCoords.Latitude, gpsCoords.Longitude, gpsCoords.Altitude);
            }

            var tileset = new Tileset
            {
                Asset = new Asset { Version = "1.0" },
                GeometricError = rootGeometricError,
                Root = new TileElement
                {
                    GeometricError = rootGeometricError,
                    Refine = "ADD",
                    Transform = gpsCoords.ToEcefTransform(),
                }
            };

            // 获取LOD-0的边界框作为参考,避免GeometricError值过大导致显示异常
            var lodChain = slices
                .OrderBy(s => s.Level)
                .ToList();

            var lod0Slice = lodChain.FirstOrDefault(s => s.Level == 0);
            var refBox = lod0Slice != null ? ParseBoundingBox(lod0Slice.BoundingBox) : modelBounds;

            foreach (var slice in slices)
            {
                var currentTileElement = tileset.Root;
                var bbox = System.Text.Json.JsonSerializer.Deserialize<Box3>(slice.BoundingBox);
                if (null == bbox)
                {
                    continue;
                }

                var tile = new TileElement
                {
                    GeometricError = slice.Level == 0 ? 0 : CalculateGeometricErrorForLod(refBox, bbox, slice.Level),
                    Refine = "REPLACE",
                    Content = new Content
                    {
                        Uri = GetRelativeUri(slice.FilePath)
                    },
                    BoundingVolume = ToBoundingVolume(bbox)
                };

                currentTileElement.Children ??= [];
                currentTileElement.Children.Add(tile);
                currentTileElement = tile;
            }

            tileset.Root.BoundingVolume = ToBoundingVolume(modelBounds);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(outputPath,
                JsonConvert.SerializeObject(tileset, Formatting.Indented));

            _logger.LogInformation("tileset.json生成成功: {Path}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成tileset.json失败: {Path}", outputPath);
            throw;
        }
    }

    /// <summary>
    /// 计算根节点的几何误差
    /// </summary>
    private double CalculateRootGeometricError(Box3 bounds)
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
    private double CalculateGeometricErrorForLod(Box3 refBox, Box3 tileBox, int lodLevel)
    {
        // LOD-0（最精细）的几何误差为0
        if (lodLevel == 0)
        {
            return 0.0;
        }

        // 获取边界框尺寸
        var refWidth = refBox.Width;
        var refHeight = refBox.Height;
        var refDepth = refBox.Depth;

        var tileWidth = tileBox.Width;
        var tileHeight = tileBox.Height;
        var tileDepth = tileBox.Depth;

        // 计算三个维度的相对差异
        var dW = Math.Abs(refWidth - tileWidth) / Math.Max(tileWidth, 0.001) + 1;
        var dH = Math.Abs(refHeight - tileHeight) / Math.Max(tileHeight, 0.001) + 1;
        var dD = Math.Abs(refDepth - tileDepth) / Math.Max(tileDepth, 0.001) + 1;

        var geometricError = Math.Pow(dW + dH + dD, lodLevel);

        return geometricError;
    }

    /// <summary>
    /// 将Box3转换为Cesium tileset box格式
    /// </summary>
    private BoundingVolume ToBoundingVolume(Box3 bounds)
    {
        var center = bounds.Center;
        var halfWidth = bounds.Width / 2;
        var halfHeight = bounds.Height / 2;
        var halfDepth = bounds.Depth / 2;

        // X不变, Y←-Z, Z←Y
        return new BoundingVolume
        {
            Box =
            [
                center.X, -center.Z, center.Y,   // center: (X, -Z, Y)
                halfWidth, 0, 0,                 // halfX: Width/2
                0, -halfDepth, 0,                // halfY: -Depth/2
                0, 0, halfHeight                 // halfZ: Height/2
            ]
        };
    }

    /// <summary>
    /// 解析包围盒JSON字符串
    /// </summary>
    private Box3 ParseBoundingBox(string boundingBoxJson)
    {
        try
        {
            var bbox = System.Text.Json.JsonSerializer.Deserialize<Box3>(boundingBoxJson);
            return bbox ?? new Box3(0, 0, 0, 1, 1, 1);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析包围盒JSON失败: {Json}", boundingBoxJson);
            return new Box3(0, 0, 0, 1, 1, 1);
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

public class Asset
{
    [JsonProperty("version")]
    public string? Version { get; set; }
}

public class BoundingVolume
{
    [JsonProperty("box")]
    public double[]? Box { get; set; }
}

public class Content
{
    [JsonProperty("uri")]
    public string? Uri { get; set; }
}

public class Tileset
{
    [JsonProperty("asset")]
    public Asset? Asset { get; set; }

    [JsonProperty("geometricError")]
    public double GeometricError { get; set; }

    [JsonProperty("root")]
    public TileElement? Root { get; set; }
}

public class TileElement
{
    [JsonProperty("transform")]
    public double[]? Transform { get; set; }

    [JsonProperty("boundingVolume")]
    public BoundingVolume? BoundingVolume { get; set; }

    [JsonProperty("geometricError")]
    public double GeometricError { get; set; }

    [JsonProperty("refine")]
    public string? Refine { get; set; }

    [JsonProperty("content")]
    public Content? Content { get; set; }

    [JsonProperty("children")]
    public List<TileElement>? Children { get; set; }
}

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
        return
        [
            rotation[0, 0], rotation[1, 0], rotation[2, 0], 0,
            rotation[0, 1], rotation[1, 1], rotation[2, 1], 0,
            rotation[0, 2], rotation[1, 2], rotation[2, 2], 0,
            x, y, z, 1
        ];
    }
}