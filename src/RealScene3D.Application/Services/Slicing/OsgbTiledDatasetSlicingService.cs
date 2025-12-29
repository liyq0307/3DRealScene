using Microsoft.Extensions.Logging;
using RealScene3D.Application.Services.Generators;
using RealScene3D.Application.Services.Parsers;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Geometry;
using System.Collections.Concurrent;
using System.Text.Json;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// OSGB 倾斜摄影数据集切片服务
///
/// 功能：处理完整的倾斜摄影数据集（多个瓦片）
/// 数据结构要求：
/// - 根目录/Data/  （瓦片目录）
/// - 根目录/metadata.xml （坐标系信息）
/// - Data/Tile_xxx/Tile_xxx.osgb （瓦片文件）
///
/// 输出：
/// - 根 tileset.json （引用所有瓦片）
/// - Data/Tile_xxx/tileset.json （每个瓦片的 tileset）
/// - Data/Tile_xxx/LOD-*/xxx.b3dm （切片文件）
/// </summary>
public class OsgbTiledDatasetSlicingService
{
    private readonly ILogger<OsgbTiledDatasetSlicingService> _logger;
    private readonly OsgbMetadataParser _metadataParser;
    private readonly OsgbLODSlicingService _lodSlicingService;

    public OsgbTiledDatasetSlicingService(
        ILogger<OsgbTiledDatasetSlicingService> logger,
        OsgbMetadataParser metadataParser,
        OsgbLODSlicingService lodSlicingService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metadataParser = metadataParser ?? throw new ArgumentNullException(nameof(metadataParser));
        _lodSlicingService = lodSlicingService ?? throw new ArgumentNullException(nameof(lodSlicingService));
    }

    /// <summary>
    /// 瓦片处理结果
    /// </summary>
    public class TileResult
    {
        public string TileName { get; set; } = string.Empty;
        public string TilesetPath { get; set; } = string.Empty;
        public Box3 BoundingBox { get; set; } = null!;
        public double GeometricError { get; set; }
        public List<Slice> Slices { get; set; } = new();
    }

    /// <summary>
    /// 处理完整的倾斜摄影数据集
    /// </summary>
    /// <param name="datasetRootPath">数据集根目录（包含 Data 目录和 metadata.xml）</param>
    /// <param name="outputDir">输出目录</param>
    /// <param name="config">切片配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>所有切片列表</returns>
    public async Task<List<Slice>> ProcessDatasetAsync(
        string datasetRootPath,
        string outputDir,
        SlicingConfig config,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("========== 开始处理 OSGB 倾斜摄影数据集 ==========");
        _logger.LogInformation("数据集根目录: {RootPath}", datasetRootPath);
        _logger.LogInformation("输出目录: {OutputDir}", outputDir);

        // 1. 验证数据集结构
        ValidateDatasetStructure(datasetRootPath);

        // 2. 解析 metadata.xml
        OsgbMetadataParser.MetadataInfo? metadata = null;
        string metadataPath = Path.Combine(datasetRootPath, "metadata.xml");
        if (File.Exists(metadataPath))
        {
            metadata = await _metadataParser.ParseAsync(metadataPath);
        }
        else
        {
            _logger.LogWarning("未找到 metadata.xml，将使用默认坐标系");
        }

        // 3. 扫描所有瓦片
        string dataDir = Path.Combine(datasetRootPath, "Data");
        var tiles = ScanTiles(dataDir);
        _logger.LogInformation("发现 {Count} 个瓦片", tiles.Count);

        if (tiles.Count == 0)
        {
            throw new InvalidOperationException($"在 {dataDir} 目录下未找到任何瓦片");
        }

        // 4. 并行处理所有瓦片
        var tileResults = new ConcurrentBag<TileResult>();
        var allSlices = new ConcurrentBag<Slice>();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(tiles, parallelOptions, async (tile, ct) =>
        {
            try
            {
                var result = await ProcessSingleTileAsync(
                    tile.OsgbPath,
                    tile.TileName,
                    outputDir,
                    config,
                    ct);

                if (result != null)
                {
                    tileResults.Add(result);
                    foreach (var slice in result.Slices)
                    {
                        allSlices.Add(slice);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理瓦片失败: {TileName}", tile.TileName);
            }
        });

        _logger.LogInformation("瓦片处理完成: 成功 {Success}/{Total}",
            tileResults.Count, tiles.Count);

        // 5. 生成根 tileset.json
        if (config.GenerateTileset && tileResults.Count > 0)
        {
            await GenerateRootTilesetAsync(
                tileResults.ToList(),
                outputDir,
                metadata,
                config);
        }

        var allSlicesList = allSlices.ToList();
        _logger.LogInformation("========== OSGB 倾斜摄影数据集处理完成 ==========");
        _logger.LogInformation("总切片数: {Count}", allSlicesList.Count);

        return allSlicesList;
    }

    /// <summary>
    /// 验证数据集结构
    /// </summary>
    private void ValidateDatasetStructure(string datasetRootPath)
    {
        if (!Directory.Exists(datasetRootPath))
        {
            throw new DirectoryNotFoundException($"数据集根目录不存在: {datasetRootPath}");
        }

        string dataDir = Path.Combine(datasetRootPath, "Data");
        if (!Directory.Exists(dataDir))
        {
            throw new DirectoryNotFoundException(
                $"未找到 Data 目录。倾斜摄影数据必须包含 Data 目录。\n" +
                $"正确的目录结构：\n" +
                $"  {datasetRootPath}/\n" +
                $"    ├ metadata.xml\n" +
                $"    └ Data/\n" +
                $"        └ Tile_xxx/\n" +
                $"            └ Tile_xxx.osgb");
        }
    }

    /// <summary>
    /// 扫描所有瓦片
    /// </summary>
    private List<(string TileName, string OsgbPath)> ScanTiles(string dataDir)
    {
        var tiles = new List<(string TileName, string OsgbPath)>();

        foreach (var tileDir in Directory.GetDirectories(dataDir))
        {
            string tileName = Path.GetFileName(tileDir);
            string osgbPath = Path.Combine(tileDir, $"{tileName}.osgb");

            if (File.Exists(osgbPath))
            {
                tiles.Add((tileName, osgbPath));
                _logger.LogDebug("发现瓦片: {TileName} -> {Path}", tileName, osgbPath);
            }
            else
            {
                _logger.LogWarning("瓦片目录缺少同名 OSGB 文件: {TileDir}", tileDir);
            }
        }

        return tiles;
    }

    /// <summary>
    /// 处理单个瓦片
    /// </summary>
    private async Task<TileResult?> ProcessSingleTileAsync(
        string osgbPath,
        string tileName,
        string outputDir,
        SlicingConfig config,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始处理瓦片: {TileName}", tileName);

        try
        {
            // 为每个瓦片创建独立的输出目录
            string tileOutputDir = Path.Combine(outputDir, "Data", tileName);
            Directory.CreateDirectory(tileOutputDir);

            // 使用 OsgbLODSlicingService 处理瓦片
            var slices = await _lodSlicingService.GenerateLODTilesAsync(
                osgbPath,
                tileOutputDir,
                config,
                gpsCoords: null,
                cancellationToken);

            if (slices.Count == 0)
            {
                _logger.LogWarning("瓦片未生成任何切片: {TileName}", tileName);
                return null;
            }

            // tileset.json 已经由 OsgbLODSlicingService 生成（通过 config.GenerateTileset）
            string tilesetPath = Path.Combine(tileOutputDir, "tileset.json");
            var globalBounds = CalculateGlobalBounds(
                slices.Select(s => JsonSerializer.Deserialize<Box3>(s.BoundingBox)!)
                       .Where(b => b != null)
                       .ToList());

            double geometricError = CalculateGeometricError(globalBounds);

            _logger.LogDebug("瓦片 tileset.json 路径: {Path}", tilesetPath);

            _logger.LogInformation(
                "瓦片处理完成: {TileName}, 切片数={Count}, 几何误差={Error:F2}",
                tileName, slices.Count, geometricError);

            return new TileResult
            {
                TileName = tileName,
                TilesetPath = tilesetPath,
                BoundingBox = globalBounds,
                GeometricError = geometricError,
                Slices = slices
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理瓦片失败: {TileName}", tileName);
            return null;
        }
    }

    /// <summary>
    /// 生成根 tileset.json
    /// </summary>
    private async Task GenerateRootTilesetAsync(
        List<TileResult> tileResults,
        string outputDir,
        OsgbMetadataParser.MetadataInfo? metadata,
        SlicingConfig config)
    {
        _logger.LogInformation("生成根 tileset.json");

        // 计算整体包围盒
        var allBounds = tileResults.Select(t => t.BoundingBox).ToList();
        var globalBounds = CalculateGlobalBounds(allBounds);

        // 构建根 tileset JSON
        var rootTileset = new
        {
            asset = new
            {
                version = "1.0",
                gltfUpAxis = "Z",
                generator = "RealScene3D.Application"
            },
            geometricError = 2000.0,
            root = new
            {
                transform = GenerateTransformMatrix(metadata, globalBounds),
                boundingVolume = new
                {
                    box = ConvertToTilesetBox(globalBounds)
                },
                geometricError = 2000.0,
                refine = "ADD",
                children = tileResults.Select(tile => new
                {
                    boundingVolume = new
                    {
                        box = ConvertToTilesetBox(tile.BoundingBox)
                    },
                    geometricError = tile.GeometricError,
                    content = new
                    {
                        uri = $"./Data/{tile.TileName}/tileset.json"
                    }
                }).ToArray()
            }
        };

        string rootTilesetPath = Path.Combine(outputDir, "tileset.json");
        string json = JsonSerializer.Serialize(rootTileset, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(rootTilesetPath, json);
        _logger.LogInformation("根 tileset.json 生成完成: {Path}", rootTilesetPath);
    }

    /// <summary>
    /// 生成变换矩阵（ENU->ECEF）
    /// </summary>
    private double[] GenerateTransformMatrix(
        OsgbMetadataParser.MetadataInfo? metadata,
        Box3 bounds)
    {
        if (metadata?.GeoOrigin != null)
        {
            // 使用 metadata 中的地理原点
            var (lon, lat, height) = metadata.GeoOrigin.Value;
            return TransformXYZ(lon, lat, height);
        }
        else
        {
            // 默认使用包围盒中心
            double centerX = (bounds.Min.X + bounds.Max.X) / 2;
            double centerY = (bounds.Min.Y + bounds.Max.Y) / 2;
            double centerZ = bounds.Min.Z;

            // 假设为投影坐标（需要转换为经纬度）
            _logger.LogWarning("未找到地理坐标原点，使用默认转换");
            return TransformXYZ(0, 0, centerZ);
        }
    }

    /// <summary>
    /// ENU -> ECEF 变换矩阵
    /// </summary>
    private double[] TransformXYZ(double lonDeg, double latDeg, double height)
    {
        const double a = 6378137.0; // WGS84 长半轴
        const double f = 1.0 / 298.257223563; // WGS84 扁率
        const double e2 = f * (2.0 - f);

        double lon = lonDeg * Math.PI / 180.0;
        double lat = latDeg * Math.PI / 180.0;

        double sinLat = Math.Sin(lat);
        double cosLat = Math.Cos(lat);
        double sinLon = Math.Sin(lon);
        double cosLon = Math.Cos(lon);

        double N = a / Math.Sqrt(1.0 - e2 * sinLat * sinLat);
        double x0 = (N + height) * cosLat * cosLon;
        double y0 = (N + height) * cosLat * sinLon;
        double z0 = (N * (1.0 - e2) + height) * sinLat;

        // ENU 基向量（ECEF坐标系中）
        double east_x = -sinLon;
        double east_y = cosLon;
        double east_z = 0.0;

        double north_x = -sinLat * cosLon;
        double north_y = -sinLat * sinLon;
        double north_z = cosLat;

        double up_x = cosLat * cosLon;
        double up_y = cosLat * sinLon;
        double up_z = sinLat;

        // 列主序 4x4 ENU->ECEF 变换矩阵
        return new double[]
        {
            east_x, east_y, east_z, 0.0,
            north_x, north_y, north_z, 0.0,
            up_x, up_y, up_z, 0.0,
            x0, y0, z0, 1.0
        };
    }

    /// <summary>
    /// 计算全局包围盒
    /// </summary>
    private Box3 CalculateGlobalBounds(List<Box3> bounds)
    {
        if (bounds.Count == 0)
        {
            return new Box3(0, 0, 0, 1, 1, 1);
        }

        double minX = bounds.Min(b => b.Min.X);
        double minY = bounds.Min(b => b.Min.Y);
        double minZ = bounds.Min(b => b.Min.Z);
        double maxX = bounds.Max(b => b.Max.X);
        double maxY = bounds.Max(b => b.Max.Y);
        double maxZ = bounds.Max(b => b.Max.Z);

        return new Box3(
            new Vertex3(minX, minY, minZ),
            new Vertex3(maxX, maxY, maxZ)
        );
    }

    /// <summary>
    /// 转换为 3D Tiles box 格式
    /// </summary>
    private double[] ConvertToTilesetBox(Box3 box)
    {
        double centerX = (box.Max.X + box.Min.X) / 2;
        double centerY = (box.Max.Y + box.Min.Y) / 2;
        double centerZ = (box.Max.Z + box.Min.Z) / 2;

        double xHalfLen = Math.Max((box.Max.X - box.Min.X) / 2, 0.01);
        double yHalfLen = Math.Max((box.Max.Y - box.Min.Y) / 2, 0.01);
        double zHalfLen = Math.Max((box.Max.Z - box.Min.Z) / 2, 0.01);

        return new double[]
        {
            centerX, centerY, centerZ,
            xHalfLen, 0, 0,
            0, yHalfLen, 0,
            0, 0, zHalfLen
        };
    }

    /// <summary>
    /// 计算几何误差
    /// </summary>
    private double CalculateGeometricError(Box3 bbox)
    {
        double maxExtent = Math.Max(
            Math.Max(bbox.Max.X - bbox.Min.X, bbox.Max.Y - bbox.Min.Y),
            bbox.Max.Z - bbox.Min.Z);

        return maxExtent / 20.0;
    }
}
