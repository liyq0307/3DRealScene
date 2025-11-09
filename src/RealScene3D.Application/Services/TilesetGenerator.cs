using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RealScene3D.Application.Services;

/// <summary>
/// Tileset生成器 - 为Cesium 3D Tiles生成tileset.json文件
/// 实现分层tileset结构，支持父子节点关系
/// </summary>
public class TilesetGenerator
{
    private readonly ILogger<TilesetGenerator> _logger;

    public TilesetGenerator(ILogger<TilesetGenerator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 生成tileset.json文件
    /// </summary>
    /// <param name="slices">切片列表</param>
    /// <param name="config">切片配置</param>
    /// <param name="modelBounds">模型包围盒</param>
    /// <param name="outputPath">输出文件路径</param>
    public async Task GenerateTilesetJsonAsync(
        List<Slice> slices,
        SlicingConfig config,
        BoundingBox3D modelBounds,
        string outputPath)
    {
        try
        {
            _logger.LogInformation("生成tileset.json，包含 {Count} 个切片", slices.Count);

            var slicesByLevel = slices.GroupBy(s => s.Level)
                                     .OrderBy(g => g.Key)
                                     .ToList();

            var rootGeometricError = CalculateGeometricError(0, config.MaxLevel, modelBounds);
            var root = BuildTilesetNode(slicesByLevel, 0, config, modelBounds);

            if (root == null)
            {
                throw new InvalidOperationException("构建tileset根节点失败");
            }

            var tileset = new TilesetRoot
            {
                Asset = new Asset
                {
                    Version = "1.0",
                    Generator = "RealScene3D TilesetGenerator"
                },
                GeometricError = rootGeometricError,
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

    private TilesetNode? BuildTilesetNode(
        List<IGrouping<int, Slice>> slicesByLevel,
        int currentLevelIndex,
        SlicingConfig config,
        BoundingBox3D modelBounds)
    {
        if (currentLevelIndex >= slicesByLevel.Count)
        {
            return null;
        }

        var currentLevel = slicesByLevel[currentLevelIndex];
        var level = currentLevel.Key;
        var representativeSlice = currentLevel.First();
        var boundingBox = ParseBoundingBox(representativeSlice.BoundingBox);
        var geometricError = CalculateGeometricError(level, config.MaxLevel, boundingBox);

        var node = new TilesetNode
        {
            BoundingVolume = new BoundingVolume
            {
                Box = ConvertToTilesetBox(boundingBox)
            },
            GeometricError = geometricError,
            Refine = level == config.MaxLevel ? "ADD" : "REPLACE"
        };

        if (currentLevel.Any())
        {
            var contentSlice = currentLevel.First();
            node.Content = new Content
            {
                Uri = GetRelativeUri(contentSlice.FilePath)
            };
        }

        if (currentLevelIndex + 1 < slicesByLevel.Count)
        {
            var children = new List<TilesetNode>();
            var nextLevelSlices = slicesByLevel[currentLevelIndex + 1];

            foreach (var childSlice in nextLevelSlices)
            {
                var childBounds = ParseBoundingBox(childSlice.BoundingBox);
                var childGeometricError = CalculateGeometricError(
                    childSlice.Level, config.MaxLevel, childBounds);

                var childNode = new TilesetNode
                {
                    BoundingVolume = new BoundingVolume
                    {
                        Box = ConvertToTilesetBox(childBounds)
                    },
                    GeometricError = childGeometricError,
                    Refine = childSlice.Level == config.MaxLevel ? "ADD" : "REPLACE",
                    Content = new Content
                    {
                        Uri = GetRelativeUri(childSlice.FilePath)
                    }
                };

                children.Add(childNode);
            }

            if (children.Any())
            {
                node.Children = children;
            }
        }

        return node;
    }

    /// <summary>
    /// 基于对角线长度和LOD级别计算几何误差
    /// </summary>
    private double CalculateGeometricError(int level, int maxLevel, BoundingBox3D bounds)
    {
        var diagonal = CalculateDiagonalLength(bounds);
        var errorFactor = Math.Pow(2, maxLevel - level);
        return diagonal * errorFactor * 0.1;
    }

    /// <summary>
    /// 计算包围盒对角线长度
    /// </summary>
    private double CalculateDiagonalLength(BoundingBox3D bounds)
    {
        var dx = bounds.MaxX - bounds.MinX;
        var dy = bounds.MaxY - bounds.MinY;
        var dz = bounds.MaxZ - bounds.MinZ;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
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
        var parts = filePath.Split('/', '\\');
        if (parts.Length >= 2)
        {
            return string.Join("/", parts.Skip(parts.Length - 2));
        }
        return filePath;
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

    /// <summary>生成器标识</summary>
    [JsonPropertyName("generator")]
    public required string Generator { get; set; }
}

/// <summary>
/// Tileset节点
/// </summary>
public class TilesetNode
{
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
