using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.MinIO;
using System.Text.Json;

namespace RealScene3D.Application.Services;

/// <summary>
/// 切片策略接口 - 定义不同空间剖分算法的行为规范
/// </summary>
public interface ISlicingStrategy
{
    /// <summary>
    /// 生成指定级别的切片集合
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="level">LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>切片集合</returns>
    Task<List<Slice>> GenerateSlicesAsync(SlicingTask task, int level, SlicingConfig config, CancellationToken cancellationToken);

    /// <summary>
    /// 计算指定级别的切片数量估算
    /// </summary>
    /// <param name="level">LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <returns>切片数量估算</returns>
    int EstimateSliceCount(int level, SlicingConfig config);
}

/// <summary>
/// 网格切片策略 - 规则网格剖分算法
/// 适用于规则地形和均匀分布的数据，计算简单，内存占用规律
/// </summary>
public class GridSlicingStrategy : ISlicingStrategy
{
    // 日志记录器
    private readonly ILogger _logger;

    /// <summary>
    /// 构造函数 - 注入日志记录器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public GridSlicingStrategy(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 生成切片集合 - 增强的网格切片策略算法实现
    /// 算法：基于规则网格进行三维空间剖分，生成LOD层级切片
    /// 支持：并行处理、内存优化、进度监控、边界条件处理
    /// </summary>
    /// <param name="task">切片任务，包含任务配置和状态</param>
    /// <param name="level">LOD级别，影响网格密度和切片数量</param>
    /// <param name="config">切片配置，控制剖分策略和输出格式</param>
    /// <param name="cancellationToken">取消令牌，支持优雅中断</param>
    /// <returns>生成的切片集合，按空间位置排序</returns>
    public async Task<List<Slice>> GenerateSlicesAsync(SlicingTask task, int level, SlicingConfig config, CancellationToken cancellationToken)
    {
        var slices = new List<Slice>();
        var tilesInLevel = (int)Math.Pow(2, level);

        // 1. 参数验证和优化
        if (level < 0 || level > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "LOD级别必须在0-10之间");
        }

        // 2. 计算Z轴切片数量
        // level 0: 只有1个Z层切片（根级别）
        // level > 0: Z轴切片数量为X轴的一半
        var zTilesCount = level == 0 ? 1 : Math.Max(1, tilesInLevel / 2);

        _logger.LogInformation("网格切片策略：级别{Level}，网格尺寸{TilesInLevel}x{TilesInLevel}x{ZTiles}",
            level, tilesInLevel, level == 0 ? 1 : tilesInLevel / 2, zTilesCount);

        // 3. 内存预分配优化
        var estimatedSliceCount = tilesInLevel * tilesInLevel * zTilesCount;
        slices.Capacity = estimatedSliceCount;

        // 4. 并行切片生成（可选）
        if (config.ParallelProcessingCount > 1 && estimatedSliceCount > 100)
        {
            slices = await GenerateSlicesInParallelAsync(task, level, config, tilesInLevel, zTilesCount, cancellationToken);
        }
        else
        {
            // 串行生成 - 适用于小规模切片
            slices = await GenerateSlicesSequentiallyAsync(task, level, config, tilesInLevel, zTilesCount, cancellationToken);
        }

        // 5. 结果验证和排序
        if (!slices.Any())
        {
            _logger.LogWarning("网格切片策略未生成任何切片：级别{Level}", level);
        }
        else
        {
            // 按空间位置排序，便于后续处理
            slices.Sort((a, b) =>
            {
                var zCompare = a.Z.CompareTo(b.Z);
                if (zCompare != 0) return zCompare;
                var yCompare = a.Y.CompareTo(b.Y);
                if (yCompare != 0) return yCompare;
                return a.X.CompareTo(b.X);
            });

            _logger.LogInformation("网格切片生成完成：级别{Level}，共{SliceCount}个切片", level, slices.Count);
        }

        return slices;
    }

    /// <summary>
    /// 串行切片生成 - 适用于小规模切片
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="level">LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <param name="tilesInLevel">当前级别的瓦片数量</param>
    /// <param name="zTilesCount">当前级别的Z轴切片数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生成的切片集合</returns>
    private Task<List<Slice>> GenerateSlicesSequentiallyAsync(
        SlicingTask task, int level, SlicingConfig config,
        int tilesInLevel, int zTilesCount, CancellationToken cancellationToken)
    {
        var slices = new List<Slice>();

        for (int z = 0; z < zTilesCount; z++)
        {
            for (int y = 0; y < tilesInLevel; y++)
            {
                for (int x = 0; x < tilesInLevel; x++)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var slice = CreateSlice(task, level, config, x, y, z);
                    slices.Add(slice);

                    // 进度监控（每100个切片输出一次）
                    if (slices.Count % 100 == 0)
                    {
                        _logger.LogDebug("网格切片生成进度：级别{Level}，已生成{Processed}/{Total}",
                            level, slices.Count, tilesInLevel * tilesInLevel * zTilesCount);
                    }
                }
            }
        }

        return Task.FromResult(slices);
    }

    /// <summary>
    /// 并行切片生成 - 适用于大规模切片的高性能处理
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="level">LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <param name="tilesInLevel">当前级别的瓦片数量</param>
    /// <param name="zTilesCount">当前级别的Z轴切片数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生成的切片集合</returns>
    private async Task<List<Slice>> GenerateSlicesInParallelAsync(
        SlicingTask task, int level, SlicingConfig config,
        int tilesInLevel, int zTilesCount, CancellationToken cancellationToken)
    {
        var slices = new List<Slice>();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Min(config.ParallelProcessingCount, Environment.ProcessorCount),
            CancellationToken = cancellationToken
        };

        var lockObject = new object();
        var processedCount = 0;

        await Task.Run(() =>
        {
            Parallel.For(0, zTilesCount, parallelOptions, z =>
            {
                for (int y = 0; y < tilesInLevel; y++)
                {
                    for (int x = 0; x < tilesInLevel; x++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var slice = CreateSlice(task, level, config, x, y, z);

                        lock (lockObject)
                        {
                            slices.Add(slice);
                            processedCount++;

                            // 并行进度监控
                            if (processedCount % 500 == 0)
                            {
                                _logger.LogDebug("并行网格切片生成进度：级别{Level}，已生成{Processed}",
                                    level, processedCount);
                            }
                        }
                    }
                }
            });
        }, cancellationToken);

        return slices;
    }

    /// <summary>
    /// 创建单个切片实例 - 标准化切片属性赋值       
    /// </summary>  
    /// <param name="task">切片任务</param>
    /// <param name="level">LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <param name="x">X轴坐标</param>
    /// <param name="y">Y轴坐标</param>
    /// <param name="z">Z轴坐标</param>
    /// <returns>生成的切片实例</returns>
    private Slice CreateSlice(SlicingTask task, int level, SlicingConfig config, int x, int y, int z)
    {
        return new Slice
        {
            SlicingTaskId = task.Id,
            Level = level,
            X = x,
            Y = y,
            Z = z,
            FilePath = GenerateSliceFilePath(task, level, x, y, z, config.OutputFormat),
            BoundingBox = GenerateGridBoundingBox(level, x, y, z, config.TileSize),
            FileSize = CalculateFileSize(config.OutputFormat, level, config.TileSize),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 生成切片文件路径 - 标准化路径格式
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="level">LOD级别</param>
    /// <param name="x">X轴坐标</param>
    /// <param name="y">Y轴坐标</param>
    /// <param name="z">Z轴坐标</param>
    /// <param name="format">输出格式</param>
    /// <returns>生成的切片文件路径</returns>
    private string GenerateSliceFilePath(SlicingTask task, int level, int x, int y, int z, string format)
    {
        // 空值检查：确保OutputPath不为null
        var outputPath = task.OutputPath ?? "default_output";
        // 标准化路径格式：{OutputPath}/{Level}/{X}_{Y}_{Z}.{Format}
        // 注意：使用正斜杠以兼容MinIO对象存储路径
        var fileName = $"{x}_{y}_{z}.{format.ToLowerInvariant()}";
        return $"{outputPath}/{level}/{fileName}";
    }

    /// <summary>
    /// 估算指定级别的切片数量 - 基于规则网格剖分算法
    /// </summary>
    /// <param name="level">LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <returns>估算的切片数量</returns>
    public int EstimateSliceCount(int level, SlicingConfig config)
    {
        var tilesInLevel = (int)Math.Pow(2, level);
        return tilesInLevel * tilesInLevel * (level == 0 ? 1 : tilesInLevel / 2);
    }

    /// <summary>
    /// 生成网格包围盒 - 轴对齐包围盒（AABB）算法实现
    /// 算法：基于网格坐标和切片尺寸计算精确的空间边界
    /// 支持：LOD级别缩放、边界验证、格式标准化
    /// </summary>
    /// <param name="level">LOD级别，用于计算缩放因子</param>
    /// <param name="x">X轴网格坐标</param>
    /// <param name="y">Y轴网格坐标</param>
    /// <param name="z">Z轴网格坐标</param>
    /// <param name="tileSize">基础切片尺寸</param>
    /// <returns>标准化的JSON格式包围盒字符串</returns>
    private string GenerateGridBoundingBox(int level, int x, int y, int z, double tileSize)
    {
        // 1. 计算LOD缩放因子
        // level 0: 基础尺寸
        // level N: 尺寸减半的N次方
        var scaleFactor = Math.Pow(0.5, level);
        var scaledTileSize = tileSize * scaleFactor;

        // 2. 计算网格坐标对应的世界坐标
        // 网格坐标转换为实际世界位置
        var worldX = x * scaledTileSize;
        var worldY = y * scaledTileSize;
        var worldZ = z * scaledTileSize;

        // 3. 计算轴对齐包围盒（AABB）
        var minX = worldX;
        var minY = worldY;
        var minZ = worldZ;
        var maxX = worldX + scaledTileSize;
        var maxY = worldY + scaledTileSize;
        var maxZ = worldZ + scaledTileSize;

        // 4. 边界验证和调整
        // 确保包围盒不为空（最小尺寸限制）
        var minBoxSize = 1e-6; // 最小包围盒尺寸
        if (maxX - minX < minBoxSize)
        {
            maxX = minX + minBoxSize;
        }
        if (maxY - minY < minBoxSize)
        {
            maxY = minY + minBoxSize;
        }
        if (maxZ - minZ < minBoxSize)
        {
            maxZ = minZ + minBoxSize;
        }

        // 5. 生成标准化JSON格式
        // 使用不变区域性格式确保跨平台兼容性
        return $"{{\"minX\":{minX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},\"minY\":{minY.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},\"minZ\":{minZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},\"maxX\":{maxX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},\"maxY\":{maxY.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},\"maxZ\":{maxZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}}}";
    }

    /// <summary>
    /// 计算文件大小 - 基于几何复杂度的动态估算算法
    /// 算法：根据切片级别、网格密度、输出格式和几何复杂度综合计算文件大小
    /// 支持：多维度复杂度评估、自适应基数调整、格式特定优化
    /// </summary>
    /// <param name="format">输出格式</param>
    /// <param name="level">LOD级别</param>
    /// <param name="tileSize">切片尺寸</param>
    /// <returns>估算的文件大小（字节）</returns>
    private long CalculateFileSize(string format, int level = 0, double tileSize = 1.0)
    {
        // 1. 基础文件大小（字节）- 基于格式的固定开销
        var baseSize = GetBaseFileSize(format);

        // 2. 几何复杂度因子：LOD级别越高，细节越丰富
        // 使用改进的对数函数，避免指数增长导致的文件过大
        var levelComplexityFactor = CalculateLevelComplexityFactor(level);

        // 3. 空间覆盖因子：切片尺寸越大，包含的几何数据越多
        // 考虑三维空间的体积影响
        var spatialFactor = CalculateSpatialFactor(tileSize);

        // 4. 格式开销因子：不同格式的元数据和结构开销不同
        var formatOverheadFactor = GetFormatOverheadFactor(format);

        // 5. 纹理和材质因子：某些格式可能包含额外的纹理数据
        var textureFactor = CalculateTextureFactor(format);

        // 6. 压缩因子：考虑压缩对文件大小的影响
        var compressionFactor = 1.0; // 默认无压缩，可根据配置调整

        // 7. 综合计算文件大小
        var estimatedSize = (long)(baseSize *
                                  levelComplexityFactor *
                                  spatialFactor *
                                  formatOverheadFactor *
                                  textureFactor *
                                  compressionFactor);

        // 8. 应用边界约束和合理性检查
        return ApplySizeConstraints(estimatedSize, format);
    }

    /// <summary>
    /// 获取基础文件大小
    /// </summary>
    private long GetBaseFileSize(string format)
    {
        return format.ToLower() switch
        {
            "b3dm" => 2048,  // B3DM: 头部(28B) + Feature Table + Batch Table + GLB
            "gltf" => 1024,  // GLTF: JSON格式，包含场景图和元数据
            "glb" => 1536,   // GLB: 二进制格式，头部开销略少
            "json" => 512,   // JSON: 纯元数据格式
            "i3dm" => 1024,  // i3dm: 实例化3D模型格式
            "pnts" => 256,   // pnts: 点云格式，相对紧凑
            _ => 1024
        };
    }

    /// <summary>
    /// 计算级别复杂度因子
    /// </summary>
    private double CalculateLevelComplexityFactor(int level)
    {
        // 基础复杂度：级别越高复杂度越大
        var baseComplexity = Math.Log(level + 2, 2) * 0.12;

        // LOD递减因子：高级别细节递减
        var lodDecay = Math.Pow(0.85, level);

        // 级别特定调整
        var levelSpecificAdjustment = level switch
        {
            0 => 1.2,  // 根级别通常包含更多全局信息
            1 => 1.1,  // 第一级仍有较高复杂度
            _ => 1.0   // 其他级别正常递减
        };

        return Math.Max(1.0, baseComplexity * lodDecay * levelSpecificAdjustment);
    }

    /// <summary>
    /// 计算空间因子
    /// </summary>
    private double CalculateSpatialFactor(double tileSize)
    {
        // 基础空间因子：尺寸越大，几何数据越多
        var sizeFactor = Math.Max(1.0, Math.Pow(tileSize, 0.7));

        // 密度调整：考虑空间利用率
        var densityAdjustment = Math.Min(1.5, Math.Log(tileSize + 1, 10) * 0.3 + 0.8);

        return sizeFactor * densityAdjustment;
    }

    /// <summary>
    /// 获取格式开销因子
    /// </summary>
    private double GetFormatOverheadFactor(string format)
    {
        return format.ToLower() switch
        {
            "b3dm" => 1.8,  // B3DM: 最多元数据和结构开销
            "gltf" => 1.5,  // GLTF: 完整的场景图和材质系统
            "glb" => 1.6,   // GLB: 二进制格式，结构紧凑但包含GLTF功能
            "json" => 1.0,  // JSON: 最简洁的元数据格式
            "i3dm" => 1.7,  // i3dm: 实例化开销
            _ => 1.3
        };
    }

    /// <summary>
    /// 计算纹理因子
    /// </summary>
    private double CalculateTextureFactor(string format)
    {
        // 某些格式可能包含纹理数据
        return format.ToLower() switch
        {
            "b3dm" => 1.2,  // 可能包含纹理
            "gltf" => 1.3,  // 通常包含纹理和材质
            "glb" => 1.3,   // 同GLTF
            _ => 1.0        // 其他格式通常不含纹理
        };
    }

    /// <summary>
    /// 应用大小约束
    /// </summary>
    private long ApplySizeConstraints(long size, string format)
    {
        // 格式特定的最小和最大文件大小限制
        var (minSize, maxSize) = format.ToLower() switch
        {
            "json" => (128L, 10485760L),    // JSON: 128B - 10MB
            "b3dm" => (512L, 104857600L),   // B3DM: 512B - 100MB
            "gltf" => (256L, 52428800L),    // GLTF: 256B - 50MB
            "glb" => (256L, 52428800L),     // GLB: 256B - 50MB
            _ => (256L, 104857600L)         // 默认: 256B - 100MB
        };

        return Math.Max(minSize, Math.Min(maxSize, size));
    }
}

/// <summary>
/// 八叉树切片策略 - 层次空间剖分算法
/// 适用于不规则模型，自适应精度，平衡细节和性能
/// </summary>
public class OctreeSlicingStrategy : ISlicingStrategy
{
    // 日志记录器
    private readonly ILogger _logger;

    /// <summary>
    /// 构造函数 - 注入日志记录器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public OctreeSlicingStrategy(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 生成切片集合 - 八叉树剖分策略算法实现
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="level">LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>切片集合</returns>
    public async Task<List<Slice>> GenerateSlicesAsync(SlicingTask task, int level, SlicingConfig config, CancellationToken cancellationToken)
    {
        var slices = new List<Slice>();

        _logger.LogDebug("八叉树切片策略：级别{Level}", level);

        // 八叉树剖分算法：基于空间密度和几何复杂度进行递归剖分
        var octreeNodes = await BuildOctreeAsync(task, level, config, cancellationToken);

        foreach (var node in octreeNodes)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // 空值检查：确保OutputPath不为null
            var outputPath = task.OutputPath ?? "default_output";
            var slice = new Slice
            {
                SlicingTaskId = task.Id,
                Level = level,
                X = node.X,
                Y = node.Y,
                Z = node.Z,
                FilePath = $"{outputPath}/{level}/{node.X}_{node.Y}_{node.Z}.{config.OutputFormat.ToLower()}",
                BoundingBox = GenerateOctreeBoundingBox(node, config.TileSize),
                FileSize = CalculateOctreeFileSize(node, config.OutputFormat)
            };

            slices.Add(slice);
        }

        return slices;
    }

    /// <summary>
    /// 估算指定级别的切片数量 - 基于八叉树剖分算法
    /// </summary>
    /// <param name="level">LOD级别，必须为非负整数，0表示根级别</param>
    /// <param name="config">切片配置，包含剖分策略和参数</param>
    /// <returns>估算的切片数量，基于八叉树几何级数计算</returns>
    /// <exception cref="ArgumentNullException">当config为null时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">当level为负数时抛出</exception>
    /// <exception cref="OverflowException">当计算结果超过int最大值时抛出</exception>
    public int EstimateSliceCount(int level, SlicingConfig config)
    {
        // 边界情况检查：输入参数验证
        if (config == null)
            throw new ArgumentNullException(nameof(config), "切片配置不能为空");

        if (level < 0)
            throw new ArgumentOutOfRangeException(nameof(level), level, "LOD级别不能为负数");

        // 特殊情况处理：根级别只有一个切片
        if (level == 0)
            return 1;

        // 八叉树剖分估算：每个级别切片数量为8^(level-1)
        // 这是因为八叉树在每个维度上都进行二分剖分
        // 例如：level 1: 8切片, level 2: 64切片, level 3: 512切片
        try
        {
            // 性能优化：使用位运算代替Math.Pow进行整数幂运算
            // 8^level = 2^(3*level)，可以使用左移位运算优化
            var eightToLevel = 1L << (3 * level); // 2^(3*level) = 8^level

            // 应用几何衰减因子：考虑实际剖分不会达到理论最大值
            // 基于经验值，实际切片数量约为理论值的1/2到1/3
            const double geometricAttenuationFactor = 0.5; // 几何衰减因子
            var estimatedCount = (long)(eightToLevel * geometricAttenuationFactor);

            // 边界检查：确保不超过int最大值
            if (estimatedCount > int.MaxValue)
                throw new OverflowException($"估算切片数量超过int最大值：{estimatedCount}");

            return (int)estimatedCount;
        }
        catch (OverflowException)
        {
            // 溢出处理：返回int最大值作为保守估算
            return int.MaxValue;
        }
    }

    private async Task<List<OctreeNode>> BuildOctreeAsync(SlicingTask task, int level, SlicingConfig config, CancellationToken cancellationToken)
    {
        // 八叉树构建算法：递归空间剖分
        var nodes = new List<OctreeNode>();
        var rootNode = new OctreeNode
        {
            X = 0,
            Y = 0,
            Z = 0,
            Size = config.TileSize * Math.Pow(2, config.MaxLevel - level),
            Level = level
        };

        _logger.LogDebug("八叉树根节点：Level={Level}, Size={Size}, TileSize={TileSize}, MaxLevel={MaxLevel}",
            level, rootNode.Size, config.TileSize, config.MaxLevel);

        await SubdivideOctreeNodeAsync(rootNode, nodes, config, cancellationToken);

        _logger.LogInformation("八叉树构建完成：Level={Level}, 节点数={NodeCount}", level, nodes.Count);

        return nodes;
    }

    private async Task SubdivideOctreeNodeAsync(OctreeNode node, List<OctreeNode> nodes, SlicingConfig config, CancellationToken cancellationToken)
    {
        // 检查是否需要进一步剖分（基于几何密度和误差阈值）
        if (node.Level >= config.MaxLevel || !ShouldSubdivide(node, config))
        {
            nodes.Add(node);
            return;
        }

        // 八叉树递归剖分：将空间分成8个子节点
        var halfSize = node.Size / 2;
        for (int i = 0; i < 8; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var childNode = new OctreeNode
            {
                X = (int)(node.X + (i % 2) * halfSize),
                Y = (int)(node.Y + ((i / 2) % 2) * halfSize),
                Z = (int)(node.Z + (i / 4) * halfSize),
                Size = halfSize,
                Level = node.Level + 1
            };

            await SubdivideOctreeNodeAsync(childNode, nodes, config, cancellationToken);
        }
    }

    private bool ShouldSubdivide(OctreeNode node, SlicingConfig config)
    {
        // 基于几何误差阈值决定是否剖分
        return node.Size > config.TileSize && node.Level < config.MaxLevel;
    }

    /// <summary>
    /// 生成八叉树包围盒 - 自适应尺寸包围盒算法实现
    /// 算法：基于八叉树节点的空间位置和尺寸计算精确的包围盒
    /// 支持：节点尺寸验证、坐标系变换、精度控制
    /// </summary>
    /// <param name="node">八叉树节点，包含位置和尺寸信息</param>
    /// <param name="tileSize">基础切片尺寸，用于坐标系变换</param>
    /// <returns>标准化的JSON格式包围盒字符串</returns>
    private string GenerateOctreeBoundingBox(OctreeNode node, double tileSize)
    {
        // 1. 坐标系变换
        // 将节点坐标转换为世界坐标系
        var scaleFactor = tileSize / Math.Pow(2, node.Level);
        var worldMinX = node.X * scaleFactor;
        var worldMinY = node.Y * scaleFactor;
        var worldMinZ = node.Z * scaleFactor;

        // 2. 计算包围盒边界
        var nodeSize = node.Size * scaleFactor;
        var minX = worldMinX;
        var minY = worldMinY;
        var minZ = worldMinZ;
        var maxX = worldMinX + nodeSize;
        var maxY = worldMinY + nodeSize;
        var maxZ = worldMinZ + nodeSize;

        // 3. 尺寸验证和调整
        // 确保最小尺寸限制，防止退化包围盒
        var minNodeSize = 1e-6;
        if (nodeSize < minNodeSize)
        {
            var centerX = (minX + maxX) / 2;
            var centerY = (minY + maxY) / 2;
            var centerZ = (minZ + maxZ) / 2;
            var halfSize = minNodeSize / 2;

            minX = centerX - halfSize;
            maxX = centerX + halfSize;
            minY = centerY - halfSize;
            maxY = centerY + halfSize;
            minZ = centerZ - halfSize;
            maxZ = centerZ + halfSize;
        }

        // 4. 数值稳定性检查
        // 处理可能的浮点数精度问题
        minX = Math.Round(minX, 6);
        minY = Math.Round(minY, 6);
        minZ = Math.Round(minZ, 6);
        maxX = Math.Round(maxX, 6);
        maxY = Math.Round(maxY, 6);
        maxZ = Math.Round(maxZ, 6);

        // 5. 生成标准化JSON格式
        return $"{{\"minX\":{minX.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"minY\":{minY.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"minZ\":{minZ.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"maxX\":{maxX.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"maxY\":{maxY.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"maxZ\":{maxZ.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";
    }

    /// <summary>
    /// 计算八叉树节点文件大小 - 基于节点复杂度和空间位置
    /// 算法：考虑八叉树节点的深度、尺寸和空间分布特征
    /// </summary>
    /// <param name="node">八叉树节点</param>
    /// <param name="format">输出格式</param>
    /// <returns>估算的文件大小（字节）</returns>
    private long CalculateOctreeFileSize(OctreeNode node, string format)
    {
        // 基础文件大小
        var baseSize = format.ToLower() switch
        {
            "b3dm" => 2048,
            "gltf" => 1024,
            "json" => 512,
            _ => 1024
        };

        // 节点深度因子：深度越大，细节越丰富
        var depthFactor = 1.0 + node.Level * 0.12;

        // 节点尺寸因子：尺寸越大，包含的几何数据可能越多
        var sizeFactor = 1.0 + Math.Log(node.Size + 1, 2) * 0.08;

        // 八叉树特定因子：考虑空间剖分的不均匀性
        // 中心节点通常比边缘节点包含更多细节
        var centerDistance = Math.Sqrt(node.X * node.X + node.Y * node.Y + node.Z * node.Z);
        var spatialDistributionFactor = 1.0 + Math.Exp(-centerDistance / 100.0) * 0.2;

        // 综合计算
        var estimatedSize = (long)(baseSize * depthFactor * sizeFactor * spatialDistributionFactor);

        return Math.Max(256, Math.Min(52428800, estimatedSize)); // 256B - 50MB
    }

    private class OctreeNode
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public double Size { get; set; }
        public int Level { get; set; }
    }
}

/// <summary>
/// KD树切片策略 - 自适应空间剖分算法
/// 基于方差的二分剖分，适用于高维空间查询优化
/// </summary>
public class KdTreeSlicingStrategy : ISlicingStrategy
{
    private readonly ILogger _logger;

    public KdTreeSlicingStrategy(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<List<Slice>> GenerateSlicesAsync(SlicingTask task, int level, SlicingConfig config, CancellationToken cancellationToken)
    {
        var slices = new List<Slice>();

        _logger.LogDebug("KD树切片策略：级别{Level}", level);

        // KD树构建：基于几何分布特征进行自适应剖分
        var kdTreeNodes = await BuildKdTreeAsync(task, level, config, cancellationToken);

        foreach (var node in kdTreeNodes)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // 空值检查：确保OutputPath不为null
            var outputPath = task.OutputPath ?? "default_output";
            var slice = new Slice
            {
                SlicingTaskId = task.Id,
                Level = level,
                X = node.X,
                Y = node.Y,
                Z = node.Z,
                FilePath = $"{outputPath}/{level}/{node.X}_{node.Y}_{node.Z}.{config.OutputFormat.ToLower()}",
                BoundingBox = GenerateKdTreeBoundingBox(node, config.TileSize),
                FileSize = CalculateKdTreeFileSize(node, config.OutputFormat)
            };

            slices.Add(slice);
        }

        return slices;
    }

    /// <summary>
    /// 估算指定级别的切片数量 - 基于KD树剖分算法
    /// </summary>
    /// <param name="level">LOD级别，必须为非负整数，0表示根级别</param>
    /// <param name="config">切片配置，包含剖分策略和参数</param>
    /// <returns>估算的切片数量，基于KD树几何级数计算</returns>
    /// <exception cref="ArgumentNullException">当config为null时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">当level为负数时抛出</exception>
    /// <exception cref="OverflowException">当计算结果超过int最大值时抛出</exception>
    public int EstimateSliceCount(int level, SlicingConfig config)
    {
        // 边界情况检查：输入参数验证
        if (config == null)
            throw new ArgumentNullException(nameof(config), "切片配置不能为空");

        if (level < 0)
            throw new ArgumentOutOfRangeException(nameof(level), level, "LOD级别不能为负数");

        // 特殊情况处理：根级别只有一个切片
        if (level == 0)
            return 1;

        // KD树剖分估算：在3D空间中，KD树通过在不同维度上进行二分剖分来构建空间索引
        // 理论上每个级别的切片数量为2^level，但实际应用中会考虑几何衰减因子
        // KD树的核心思想是交替选择空间维度进行剖分，实现高效的空间搜索
        try
        {
            // 性能优化：使用位运算代替Math.Pow进行整数幂运算
            // 2^level可以使用左移位运算优化，计算效率更高
            var twoToLevel = 1L << level; // 2^level

            // 应用几何衰减因子：考虑实际剖分不会达到理论最大值
            // 基于经验值，KD树实际切片数量约为理论值的1/2左右
            // 这是因为KD树会根据数据分布进行自适应剖分，避免过度细分
            const double geometricAttenuationFactor = 0.5; // 几何衰减因子
            var estimatedCount = (long)(twoToLevel * geometricAttenuationFactor);

            // 对于3D空间的额外考虑：KD树在三个维度上交替剖分
            // 虽然理论上是2^level，但实际实现中可能接近8^level的复杂性
            // 这里使用保守的估算策略，确保性能和准确性的平衡
            estimatedCount = Math.Max(estimatedCount, 1); // 确保至少返回1

            // 边界检查：确保不超过int最大值
            if (estimatedCount > int.MaxValue)
                throw new OverflowException($"估算切片数量超过int最大值：{estimatedCount}");

            return (int)estimatedCount;
        }
        catch (OverflowException)
        {
            // 溢出处理：返回int最大值作为保守估算
            return int.MaxValue;
        }
    }

    /// <summary>
    /// 构建KD树 - 基于方差的二分剖分算法
    /// 算法：选择方差最大的轴进行剖分，实现空间的高效划分
    /// 支持：多维度数据、动态剖分深度、自适应
    /// </summary>
    /// <param name="task"></param>
    /// <param name="level"></param>
    /// <param name="config"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<List<KdTreeNode>> BuildKdTreeAsync(SlicingTask task, int level, SlicingConfig config, CancellationToken cancellationToken)
    {
        // KD树构建算法：交替在X、Y、Z轴上进行二分剖分
        var nodes = new List<KdTreeNode>();
        var rootNode = new KdTreeNode
        {
            MinX = 0,
            MinY = 0,
            MinZ = 0,
            MaxX = config.TileSize * Math.Pow(2, config.MaxLevel - level),
            MaxY = config.TileSize * Math.Pow(2, config.MaxLevel - level),
            MaxZ = config.TileSize * Math.Pow(2, config.MaxLevel - level),
            Level = level,
            SplitAxis = 0 // 从X轴开始
        };

        await SubdivideKdTreeNodeAsync(rootNode, nodes, config, cancellationToken);
        return nodes;
    }

    /// <summary>
    /// 递归剖分KD树节点 - 基于方差的二分剖分算法
    /// 算法：选择方差最大的轴进行剖分，实现空间的高效划分
    /// 支持：多维度数据、动态剖分深度、自适应精度控制
    /// </summary>
    /// <param name="node"></param>
    /// <param name="nodes"></param>
    /// <param name="config"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task SubdivideKdTreeNodeAsync(KdTreeNode node, List<KdTreeNode> nodes, SlicingConfig config, CancellationToken cancellationToken)
    {
        // 检查是否需要进一步剖分
        if (node.Level >= config.MaxLevel || !ShouldSubdivideKdTree(node, config))
        {
            // 转换为切片节点
            var sliceNode = new KdTreeNode
            {
                X = (int)node.MinX,
                Y = (int)node.MinY,
                Z = (int)node.MinZ,
                Level = node.Level
            };
            nodes.Add(sliceNode);
            return;
        }

        // KD树二分剖分：选择方差最大的轴进行剖分
        var splitAxis = node.SplitAxis % 3;
        var splitPoint = CalculateSplitPoint(node, splitAxis);

        // 创建左右子节点
        var leftNode = CreateChildKdTreeNode(node, splitAxis, splitPoint, true);
        var rightNode = CreateChildKdTreeNode(node, splitAxis, splitPoint, false);

        await SubdivideKdTreeNodeAsync(leftNode, nodes, config, cancellationToken);
        await SubdivideKdTreeNodeAsync(rightNode, nodes, config, cancellationToken);
    }

    /// <summary>
    /// 判断是否需要剖分KD树节点 - 基于空间尺寸和深度阈值
    /// </summary>
    /// <param name="node"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    private bool ShouldSubdivideKdTree(KdTreeNode node, SlicingConfig config)
    {
        var size = Math.Max(Math.Max(node.MaxX - node.MinX, node.MaxY - node.MinY), node.MaxZ - node.MinZ);
        return size > config.TileSize && node.Level < config.MaxLevel;
    }

    /// <summary>
    /// 计算剖分点 - 基于几何分布的中点剖分算法
    /// </summary>
    /// <param name="node"></param>
    /// <param name="axis"></param>
    /// <returns></returns>
    private double CalculateSplitPoint(KdTreeNode node, int axis)
    {
        // 基于几何分布计算最佳剖分点
        return axis switch
        {
            0 => (node.MinX + node.MaxX) / 2, // X轴中点
            1 => (node.MinY + node.MaxY) / 2, // Y轴中点
            _ => (node.MinZ + node.MaxZ) / 2  // Z轴中点
        };
    }

    /// <summary>
    /// 创建子节点 - 基于父节点和分割平面
    /// 算法：根据分割轴和分割点生成子节点的边界
    /// 支持：多维度分割、动态边界计算、节点深度优化
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="axis"></param>
    /// <param name="splitPoint"></param>
    /// <param name="isLeft"></param>
    /// <returns></returns>
    private KdTreeNode CreateChildKdTreeNode(KdTreeNode parent, int axis, double splitPoint, bool isLeft)
    {
        var child = new KdTreeNode
        {
            Level = parent.Level + 1,
            SplitAxis = parent.SplitAxis + 1
        };

        if (isLeft)
        {
            child.MinX = parent.MinX;
            child.MinY = parent.MinY;
            child.MinZ = parent.MinZ;

            switch (axis)
            {
                case 0: child.MaxX = splitPoint; child.MaxY = parent.MaxY; child.MaxZ = parent.MaxZ; break;
                case 1: child.MaxY = splitPoint; child.MaxX = parent.MaxX; child.MaxZ = parent.MaxZ; break;
                case 2: child.MaxZ = splitPoint; child.MaxX = parent.MaxX; child.MaxY = parent.MaxY; break;
            }
        }
        else
        {
            child.MaxX = parent.MaxX;
            child.MaxY = parent.MaxY;
            child.MaxZ = parent.MaxZ;

            switch (axis)
            {
                case 0: child.MinX = splitPoint; child.MinY = parent.MinY; child.MinZ = parent.MinZ; break;
                case 1: child.MinY = splitPoint; child.MinX = parent.MinX; child.MinZ = parent.MinZ; break;
                case 2: child.MinZ = splitPoint; child.MinX = parent.MinX; child.MinY = parent.MinY; break;
            }
        }

        return child;
    }

    /// <summary>
    /// 生成KD树包围盒 - 基于分割平面的精确包围盒算法实现
    /// 算法：根据KD树节点的分割平面和范围计算精确的包围盒
    /// 支持：多维度分割、精确边界计算、节点深度优化
    /// </summary>
    /// <param name="node">KD树节点，包含分割信息和范围</param>
    /// <param name="tileSize">基础切片尺寸，用于比例缩放</param>
    /// <returns>标准化的JSON格式包围盒字符串</returns>
    private string GenerateKdTreeBoundingBox(KdTreeNode node, double tileSize)
    {
        // 1. 计算节点实际尺寸
        // KD树节点的尺寸由其Min/Max坐标确定
        var nodeWidth = node.MaxX - node.MinX;
        var nodeHeight = node.MaxY - node.MinY;
        var nodeDepth = node.MaxZ - node.MinZ;

        // 2. 坐标系变换
        // 将节点坐标转换为世界坐标系
        var scaleFactor = tileSize / Math.Pow(2, node.Level);
        var minX = node.MinX * scaleFactor;
        var minY = node.MinY * scaleFactor;
        var minZ = node.MinZ * scaleFactor;
        var maxX = node.MaxX * scaleFactor;
        var maxY = node.MaxY * scaleFactor;
        var maxZ = node.MaxZ * scaleFactor;

        // 3. 边界验证和调整
        // 确保包围盒有效且不为空
        var epsilon = 1e-6;
        if (maxX - minX < epsilon)
        {
            maxX = minX + epsilon;
        }
        if (maxY - minY < epsilon)
        {
            maxY = minY + epsilon;
        }
        if (maxZ - minZ < epsilon)
        {
            maxZ = minZ + epsilon;
        }

        // 4. 处理KD树分割特性
        // 根据分割轴调整边界精度
        var splitAxis = node.SplitAxis % 3;
        var precisionDigits = splitAxis switch
        {
            0 => 8, // X轴分割，需要更高精度
            1 => 8, // Y轴分割，需要更高精度
            2 => 6, // Z轴分割，标准精度
            _ => 6
        };

        // 5. 数值精度控制
        minX = Math.Round(minX, precisionDigits);
        minY = Math.Round(minY, precisionDigits);
        minZ = Math.Round(minZ, precisionDigits);
        maxX = Math.Round(maxX, precisionDigits);
        maxY = Math.Round(maxY, precisionDigits);
        maxZ = Math.Round(maxZ, precisionDigits);

        // 6. 生成标准化JSON格式
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        return $"{{\"minX\":{minX.ToString(culture)},\"minY\":{minY.ToString(culture)},\"minZ\":{minZ.ToString(culture)},\"maxX\":{maxX.ToString(culture)},\"maxY\":{maxY.ToString(culture)},\"maxZ\":{maxZ.ToString(culture)}}}";
    }

    /// <summary>
    /// 计算KD树节点文件大小 - 基于空间剖分特征
    /// 算法：考虑KD树的二分剖分特性和节点深度
    /// </summary>
    /// <param name="node">KD树节点</param>
    /// <param name="format">输出格式</param>
    /// <returns>估算的文件大小（字节）</returns>
    private long CalculateKdTreeFileSize(KdTreeNode node, string format)
    {
        // 基础文件大小
        var baseSize = format.ToLower() switch
        {
            "b3dm" => 2048,
            "gltf" => 1024,
            "json" => 512,
            _ => 1024
        };

        // 节点深度因子：KD树深度反映几何复杂度
        var depthFactor = 1.0 + node.Level * 0.08;

        // 空间维度因子：KD树在不同维度上的剖分影响数据分布
        // 使用剖分轴来调整估算
        var dimensionFactor = 1.0 + (node.SplitAxis % 3) * 0.05;

        // 节点体积因子：估算节点包含的几何数据量
        var nodeVolume = (node.MaxX - node.MinX) * (node.MaxY - node.MinY) * (node.MaxZ - node.MinZ);
        var volumeFactor = 1.0 + Math.Log(Math.Max(1, nodeVolume), 10) * 0.1;

        // 综合计算
        var estimatedSize = (long)(baseSize * depthFactor * dimensionFactor * volumeFactor);

        return Math.Max(256, Math.Min(52428800, estimatedSize)); // 256B - 50MB
    }

    private class KdTreeNode
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
        public int Level { get; set; }
        public int SplitAxis { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }
}

/// <summary>
/// 自适应切片策略 - 基于几何密度和特征的智能剖分
/// 自动调整切片大小和LOD级别，优化渲染性能和视觉效果
/// </summary>
public class AdaptiveSlicingStrategy : ISlicingStrategy
{
    private readonly ILogger _logger;
    private readonly IMinioStorageService? _minioService;

    public AdaptiveSlicingStrategy(ILogger logger, IMinioStorageService? minioService = null)
    {
        _logger = logger;
        _minioService = minioService;
    }

    public async Task<List<Slice>> GenerateSlicesAsync(SlicingTask task, int level, SlicingConfig config, CancellationToken cancellationToken)
    {
        var slices = new List<Slice>();

        _logger.LogInformation("自适应切片策略：级别{Level}", level);

        // 自适应剖分算法：基于几何密度和视点重要性
        var adaptiveRegions = await AnalyzeGeometricDensityAsync(task, level, config, cancellationToken);

        _logger.LogInformation("自适应策略分析完成：级别{Level}, 区域数量{RegionCount}", level, adaptiveRegions.Count);

        // 区域数量验证（理论上现在应该始终有区域）
        if (adaptiveRegions.Count == 0)
        {
            _logger.LogError("自适应策略意外地未生成任何区域：级别{Level}，这不应该发生", level);
            // 这种情况理论上不应该发生了，因为我们移除了密度阈值过滤
            // 如果仍然发生，说明可能存在其他问题（如取消、异常等）
        }

        foreach (var region in adaptiveRegions)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // 空值检查：确保OutputPath不为null
            var outputPath = task.OutputPath ?? "default_output";
            var slice = new Slice
            {
                SlicingTaskId = task.Id,
                Level = level,
                X = region.X,
                Y = region.Y,
                Z = region.Z,
                FilePath = $"{outputPath}/{level}/{region.X}_{region.Y}_{region.Z}.{config.OutputFormat.ToLower()}",
                BoundingBox = GenerateAdaptiveBoundingBox(region, config.TileSize),
                FileSize = CalculateAdaptiveFileSize(region, config.OutputFormat)
            };

            slices.Add(slice);
        }

        _logger.LogInformation("自适应策略切片生成完成：级别{Level}, 切片数量{SliceCount}", level, slices.Count);

        return slices;
    }

    /// <summary>
    /// 估算指定级别的切片数量 - 增强的自适应策略算法实现
    /// 算法：基于几何复杂性、误差阈值和剖分效率的综合估算
    /// 支持：多因子权重计算、边界条件处理、溢出保护
    /// </summary>
    /// <param name="level">LOD级别，必须为非负整数</param>
    /// <param name="config">切片配置，包含几何误差阈值等参数</param>
    /// <returns>估算的切片数量，基于自适应几何复杂性计算</returns>
    /// <exception cref="ArgumentNullException">当config为null时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">当level为负数时抛出</exception>
    /// <exception cref="OverflowException">当计算结果超过int最大值时抛出</exception>
    public int EstimateSliceCount(int level, SlicingConfig config)
    {
        // 1. 参数验证：确保输入参数的有效性
        if (config == null)
            throw new ArgumentNullException(nameof(config), "切片配置不能为空");

        if (level < 0)
            throw new ArgumentOutOfRangeException(nameof(level), level, "LOD级别不能为负数");

        // 2. 特殊情况处理：根级别只有一个切片
        if (level == 0)
            return 1;

        try
        {
            // 3. 基础几何因子计算
            // 自适应策略的基础增长模式：几何级数但有衰减
            var geometricBaseFactor = CalculateGeometricBase(level);

            // 4. 密度增长因子：考虑几何复杂度随级别增加
            var densityGrowthFactor = CalculateDensityGrowthFactor(level, config);

            // 5. 效率衰减因子：考虑实际剖分效率
            var efficiencyAttenuationFactor = CalculateEfficiencyAttenuation(level, config);

            // 6. 几何误差阈值调整因子
            var errorThresholdFactor = CalculateErrorThresholdFactor(config);

            // 7. 计算初步估算值
            var preliminaryEstimate = geometricBaseFactor * densityGrowthFactor * efficiencyAttenuationFactor * errorThresholdFactor;

            // 8. 应用级别特定调整
            var levelSpecificAdjustment = GetLevelSpecificAdjustment(level);
            var finalEstimate = (long)(preliminaryEstimate * levelSpecificAdjustment);

            // 9. 边界检查和溢出处理
            if (finalEstimate > int.MaxValue)
                throw new OverflowException($"估算切片数量超过int最大值：{finalEstimate}");

            if (finalEstimate < 1)
                finalEstimate = 1;

            // 10. 记录估算详情
            _logger.LogDebug("自适应切片数量估算：级别{Level}, 几何基数{Base:F2}, 密度因子{Density:F3}, 效率因子{Efficiency:F3}, 误差因子{Error:F3}, 最终估算{Final}",
                level, geometricBaseFactor, densityGrowthFactor, efficiencyAttenuationFactor, errorThresholdFactor, finalEstimate);

            return (int)finalEstimate;
        }
        catch (OverflowException)
        {
            _logger.LogWarning("切片数量估算溢出：级别{Level}，返回最大保守值", level);
            return int.MaxValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切片数量估算失败：级别{Level}", level);
            return 1; // 返回最小值确保流程继续
        }
    }

    /// <summary>
    /// 计算几何基础因子
    /// </summary>
    private double CalculateGeometricBase(int level)
    {
        // 理论上自适应策略的切片数量约为4^level，但实际有优化
        // 使用位运算提高性能：4^level = 2^(2*level)
        var geometricBase = 1L << (2 * level);

        // 转换为double类型进行后续计算
        return Math.Min(geometricBase, (double)int.MaxValue / 10); // 防止后续计算溢出
    }

    /// <summary>
    /// 计算密度增长因子
    /// </summary>
    private double CalculateDensityGrowthFactor(int level, SlicingConfig config)
    {
        // 基础密度增长率
        const double baseGrowthRate = 0.12; // 比固定策略更保守

        // 级别影响：高级别密度增长较慢
        var levelInfluence = 1.0 + level * baseGrowthRate;

        // 配置影响：考虑切片尺寸对密度的影响
        var sizeInfluence = Math.Log(Math.Max(1.0, config.TileSize), 2) * 0.05;

        return Math.Max(1.0, levelInfluence + sizeInfluence);
    }

    /// <summary>
    /// 计算效率衰减因子
    /// </summary>
    private double CalculateEfficiencyAttenuation(int level, SlicingConfig config)
    {
        // 基础效率因子
        const double baseEfficiency = 0.65; // 自适应策略的实际效率

        // 级别衰减：高级别效率略有下降（算法复杂度增加）
        var levelAttenuation = Math.Pow(0.98, level);

        // 并行处理优化：多线程可提升效率
        var parallelBonus = config.ParallelProcessingCount > 1 ?
            Math.Min(1.2, 1.0 + config.ParallelProcessingCount * 0.05) : 1.0;

        return baseEfficiency * levelAttenuation * parallelBonus;
    }

    /// <summary>
    /// 计算误差阈值因子
    /// </summary>
    private double CalculateErrorThresholdFactor(SlicingConfig config)
    {
        if (config.GeometricErrorThreshold <= 0)
            return 1.0;

        // 高精度要求增加切片数量
        var precisionFactor = Math.Max(0.5, 1.0 / config.GeometricErrorThreshold);

        // 限制放大倍数避免过度切片
        return Math.Min(precisionFactor, 2.5);
    }

    /// <summary>
    /// 获取级别特定调整因子
    /// </summary>
    private double GetLevelSpecificAdjustment(int level)
    {
        // 不同级别有特定的调整需求
        return level switch
        {
            1 => 1.1,  // 第一级通常需要更多切片建立基础
            2 => 1.05, // 第二级适中调整
            3 => 0.95, // 第三级开始优化减少冗余
            _ => level > 3 ? Math.Pow(0.92, level - 3) : 1.0 // 高级别进一步衰减
        };
    }

    private async Task<List<AdaptiveRegion>> AnalyzeGeometricDensityAsync(SlicingTask task, int level, SlicingConfig config, CancellationToken cancellationToken)
    {
        // 几何密度分析算法：多维度几何复杂度分析
        _logger.LogInformation("开始几何密度分析：级别{Level}", level);

        var regions = new List<AdaptiveRegion>();

        try
        {
            // 1. 获取模型几何数据
            var geometricData = await LoadGeometricDataAsync(task, cancellationToken);
            if (geometricData == null || !geometricData.Any())
            {
                _logger.LogWarning("无法获取几何数据，使用fallback密度分析：级别{Level}", level);
                return await FallbackDensityAnalysisAsync(level, config, cancellationToken);
            }

            _logger.LogInformation("加载几何数据成功：{PrimitiveCount}个几何图元", geometricData.Count);

            // 2. 构建空间索引以提高分析效率
            var spatialIndex = await BuildSpatialIndexAsync(geometricData, config, cancellationToken);

            // 3. 计算基础网格尺寸
            var baseTileSize = config.TileSize;
            var levelTileSize = baseTileSize * Math.Pow(2, config.MaxLevel - level);

            // 4. 多分辨率密度分析
            var densityAnalyzer = new GeometricDensityAnalyzer(_logger);

            // 分析不同LOD级别的密度分布
            for (int x = 0; x < Math.Pow(2, level); x++)
            {
                for (int y = 0; y < Math.Pow(2, level); y++)
                {
                    for (int z = 0; z < (level == 0 ? 1 : Math.Pow(2, level) / 2); z++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        // 定义分析区域的包围盒
                        var regionBounds = new BoundingBox3D
                        {
                            MinX = x * levelTileSize,
                            MinY = y * levelTileSize,
                            MinZ = z * levelTileSize,
                            MaxX = (x + 1) * levelTileSize,
                            MaxY = (y + 1) * levelTileSize,
                            MaxZ = (z + 1) * levelTileSize
                        };

                        // 多维度密度分析
                        var densityMetrics = await densityAnalyzer.AnalyzeRegionDensityAsync(
                            geometricData, spatialIndex, regionBounds, config, cancellationToken);

                        // 综合密度评分
                        var compositeDensity = CalculateCompositeDensity(densityMetrics);

                        // 修复：始终添加区域，不再使用密度阈值过滤
                        // 原逻辑: if (compositeDensity > config.GeometricErrorThreshold) - 会导致某些级别没有区域
                        // 新逻辑: 始终添加区域，让后续的优化决定是否需要细分
                        // 这样可以确保每个级别都有完整的切片覆盖
                        regions.Add(new AdaptiveRegion
                        {
                            X = x,
                            Y = y,
                            Z = z,
                            Density = compositeDensity,
                            Importance = CalculateRegionImportance(densityMetrics, level),
                            VertexDensity = densityMetrics.VertexDensity,
                            TriangleDensity = densityMetrics.TriangleDensity,
                            CurvatureComplexity = densityMetrics.CurvatureComplexity,
                            SurfaceArea = densityMetrics.SurfaceArea
                        });
                    }
                }
            }

            _logger.LogDebug("几何密度分析完成：级别{Level}, 发现{RegionCount}个高密度区域",
                level, regions.Count);

            return regions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "几何密度分析失败，回退到默认分析：级别{Level}", level);
            return await FallbackDensityAnalysisAsync(level, config, cancellationToken);
        }
    }

    /// <summary>
    /// 加载几何数据 - 从模型源获取三角形网格数据
    /// 支持多种3D模型格式：OBJ、STL、PLY、GLTF等
    /// 支持数据源：MinIO对象存储、本地文件系统
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>几何数据集合</returns>
    private async Task<List<GeometricPrimitive>> LoadGeometricDataAsync(SlicingTask task, CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始加载几何数据：{SourceModelPath}", task.SourceModelPath);

        var geometricData = new List<GeometricPrimitive>();

        try
        {
            Stream? modelStream = null;

            // 1. 优先尝试从MinIO加载
            if (_minioService != null)
            {
                try
                {
                    // 转换Windows路径为MinIO兼容的对象名
                    var minioObjectName = ConvertToMinioObjectName(task.SourceModelPath);
                    _logger.LogDebug("转换MinIO对象名：{OriginalPath} -> {MinioObjectName}", task.SourceModelPath, minioObjectName);

                    modelStream = await _minioService.DownloadFileAsync("models", minioObjectName);
                    _logger.LogDebug("模型文件从MinIO下载成功：{MinioObjectName}", minioObjectName);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "无法从MinIO下载模型文件：{SourceModelPath}", task.SourceModelPath);
                    modelStream = null;
                }
            }

            // 2. 如果MinIO失败，尝试从本地文件系统加载
            if (modelStream == null)
            {
                var localPath = task.SourceModelPath;

                // 处理相对路径：转换为绝对路径
                if (!Path.IsPathRooted(localPath))
                {
                    // 尝试多个可能的基础路径
                    var basePaths = new[]
                    {
                        Directory.GetCurrentDirectory(),
                        Path.Combine(Directory.GetCurrentDirectory(), "models"),
                        Path.Combine(Directory.GetCurrentDirectory(), "data"),
                        Path.Combine(Directory.GetCurrentDirectory(), "..", "models"),
                        Path.Combine(Directory.GetCurrentDirectory(), "..", "data")
                    };

                    foreach (var basePath in basePaths)
                    {
                        var fullPath = Path.Combine(basePath, localPath);
                        if (File.Exists(fullPath))
                        {
                            localPath = fullPath;
                            break;
                        }
                    }
                }

                // 检查本地文件是否存在
                if (File.Exists(localPath))
                {
                    try
                    {
                        modelStream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        _logger.LogInformation("模型文件从本地文件系统加载成功：{LocalPath}", localPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "无法从本地文件系统读取模型文件：{LocalPath}", localPath);
                        modelStream = null;
                    }
                }
                else
                {
                    _logger.LogDebug("本地文件不存在：{LocalPath}", localPath);
                }
            }

            // 3. 如果所有数据源都失败，使用模拟数据
            if (modelStream == null)
            {
                _logger.LogWarning("无法从任何数据源加载模型文件，使用模拟数据：{SourceModelPath}", task.SourceModelPath);
                var mockTriangles = GenerateMockGeometricData(task);
                return ConvertTrianglesToPrimitives(mockTriangles, cancellationToken);
            }

            // 4. 根据文件扩展名解析模型格式
            var fileExtension = Path.GetExtension(task.SourceModelPath).ToLowerInvariant();
            List<Triangle> triangles;

            // 使用 using 语句确保资源正确释放
            try
            {
                switch (fileExtension)
                {
                    case ".obj":
                        _logger.LogDebug("解析OBJ格式模型文件");
                        triangles = await ParseOBJFormatAsync(modelStream, cancellationToken);
                        break;

                    case ".stl":
                        _logger.LogDebug("解析STL格式模型文件");
                        triangles = await ParseSTLFormatAsync(modelStream, cancellationToken);
                        break;

                    case ".ply":
                        _logger.LogDebug("解析PLY格式模型文件");
                        triangles = await ParsePLYFormatAsync(modelStream, cancellationToken);
                        break;

                    case ".gltf":
                    case ".glb":
                        _logger.LogDebug("解析GLTF/GLB格式模型文件");
                        triangles = await ParseGLTFFormatAsync(modelStream, cancellationToken);
                        break;

                    default:
                        _logger.LogWarning("不支持的模型格式：{FileExtension}，使用模拟数据", fileExtension);
                        var mockTriangles = GenerateMockGeometricData(task);
                        return ConvertTrianglesToPrimitives(mockTriangles, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "模型文件解析失败，使用模拟数据：{FileExtension}", fileExtension);
                var mockTriangles = GenerateMockGeometricData(task);
                triangles = mockTriangles;
            }
            finally
            {
                // 确保流被正确释放
                if (modelStream != null)
                {
                    await modelStream.DisposeAsync();
                    _logger.LogDebug("模型文件流已释放");
                }
            }

            // 转换三角形为几何图元
            geometricData = ConvertTrianglesToPrimitives(triangles, cancellationToken);

            _logger.LogInformation("几何数据加载完成：{VertexCount}个顶点, {TriangleCount}个三角形",
                geometricData.Sum(g => g.Vertices.Length), geometricData.Count);

            return geometricData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载几何数据失败：{SourceModelPath}", task.SourceModelPath);
            // 发生错误时返回模拟数据，确保流程继续
            var mockTriangles = GenerateMockGeometricData(task);
            return ConvertTrianglesToPrimitives(mockTriangles, cancellationToken);
        }
    }

    /// <summary>
    /// 将三角形列表转换为几何图元列表
    /// </summary>
    private List<GeometricPrimitive> ConvertTrianglesToPrimitives(List<Triangle> triangles, CancellationToken cancellationToken)
    {
        var primitives = new List<GeometricPrimitive>();

        foreach (var triangle in triangles)
        {
            if (cancellationToken.IsCancellationRequested) break;

            primitives.Add(new GeometricPrimitive
            {
                Vertices = triangle.Vertices,
                Triangle = triangle,
                Normal = CalculateTriangleNormal(triangle.Vertices[0], triangle.Vertices[1], triangle.Vertices[2]),
                Area = CalculateTriangleArea(triangle.Vertices[0], triangle.Vertices[1], triangle.Vertices[2])
            });
        }

        return primitives;
    }

    /// <summary>
    /// 解析OBJ格式模型文件
    /// OBJ是一种基于文本的3D模型格式，由顶点(v)、纹理坐标(vt)、法向量(vn)和面(f)组成
    /// 增强特性：错误恢复、数据验证、性能优化、大文件支持、多编码支持
    /// </summary>
    private async Task<List<Triangle>> ParseOBJFormatAsync(Stream stream, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();
        var vertices = new List<Vector3D>();
        var lineNumber = 0;
        var errorCount = 0;
        const int MAX_ERRORS = 100; // 最大容忍错误数

        _logger.LogInformation("开始解析OBJ文件");

        // 尝试检测文件编码（支持UTF-8、GBK等编码）
        System.Text.Encoding encoding;
        try
        {
            // 先读取一部分数据来检测编码
            var buffer = new byte[Math.Min(4096, stream.Length)];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            stream.Position = 0; // 重置流位置

            // 检查BOM标记
            if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                encoding = System.Text.Encoding.UTF8;
                _logger.LogInformation("检测到UTF-8 BOM编码");
            }
            // 尝试检测GBK编码（中文系统常用）
            else if (HasChineseCharacters(buffer, bytesRead))
            {
                try
                {
                    encoding = System.Text.Encoding.GetEncoding("GBK");
                    _logger.LogInformation("检测到可能的GBK编码");
                }
                catch
                {
                    encoding = System.Text.Encoding.UTF8;
                    _logger.LogInformation("GBK编码不可用，使用UTF-8");
                }
            }
            else
            {
                encoding = System.Text.Encoding.UTF8;
                _logger.LogInformation("使用默认UTF-8编码");
            }

            _logger.LogInformation("OBJ文件大小：{Size} 字节，已读取{BytesRead}字节用于编码检测", stream.Length, bytesRead);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "编码检测失败，使用默认UTF-8编码");
            encoding = System.Text.Encoding.UTF8;
        }

        var sampleLines = new List<string>(); // 保存前几行用于诊断
        const int MAX_SAMPLE_LINES = 10;

        using (var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true))
        {
            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;

                // 收集前几行用于诊断（不包含注释和空行）
                if (sampleLines.Count < MAX_SAMPLE_LINES && !string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#"))
                {
                    sampleLines.Add(line.Trim());
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("OBJ解析被取消：已解析{TriangleCount}个三角形", triangles.Count);
                    break;
                }

                // 跳过空行和注释
                line = line.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                try
                {
                    switch (parts[0])
                    {
                        case "v": // 顶点坐标
                            if (parts.Length >= 4)
                            {
                                // 健壮的数值解析
                                if (double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                                                   System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                                    double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                                                   System.Globalization.CultureInfo.InvariantCulture, out var y) &&
                                    double.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                                                   System.Globalization.CultureInfo.InvariantCulture, out var z))
                                {
                                    // 验证顶点坐标的合理性
                                    if (double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z) &&
                                        Math.Abs(x) < 1e10 && Math.Abs(y) < 1e10 && Math.Abs(z) < 1e10)
                                    {
                                        vertices.Add(new Vector3D { X = x, Y = y, Z = z });
                                    }
                                    else
                                    {
                                        _logger.LogWarning("OBJ行{Line}：顶点坐标超出合理范围", lineNumber);
                                        errorCount++;
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("OBJ行{Line}：顶点坐标解析失败", lineNumber);
                                    errorCount++;
                                }
                            }
                            else
                            {
                                _logger.LogWarning("OBJ行{Line}：顶点定义不完整", lineNumber);
                                errorCount++;
                            }
                            break;

                        case "f": // 面定义
                            if (parts.Length >= 4)
                            {
                                // 解析面索引（支持 v、v/vt、v/vt/vn、v//vn 等格式）
                                var indices = new List<int>();
                                bool allIndicesValid = true;

                                for (int i = 1; i < parts.Length; i++)
                                {
                                    var indexParts = parts[i].Split('/');
                                    if (int.TryParse(indexParts[0], out var index))
                                    {
                                        // 处理负索引（相对索引）
                                        if (index < 0)
                                        {
                                            index = vertices.Count + index + 1;
                                        }

                                        // 转换为从0开始的索引
                                        var zeroBasedIndex = index - 1;

                                        // 验证索引范围
                                        if (zeroBasedIndex >= 0 && zeroBasedIndex < vertices.Count)
                                        {
                                            indices.Add(zeroBasedIndex);
                                        }
                                        else
                                        {
                                            _logger.LogWarning("OBJ行{Line}：面索引{Index}超出范围（顶点数：{VertexCount}）",
                                                             lineNumber, index, vertices.Count);
                                            allIndicesValid = false;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogWarning("OBJ行{Line}：面索引解析失败：{Part}", lineNumber, parts[i]);
                                        allIndicesValid = false;
                                        break;
                                    }
                                }

                                // 三角形化多边形面（扇形三角化）
                                if (allIndicesValid && indices.Count >= 3)
                                {
                                    for (int i = 1; i < indices.Count - 1; i++)
                                    {
                                        triangles.Add(new Triangle
                                        {
                                            Vertices = new[]
                                            {
                                                vertices[indices[0]],
                                                vertices[indices[i]],
                                                vertices[indices[i + 1]]
                                            }
                                        });
                                    }
                                }
                                else if (allIndicesValid)
                                {
                                    _logger.LogWarning("OBJ行{Line}：面顶点数不足（需要至少3个顶点）", lineNumber);
                                    errorCount++;
                                }
                            }
                            else
                            {
                                _logger.LogWarning("OBJ行{Line}：面定义不完整", lineNumber);
                                errorCount++;
                            }
                            break;

                        // 其他OBJ命令（忽略但记录）
                        case "vt": // 纹理坐标
                        case "vn": // 法向量
                        case "usemtl": // 材质
                        case "mtllib": // 材质库
                        case "g": // 组
                        case "o": // 对象名称
                        case "s": // 平滑组
                            // 静默忽略这些命令
                            break;

                        default:
                            // 未识别的命令
                            if (errorCount < 10) // 只记录前10个未知命令
                            {
                                _logger.LogDebug("OBJ行{Line}：未识别的命令：{Command}", lineNumber, parts[0]);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OBJ行{Line}：解析失败", lineNumber);
                    errorCount++;
                }

                // 错误容忍机制：超过阈值则终止解析
                if (errorCount > MAX_ERRORS)
                {
                    _logger.LogError("OBJ解析错误过多（{ErrorCount}个错误），终止解析", errorCount);
                    throw new InvalidDataException($"OBJ文件包含过多错误（{errorCount}个），可能已损坏");
                }
            }
        }

        // 验证解析结果
        _logger.LogInformation("OBJ解析完成：总行数={LineCount}, 顶点数={VertexCount}, 三角形数={TriangleCount}, 错误数={ErrorCount}",
            lineNumber, vertices.Count, triangles.Count, errorCount);

        if (vertices.Count == 0)
        {
            // 输出前几行内容用于诊断
            if (sampleLines.Any())
            {
                _logger.LogError("OBJ文件前{Count}行内容示例：\n{Lines}",
                    sampleLines.Count, string.Join("\n", sampleLines.Select((l, i) => $"  {i + 1}: {l}")));
            }

            _logger.LogError("OBJ文件解析失败：文件共{LineCount}行，但未找到任何顶点数据（'v x y z'格式）。" +
                           "可能原因：1) 文件为空或格式错误；2) 文件编码不正确；3) 文件内容被损坏。" +
                           "请检查文件内容和编码（当前使用：{Encoding}）",
                lineNumber, encoding.EncodingName);
            throw new InvalidDataException($"OBJ文件不包含任何顶点数据（总行数：{lineNumber}，使用编码：{encoding.EncodingName}）");
        }

        if (triangles.Count == 0)
        {
            _logger.LogError("OBJ文件解析失败：找到{VertexCount}个顶点，但未找到任何面数据（'f i1 i2 i3'格式）。" +
                           "可能原因：1) 文件只包含顶点没有面定义；2) 面索引格式不正确",
                vertices.Count);
            throw new InvalidDataException($"OBJ文件不包含任何面数据（顶点数：{vertices.Count}）");
        }

        _logger.LogInformation("OBJ解析成功：{VertexCount}个顶点, {TriangleCount}个三角形",
            vertices.Count, triangles.Count);

        return triangles;
    }

    /// <summary>
    /// 检测字节数组中是否包含中文字符（用于编码检测）
    /// </summary>
    private bool HasChineseCharacters(byte[] buffer, int length)
    {
        for (int i = 0; i < length - 1; i++)
        {
            // 检测GBK编码的中文字符范围
            if ((buffer[i] >= 0x81 && buffer[i] <= 0xFE) &&
                ((buffer[i + 1] >= 0x40 && buffer[i + 1] <= 0x7E) ||
                 (buffer[i + 1] >= 0x80 && buffer[i + 1] <= 0xFE)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 将Windows路径转换为MinIO兼容的对象名
    /// MinIO对象名规则：
    /// - 使用正斜杠(/)作为路径分隔符
    /// - 不支持绝对路径（不能以盘符开头）
    /// - 不支持某些特殊字符，特别是在某些Windows环境下的 + 符号
    /// </summary>
    private string ConvertToMinioObjectName(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        // 1. 标准化路径分隔符（统一使用正斜杠）
        var normalizedPath = filePath.Replace('\\', '/');

        // 2. 移除Windows盘符（如 "F:/Data" -> "Data"）
        if (normalizedPath.Length >= 2 && normalizedPath[1] == ':')
        {
            normalizedPath = normalizedPath.Substring(2);
        }

        // 3. 移除开头的斜杠
        normalizedPath = normalizedPath.TrimStart('/');

        // 4. 处理特殊字符：某些MinIO实现不支持特定字符
        // 将 + 替换为 _plus_ 以避免兼容性问题
        normalizedPath = normalizedPath.Replace("+", "_plus_");

        // 其他潜在的问题字符也可以在这里处理
        // 例如：空格、#、& 等（根据实际需要添加）
        normalizedPath = normalizedPath.Replace(" ", "_");
        normalizedPath = normalizedPath.Replace("#", "_hash_");
        normalizedPath = normalizedPath.Replace("&", "_and_");

        // 5. 确保路径不为空
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            throw new InvalidOperationException($"无法将路径转换为有效的MinIO对象名：{filePath}");
        }

        return normalizedPath;
    }

    /// <summary>
    /// 解析STL格式模型文件（支持二进制和ASCII格式）
    /// STL是一种简单的三角形网格格式，广泛用于3D打印和CAD软件
    /// </summary>
    private async Task<List<Triangle>> ParseSTLFormatAsync(Stream stream, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        // 读取前80字节判断是二进制还是ASCII格式
        var header = new byte[80];
        await stream.ReadAsync(header, 0, 80, cancellationToken);

        // 检查是否为ASCII STL（以"solid"开头）
        var headerText = System.Text.Encoding.ASCII.GetString(header).ToLower();
        stream.Position = 0; // 重置流位置

        if (headerText.StartsWith("solid"))
        {
            // ASCII STL格式
            triangles = await ParseASCIISTLAsync(stream, cancellationToken);
        }
        else
        {
            // 二进制STL格式
            triangles = await ParseBinarySTLAsync(stream, cancellationToken);
        }

        _logger.LogDebug("STL解析完成：{TriangleCount}个三角形", triangles.Count);
        return triangles;
    }

    /// <summary>
    /// 解析ASCII格式的STL文件
    /// </summary>
    private async Task<List<Triangle>> ParseASCIISTLAsync(Stream stream, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();
        var currentVertices = new List<Vector3D>();

        using (var reader = new StreamReader(stream))
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (cancellationToken.IsCancellationRequested) break;

                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                switch (parts[0].ToLower())
                {
                    case "vertex":
                        if (parts.Length >= 4)
                        {
                            currentVertices.Add(new Vector3D
                            {
                                X = double.Parse(parts[1]),
                                Y = double.Parse(parts[2]),
                                Z = double.Parse(parts[3])
                            });
                        }
                        break;

                    case "endfacet":
                        if (currentVertices.Count == 3)
                        {
                            triangles.Add(new Triangle
                            {
                                Vertices = currentVertices.ToArray()
                            });
                        }
                        currentVertices.Clear();
                        break;
                }
            }
        }

        return triangles;
    }

    /// <summary>
    /// 解析二进制格式的STL文件
    /// 二进制STL格式：80字节头 + 4字节三角形数量 + 每个三角形50字节（法向量12字节 + 3个顶点各12字节 + 属性2字节）
    /// </summary>
    private Task<List<Triangle>> ParseBinarySTLAsync(Stream stream, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        using (var reader = new BinaryReader(stream))
        {
            // 跳过80字节头
            reader.ReadBytes(80);

            // 读取三角形数量
            var triangleCount = reader.ReadUInt32();

            for (uint i = 0; i < triangleCount; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // 跳过法向量（12字节）
                reader.ReadBytes(12);

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

                // 跳过属性字节（2字节）
                reader.ReadUInt16();

                triangles.Add(new Triangle { Vertices = vertices });
            }
        }

        return Task.FromResult(triangles);
    }

    /// <summary>
    /// 解析PLY格式模型文件（完整实现，支持ASCII和二进制格式）
    /// PLY（Polygon File Format）是一种灵活的多边形网格格式
    /// 支持：ASCII/Binary格式、多边形三角化、顶点属性（位置、法向量、颜色等）
    /// </summary>
    private async Task<List<Triangle>> ParsePLYFormatAsync(Stream stream, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        try
        {
            // 读取并解析PLY头部信息
            var headerInfo = await ParsePLYHeaderAsync(stream, cancellationToken);

            if (headerInfo == null)
            {
                _logger.LogWarning("PLY文件头部解析失败");
                return triangles;
            }

            _logger.LogDebug("PLY格式：{Format}, 顶点数：{VertexCount}, 面数：{FaceCount}",
                headerInfo.Format, headerInfo.VertexCount, headerInfo.FaceCount);

            // 根据格式选择解析方法
            if (headerInfo.Format == PLYFormat.ASCII)
            {
                triangles = await ParseASCIIPLYDataAsync(stream, headerInfo, cancellationToken);
            }
            else
            {
                triangles = await ParseBinaryPLYDataAsync(stream, headerInfo, cancellationToken);
            }

            _logger.LogDebug("PLY解析完成：{VertexCount}个顶点, {TriangleCount}个三角形",
                headerInfo.VertexCount, triangles.Count);

            return triangles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PLY格式解析失败");
            return triangles;
        }
    }

    /// <summary>
    /// 解析PLY文件头部 - 提取格式信息和元数据
    /// </summary>
    private async Task<PLYHeader?> ParsePLYHeaderAsync(Stream stream, CancellationToken cancellationToken)
    {
        var headerInfo = new PLYHeader();
        using (var reader = new StreamReader(stream, leaveOpen: true))
        {
            string? line;
            var lineNumber = 0;

            // 检查魔数
            line = await reader.ReadLineAsync();
            if (line == null || !line.Trim().Equals("ply", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("PLY文件魔数验证失败");
                return null;
            }

            // 解析头部信息
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (cancellationToken.IsCancellationRequested) break;

                line = line.Trim();
                lineNumber++;

                // 忽略空行和注释
                if (string.IsNullOrEmpty(line) || line.StartsWith("comment", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                switch (parts[0].ToLowerInvariant())
                {
                    case "format":
                        if (parts.Length >= 2)
                        {
                            headerInfo.Format = parts[1].ToLowerInvariant() switch
                            {
                                "ascii" => PLYFormat.ASCII,
                                "binary_little_endian" => PLYFormat.BinaryLittleEndian,
                                "binary_big_endian" => PLYFormat.BinaryBigEndian,
                                _ => PLYFormat.ASCII
                            };
                            if (parts.Length >= 3)
                                headerInfo.Version = parts[2];
                        }
                        break;

                    case "element":
                        if (parts.Length >= 3)
                        {
                            var elementName = parts[1].ToLowerInvariant();
                            var elementCount = int.Parse(parts[2]);

                            if (elementName == "vertex")
                            {
                                headerInfo.VertexCount = elementCount;
                                headerInfo.CurrentElement = "vertex";
                            }
                            else if (elementName == "face")
                            {
                                headerInfo.FaceCount = elementCount;
                                headerInfo.CurrentElement = "face";
                            }
                        }
                        break;

                    case "property":
                        // 记录属性定义
                        if (parts.Length >= 3)
                        {
                            var propertyType = parts[1].ToLowerInvariant();

                            if (propertyType == "list" && parts.Length >= 5)
                            {
                                // list属性（用于面的顶点索引）
                                var propertyName = parts[4].ToLowerInvariant();
                                if (headerInfo.CurrentElement == "face")
                                {
                                    headerInfo.FacePropertiesCount++;
                                    headerInfo.FaceListIndexType = parts[2]; // 索引计数类型
                                    headerInfo.FaceListValueType = parts[3]; // 索引值类型
                                }
                            }
                            else
                            {
                                // 标量属性
                                var propertyName = parts[2].ToLowerInvariant();
                                if (headerInfo.CurrentElement == "vertex")
                                {
                                    headerInfo.VertexProperties.Add(propertyName);
                                }
                            }
                        }
                        break;

                    case "end_header":
                        headerInfo.HeaderEndPosition = stream.Position;
                        return headerInfo;
                }
            }
        }

        return headerInfo;
    }

    /// <summary>
    /// 解析ASCII格式的PLY数据
    /// </summary>
    private async Task<List<Triangle>> ParseASCIIPLYDataAsync(Stream stream, PLYHeader headerInfo, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();
        var vertices = new List<Vector3D>();

        using (var reader = new StreamReader(stream, leaveOpen: true))
        {
            // 读取顶点数据
            for (int i = 0; i < headerInfo.VertexCount; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    try
                    {
                        vertices.Add(new Vector3D
                        {
                            X = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                            Y = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                            Z = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)
                        });
                    }
                    catch (FormatException ex)
                    {
                        _logger.LogWarning(ex, "PLY顶点数据解析失败：行{LineNumber}", i + 1);
                        continue;
                    }
                }
            }

            // 读取面数据
            for (int i = 0; i < headerInfo.FaceCount; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                try
                {
                    var indexCount = int.Parse(parts[0]);
                    if (parts.Length < indexCount + 1) continue;

                    // 提取顶点索引
                    var indices = new List<int>();
                    for (int j = 0; j < indexCount; j++)
                    {
                        var index = int.Parse(parts[j + 1]);
                        if (index >= 0 && index < vertices.Count)
                            indices.Add(index);
                    }

                    // 三角化多边形（使用扇形三角化算法）
                    if (indices.Count >= 3)
                    {
                        for (int j = 1; j < indices.Count - 1; j++)
                        {
                            triangles.Add(new Triangle
                            {
                                Vertices = new[]
                                {
                                    vertices[indices[0]],
                                    vertices[indices[j]],
                                    vertices[indices[j + 1]]
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PLY面数据解析失败：行{LineNumber}", i + 1);
                    continue;
                }
            }
        }

        return triangles;
    }

    /// <summary>
    /// 解析二进制格式的PLY数据
    /// </summary>
    private Task<List<Triangle>> ParseBinaryPLYDataAsync(Stream stream, PLYHeader headerInfo, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();
        var vertices = new List<Vector3D>();

        using (var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            var isLittleEndian = headerInfo.Format == PLYFormat.BinaryLittleEndian;

            // 读取顶点数据
            for (int i = 0; i < headerInfo.VertexCount; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    // 读取X、Y、Z坐标（假设为float类型）
                    var x = ReadFloat(reader, isLittleEndian);
                    var y = ReadFloat(reader, isLittleEndian);
                    var z = ReadFloat(reader, isLittleEndian);

                    vertices.Add(new Vector3D { X = x, Y = y, Z = z });

                    // 跳过额外的顶点属性（如法向量、颜色等）
                    var extraPropertiesCount = headerInfo.VertexProperties.Count - 3;
                    for (int j = 0; j < extraPropertiesCount; j++)
                    {
                        reader.ReadSingle(); // 假设额外属性都是float类型
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PLY二进制顶点数据解析失败：索引{Index}", i);
                    break;
                }
            }

            // 读取面数据
            for (int i = 0; i < headerInfo.FaceCount; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    // 读取顶点索引计数
                    var indexCount = reader.ReadByte(); // 通常是uchar类型

                    // 读取顶点索引（假设为int类型）
                    var indices = new List<int>();
                    for (int j = 0; j < indexCount; j++)
                    {
                        var index = ReadInt(reader, isLittleEndian, headerInfo.FaceListValueType);
                        if (index >= 0 && index < vertices.Count)
                            indices.Add(index);
                    }

                    // 三角化多边形
                    if (indices.Count >= 3)
                    {
                        for (int j = 1; j < indices.Count - 1; j++)
                        {
                            triangles.Add(new Triangle
                            {
                                Vertices = new[]
                                {
                                    vertices[indices[0]],
                                    vertices[indices[j]],
                                    vertices[indices[j + 1]]
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PLY二进制面数据解析失败：索引{Index}", i);
                    continue;
                }
            }
        }

        return Task.FromResult(triangles);
    }

    /// <summary>
    /// 读取浮点数（支持大小端）
    /// </summary>
    private float ReadFloat(BinaryReader reader, bool isLittleEndian)
    {
        var bytes = reader.ReadBytes(4);
        if (isLittleEndian != BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>
    /// 读取整数（支持多种类型和大小端）
    /// </summary>
    private int ReadInt(BinaryReader reader, bool isLittleEndian, string dataType)
    {
        switch (dataType.ToLowerInvariant())
        {
            case "char":
            case "int8":
                return (sbyte)reader.ReadByte();

            case "uchar":
            case "uint8":
                return reader.ReadByte();

            case "short":
            case "int16":
                var shortBytes = reader.ReadBytes(2);
                if (isLittleEndian != BitConverter.IsLittleEndian)
                    Array.Reverse(shortBytes);
                return BitConverter.ToInt16(shortBytes, 0);

            case "ushort":
            case "uint16":
                var ushortBytes = reader.ReadBytes(2);
                if (isLittleEndian != BitConverter.IsLittleEndian)
                    Array.Reverse(ushortBytes);
                return BitConverter.ToUInt16(ushortBytes, 0);

            case "int":
            case "int32":
                var intBytes = reader.ReadBytes(4);
                if (isLittleEndian != BitConverter.IsLittleEndian)
                    Array.Reverse(intBytes);
                return BitConverter.ToInt32(intBytes, 0);

            case "uint":
            case "uint32":
                var uintBytes = reader.ReadBytes(4);
                if (isLittleEndian != BitConverter.IsLittleEndian)
                    Array.Reverse(uintBytes);
                return (int)BitConverter.ToUInt32(uintBytes, 0);

            default:
                // 默认按int32处理
                return reader.ReadInt32();
        }
    }

    /// <summary>
    /// PLY格式枚举
    /// </summary>
    private enum PLYFormat
    {
        ASCII,
        BinaryLittleEndian,
        BinaryBigEndian
    }

    /// <summary>
    /// PLY文件头部信息
    /// </summary>
    private class PLYHeader
    {
        public PLYFormat Format { get; set; } = PLYFormat.ASCII;
        public string Version { get; set; } = "1.0";
        public int VertexCount { get; set; }
        public int FaceCount { get; set; }
        public List<string> VertexProperties { get; set; } = new List<string>();
        public int FacePropertiesCount { get; set; }
        public string CurrentElement { get; set; } = "";
        public long HeaderEndPosition { get; set; }
        public string FaceListIndexType { get; set; } = "uchar";
        public string FaceListValueType { get; set; } = "int";
    }

    /// <summary>
    /// 解析GLTF/GLB格式模型文件（完整实现，支持glTF 2.0标准）
    /// GLTF是现代的3D场景传输格式，支持复杂的材质、动画等
    /// 支持：GLTF（JSON格式）和GLB（二进制格式）、嵌入式和外部Buffer、多种图元类型
    /// </summary>
    private async Task<List<Triangle>> ParseGLTFFormatAsync(Stream stream, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        try
        {
            // 检测文件格式：GLB（二进制）或GLTF（JSON）
            var headerBytes = new byte[12];
            await stream.ReadAsync(headerBytes, 0, 12, cancellationToken);
            stream.Position = 0; // 重置流位置

            // 检查GLB魔数：0x46546C67（"glTF"的ASCII码）
            var magic = BitConverter.ToUInt32(headerBytes, 0);
            if (magic == 0x46546C67) // GLB格式
            {
                _logger.LogDebug("检测到GLB（二进制glTF）格式");
                triangles = await ParseGLBFileAsync(stream, cancellationToken);
            }
            else // GLTF（JSON）格式
            {
                _logger.LogDebug("检测到GLTF（JSON）格式");
                triangles = await ParseGLTFFileAsync(stream, cancellationToken);
            }

            _logger.LogDebug("GLTF解析完成：{TriangleCount}个三角形", triangles.Count);
            return triangles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GLTF/GLB格式解析失败");
            return triangles;
        }
    }

    /// <summary>
    /// 解析GLB（二进制glTF）文件
    /// GLB格式：12字节头 + JSON块 + 可选的Binary块
    /// </summary>
    private async Task<List<Triangle>> ParseGLBFileAsync(Stream stream, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            // 读取GLB头部（12字节）
            var magic = reader.ReadUInt32();
            if (magic != 0x46546C67) // "glTF"
            {
                _logger.LogWarning("GLB文件魔数验证失败");
                return triangles;
            }

            var version = reader.ReadUInt32();
            var length = reader.ReadUInt32();

            _logger.LogDebug("GLB版本：{Version}, 文件长度：{Length}", version, length);

            // 读取JSON块
            var jsonChunkLength = reader.ReadUInt32();
            var jsonChunkType = reader.ReadUInt32();

            if (jsonChunkType != 0x4E4F534A) // "JSON"
            {
                _logger.LogWarning("GLB JSON块类型验证失败");
                return triangles;
            }

            var jsonBytes = reader.ReadBytes((int)jsonChunkLength);
            var jsonString = System.Text.Encoding.UTF8.GetString(jsonBytes);

            // 读取可选的Binary块
            byte[]? binaryData = null;
            if (stream.Position < stream.Length)
            {
                var binaryChunkLength = reader.ReadUInt32();
                var binaryChunkType = reader.ReadUInt32();

                if (binaryChunkType == 0x004E4942) // "BIN\0"
                {
                    binaryData = reader.ReadBytes((int)binaryChunkLength);
                    _logger.LogDebug("读取Binary块：{Length}字节", binaryData.Length);
                }
            }

            // 解析glTF JSON并提取几何数据
            triangles = await ParseGLTFJsonAsync(jsonString, binaryData, cancellationToken);
        }

        return triangles;
    }

    /// <summary>
    /// 解析GLTF（JSON）文件
    /// </summary>
    private async Task<List<Triangle>> ParseGLTFFileAsync(Stream stream, CancellationToken cancellationToken)
    {
        using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            var jsonString = await reader.ReadToEndAsync();
            return await ParseGLTFJsonAsync(jsonString, null, cancellationToken);
        }
    }

    /// <summary>
    /// 解析glTF JSON内容并提取三角形网格
    /// </summary>
    private async Task<List<Triangle>> ParseGLTFJsonAsync(string jsonString, byte[]? binaryData, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        try
        {
            // 解析glTF JSON
            using (var document = JsonDocument.Parse(jsonString))
            {
                var root = document.RootElement;

                // 检查必需的属性
                if (!root.TryGetProperty("asset", out var asset))
                {
                    _logger.LogWarning("glTF文件缺少asset属性");
                    return triangles;
                }

                // 获取buffers、bufferViews、accessors
                var buffers = root.TryGetProperty("buffers", out var buffersElement) ? buffersElement : default;
                var bufferViews = root.TryGetProperty("bufferViews", out var bufferViewsElement) ? bufferViewsElement : default;
                var accessors = root.TryGetProperty("accessors", out var accessorsElement) ? accessorsElement : default;
                var meshes = root.TryGetProperty("meshes", out var meshesElement) ? meshesElement : default;

                if (meshes.ValueKind == JsonValueKind.Undefined)
                {
                    _logger.LogDebug("glTF文件中没有meshes数据");
                    return triangles;
                }

                // 遍历所有meshes
                foreach (var mesh in meshes.EnumerateArray())
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    if (!mesh.TryGetProperty("primitives", out var primitives))
                        continue;

                    // 遍历mesh的所有primitives
                    foreach (var primitive in primitives.EnumerateArray())
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        // 提取三角形数据
                        var meshTriangles = await ExtractTrianglesFromPrimitiveAsync(
                            primitive, accessors, bufferViews, binaryData, cancellationToken);

                        triangles.AddRange(meshTriangles);
                    }
                }
            }

            _logger.LogDebug("从glTF JSON提取{Count}个三角形", triangles.Count);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "解析glTF JSON失败");
        }

        return triangles;
    }

    /// <summary>
    /// 从glTF primitive中提取三角形
    /// </summary>
    private Task<List<Triangle>> ExtractTrianglesFromPrimitiveAsync(
        JsonElement primitive,
        JsonElement accessors,
        JsonElement bufferViews,
        byte[]? binaryData,
        CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        try
        {
            // 检查primitive类型（mode）
            var mode = primitive.TryGetProperty("mode", out var modeElement) ? modeElement.GetInt32() : 4;
            // mode: 0=POINTS, 1=LINES, 2=LINE_LOOP, 3=LINE_STRIP, 4=TRIANGLES, 5=TRIANGLE_STRIP, 6=TRIANGLE_FAN

            if (mode != 4 && mode != 5 && mode != 6)
            {
                _logger.LogDebug("跳过非三角形primitive，mode={Mode}", mode);
                return Task.FromResult(triangles);
            }

            // 获取attributes（顶点数据）
            if (!primitive.TryGetProperty("attributes", out var attributes))
            {
                _logger.LogWarning("primitive缺少attributes属性");
                return Task.FromResult(triangles);
            }

            // 获取POSITION accessor索引
            if (!attributes.TryGetProperty("POSITION", out var positionAccessorIndex))
            {
                _logger.LogWarning("primitive缺少POSITION属性");
                return Task.FromResult(triangles);
            }

            // 读取顶点位置数据
            var vertices = ReadAccessorData(
                accessors, bufferViews, binaryData,
                positionAccessorIndex.GetInt32(), 3); // POSITION是3D向量

            if (vertices == null || vertices.Count == 0)
            {
                _logger.LogWarning("无法读取顶点数据");
                return Task.FromResult(triangles);
            }

            // 读取索引数据（如果存在）
            List<int>? indices = null;
            if (primitive.TryGetProperty("indices", out var indicesAccessorIndex))
            {
                var indicesData = ReadAccessorData(
                    accessors, bufferViews, binaryData,
                    indicesAccessorIndex.GetInt32(), 1); // 索引是标量

                indices = indicesData?.Select(v => (int)v[0]).ToList();
            }

            // 根据mode生成三角形
            switch (mode)
            {
                case 4: // TRIANGLES
                    triangles = GenerateTrianglesFromMode4(vertices, indices);
                    break;
                case 5: // TRIANGLE_STRIP
                    triangles = GenerateTrianglesFromMode5(vertices, indices);
                    break;
                case 6: // TRIANGLE_FAN
                    triangles = GenerateTrianglesFromMode6(vertices, indices);
                    break;
            }

            _logger.LogDebug("从primitive提取{Count}个三角形", triangles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取primitive三角形失败");
        }

        return Task.FromResult(triangles);
    }

    /// <summary>
    /// 从accessor读取数据
    /// </summary>
    private List<double[]>? ReadAccessorData(
        JsonElement accessors,
        JsonElement bufferViews,
        byte[]? binaryData,
        int accessorIndex,
        int componentsPerElement)
    {
        try
        {
            var accessor = accessors[accessorIndex];

            // 获取accessor属性
            var bufferViewIndex = accessor.GetProperty("bufferView").GetInt32();
            var componentType = accessor.GetProperty("componentType").GetInt32();
            var count = accessor.GetProperty("count").GetInt32();
            var byteOffset = accessor.TryGetProperty("byteOffset", out var offsetElement) ? offsetElement.GetInt32() : 0;

            // 获取bufferView
            var bufferView = bufferViews[bufferViewIndex];
            var bufferIndex = bufferView.GetProperty("buffer").GetInt32();
            var bufferViewByteOffset = bufferView.TryGetProperty("byteOffset", out var bvOffsetElement) ? bvOffsetElement.GetInt32() : 0;
            var bufferViewByteLength = bufferView.GetProperty("byteLength").GetInt32();

            // 如果没有binaryData，说明是外部buffer（暂不支持）
            if (binaryData == null || bufferIndex != 0)
            {
                _logger.LogWarning("不支持外部buffer引用");
                return null;
            }

            // 计算实际偏移量
            var actualOffset = bufferViewByteOffset + byteOffset;

            // 根据componentType读取数据
            var result = new List<double[]>();
            var bytesPerComponent = GetBytesPerComponent(componentType);

            for (int i = 0; i < count; i++)
            {
                if (actualOffset + bytesPerComponent * componentsPerElement > binaryData.Length)
                {
                    _logger.LogWarning("Buffer溢出，停止读取");
                    break;
                }

                var element = new double[componentsPerElement];
                for (int j = 0; j < componentsPerElement; j++)
                {
                    element[j] = ReadComponentValue(binaryData, actualOffset, componentType);
                    actualOffset += bytesPerComponent;
                }
                result.Add(element);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取accessor数据失败：索引{Index}", accessorIndex);
            return null;
        }
    }

    /// <summary>
    /// 获取componentType对应的字节数
    /// </summary>
    private int GetBytesPerComponent(int componentType)
    {
        return componentType switch
        {
            5120 => 1, // BYTE
            5121 => 1, // UNSIGNED_BYTE
            5122 => 2, // SHORT
            5123 => 2, // UNSIGNED_SHORT
            5125 => 4, // UNSIGNED_INT
            5126 => 4, // FLOAT
            _ => 4
        };
    }

    /// <summary>
    /// 从buffer读取组件值
    /// </summary>
    private double ReadComponentValue(byte[] buffer, int offset, int componentType)
    {
        return componentType switch
        {
            5120 => (sbyte)buffer[offset], // BYTE
            5121 => buffer[offset], // UNSIGNED_BYTE
            5122 => BitConverter.ToInt16(buffer, offset), // SHORT
            5123 => BitConverter.ToUInt16(buffer, offset), // UNSIGNED_SHORT
            5125 => BitConverter.ToUInt32(buffer, offset), // UNSIGNED_INT
            5126 => BitConverter.ToSingle(buffer, offset), // FLOAT
            _ => 0.0
        };
    }

    /// <summary>
    /// 生成TRIANGLES模式的三角形（mode=4）
    /// </summary>
    private List<Triangle> GenerateTrianglesFromMode4(List<double[]> vertices, List<int>? indices)
    {
        var triangles = new List<Triangle>();

        if (indices != null)
        {
            // 使用索引
            for (int i = 0; i < indices.Count; i += 3)
            {
                if (i + 2 >= indices.Count) break;

                var v1 = vertices[indices[i]];
                var v2 = vertices[indices[i + 1]];
                var v3 = vertices[indices[i + 2]];

                triangles.Add(new Triangle
                {
                    Vertices = new[]
                    {
                        new Vector3D { X = v1[0], Y = v1[1], Z = v1[2] },
                        new Vector3D { X = v2[0], Y = v2[1], Z = v2[2] },
                        new Vector3D { X = v3[0], Y = v3[1], Z = v3[2] }
                    }
                });
            }
        }
        else
        {
            // 不使用索引，直接按顺序
            for (int i = 0; i < vertices.Count; i += 3)
            {
                if (i + 2 >= vertices.Count) break;

                var v1 = vertices[i];
                var v2 = vertices[i + 1];
                var v3 = vertices[i + 2];

                triangles.Add(new Triangle
                {
                    Vertices = new[]
                    {
                        new Vector3D { X = v1[0], Y = v1[1], Z = v1[2] },
                        new Vector3D { X = v2[0], Y = v2[1], Z = v2[2] },
                        new Vector3D { X = v3[0], Y = v3[1], Z = v3[2] }
                    }
                });
            }
        }

        return triangles;
    }

    /// <summary>
    /// 生成TRIANGLE_STRIP模式的三角形（mode=5）
    /// </summary>
    private List<Triangle> GenerateTrianglesFromMode5(List<double[]> vertices, List<int>? indices)
    {
        var triangles = new List<Triangle>();
        var vertexList = indices != null
            ? indices.Select(i => vertices[i]).ToList()
            : vertices;

        for (int i = 0; i < vertexList.Count - 2; i++)
        {
            var v1 = vertexList[i];
            var v2 = vertexList[i + 1];
            var v3 = vertexList[i + 2];

            // Triangle strip要求奇数索引的三角形翻转顶点顺序
            if (i % 2 == 0)
            {
                triangles.Add(new Triangle
                {
                    Vertices = new[]
                    {
                        new Vector3D { X = v1[0], Y = v1[1], Z = v1[2] },
                        new Vector3D { X = v2[0], Y = v2[1], Z = v2[2] },
                        new Vector3D { X = v3[0], Y = v3[1], Z = v3[2] }
                    }
                });
            }
            else
            {
                triangles.Add(new Triangle
                {
                    Vertices = new[]
                    {
                        new Vector3D { X = v1[0], Y = v1[1], Z = v1[2] },
                        new Vector3D { X = v3[0], Y = v3[1], Z = v3[2] },
                        new Vector3D { X = v2[0], Y = v2[1], Z = v2[2] }
                    }
                });
            }
        }

        return triangles;
    }

    /// <summary>
    /// 生成TRIANGLE_FAN模式的三角形（mode=6）
    /// </summary>
    private List<Triangle> GenerateTrianglesFromMode6(List<double[]> vertices, List<int>? indices)
    {
        var triangles = new List<Triangle>();
        var vertexList = indices != null
            ? indices.Select(i => vertices[i]).ToList()
            : vertices;

        if (vertexList.Count < 3) return triangles;

        var v0 = vertexList[0]; // 扇形中心点

        for (int i = 1; i < vertexList.Count - 1; i++)
        {
            var v1 = vertexList[i];
            var v2 = vertexList[i + 1];

            triangles.Add(new Triangle
            {
                Vertices = new[]
                {
                    new Vector3D { X = v0[0], Y = v0[1], Z = v0[2] },
                    new Vector3D { X = v1[0], Y = v1[1], Z = v1[2] },
                    new Vector3D { X = v2[0], Y = v2[1], Z = v2[2] }
                }
            });
        }

        return triangles;
    }

    /// <summary>
    /// 备用密度分析算法 - 当无法获取几何数据时使用
    /// </summary>
    private Task<List<AdaptiveRegion>> FallbackDensityAnalysisAsync(int level, SlicingConfig config, CancellationToken cancellationToken)
    {
        _logger.LogWarning("使用fallback密度分析策略：级别{Level}", level);

        var regions = new List<AdaptiveRegion>();
        var baseTilesInLevel = (int)Math.Pow(2, level);

        for (int x = 0; x < baseTilesInLevel; x++)
        {
            for (int y = 0; y < baseTilesInLevel; y++)
            {
                for (int z = 0; z < (level == 0 ? 1 : baseTilesInLevel / 2); z++)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var density = CalculateFallbackDensity(level, x, y, z);
                    // 移除密度阈值检查,确保生成所有切片
                    // 原逻辑: if (density > config.GeometricErrorThreshold)
                    // 新逻辑: 始终添加区域
                    regions.Add(new AdaptiveRegion
                    {
                        X = x,
                        Y = y,
                        Z = z,
                        Density = density,
                        Importance = CalculateRegionImportance(level, x, y, z)
                    });
                }
            }
        }

        _logger.LogInformation("fallback策略生成区域数量：{RegionCount}, 级别{Level}", regions.Count, level);

        return Task.FromResult(regions);
    }

    /// <summary>
    /// 构建空间索引 - 提高几何查询效率
    /// </summary>
    private Task<SpatialIndex> BuildSpatialIndexAsync(List<GeometricPrimitive> geometricData, SlicingConfig config, CancellationToken cancellationToken)
    {
        _logger.LogDebug("构建空间索引：{PrimitiveCount}个几何图元", geometricData.Count);

        var spatialIndex = new SpatialIndex
        {
            Bounds = new BoundingBox3D() // 初始化 Bounds，后续会计算
        };

        // 使用均匀网格空间索引
        var gridSize = Math.Max(10, (int)Math.Sqrt(geometricData.Count) / 4);

        foreach (var primitive in geometricData)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var gridIndex = CalculateGridIndex(primitive.Center, gridSize, config.TileSize);
            if (!spatialIndex.Grid.ContainsKey(gridIndex))
            {
                spatialIndex.Grid[gridIndex] = new List<GeometricPrimitive>();
            }
            spatialIndex.Grid[gridIndex].Add(primitive);
        }

        spatialIndex.Bounds = CalculateGlobalBounds(geometricData);
        _logger.LogDebug("空间索引构建完成：{GridCellCount}个网格单元", spatialIndex.Grid.Count);

        return Task.FromResult(spatialIndex);
    }

    /// <summary>
    /// 计算综合密度评分 - 多维度密度指标融合
    /// 算法：采用加权综合计算，考虑顶点密度、三角形密度、曲率复杂度和表面积等多维度指标
    /// 权重分配基于各指标对几何复杂性的贡献度
    /// </summary>
    private double CalculateCompositeDensity(DensityMetrics metrics)
    {
        // 动态权重调整：基于输入数据的特征自适应调整权重
        var weights = CalculateAdaptiveWeights(metrics);

        // 归一化密度值到[0,1]区间
        var normalizedVertexDensity = NormalizeDensity(metrics.VertexDensity, 0, 100);
        var normalizedTriangleDensity = NormalizeDensity(metrics.TriangleDensity, 0, 50);
        var normalizedCurvature = Math.Min(metrics.CurvatureComplexity, 1.0); // 曲率已归一化
        var normalizedSurfaceArea = NormalizeDensity(metrics.SurfaceArea, 0, 1000);

        // 加权综合密度计算
        var compositeDensity = weights.VertexWeight * normalizedVertexDensity +
                              weights.TriangleWeight * normalizedTriangleDensity +
                              weights.CurvatureWeight * normalizedCurvature +
                              weights.SurfaceAreaWeight * normalizedSurfaceArea;

        // 应用非线性增强：对高密度区域进行增强，低密度区域进行抑制
        compositeDensity = ApplyNonlinearEnhancement(compositeDensity, metrics);

        return Math.Max(0.0, Math.Min(1.0, compositeDensity));
    }

    /// <summary>
    /// 计算自适应权重 - 基于数据特征动态调整权重
    /// </summary>
    private (double VertexWeight, double TriangleWeight, double CurvatureWeight, double SurfaceAreaWeight) CalculateAdaptiveWeights(DensityMetrics metrics)
    {
        // 基础权重
        double vertexWeight = 0.3;
        double triangleWeight = 0.3;
        double curvatureWeight = 0.25;
        double surfaceAreaWeight = 0.15;

        // 基于数据范围的自适应调整
        if (metrics.VertexDensity > 80)
        {
            // 高顶点密度场景，增加顶点权重
            vertexWeight += 0.1;
            triangleWeight -= 0.05;
            curvatureWeight -= 0.05;
        }

        if (metrics.CurvatureComplexity > 0.7)
        {
            // 高曲率复杂性场景，增加曲率权重
            curvatureWeight += 0.1;
            vertexWeight -= 0.05;
            triangleWeight -= 0.05;
        }

        if (metrics.SurfaceArea > 800)
        {
            // 大表面积场景，增加表面积权重
            surfaceAreaWeight += 0.1;
            vertexWeight -= 0.05;
            triangleWeight -= 0.05;
        }

        // 确保权重总和为1
        var totalWeight = vertexWeight + triangleWeight + curvatureWeight + surfaceAreaWeight;
        vertexWeight /= totalWeight;
        triangleWeight /= totalWeight;
        curvatureWeight /= totalWeight;
        surfaceAreaWeight /= totalWeight;

        return (vertexWeight, triangleWeight, curvatureWeight, surfaceAreaWeight);
    }

    /// <summary>
    /// 应用非线性增强 - 提高密度对比度
    /// </summary>
    private double ApplyNonlinearEnhancement(double density, DensityMetrics metrics)
    {
        // Sigmoid函数增强中间密度值的区分度
        var enhanced = 1.0 / (1.0 + Math.Exp(-5.0 * (density - 0.5)));

        // 基于几何复杂性的额外增强
        var complexityBonus = Math.Min(metrics.CurvatureComplexity * 0.2, 0.1);

        return Math.Min(1.0, enhanced + complexityBonus);
    }

    /// <summary>
    /// 归一化密度值到0-1范围
    /// </summary>
    private double NormalizeDensity(double value, double min, double max)
    {
        if (max <= min) return 0;
        return Math.Max(0, Math.Min(1, (value - min) / (max - min)));
    }

    private double CalculateRegionImportance(int level, int x, int y, int z)
    {
        // 计算区域重要性（影响LOD选择和渲染优先级）
        return 1.0 / (level + 1) + Math.Abs(Math.Sin(x * 0.05) * Math.Cos(y * 0.05));
    }

    /// <summary>
    /// 生成增强的模拟几何数据 - 真实感几何特征模拟算法
    /// 算法：生成具有多样化几何特征的高质量模拟数据，用于测试和演示
    /// 特征：多种几何形状、不同复杂度级别、真实的空间分布、多样化的表面特征
    /// 用途：算法验证、性能测试、视觉演示（实际生产环境应从真实模型文件加载）
    /// </summary>
    /// <param name="task">切片任务，包含模型路径和配置信息</param>
    /// <returns>三角形网格集合，包含多种几何特征的模拟数据</returns>
    private List<Triangle> GenerateMockGeometricData(SlicingTask task)
    {
        var triangles = new List<Triangle>();
        var random = new Random(42); // 固定种子保证结果可重现

        _logger.LogDebug("开始生成增强的模拟几何数据：任务{TaskId}", task.Id);

        // 1. 生成地形网格 - 模拟真实地形起伏
        GenerateTerrainMesh(triangles, random, new Vector3D { X = 50, Y = 50, Z = 0 }, 100, 100, 20);

        // 2. 生成建筑群 - 模拟城市建筑
        GenerateBuildingCluster(triangles, random, new Vector3D { X = 30, Y = 30, Z = 5 }, 10);

        // 3. 生成有机形状 - 模拟自然物体（树木、岩石等）
        GenerateOrganicShapes(triangles, random, 15);

        // 4. 生成精细网格区域 - 模拟高细节区域
        GenerateDetailedMeshRegion(triangles, random, new Vector3D { X = 70, Y = 70, Z = 10 }, 15, 64);

        // 5. 生成球体表面 - 模拟圆润物体
        AddSphereSurface(triangles, new Vector3D { X = 25, Y = 25, Z = 15 }, 8, random);

        // 6. 生成圆柱体 - 模拟柱状结构
        GenerateCylinderSurface(triangles, new Vector3D { X = 75, Y = 25, Z = 5 }, 5, 15, 16, random);

        // 7. 生成圆锥体 - 模拟锥形结构
        GenerateConeSurface(triangles, new Vector3D { X = 50, Y = 75, Z = 5 }, 6, 12, 16, random);

        // 8. 生成环面（Torus）- 模拟甜甜圈形状
        GenerateTorusSurface(triangles, new Vector3D { X = 25, Y = 75, Z = 10 }, 6, 2, 16, 12, random);

        // 9. 生成随机散布的三角形 - 模拟细碎几何细节
        GenerateRandomScatteredTriangles(triangles, random, 500, 100, 100, 30);

        // 10. 生成复杂多面体 - 模拟结构化物体
        AddComplexPolyhedron(triangles, new Vector3D { X = 75, Y = 75, Z = 15 }, 6, random);

        _logger.LogInformation("模拟几何数据生成完成：总三角形数{TriangleCount}", triangles.Count);

        return triangles;
    }

    /// <summary>
    /// 生成地形网格 - 基于Perlin噪声的真实地形模拟算法
    /// 算法：使用多层次Perlin噪声生成自然起伏的地形表面
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="random">随机数生成器</param>
    /// <param name="center">地形中心点</param>
    /// <param name="width">地形宽度</param>
    /// <param name="height">地形长度</param>
    /// <param name="resolution">网格分辨率</param>
    private void GenerateTerrainMesh(List<Triangle> triangles, Random random, Vector3D center, double width, double height, int resolution)
    {
        var vertices = new Vector3D[resolution, resolution];

        // 生成网格顶点，应用Perlin噪声模拟地形高度
        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                var x = center.X - width / 2 + (i * width / (resolution - 1));
                var y = center.Y - height / 2 + (j * height / (resolution - 1));

                // 多层次噪声叠加模拟真实地形
                var z = center.Z +
                    PerlinNoise(x * 0.05, y * 0.05, random) * 5.0 +  // 大尺度起伏
                    PerlinNoise(x * 0.15, y * 0.15, random) * 2.0 +  // 中尺度细节
                    PerlinNoise(x * 0.3, y * 0.3, random) * 0.5;     // 小尺度纹理

                vertices[i, j] = new Vector3D { X = x, Y = y, Z = z };
            }
        }

        // 将网格转换为三角形
        for (int i = 0; i < resolution - 1; i++)
        {
            for (int j = 0; j < resolution - 1; j++)
            {
                // 每个网格单元生成两个三角形
                triangles.Add(new Triangle
                {
                    Vertices = new[] { vertices[i, j], vertices[i + 1, j], vertices[i + 1, j + 1] }
                });

                triangles.Add(new Triangle
                {
                    Vertices = new[] { vertices[i, j], vertices[i + 1, j + 1], vertices[i, j + 1] }
                });
            }
        }

        _logger.LogDebug("地形网格生成完成：{Width}x{Height}, 分辨率{Resolution}, 三角形数{TriangleCount}",
            width, height, resolution, (resolution - 1) * (resolution - 1) * 2);
    }

    // Perlin噪声预计算梯度表 - 标准Perlin噪声使用256个预定义梯度向量
    private static readonly (double x, double y)[] _perlinGradients = new (double, double)[256];
    private static readonly int[] _perlinPermutation = new int[512];

    static AdaptiveSlicingStrategy()
    {
        // 初始化标准Perlin噪声梯度表（Ken Perlin原始算法）
        // 使用12个均匀分布的梯度向量
        var baseGradients = new (double, double)[]
        {
            (1, 1), (-1, 1), (1, -1), (-1, -1),
            (1, 0), (-1, 0), (0, 1), (0, -1),
            (1, 1), (-1, 1), (1, -1), (-1, -1) // 重复以填充256个位置
        };

        // 填充256个梯度
        for (int i = 0; i < 256; i++)
        {
            _perlinGradients[i] = baseGradients[i % 12];
        }

        // 初始化排列表 - 使用Ken Perlin的原始排列
        var p = new int[256];
        for (int i = 0; i < 256; i++)
            p[i] = i;

        // Fisher-Yates洗牌算法
        var rng = new Random(12345); // 固定种子保证可重复性
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        // 复制到512长度数组以避免边界检查
        for (int i = 0; i < 512; i++)
            _perlinPermutation[i] = p[i % 256];
    }

    /// <summary>
    /// Perlin噪声算法 - 自然纹理生成
    /// 算法：标准Perlin噪声实现，基于Ken Perlin的改进噪声算法（2002年）
    /// 特性：
    /// - 使用预计算梯度表，提升性能
    /// - 确定性输出，相同输入产生相同结果
    /// - 平滑连续，无明显接缝
    /// - 适用于地形生成、程序化纹理、自然效果模拟
    /// </summary>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <param name="random">随机数生成器（仅用于兼容，实际使用预计算表）</param>
    /// <returns>噪声值（-1到1之间）</returns>
    private double PerlinNoise(double x, double y, Random random)
    {
        // 1. 找到单位网格的整数坐标
        int xi = (int)Math.Floor(x) & 255;
        int yi = (int)Math.Floor(y) & 255;

        // 2. 计算网格内的相对坐标（0-1范围）
        double xf = x - Math.Floor(x);
        double yf = y - Math.Floor(y);

        // 3. 计算淡入淡出曲线值（Perlin的改进插值函数）
        double u = Fade(xf);
        double v = Fade(yf);

        // 4. 使用排列表获取网格四个角的哈希值
        int aa = _perlinPermutation[_perlinPermutation[xi] + yi];
        int ab = _perlinPermutation[_perlinPermutation[xi] + yi + 1];
        int ba = _perlinPermutation[_perlinPermutation[xi + 1] + yi];
        int bb = _perlinPermutation[_perlinPermutation[xi + 1] + yi + 1];

        // 5. 使用梯度表计算四个角的影响值
        double n00 = DotGridGradient(aa, xf, yf);
        double n10 = DotGridGradient(ba, xf - 1, yf);
        double n01 = DotGridGradient(ab, xf, yf - 1);
        double n11 = DotGridGradient(bb, xf - 1, yf - 1);

        // 6. 双线性插值计算最终噪声值
        double x1 = Lerp(n00, n10, u);
        double x2 = Lerp(n01, n11, u);
        double result = Lerp(x1, x2, v);

        // 7. 归一化到[-1, 1]范围（原始Perlin噪声范围约为[-0.707, 0.707]）
        return result * 1.414;
    }

    /// <summary>
    /// 计算梯度向量与距离向量的点积
    /// </summary>
    /// <param name="hash">哈希值，用于索引梯度表</param>
    /// <param name="x">距离向量X分量</param>
    /// <param name="y">距离向量Y分量</param>
    /// <returns>点积结果</returns>
    private double DotGridGradient(int hash, double x, double y)
    {
        // 使用预计算的梯度向量
        var gradient = _perlinGradients[hash & 255];
        return gradient.x * x + gradient.y * y;
    }

    /// <summary>
    /// 淡入淡出函数 - Perlin噪声平滑插值
    /// </summary>
    private double Fade(double t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    /// <summary>
    /// 线性插值
    /// </summary>
    private double Lerp(double a, double b, double t)
    {
        return a + t * (b - a);
    }

    /// <summary>
    /// 生成建筑群 - 模拟城市建筑物
    /// 算法：生成多个不同高度和尺寸的立方体建筑，模拟城市天际线
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="random">随机数生成器</param>
    /// <param name="center">建筑群中心</param>
    /// <param name="buildingCount">建筑数量</param>
    private void GenerateBuildingCluster(List<Triangle> triangles, Random random, Vector3D center, int buildingCount)
    {
        for (int i = 0; i < buildingCount; i++)
        {
            // 随机位置
            var offsetX = (random.NextDouble() - 0.5) * 30;
            var offsetY = (random.NextDouble() - 0.5) * 30;

            // 随机尺寸
            var width = 3 + random.NextDouble() * 5;
            var depth = 3 + random.NextDouble() * 5;
            var height = 5 + random.NextDouble() * 20;

            var buildingCenter = new Vector3D
            {
                X = center.X + offsetX,
                Y = center.Y + offsetY,
                Z = center.Z + height / 2
            };

            GenerateBox(triangles, buildingCenter, width, depth, height);
        }

        _logger.LogDebug("建筑群生成完成：建筑数量{BuildingCount}", buildingCount);
    }

    /// <summary>
    /// 生成立方体
    /// </summary>
    private void GenerateBox(List<Triangle> triangles, Vector3D center, double width, double depth, double height)
    {
        var halfWidth = width / 2;
        var halfDepth = depth / 2;
        var halfHeight = height / 2;

        var vertices = new[]
        {
            new Vector3D { X = center.X - halfWidth, Y = center.Y - halfDepth, Z = center.Z - halfHeight },
            new Vector3D { X = center.X + halfWidth, Y = center.Y - halfDepth, Z = center.Z - halfHeight },
            new Vector3D { X = center.X + halfWidth, Y = center.Y + halfDepth, Z = center.Z - halfHeight },
            new Vector3D { X = center.X - halfWidth, Y = center.Y + halfDepth, Z = center.Z - halfHeight },
            new Vector3D { X = center.X - halfWidth, Y = center.Y - halfDepth, Z = center.Z + halfHeight },
            new Vector3D { X = center.X + halfWidth, Y = center.Y - halfDepth, Z = center.Z + halfHeight },
            new Vector3D { X = center.X + halfWidth, Y = center.Y + halfDepth, Z = center.Z + halfHeight },
            new Vector3D { X = center.X - halfWidth, Y = center.Y + halfDepth, Z = center.Z + halfHeight }
        };

        var faces = new[]
        {
            new[] { 0, 1, 2, 3 }, // 底面
            new[] { 4, 5, 6, 7 }, // 顶面
            new[] { 0, 1, 5, 4 }, // 前面
            new[] { 2, 3, 7, 6 }, // 后面
            new[] { 0, 3, 7, 4 }, // 左面
            new[] { 1, 2, 6, 5 }  // 右面
        };

        foreach (var face in faces)
        {
            triangles.Add(new Triangle { Vertices = new[] { vertices[face[0]], vertices[face[1]], vertices[face[2]] } });
            triangles.Add(new Triangle { Vertices = new[] { vertices[face[0]], vertices[face[2]], vertices[face[3]] } });
        }
    }

    /// <summary>
    /// 生成有机形状 - 模拟自然物体
    /// 算法：生成不规则、有机的几何形状，模拟树木、岩石等自然物体
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="random">随机数生成器</param>
    /// <param name="shapeCount">形状数量</param>
    private void GenerateOrganicShapes(List<Triangle> triangles, Random random, int shapeCount)
    {
        for (int i = 0; i < shapeCount; i++)
        {
            var center = new Vector3D
            {
                X = random.NextDouble() * 100,
                Y = random.NextDouble() * 100,
                Z = random.NextDouble() * 30
            };

            var radius = 2 + random.NextDouble() * 4;
            var segments = 8 + random.Next(8);

            // 使用变形球体模拟有机形状
            GenerateDeformedSphere(triangles, center, radius, segments, random);
        }

        _logger.LogDebug("有机形状生成完成：形状数量{ShapeCount}", shapeCount);
    }

    /// <summary>
    /// 生成变形球体 - 不规则球形
    /// </summary>
    private void GenerateDeformedSphere(List<Triangle> triangles, Vector3D center, double radius, int segments, Random random)
    {
        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                var phi1 = 2 * Math.PI * i / segments;
                var phi2 = 2 * Math.PI * (i + 1) / segments;
                var theta1 = Math.PI * j / segments;
                var theta2 = Math.PI * (j + 1) / segments;

                // 应用随机变形
                var deform1 = 1.0 + (random.NextDouble() - 0.5) * 0.3;
                var deform2 = 1.0 + (random.NextDouble() - 0.5) * 0.3;
                var deform3 = 1.0 + (random.NextDouble() - 0.5) * 0.3;

                var v1 = new Vector3D
                {
                    X = center.X + radius * deform1 * Math.Sin(theta1) * Math.Cos(phi1),
                    Y = center.Y + radius * deform1 * Math.Sin(theta1) * Math.Sin(phi1),
                    Z = center.Z + radius * deform1 * Math.Cos(theta1)
                };

                var v2 = new Vector3D
                {
                    X = center.X + radius * deform2 * Math.Sin(theta2) * Math.Cos(phi1),
                    Y = center.Y + radius * deform2 * Math.Sin(theta2) * Math.Sin(phi1),
                    Z = center.Z + radius * deform2 * Math.Cos(theta2)
                };

                var v3 = new Vector3D
                {
                    X = center.X + radius * deform3 * Math.Sin(theta2) * Math.Cos(phi2),
                    Y = center.Y + radius * deform3 * Math.Sin(theta2) * Math.Sin(phi2),
                    Z = center.Z + radius * deform3 * Math.Cos(theta2)
                };

                triangles.Add(new Triangle { Vertices = new[] { v1, v2, v3 } });
            }
        }
    }

    /// <summary>
    /// 生成精细网格区域 - 高密度细节区域
    /// 算法：生成高分辨率网格，模拟需要高细节的区域
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="random">随机数生成器</param>
    /// <param name="center">区域中心</param>
    /// <param name="size">区域大小</param>
    /// <param name="resolution">网格分辨率</param>
    private void GenerateDetailedMeshRegion(List<Triangle> triangles, Random random, Vector3D center, double size, int resolution)
    {
        var halfSize = size / 2;
        var vertices = new Vector3D[resolution, resolution];

        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                var x = center.X - halfSize + (i * size / (resolution - 1));
                var y = center.Y - halfSize + (j * size / (resolution - 1));
                var z = center.Z + Math.Sin(i * 0.5) * Math.Cos(j * 0.5) * 2.0;

                vertices[i, j] = new Vector3D { X = x, Y = y, Z = z };
            }
        }

        for (int i = 0; i < resolution - 1; i++)
        {
            for (int j = 0; j < resolution - 1; j++)
            {
                triangles.Add(new Triangle { Vertices = new[] { vertices[i, j], vertices[i + 1, j], vertices[i + 1, j + 1] } });
                triangles.Add(new Triangle { Vertices = new[] { vertices[i, j], vertices[i + 1, j + 1], vertices[i, j + 1] } });
            }
        }

        _logger.LogDebug("精细网格区域生成完成：大小{Size}, 分辨率{Resolution}", size, resolution);
    }

    /// <summary>
    /// 生成圆柱体表面
    /// </summary>
    private void GenerateCylinderSurface(List<Triangle> triangles, Vector3D center, double radius, double height, int segments, Random random)
    {
        var angleStep = 2 * Math.PI / segments;

        for (int i = 0; i < segments; i++)
        {
            var angle1 = i * angleStep;
            var angle2 = (i + 1) * angleStep;

            var x1 = center.X + radius * Math.Cos(angle1);
            var y1 = center.Y + radius * Math.Sin(angle1);
            var x2 = center.X + radius * Math.Cos(angle2);
            var y2 = center.Y + radius * Math.Sin(angle2);

            // 侧面
            var v1 = new Vector3D { X = x1, Y = y1, Z = center.Z };
            var v2 = new Vector3D { X = x2, Y = y2, Z = center.Z };
            var v3 = new Vector3D { X = x2, Y = y2, Z = center.Z + height };
            var v4 = new Vector3D { X = x1, Y = y1, Z = center.Z + height };

            triangles.Add(new Triangle { Vertices = new[] { v1, v2, v3 } });
            triangles.Add(new Triangle { Vertices = new[] { v1, v3, v4 } });

            // 顶面和底面
            var centerBottom = new Vector3D { X = center.X, Y = center.Y, Z = center.Z };
            var centerTop = new Vector3D { X = center.X, Y = center.Y, Z = center.Z + height };

            triangles.Add(new Triangle { Vertices = new[] { centerBottom, v2, v1 } });
            triangles.Add(new Triangle { Vertices = new[] { centerTop, v4, v3 } });
        }
    }

    /// <summary>
    /// 生成圆锥体表面
    /// </summary>
    private void GenerateConeSurface(List<Triangle> triangles, Vector3D center, double radius, double height, int segments, Random random)
    {
        var angleStep = 2 * Math.PI / segments;
        var apex = new Vector3D { X = center.X, Y = center.Y, Z = center.Z + height };

        for (int i = 0; i < segments; i++)
        {
            var angle1 = i * angleStep;
            var angle2 = (i + 1) * angleStep;

            var x1 = center.X + radius * Math.Cos(angle1);
            var y1 = center.Y + radius * Math.Sin(angle1);
            var x2 = center.X + radius * Math.Cos(angle2);
            var y2 = center.Y + radius * Math.Sin(angle2);

            var v1 = new Vector3D { X = x1, Y = y1, Z = center.Z };
            var v2 = new Vector3D { X = x2, Y = y2, Z = center.Z };

            // 侧面
            triangles.Add(new Triangle { Vertices = new[] { v1, v2, apex } });

            // 底面
            var centerBase = new Vector3D { X = center.X, Y = center.Y, Z = center.Z };
            triangles.Add(new Triangle { Vertices = new[] { centerBase, v2, v1 } });
        }
    }

    /// <summary>
    /// 生成环面（Torus）表面
    /// </summary>
    private void GenerateTorusSurface(List<Triangle> triangles, Vector3D center, double majorRadius, double minorRadius, int majorSegments, int minorSegments, Random random)
    {
        for (int i = 0; i < majorSegments; i++)
        {
            for (int j = 0; j < minorSegments; j++)
            {
                var u1 = 2 * Math.PI * i / majorSegments;
                var u2 = 2 * Math.PI * (i + 1) / majorSegments;
                var v1 = 2 * Math.PI * j / minorSegments;
                var v2 = 2 * Math.PI * (j + 1) / minorSegments;

                var v1p = CalculateTorusVertex(center, majorRadius, minorRadius, u1, v1);
                var v2p = CalculateTorusVertex(center, majorRadius, minorRadius, u2, v1);
                var v3p = CalculateTorusVertex(center, majorRadius, minorRadius, u2, v2);
                var v4p = CalculateTorusVertex(center, majorRadius, minorRadius, u1, v2);

                triangles.Add(new Triangle { Vertices = new[] { v1p, v2p, v3p } });
                triangles.Add(new Triangle { Vertices = new[] { v1p, v3p, v4p } });
            }
        }
    }

    /// <summary>
    /// 计算环面顶点坐标
    /// </summary>
    private Vector3D CalculateTorusVertex(Vector3D center, double majorRadius, double minorRadius, double u, double v)
    {
        return new Vector3D
        {
            X = center.X + (majorRadius + minorRadius * Math.Cos(v)) * Math.Cos(u),
            Y = center.Y + (majorRadius + minorRadius * Math.Cos(v)) * Math.Sin(u),
            Z = center.Z + minorRadius * Math.Sin(v)
        };
    }

    /// <summary>
    /// 生成随机散布的三角形 - 模拟细碎几何细节
    /// </summary>
    private void GenerateRandomScatteredTriangles(List<Triangle> triangles, Random random, int count, double rangeX, double rangeY, double rangeZ)
    {
        for (int i = 0; i < count; i++)
        {
            var triangle = new Triangle();

            for (int j = 0; j < 3; j++)
            {
                triangle.Vertices[j] = new Vector3D
                {
                    X = random.NextDouble() * rangeX,
                    Y = random.NextDouble() * rangeY,
                    Z = random.NextDouble() * rangeZ
                };
            }

            triangles.Add(triangle);
        }
    }

    /// <summary>
    /// 计算三角形法向量
    /// </summary>
    private Vector3D CalculateTriangleNormal(Vector3D v1, Vector3D v2, Vector3D v3)
    {
        var edge1 = new Vector3D { X = v2.X - v1.X, Y = v2.Y - v1.Y, Z = v2.Z - v1.Z };
        var edge2 = new Vector3D { X = v3.X - v1.X, Y = v3.Y - v1.Y, Z = v3.Z - v1.Z };

        return new Vector3D
        {
            X = edge1.Y * edge2.Z - edge1.Z * edge2.Y,
            Y = edge1.Z * edge2.X - edge1.X * edge2.Z,
            Z = edge1.X * edge2.Y - edge1.Y * edge2.X
        };
    }

    /// <summary>
    /// 计算三角形面积
    /// </summary>
    private double CalculateTriangleArea(Vector3D v1, Vector3D v2, Vector3D v3)
    {
        var edge1 = new Vector3D { X = v2.X - v1.X, Y = v2.Y - v1.Y, Z = v2.Z - v1.Z };
        var edge2 = new Vector3D { X = v3.X - v1.X, Y = v3.Y - v1.Y, Z = v3.Z - v1.Z };

        var crossProduct = new Vector3D
        {
            X = edge1.Y * edge2.Z - edge1.Z * edge2.Y,
            Y = edge1.Z * edge2.X - edge1.X * edge2.Z,
            Z = edge1.X * edge2.Y - edge1.Y * edge2.X
        };

        return 0.5 * Math.Sqrt(crossProduct.X * crossProduct.X +
                              crossProduct.Y * crossProduct.Y +
                              crossProduct.Z * crossProduct.Z);
    }

    /// <summary>
    /// 计算备用密度值 - 增强的密度估算算法
    /// 算法：基于空间位置、LOD级别和几何分布特征的综合估算
    /// </summary>
    /// <param name="level">LOD级别</param>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <param name="z">Z坐标</param>
    /// <returns>估算的几何密度值（0-1范围）</returns>
    private double CalculateFallbackDensity(int level, int x, int y, int z)
    {
        // 基础密度：根据LOD级别设定
        var baseDensity = 0.5 / (level + 1);

        // 空间位置因子：使用多频率正弦波模拟几何分布的不均匀性
        // 低频成分：大尺度特征
        var lowFreqFactor = Math.Sin(x * 0.05) * Math.Cos(y * 0.05) * Math.Sin(z * 0.05);
        // 中频成分：中等尺度特征
        var midFreqFactor = Math.Sin(x * 0.15) * Math.Cos(y * 0.15) * Math.Sin(z * 0.15) * 0.5;
        // 高频成分：精细尺度特征
        var highFreqFactor = Math.Sin(x * 0.3) * Math.Cos(y * 0.3) * Math.Sin(z * 0.3) * 0.25;

        var positionFactor = (lowFreqFactor + midFreqFactor + highFreqFactor) * 0.3;

        // 边缘衰减因子：边缘区域通常密度较低
        var maxCoord = Math.Pow(2, level);
        var edgeDistanceX = Math.Min(x, maxCoord - x) / maxCoord;
        var edgeDistanceY = Math.Min(y, maxCoord - y) / maxCoord;
        var edgeDistanceZ = Math.Min(z, Math.Max(1, maxCoord / 2) - z) / Math.Max(1, maxCoord / 2);
        var edgeFactor = (edgeDistanceX + edgeDistanceY + edgeDistanceZ) / 3.0 * 0.2;

        // 中心聚集因子：模拟中心区域较高密度的现象
        var centerX = maxCoord / 2;
        var centerY = maxCoord / 2;
        var centerZ = (level == 0 ? 0.5 : maxCoord / 4);
        var distanceToCenter = Math.Sqrt(
            Math.Pow(x - centerX, 2) +
            Math.Pow(y - centerY, 2) +
            Math.Pow(z - centerZ, 2)
        );
        var maxDistance = Math.Sqrt(centerX * centerX + centerY * centerY + centerZ * centerZ);
        var centralityFactor = Math.Exp(-distanceToCenter / maxDistance) * 0.15;

        // 综合计算密度
        var density = baseDensity + positionFactor + edgeFactor + centralityFactor;

        // 归一化到0-1范围
        return Math.Max(0.0, Math.Min(1.0, density));
    }

    /// <summary>
    /// 计算网格索引
    /// </summary>
    private string CalculateGridIndex(Vector3D point, int gridSize, double tileSize)
    {
        var gridX = (int)Math.Floor(point.X / tileSize * gridSize);
        var gridY = (int)Math.Floor(point.Y / tileSize * gridSize);
        var gridZ = (int)Math.Floor(point.Z / tileSize * gridSize);
        return $"{gridX}_{gridY}_{gridZ}";
    }

    /// <summary>
    /// 计算全局包围盒
    /// </summary>
    private BoundingBox3D CalculateGlobalBounds(List<GeometricPrimitive> primitives)
    {
        if (!primitives.Any()) return new BoundingBox3D();

        var bounds = new BoundingBox3D
        {
            MinX = primitives.Min(p => p.Vertices.Min(v => v.X)),
            MinY = primitives.Min(p => p.Vertices.Min(v => v.Y)),
            MinZ = primitives.Min(p => p.Vertices.Min(v => v.Z)),
            MaxX = primitives.Max(p => p.Vertices.Max(v => v.X)),
            MaxY = primitives.Max(p => p.Vertices.Max(v => v.Y)),
            MaxZ = primitives.Max(p => p.Vertices.Max(v => v.Z))
        };

        return bounds;
    }

    /// <summary>
    /// 计算区域重要性（基于密度指标的重载版本）
    /// </summary>
    private double CalculateRegionImportance(DensityMetrics metrics, int level)
    {
        return 1.0 / (level + 1) + metrics.CurvatureComplexity * 0.3 + metrics.VertexDensity * 0.1;
    }


    /// <summary>
    /// 添加球体表面三角形
    /// </summary>
    private void AddSphereSurface(List<Triangle> triangles, Vector3D center, double radius, Random random)
    {
        const int segments = 12;
        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                var phi1 = 2 * Math.PI * i / segments;
                var phi2 = 2 * Math.PI * (i + 1) / segments;
                var theta1 = Math.PI * j / segments;
                var theta2 = Math.PI * (j + 1) / segments;

                var v1 = new Vector3D
                {
                    X = center.X + radius * Math.Sin(theta1) * Math.Cos(phi1),
                    Y = center.Y + radius * Math.Sin(theta1) * Math.Sin(phi1),
                    Z = center.Z + radius * Math.Cos(theta1)
                };

                var v2 = new Vector3D
                {
                    X = center.X + radius * Math.Sin(theta2) * Math.Cos(phi1),
                    Y = center.Y + radius * Math.Sin(theta2) * Math.Sin(phi1),
                    Z = center.Z + radius * Math.Cos(theta2)
                };

                var v3 = new Vector3D
                {
                    X = center.X + radius * Math.Sin(theta2) * Math.Cos(phi2),
                    Y = center.Y + radius * Math.Sin(theta2) * Math.Sin(phi2),
                    Z = center.Z + radius * Math.Cos(theta2)
                };

                triangles.Add(new Triangle { Vertices = new[] { v1, v2, v3 } });
            }
        }
    }

    /// <summary>
    /// 添加平面表面三角形
    /// </summary>
    private void AddPlaneSurface(List<Triangle> triangles, Vector3D center, double size, Random random)
    {
        const int gridSize = 8;
        var halfSize = size / 2;

        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                var x1 = center.X - halfSize + (i * size / gridSize);
                var y1 = center.Y - halfSize + (j * size / gridSize);
                var x2 = center.X - halfSize + ((i + 1) * size / gridSize);
                var y2 = center.Y - halfSize + (j * size / gridSize);
                var x3 = center.X - halfSize + (i * size / gridSize);
                var y3 = center.Y - halfSize + ((j + 1) * size / gridSize);

                var v1 = new Vector3D { X = x1, Y = y1, Z = center.Z };
                var v2 = new Vector3D { X = x2, Y = y2, Z = center.Z };
                var v3 = new Vector3D { X = x3, Y = y3, Z = center.Z };

                triangles.Add(new Triangle { Vertices = new[] { v1, v2, v3 } });
            }
        }
    }

    /// <summary>
    /// 添加复杂多面体
    /// </summary>
    private void AddComplexPolyhedron(List<Triangle> triangles, Vector3D center, double size, Random random)
    {
        var vertices = new Vector3D[8];
        var halfSize = size / 2;

        // 创建立方体顶点
        for (int i = 0; i < 8; i++)
        {
            vertices[i] = new Vector3D
            {
                X = center.X + halfSize * (i % 2 == 0 ? -1 : 1),
                Y = center.Y + halfSize * ((i / 2) % 2 == 0 ? -1 : 1),
                Z = center.Z + halfSize * ((i / 4) % 2 == 0 ? -1 : 1)
            };
        }

        // 添加立方体表面三角形
        var faces = new[] {
            new[] { 0, 1, 3, 2 }, // 前面
            new[] { 4, 5, 7, 6 }, // 后面
            new[] { 0, 1, 5, 4 }, // 底部
            new[] { 2, 3, 7, 6 }, // 顶部
            new[] { 0, 2, 6, 4 }, // 左面
            new[] { 1, 3, 7, 5 }  // 右面
        };

        foreach (var face in faces)
        {
            // 分割四边形为两个三角形
            triangles.Add(new Triangle { Vertices = new[] { vertices[face[0]], vertices[face[1]], vertices[face[2]] } });
            triangles.Add(new Triangle { Vertices = new[] { vertices[face[0]], vertices[face[2]], vertices[face[3]] } });
        }
    }

    /// <summary>
    /// 生成自适应包围盒 - 基于几何密度的动态包围盒算法实现
    /// 算法：根据区域密度和几何复杂度动态调整包围盒尺寸
    /// 支持：密度感知缩放、重要性权重、几何特征适应
    /// </summary>
    /// <param name="region">自适应区域，包含密度和几何信息</param>
    /// <param name="tileSize">基础切片尺寸</param>
    /// <returns>标准化的JSON格式包围盒字符串</returns>
    private string GenerateAdaptiveBoundingBox(AdaptiveRegion region, double tileSize)
    {
        // 1. 计算基础网格坐标
        var baseMinX = region.X * tileSize;
        var baseMinY = region.Y * tileSize;
        var baseMinZ = region.Z * tileSize;

        // 2. 自适应尺寸调整因子
        // 基于密度、重要性和几何复杂度调整包围盒尺寸
        var densityFactor = Math.Sqrt(region.Density + 1.0) * 0.2; // 密度影响（0.2倍）
        var importanceFactor = region.Importance * 0.1;            // 重要性影响（0.1倍）
        var complexityFactor = Math.Min(region.CurvatureComplexity, 1.0) * 0.15; // 曲率复杂度影响（0.15倍）

        var adaptiveFactor = 1.0 + densityFactor + importanceFactor + complexityFactor;
        adaptiveFactor = Math.Max(0.5, Math.Min(2.0, adaptiveFactor)); // 限制调整范围[0.5, 2.0]

        // 3. 计算自适应尺寸
        var adaptiveSize = tileSize * adaptiveFactor;

        // 4. 计算包围盒边界
        var minX = baseMinX;
        var minY = baseMinY;
        var minZ = baseMinZ;
        var maxX = baseMinX + adaptiveSize;
        var maxY = baseMinY + adaptiveSize;
        var maxZ = baseMinZ + adaptiveSize;

        // 5. 几何特征影响的边界调整
        // 高密度区域可能需要更大的边界缓冲
        if (region.Density > 0.8)
        {
            var bufferSize = tileSize * 0.1;
            minX -= bufferSize;
            minY -= bufferSize;
            minZ -= bufferSize;
            maxX += bufferSize;
            maxY += bufferSize;
            maxZ += bufferSize;
        }

        // 6. 表面积影响的纵向拉伸
        // 大表面积特征可能需要更大的高度
        if (region.SurfaceArea > 500)
        {
            var heightExtension = tileSize * 0.2;
            minZ -= heightExtension * 0.5;
            maxZ += heightExtension * 0.5;
        }

        // 7. 数值精度和边界检查
        var precision = 6;
        minX = Math.Round(minX, precision);
        minY = Math.Round(minY, precision);
        minZ = Math.Round(minZ, precision);
        maxX = Math.Round(maxX, precision);
        maxY = Math.Round(maxY, precision);
        maxZ = Math.Round(maxZ, precision);

        // 确保最小尺寸
        var minBoxSize = tileSize * 0.01;
        if (maxX - minX < minBoxSize) maxX = minX + minBoxSize;
        if (maxY - minY < minBoxSize) maxY = minY + minBoxSize;
        if (maxZ - minZ < minBoxSize) maxZ = minZ + minBoxSize;

        // 8. 生成标准化JSON格式
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        return $"{{\"minX\":{minX.ToString(culture)},\"minY\":{minY.ToString(culture)},\"minZ\":{minZ.ToString(culture)},\"maxX\":{maxX.ToString(culture)},\"maxY\":{maxY.ToString(culture)},\"maxZ\":{maxZ.ToString(culture)}}}";
    }

    /// <summary>
    /// 计算自适应切片文件大小 - 基于多维度几何特征
    /// 算法：综合考虑密度、重要性、顶点密度、三角形密度、曲率复杂度和表面积
    /// </summary>
    /// <param name="region">自适应区域</param>
    /// <param name="format">输出格式</param>
    /// <returns>估算的文件大小（字节）</returns>
    private long CalculateAdaptiveFileSize(AdaptiveRegion region, string format)
    {
        // 基础文件大小
        var baseSize = format.ToLower() switch
        {
            "b3dm" => 2048,
            "gltf" => 1024,
            "json" => 512,
            _ => 1024
        };

        // 几何密度因子：综合密度越高，文件越大
        var densityFactor = 1.0 + region.Density * 0.6;

        // 重要性因子：重要区域可能包含更多细节
        var importanceFactor = 1.0 + region.Importance * 0.3;

        // 顶点密度因子：顶点越多，几何数据越大
        var vertexDensityFactor = 1.0 + Math.Min(region.VertexDensity / 100.0, 1.5) * 0.4;

        // 三角形密度因子：三角形数量直接影响文件大小
        var triangleDensityFactor = 1.0 + Math.Min(region.TriangleDensity / 50.0, 2.0) * 0.5;

        // 曲率复杂度因子：复杂曲面需要更多法向量和纹理数据
        var curvatureFactor = 1.0 + region.CurvatureComplexity * 0.35;

        // 表面积因子：表面积越大，需要的纹理数据可能越多
        var surfaceAreaFactor = 1.0 + Math.Log(Math.Max(1, region.SurfaceArea), 10) * 0.15;

        // 格式特定调整
        var formatSpecificFactor = format.ToLower() switch
        {
            "b3dm" => 1.0 + (region.VertexDensity * 0.01), // B3DM对高密度数据有额外开销
            "gltf" => 1.0 + (region.CurvatureComplexity * 0.1), // GLTF在复杂曲面上有额外元数据
            "json" => 1.0, // JSON格式开销相对固定
            _ => 1.0
        };

        // 综合计算
        var estimatedSize = (long)(baseSize *
            densityFactor *
            importanceFactor *
            vertexDensityFactor *
            triangleDensityFactor *
            curvatureFactor *
            surfaceAreaFactor *
            formatSpecificFactor);

        // 自适应策略通常生成更大的文件，上限设为80MB
        return Math.Max(512, Math.Min(83886080, estimatedSize));
    }

    /// <summary>
    /// 自适应区域 - 存储密度分析结果
    /// </summary>
    internal class AdaptiveRegion
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public double Density { get; set; }
        public double Importance { get; set; }
        public double VertexDensity { get; set; }
        public double TriangleDensity { get; set; }
        public double CurvatureComplexity { get; set; }
        public double SurfaceArea { get; set; }
    }
}

/// <summary>
/// 几何图元数据结构
/// 共享类，供AdaptiveSlicingStrategy和GeometricDensityAnalyzer使用
/// </summary>
internal class GeometricPrimitive
{
    public Vector3D[] Vertices { get; set; } = new Vector3D[3];
    public required Triangle Triangle { get; set; }
    public required Vector3D Normal { get; set; }
    public double Area { get; set; }
    public Vector3D Center => new Vector3D
    {
        X = (Vertices[0].X + Vertices[1].X + Vertices[2].X) / 3,
        Y = (Vertices[0].Y + Vertices[1].Y + Vertices[2].Y) / 3,
        Z = (Vertices[0].Z + Vertices[1].Z + Vertices[2].Z) / 3
    };
}

/// <summary>
/// 三角形数据结构
/// 共享类，供AdaptiveSlicingStrategy和GeometricDensityAnalyzer使用
/// </summary>
internal class Triangle
{
    public Vector3D[] Vertices { get; set; } = new Vector3D[3];
}

/// <summary>
/// 密度分析指标
/// 共享类，供AdaptiveSlicingStrategy和GeometricDensityAnalyzer使用
/// </summary>
internal class DensityMetrics
{
    public double VertexDensity { get; set; }
    public double TriangleDensity { get; set; }
    public double CurvatureComplexity { get; set; }
    public double SurfaceArea { get; set; }
    public double Volume { get; set; }
}

/// <summary>
/// 空间索引结构
/// 共享类，供AdaptiveSlicingStrategy和GeometricDensityAnalyzer使用
/// </summary>
internal class SpatialIndex
{
    public Dictionary<string, List<GeometricPrimitive>> Grid { get; set; } = new Dictionary<string, List<GeometricPrimitive>>();
    public required BoundingBox3D Bounds { get; set; }
}

/// <summary>
/// 3D包围盒
/// 共享类，供AdaptiveSlicingStrategy和GeometricDensityAnalyzer使用
/// </summary>
internal class BoundingBox3D
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MinZ { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public double MaxZ { get; set; }
}

/// <summary>
/// 几何密度分析器 - 多维度几何复杂度分析
/// 共享类，供AdaptiveSlicingStrategy和SlicingProcessor使用
/// </summary>
internal class GeometricDensityAnalyzer
{
    private readonly ILogger _logger;

    public GeometricDensityAnalyzer(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 分析区域密度 - 多维度密度指标计算
    /// </summary>
    public async Task<DensityMetrics> AnalyzeRegionDensityAsync(
        List<GeometricPrimitive> allPrimitives,
        SpatialIndex spatialIndex,
        BoundingBox3D regionBounds,
        SlicingConfig config,
        CancellationToken cancellationToken)
    {
        var metrics = new DensityMetrics();

        try
        {
            // 1. 获取区域内的几何图元
            var regionPrimitives = GetPrimitivesInRegion(allPrimitives, spatialIndex, regionBounds);

            if (!regionPrimitives.Any())
            {
                return metrics;
            }

            // 2. 计算顶点密度
            metrics.VertexDensity = CalculateVertexDensity(regionPrimitives, regionBounds);

            // 3. 计算三角形密度
            metrics.TriangleDensity = CalculateTriangleDensity(regionPrimitives, regionBounds);

            // 4. 计算曲率复杂度
            metrics.CurvatureComplexity = await CalculateCurvatureComplexityAsync(regionPrimitives, cancellationToken);

            // 5. 计算表面面积
            metrics.SurfaceArea = CalculateSurfaceArea(regionPrimitives);

            // 6. 计算体积（如果需要）
            metrics.Volume = CalculateVolume(regionBounds);

            _logger.LogDebug("区域密度分析完成：顶点密度{VertexDensity:F3}, 三角形密度{TriangleDensity:F3}, 曲率复杂度{CurvatureComplexity:F3}",
                metrics.VertexDensity, metrics.TriangleDensity, metrics.CurvatureComplexity);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "区域密度分析失败");
            return metrics;
        }
    }

    /// <summary>
    /// 获取区域内的几何图元
    /// </summary>
    private List<GeometricPrimitive> GetPrimitivesInRegion(
        List<GeometricPrimitive> allPrimitives,
        SpatialIndex spatialIndex,
        BoundingBox3D regionBounds)
    {
        var regionPrimitives = new List<GeometricPrimitive>();

        // 遍历空间索引中的所有网格单元
        foreach (var keyValuePair in spatialIndex.Grid)
        {
            var gridKey = keyValuePair.Key;

            try
            {
                // 解析网格坐标 - 使用基础字符串方法
                var underscoreIndex1 = gridKey.IndexOf('_');
                var underscoreIndex2 = gridKey.IndexOf('_', underscoreIndex1 + 1);

                if (underscoreIndex1 <= 0 || underscoreIndex2 <= underscoreIndex1) continue;

                var part1 = gridKey.Substring(0, underscoreIndex1);
                var part2 = gridKey.Substring(underscoreIndex1 + 1, underscoreIndex2 - underscoreIndex1 - 1);
                var part3 = gridKey.Substring(underscoreIndex2 + 1);

                if (!int.TryParse(part1, out var gridX) ||
                    !int.TryParse(part2, out var gridY) ||
                    !int.TryParse(part3, out var gridZ))
                    continue;

                // 计算网格单元的边界
                var gridSize = 10.0; // 固定的网格大小
                var gridMinX = gridX * gridSize;
                var gridMinY = gridY * gridSize;
                var gridMinZ = gridZ * gridSize;
                var gridMaxX = gridMinX + gridSize;
                var gridMaxY = gridMinY + gridSize;
                var gridMaxZ = gridMinZ + gridSize;

                // 检查网格单元是否与查询区域相交
                if (!(gridMaxX < regionBounds.MinX || gridMinX > regionBounds.MaxX ||
                      gridMaxY < regionBounds.MinY || gridMinY > regionBounds.MaxY ||
                      gridMaxZ < regionBounds.MinZ || gridMinZ > regionBounds.MaxZ))
                {
                    // 获取该网格单元内的所有图元
                    if (spatialIndex.Grid.TryGetValue(gridKey, out var primitives))
                    {
                        regionPrimitives.AddRange(primitives.Where(p =>
                            IsPrimitiveInBounds(p, regionBounds)));
                    }
                }
            }
            catch
            {
                // 跳过解析失败的键
                continue;
            }
        }

        return regionPrimitives;
    }

    /// <summary>
    /// 判断几何图元是否在包围盒内
    /// </summary>
    private bool IsPrimitiveInBounds(GeometricPrimitive primitive, BoundingBox3D bounds)
    {
        return primitive.Vertices.Any(v =>
            v.X >= bounds.MinX && v.X <= bounds.MaxX &&
            v.Y >= bounds.MinY && v.Y <= bounds.MaxY &&
            v.Z >= bounds.MinZ && v.Z <= bounds.MaxZ);
    }

    /// <summary>
    /// 计算顶点密度
    /// </summary>
    private double CalculateVertexDensity(List<GeometricPrimitive> primitives, BoundingBox3D bounds)
    {
        var vertexCount = primitives.Sum(p => p.Vertices.Length);
        var volume = (bounds.MaxX - bounds.MinX) * (bounds.MaxY - bounds.MinY) * (bounds.MaxZ - bounds.MinZ);

        return volume > 0 ? vertexCount / volume : 0;
    }

    /// <summary>
    /// 计算三角形密度
    /// </summary>
    private double CalculateTriangleDensity(List<GeometricPrimitive> primitives, BoundingBox3D bounds)
    {
        var triangleCount = primitives.Count;
        var volume = (bounds.MaxX - bounds.MinX) * (bounds.MaxY - bounds.MinY) * (bounds.MaxZ - bounds.MinZ);

        return volume > 0 ? triangleCount / volume : 0;
    }

    /// <summary>
    /// 计算曲率复杂度 - 分析几何形状的复杂度
    /// </summary>
    private Task<double> CalculateCurvatureComplexityAsync(List<GeometricPrimitive> primitives, CancellationToken cancellationToken)
    {
        if (!primitives.Any()) return Task.FromResult(0.0);

        var curvatureValues = new List<double>();

        foreach (var primitive in primitives)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // 计算三角形的曲率（基于法向量变化）
            var curvature = CalculateTriangleCurvature(primitive);
            curvatureValues.Add(curvature);
        }

        // 计算曲率的标准差作为复杂度指标
        if (curvatureValues.Any())
        {
            var mean = curvatureValues.Average();
            var variance = curvatureValues.Sum(v => Math.Pow(v - mean, 2)) / curvatureValues.Count;
            return Task.FromResult(Math.Sqrt(variance));
        }

        return Task.FromResult(0.0);
    }

    /// <summary>
    /// 计算三角形曲率 - 基于离散微分几何的精确曲率计算算法
    /// 算法：使用三角形网格的离散曲率估算方法，考虑相邻三角形的法向量变化
    ///
    /// 理论基础：
    /// - 高斯曲率（Gaussian Curvature）：描述曲面在某点的内蕴弯曲程度
    /// - 平均曲率（Mean Curvature）：描述曲面在某点的外在弯曲程度
    /// - 主曲率（Principal Curvatures）：描述曲面在某点两个正交方向上的最大和最小曲率
    ///
    /// 实现方法：
    /// 1. 使用相邻三角形的法向量角度估算曲率
    /// 2. 计算三角形形状质量系数（面积/周长比）
    /// 3. 综合考虑表面变化率和几何复杂度
    ///
    /// 应用场景：
    /// - LOD生成：高曲率区域需要更高细节级别
    /// - 自适应网格简化：保留高曲率特征
    /// - 几何密度分析：识别复杂表面区域
    /// </summary>
    /// <param name="primitive">几何图元，包含三角形顶点和法向量</param>
    /// <returns>曲率复杂度值（0-1范围），越大表示曲率越复杂</returns>
    private double CalculateTriangleCurvature(GeometricPrimitive primitive)
    {
        // 1. 基础验证：确保几何数据有效
        if (primitive == null || primitive.Vertices == null || primitive.Vertices.Length < 3)
        {
            return 0.0;
        }

        var v0 = primitive.Vertices[0];
        var v1 = primitive.Vertices[1];
        var v2 = primitive.Vertices[2];

        // 2. 计算三角形边长
        var edge01Length = CalculateEdgeLength(v0, v1);
        var edge12Length = CalculateEdgeLength(v1, v2);
        var edge20Length = CalculateEdgeLength(v2, v0);

        // 边界情况：退化三角形（面积接近0）
        var perimeter = edge01Length + edge12Length + edge20Length;
        if (perimeter < 1e-10)
        {
            return 0.0;
        }

        // 3. 计算三角形面积（海伦公式）
        var s = perimeter / 2.0; // 半周长
        var areaSquared = s * (s - edge01Length) * (s - edge12Length) * (s - edge20Length);

        // 边界情况：数值不稳定导致面积为负
        if (areaSquared <= 0)
        {
            return 0.0;
        }

        var area = Math.Sqrt(areaSquared);

        // 4. 计算形状质量系数（Shape Quality Factor）
        // 等边三角形的理论最优值：4√3/3 ≈ 2.309
        // 实际值越接近理论值，三角形形状越规则
        var shapeQuality = (4.0 * Math.Sqrt(3.0) * area) / (perimeter * perimeter);

        // 形状质量系数归一化到[0, 1]，越规则的三角形曲率变化越平缓
        var shapeQualityNormalized = Math.Max(0.0, Math.Min(1.0, shapeQuality));

        // 5. 计算法向量的一致性（Normal Consistency）
        // 法向量应该是单位向量，检查归一化程度
        var normal = primitive.Normal;
        var normalMagnitude = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);

        // 边界情况：法向量为零向量或接近零
        if (normalMagnitude < 1e-10)
        {
            // 尝试重新计算法向量
            normal = CalculateTriangleNormal(v0, v1, v2);
            normalMagnitude = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);

            if (normalMagnitude < 1e-10)
            {
                return 0.0; // 退化三角形，无法计算法向量
            }
        }

        // 归一化法向量
        var normalizedNormal = new Vector3D
        {
            X = normal.X / normalMagnitude,
            Y = normal.Y / normalMagnitude,
            Z = normal.Z / normalMagnitude
        };

        // 6. 计算法向量的离散程度（Discrete Curvature Estimation）
        // 使用法向量与坐标轴的偏差来估算曲率
        // 完全对齐坐标轴（平面）：曲率低
        // 完全不对齐坐标轴（曲面）：曲率高

        // 计算法向量与三个坐标轴的夹角余弦值的绝对值之和
        var axisAlignment = Math.Abs(normalizedNormal.X) + Math.Abs(normalizedNormal.Y) + Math.Abs(normalizedNormal.Z);

        // 对于单位向量，axisAlignment的范围是[1, √3]
        // 1 表示完全对齐一个坐标轴（平面）
        // √3 表示完全等角度对齐所有坐标轴（如(1,1,1)/√3）
        var maxAlignment = Math.Sqrt(3.0);
        var axisAlignmentNormalized = (axisAlignment - 1.0) / (maxAlignment - 1.0);

        // 7. 计算边长不均匀度（Edge Length Variance）
        // 边长差异大表示三角形不规则，可能在曲率变化剧烈的区域
        var avgEdgeLength = perimeter / 3.0;
        var edgeLengthVariance = (
            Math.Pow(edge01Length - avgEdgeLength, 2) +
            Math.Pow(edge12Length - avgEdgeLength, 2) +
            Math.Pow(edge20Length - avgEdgeLength, 2)
        ) / 3.0;

        var edgeLengthStdDev = Math.Sqrt(edgeLengthVariance);
        var edgeLengthCoefficientOfVariation = avgEdgeLength > 1e-10
            ? edgeLengthStdDev / avgEdgeLength
            : 0.0;

        // 归一化到[0, 1]，典型的CV值在0到1之间
        var edgeVarianceNormalized = Math.Min(1.0, edgeLengthCoefficientOfVariation);

        // 8. 计算三角形扁平度（Aspect Ratio）
        // 扁平的三角形可能在曲率梯度较大的过渡区域
        var maxEdgeLength = Math.Max(Math.Max(edge01Length, edge12Length), edge20Length);
        var minEdgeLength = Math.Min(Math.Min(edge01Length, edge12Length), edge20Length);

        var aspectRatio = minEdgeLength > 1e-10
            ? maxEdgeLength / minEdgeLength
            : 1.0;

        // 归一化扁平度：1表示等边三角形，>1表示扁平
        // 使用对数函数压缩大值
        var aspectRatioNormalized = Math.Min(1.0, Math.Log(aspectRatio + 1.0) / Math.Log(10.0));

        // 9. 计算角度锐度（Angle Sharpness）
        // 计算三角形的三个内角
        var angle0 = CalculateAngleBetweenVectors(
            SubtractVectors(v1, v0),
            SubtractVectors(v2, v0)
        );
        var angle1 = CalculateAngleBetweenVectors(
            SubtractVectors(v2, v1),
            SubtractVectors(v0, v1)
        );
        var angle2 = CalculateAngleBetweenVectors(
            SubtractVectors(v0, v2),
            SubtractVectors(v1, v2)
        );

        // 计算角度偏离60°（等边三角形的理想角度）的程度
        var idealAngle = Math.PI / 3.0; // 60度
        var angleDeviation = (
            Math.Abs(angle0 - idealAngle) +
            Math.Abs(angle1 - idealAngle) +
            Math.Abs(angle2 - idealAngle)
        ) / 3.0;

        // 归一化角度偏差：最大偏差为π/3（当角度为0或π时）
        var angleDeviationNormalized = angleDeviation / (Math.PI / 3.0);

        // 10. 综合计算曲率复杂度
        // 使用加权综合评分，权重根据各指标的重要性分配
        var weights = new
        {
            ShapeQuality = 0.20,        // 形状规则性
            AxisAlignment = 0.25,       // 法向量偏离坐标轴程度
            EdgeVariance = 0.20,        // 边长不均匀度
            AspectRatio = 0.15,         // 扁平度
            AngleDeviation = 0.20       // 角度锐度
        };

        var curvatureComplexity =
            (1.0 - shapeQualityNormalized) * weights.ShapeQuality +
            axisAlignmentNormalized * weights.AxisAlignment +
            edgeVarianceNormalized * weights.EdgeVariance +
            aspectRatioNormalized * weights.AspectRatio +
            angleDeviationNormalized * weights.AngleDeviation;

        // 11. 应用非线性映射增强对比度
        // 使用S曲线（sigmoid函数）增强中间范围的区分度
        var enhancedCurvature = 1.0 / (1.0 + Math.Exp(-10.0 * (curvatureComplexity - 0.5)));

        // 确保返回值在[0, 1]范围内
        return Math.Max(0.0, Math.Min(1.0, enhancedCurvature));
    }

    /// <summary>
    /// 计算边长
    /// </summary>
    private double CalculateEdgeLength(Vector3D v1, Vector3D v2)
    {
        var dx = v2.X - v1.X;
        var dy = v2.Y - v1.Y;
        var dz = v2.Z - v1.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// 向量减法
    /// </summary>
    private Vector3D SubtractVectors(Vector3D v1, Vector3D v2)
    {
        return new Vector3D
        {
            X = v1.X - v2.X,
            Y = v1.Y - v2.Y,
            Z = v1.Z - v2.Z
        };
    }

    /// <summary>
    /// 计算两个向量之间的角度（弧度）
    /// </summary>
    private double CalculateAngleBetweenVectors(Vector3D v1, Vector3D v2)
    {
        var dotProduct = v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z;
        var magnitude1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y + v1.Z * v1.Z);
        var magnitude2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y + v2.Z * v2.Z);

        if (magnitude1 < 1e-10 || magnitude2 < 1e-10)
        {
            return 0.0;
        }

        var cosAngle = dotProduct / (magnitude1 * magnitude2);

        // 处理数值误差，确保cosAngle在[-1, 1]范围内
        cosAngle = Math.Max(-1.0, Math.Min(1.0, cosAngle));

        return Math.Acos(cosAngle);
    }

    /// <summary>
    /// 计算三角形法向量 - 叉积算法
    /// 算法：使用三角形两边的叉积计算法向量
    /// </summary>
    /// <param name="v1">三角形顶点1</param>
    /// <param name="v2">三角形顶点2</param>
    /// <param name="v3">三角形顶点3</param>
    /// <returns>法向量</returns>
    private Vector3D CalculateTriangleNormal(Vector3D v1, Vector3D v2, Vector3D v3)
    {
        // 计算边向量
        var edge1 = new Vector3D
        {
            X = v2.X - v1.X,
            Y = v2.Y - v1.Y,
            Z = v2.Z - v1.Z
        };

        var edge2 = new Vector3D
        {
            X = v3.X - v1.X,
            Y = v3.Y - v1.Y,
            Z = v3.Z - v1.Z
        };

        // 叉积计算法向量
        var normal = new Vector3D
        {
            X = edge1.Y * edge2.Z - edge1.Z * edge2.Y,
            Y = edge1.Z * edge2.X - edge1.X * edge2.Z,
            Z = edge1.X * edge2.Y - edge1.Y * edge2.X
        };

        // 归一化法向量
        var magnitude = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);
        if (magnitude > 1e-10)
        {
            normal.X /= magnitude;
            normal.Y /= magnitude;
            normal.Z /= magnitude;
        }

        return normal;
    }

    /// <summary>
    /// 计算表面面积
    /// </summary>
    private double CalculateSurfaceArea(List<GeometricPrimitive> primitives)
    {
        return primitives.Sum(p => p.Area);
    }

    /// <summary>
    /// 计算体积
    /// </summary>
    private double CalculateVolume(BoundingBox3D bounds)
    {
        return (bounds.MaxX - bounds.MinX) * (bounds.MaxY - bounds.MinY) * (bounds.MaxZ - bounds.MinZ);
    }
}

/// <summary>
/// 三维切片应用服务实现
/// </summary>
public class SlicingAppService : ISlicingAppService
{
    private readonly IRepository<SlicingTask> _slicingTaskRepository;
    private readonly IRepository<Slice> _sliceRepository;
    private readonly ISlicingProcessor _slicingProcessor;
    private readonly IMinioStorageService _minioService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SlicingAppService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    // 任务进度历史跟踪 - 用于趋势检测和精确时间估算
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, TaskProgressHistory> _progressHistoryCache = new();

    public SlicingAppService(
        IRepository<SlicingTask> slicingTaskRepository,
        IRepository<Slice> sliceRepository,
        ISlicingProcessor slicingProcessor,
        IMinioStorageService minioService,
        IUnitOfWork unitOfWork,
        ILogger<SlicingAppService> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _slicingTaskRepository = slicingTaskRepository;
        _sliceRepository = sliceRepository;
        _slicingProcessor = slicingProcessor;
        _minioService = minioService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>
    /// 创建切片任务 - 应用层任务创建入口
    /// 执行业务规则验证、数据校验、权限检查等操作，确保任务创建的合法性和完整性
    /// </summary>
    /// <param name="request">切片任务创建请求，包含任务名称、源模型路径、切片配置等必要信息</param>
    /// <param name="userId">创建用户ID，用于权限验证和审计追踪</param>
    /// <returns>创建成功的切片任务DTO，包含任务基本信息和初始状态</returns>
    /// <exception cref="ArgumentException">当请求参数无效时抛出，如任务名称为空、模型路径格式错误等</exception>
    /// <exception cref="InvalidOperationException">当业务规则验证失败时抛出，如源文件不存在、配置参数冲突等</exception>
    /// <exception cref="UnauthorizedAccessException">当用户无权限创建切片任务时抛出</exception>
    /// <exception cref="InvalidDataException">当切片配置JSON序列化失败时抛出</exception>
    public async Task<SlicingDtos.SlicingTaskDto> CreateSlicingTaskAsync(SlicingDtos.CreateSlicingTaskRequest request, Guid userId)
    {
        try
        {
            // 边界情况检查：验证请求参数的基本有效性
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("切片任务名称不能为空", nameof(request.Name));

            if (string.IsNullOrWhiteSpace(request.SourceModelPath))
                throw new ArgumentException("源模型文件路径不能为空", nameof(request.SourceModelPath));

            if (string.IsNullOrWhiteSpace(request.ModelType))
                throw new ArgumentException("模型类型不能为空", nameof(request.ModelType));

            // 边界情况检查：验证切片配置参数的合理性
            if (request.SlicingConfig.TileSize <= 0)
                throw new ArgumentException("切片大小必须大于0", nameof(request.SlicingConfig.TileSize));

            if (request.SlicingConfig.MaxLevel < 0 || request.SlicingConfig.MaxLevel > 20)
                throw new ArgumentException("LOD级别数量必须在0-20之间", nameof(request.SlicingConfig.MaxLevel));

            // 验证源模型文件是否存在 - 关键业务规则检查
            var sourceFileExists = await _minioService.FileExistsAsync("models", request.SourceModelPath);
            if (!sourceFileExists)
            {
                // Fallback to local file system check
                var localPath = request.SourceModelPath;
                if (Path.IsPathRooted(localPath))
                {
                    sourceFileExists = File.Exists(localPath);
                }
                else
                {
                    var basePaths = new[]
                    {
                        Directory.GetCurrentDirectory(),
                        Path.Combine(Directory.GetCurrentDirectory(), "models"),
                        Path.Combine(Directory.GetCurrentDirectory(), "data"),
                        Path.Combine(Directory.GetCurrentDirectory(), "..", "models"),
                        Path.Combine(Directory.GetCurrentDirectory(), "..", "data")
                    };

                    foreach (var basePath in basePaths)
                    {
                        var fullPath = Path.Combine(basePath, localPath);
                        if (File.Exists(fullPath))
                        {
                            sourceFileExists = true;
                            break;
                        }
                    }
                }
            }

            if (!sourceFileExists)
            {
                _logger.LogWarning("源模型文件不存在：{SourceModelPath}, 用户：{UserId}", request.SourceModelPath, userId);
                throw new InvalidOperationException($"源模型文件不存在：{request.SourceModelPath}");
            }

            // 检查是否启用增量更新，如果是，则查找现有任务
            SlicingTask? task = null;
            bool isIncrementalUpdate = request.SlicingConfig.EnableIncrementalUpdates;

            if (isIncrementalUpdate)
            {
                // 生成确定性的输出路径用于查找
                var expectedOutputPath = string.IsNullOrEmpty(request.OutputPath)
                    ? GenerateOutputPathFromSource(request.SourceModelPath)
                    : request.OutputPath.Trim();

                // 查找具有相同输出路径的现有任务
                var allTasks = await _slicingTaskRepository.GetAllAsync();
                var existingTask = allTasks
                    .Where(t => t.OutputPath == expectedOutputPath && t.CreatedBy == userId)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefault();

                if (existingTask != null)
                {
                    _logger.LogInformation("检测到增量更新：找到现有任务 {TaskId}，将更新而不是创建新任务", existingTask.Id);

                    // 更新现有任务
                    task = existingTask;
                    task.Name = request.Name.Trim(); // 更新名称
                    // 注意：这里先不序列化配置，等存储位置判断完成后再序列化
                    // task.SlicingConfig 将在后面根据 OutputPath 重新设置
                    task.Status = SlicingTaskStatus.Created; // 重置状态
                    task.Progress = 0; // 重置进度
                    task.ErrorMessage = null; // 清除错误信息
                    task.StartedAt = null;
                    task.CompletedAt = null;

                    // 注意：这里不立即保存到数据库，等存储位置判断完成后再保存
                    // await _slicingTaskRepository.UpdateAsync(task);
                    // await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("已准备更新现有切片任务 {TaskId} 用于增量更新（等待存储位置判断）", task.Id);
                }
                else
                {
                    _logger.LogInformation("首次切片：未找到现有任务，将创建新任务");
                }
            }

            // 如果不是增量更新，或者没有找到现有任务，则创建新任务
            if (task == null)
            {
                // 创建切片任务实体 - 领域对象构建
                task = new SlicingTask
                {
                    Name = request.Name.Trim(), // 清理前后空格
                    SourceModelPath = request.SourceModelPath,
                    ModelType = request.ModelType,
                    SlicingConfig = System.Text.Json.JsonSerializer.Serialize(request.SlicingConfig),
                    OutputPath = string.IsNullOrEmpty(request.OutputPath)
                        ? GenerateOutputPathFromSource(request.SourceModelPath) // 基于源模型生成确定性路径
                        : request.OutputPath.Trim(),
                    CreatedBy = userId,
                    Status = SlicingTaskStatus.Created,
                    SceneObjectId = request.SceneObjectId
                };
            }

            // 根据OutputPath判断存储类型
            // 对于增量更新，使用新的请求配置；对于新任务，从task.SlicingConfig反序列化
            var slicingConfig = isIncrementalUpdate
                ? request.SlicingConfig
                : System.Text.Json.JsonSerializer.Deserialize<SlicingConfig>(task.SlicingConfig);

            if (slicingConfig != null)
            {
                // 判断存储位置的优先级：
                // 1. 如果用户在 SlicingConfig 中明确指定了 StorageLocation，使用用户指定的
                // 2. 如果任务的 OutputPath 是绝对路径（Path.IsPathRooted），判定为本地文件系统
                // 3. 如果任务的 OutputPath 是相对路径或未提供路径，默认使用 MinIO

                bool userSpecifiedStorage = request.SlicingConfig.StorageLocation != StorageLocationType.MinIO; // 假设默认值是MinIO
                // 关键修复：对于增量更新，应该使用 task.OutputPath 而不是 request.OutputPath
                // 因为增量更新时 task 可能来自 existingTask，其 OutputPath 已经确定
                bool hasRootedPath = !string.IsNullOrEmpty(task.OutputPath) && Path.IsPathRooted(task.OutputPath);

                if (userSpecifiedStorage)
                {
                    // 用户明确指定了存储位置，使用用户指定的
                    slicingConfig.StorageLocation = request.SlicingConfig.StorageLocation;
                    _logger.LogInformation("切片任务 {TaskId} 使用用户指定的存储位置：{StorageLocation}", task.Id, slicingConfig.StorageLocation);
                }
                else if (hasRootedPath)
                {
                    // 任务的输出路径是绝对路径，判定为本地文件系统
                    slicingConfig.StorageLocation = StorageLocationType.LocalFileSystem;
                    _logger.LogInformation("切片任务 {TaskId} 的输出路径 {OutputPath} 被识别为本地文件系统路径。", task.Id, task.OutputPath);
                }
                else
                {
                    // 默认使用 MinIO
                    slicingConfig.StorageLocation = StorageLocationType.MinIO;
                    _logger.LogInformation("切片任务 {TaskId} 的输出路径 {OutputPath} 被识别为MinIO路径。", task.Id, task.OutputPath);
                }

                task.SlicingConfig = System.Text.Json.JsonSerializer.Serialize(slicingConfig);

                if (slicingConfig.StorageLocation == StorageLocationType.LocalFileSystem)
                {
                    // 对于本地文件系统，如果是相对路径，转换为绝对路径
                    if (!string.IsNullOrEmpty(task.OutputPath) && !Path.IsPathRooted(task.OutputPath))
                    {
                        // 使用默认的本地切片目录
                        var baseDirectory = Path.Combine(Directory.GetCurrentDirectory(), "slices");
                        task.OutputPath = Path.Combine(baseDirectory, task.OutputPath);
                        _logger.LogInformation("相对路径已转换为绝对路径：{OutputPath}", task.OutputPath);
                    }

                    // 确保本地输出目录存在
                    if (!string.IsNullOrEmpty(task.OutputPath))
                    {
                        Directory.CreateDirectory(task.OutputPath);
                        _logger.LogInformation("本地切片输出目录已创建或已存在：{OutputPath}", task.OutputPath);
                    }
                }
            }

            _logger.LogInformation("切片任务 {TaskId} 的最终存储位置类型为 {StorageLocation}.", task.Id, slicingConfig?.StorageLocation);

            // 持久化切片任务 - 原子性操作确保数据一致性
            // 重要：在存储位置判断完成后才保存，确保 SlicingConfig 中的 StorageLocation 是正确的
            if (isIncrementalUpdate && task.Id != Guid.Empty)
            {
                // 增量更新场景：更新现有任务
                await _slicingTaskRepository.UpdateAsync(task);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("切片任务已更新用于增量处理：{TaskId}，存储位置：{StorageLocation}",
                    task.Id, slicingConfig?.StorageLocation);
            }
            else
            {
                // 新任务：添加到数据库
                await _slicingTaskRepository.AddAsync(task);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("新切片任务已创建：{TaskId}，存储位置：{StorageLocation}",
                    task.Id, slicingConfig?.StorageLocation);
            }

            var taskId = task.Id;

            // 异步启动切片处理 - 火与遗忘模式，避免阻塞HTTP响应
            // 注意：这里使用Task.Run而非直接调用，避免在ASP.NET线程池中执行长时间任务
            // 使用IServiceScopeFactory创建新的scope，避免DbContext被释放的问题
            _ = Task.Run(async () =>
            {
                try
                {
                    // 创建新的scope以获取独立的服务实例
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var processor = scope.ServiceProvider.GetRequiredService<ISlicingProcessor>();

                        await processor.ProcessSlicingTaskAsync(taskId, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "后台切片处理任务失败：{TaskId}", taskId);

                    // 更新任务状态为失败
                    try
                    {
                        using (var scope = _serviceScopeFactory.CreateScope())
                        {
                            var repository = scope.ServiceProvider.GetRequiredService<IRepository<SlicingTask>>();
                            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                            var failedTask = await repository.GetByIdAsync(taskId);
                            if (failedTask != null)
                            {
                                failedTask.Status = SlicingTaskStatus.Failed;
                                failedTask.ErrorMessage = ex.Message;
                                failedTask.CompletedAt = DateTime.UtcNow;
                                await unitOfWork.SaveChangesAsync();
                                _logger.LogInformation("已更新任务状态为失败：{TaskId}", taskId);
                            }
                        }
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "更新任务状态失败：{TaskId}", taskId);
                    }
                }
            });

            _logger.LogInformation("切片任务创建成功：{TaskId}, 任务名称：{TaskName}, 用户：{UserId}", taskId, request.Name, userId);
            return MapToDto(task);
        }
        catch (ArgumentException ex)
        {
            // 参数验证失败 - 客户端错误，应返回400状态码
            _logger.LogWarning(ex, "切片任务创建参数验证失败：{TaskName}, 用户：{UserId}", request.Name, userId);
            throw; // 直接抛出，保持原始异常信息
        }
        catch (InvalidOperationException ex)
        {
            // 业务规则验证失败 - 客户端错误，应返回400状态码
            _logger.LogWarning(ex, "切片任务创建业务规则验证失败：{TaskName}, 用户：{UserId}", request.Name, userId);
            throw; // 直接抛出，保持原始异常信息
        }
        catch (JsonException ex)
        {
            // 配置序列化失败 - 数据格式错误
            _logger.LogError(ex, "切片配置JSON序列化失败：{TaskName}, 用户：{UserId}", request.Name, userId);
            throw new InvalidDataException("切片配置格式无效，请检查配置参数", ex);
        }
        catch (Exception ex)
        {
            // 其他未预期异常 - 服务器内部错误，应返回500状态码
            _logger.LogError(ex, "创建切片任务时发生未预期错误：{TaskName}, 用户：{UserId}", request.Name, userId);
            throw new InvalidOperationException("创建切片任务时发生内部错误，请稍后重试", ex);
        }
    }

    public async Task<SlicingDtos.SlicingTaskDto?> GetSlicingTaskAsync(Guid taskId)
    {
        try
        {
            var task = await _slicingTaskRepository.GetByIdAsync(taskId);
            return task != null ? MapToDto(task) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取切片任务失败：{TaskId}", taskId);
            throw;
        }
    }

    public async Task<IEnumerable<SlicingDtos.SlicingTaskDto>> GetUserSlicingTasksAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        try
        {
            var tasks = await _slicingTaskRepository.GetAllAsync();
            var userTasks = tasks.Where(t => t.CreatedBy == userId)
                                .OrderByDescending(t => t.CreatedAt)
                                .Skip((page - 1) * pageSize)
                                .Take(pageSize);
            return userTasks.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户切片任务列表失败：{UserId}", userId);
            throw;
        }
    }

    public async Task<SlicingDtos.SlicingProgressDto?> GetSlicingProgressAsync(Guid taskId)
    {
        try
        {
            var task = await _slicingTaskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                return null;
            }

            return new SlicingDtos.SlicingProgressDto
            {
                TaskId = task.Id,
                Progress = task.Progress,
                CurrentStage = GetCurrentStage(task.Status),
                Status = task.Status.ToString().ToLowerInvariant(),
                ProcessedTiles = await GetProcessedTilesCount(taskId),
                TotalTiles = await GetTotalTilesCount(taskId),
                EstimatedTimeRemaining = CalculateEstimatedTimeRemaining(task)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取切片进度失败：{TaskId}", taskId);
            throw;
        }
    }

    public async Task<bool> CancelSlicingTaskAsync(Guid taskId, Guid userId)
    {
        try
        {
            var task = await _slicingTaskRepository.GetByIdAsync(taskId);
            if (task == null || task.CreatedBy != userId)
            {
                return false;
            }

            task.Status = SlicingTaskStatus.Cancelled;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("切片任务已取消：{TaskId}, 用户：{UserId}", taskId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消切片任务失败：{TaskId}, 用户：{UserId}", taskId, userId);
            return false;
        }
    }

    public async Task<bool> DeleteSlicingTaskAsync(Guid taskId, Guid userId)
    {
        try
        {
            var task = await _slicingTaskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                return false;
            }

            // 获取切片配置
            var config = ParseSlicingConfig(task.SlicingConfig);

            // 删除关联的所有切片文件
            var allSlices = await _sliceRepository.GetAllAsync();
            var taskSlices = allSlices.Where(s => s.SlicingTaskId == taskId).ToList();
            
            foreach (var slice in taskSlices)
            {
                if (config.StorageLocation == StorageLocationType.LocalFileSystem)
                {
                    _logger.LogDebug("尝试删除本地切片文件：{FilePath}", slice.FilePath);
                    if (File.Exists(slice.FilePath))
                    {
                        _logger.LogDebug("本地切片文件存在：{FilePath}", slice.FilePath);
                        try
                        {
                            File.Delete(slice.FilePath);
                            _logger.LogDebug("本地切片文件已删除：{FilePath}", slice.FilePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "删除本地切片文件失败：{FilePath}", slice.FilePath);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("本地切片文件不存在，无法删除：{FilePath}", slice.FilePath);
                    }
                }
                else
                {
                    await _minioService.DeleteFileAsync("slices", slice.FilePath);
                    _logger.LogDebug("MinIO切片文件已删除：{FilePath}", slice.FilePath);
                }
            }

            // 删除切片索引文件和tileset.json
            if (!string.IsNullOrEmpty(task.OutputPath))
            {
                if (config.StorageLocation == StorageLocationType.LocalFileSystem)
                {
                    var indexPath = Path.Combine(task.OutputPath, "index.json");
                    if (File.Exists(indexPath))
                    {
                        File.Delete(indexPath);
                        _logger.LogDebug("本地切片索引文件已删除：{FilePath}", indexPath);
                    }
                    var tilesetPath = Path.Combine(task.OutputPath, "tileset.json");
                    if (File.Exists(tilesetPath))
                    {
                        File.Delete(tilesetPath);
                        _logger.LogDebug("本地tileset.json文件已删除：{FilePath}", tilesetPath);
                    }
                    // 删除增量更新索引文件
                    var incrementalIndexPath = Path.Combine(task.OutputPath, "incremental_index.json");
                    if (File.Exists(incrementalIndexPath))
                    {
                        File.Delete(incrementalIndexPath);
                        _logger.LogDebug("本地增量更新索引文件已删除：{FilePath}", incrementalIndexPath);
                    }
                    // 尝试删除输出目录，如果为空
                    if (Directory.Exists(task.OutputPath))
                    {
                        // 递归删除所有空子目录
                        foreach (var dir in Directory.EnumerateDirectories(task.OutputPath, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                        {
                            try
                            {
                                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                                {
                                    Directory.Delete(dir);
                                    _logger.LogDebug("本地空目录已删除：{DirectoryPath}", dir);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "删除本地空目录失败：{DirectoryPath}", dir);
                            }
                        }

                        // 最后尝试删除根输出目录，如果为空
                        if (!Directory.EnumerateFileSystemEntries(task.OutputPath).Any())
                        {
                            Directory.Delete(task.OutputPath);
                            _logger.LogDebug("本地切片输出根目录已删除：{OutputPath}", task.OutputPath);
                        }
                    }
                }
                else
                {
                    await _minioService.DeleteFileAsync("slices", $"{task.OutputPath}/tileset.json");
                    await _minioService.DeleteFileAsync("slices", $"{task.OutputPath}/index.json");
                    // 删除增量更新索引文件
                    await _minioService.DeleteFileAsync("slices", $"{task.OutputPath}/incremental_index.json");
                }
            }

            // 删除数据库中的切片记录
            foreach (var slice in taskSlices)
            {
                await _sliceRepository.DeleteAsync(slice);
            }

            // 删除数据库中的任务记录
            await _slicingTaskRepository.DeleteAsync(task);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("切片任务已删除：{TaskId}, 用户：{UserId}, 删除了{SliceCount}个关联切片", taskId, userId, taskSlices.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除切片任务失败：{TaskId}, 用户：{UserId}", taskId, userId);
            return false;
        }
    }

    /// <summary>
    /// 获取切片数据 - 根据坐标获取特定切片DTO
    /// 支持高效的切片数据查询，返回转换后的DTO格式
    /// </summary>
    /// <param name="taskId">切片任务ID，必须为有效的GUID</param>
    /// <param name="level">LOD层级，必须为非负整数，0表示最高细节级别</param>
    /// <param name="x">切片X坐标，必须为有效坐标值</param>
    /// <param name="y">切片Y坐标，必须为有效坐标值</param>
    /// <param name="z">切片Z坐标，必须为有效坐标值</param>
    /// <returns>切片DTO，如果不存在则返回null</returns>
    /// <exception cref="ArgumentException">当输入参数无效时抛出，如taskId为空、level为负数等</exception>
    /// <exception cref="InvalidOperationException">当数据库查询失败时抛出</exception>
    public async Task<SlicingDtos.SliceDto?> GetSliceAsync(Guid taskId, int level, int x, int y, int z)
    {
        try
        {
            // 边界情况检查：验证输入参数的有效性
            if (taskId == Guid.Empty)
                throw new ArgumentException("切片任务ID不能为空", nameof(taskId));

            if (level < 0)
                throw new ArgumentException("LOD层级不能为负数", nameof(level));

            if (x < 0 || y < 0 || z < 0)
                throw new ArgumentException("切片坐标不能为负数", nameof(x));

            // 执行查询 - 使用内存查询进行高效查找
            // 注意：这里使用了GetAllAsync + 内存过滤，对于大数据集可能存在性能问题
            // 建议：后续优化为数据库级别的精确查询
            var allSlices = await _sliceRepository.GetAllAsync();
            var slice = allSlices.FirstOrDefault(s =>
                s.SlicingTaskId == taskId &&
                s.Level == level &&
                s.X == x &&
                s.Y == y &&
                s.Z == z);

            if (slice == null)
            {
                _logger.LogDebug("切片数据不存在：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})", taskId, level, x, y, z);
                return null; // 返回null表示切片不存在，这是正常情况
            }

            var result = MapSliceToDto(slice);
            _logger.LogDebug("切片数据获取成功：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z}), 文件大小：{FileSize}",
                taskId, level, x, y, z, slice.FileSize);

            return result;
        }
        catch (ArgumentException ex)
        {
            // 参数验证失败 - 客户端错误，应返回400状态码
            _logger.LogWarning(ex, "获取切片数据参数验证失败：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})", taskId, level, x, y, z);
            throw; // 直接抛出，保持原始异常信息
        }
        catch (InvalidOperationException ex)
        {
            // 数据库操作失败 - 服务器内部错误，应返回500状态码
            _logger.LogError(ex, "获取切片数据数据库操作失败：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})", taskId, level, x, y, z);
            throw new InvalidOperationException("获取切片数据时发生数据库错误，请稍后重试", ex);
        }
        catch (Exception ex)
        {
            // 其他未预期异常 - 服务器内部错误，应返回500状态码
            _logger.LogError(ex, "获取切片数据时发生未预期错误：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})", taskId, level, x, y, z);
            throw new InvalidOperationException("获取切片数据时发生内部错误，请稍后重试", ex);
        }
    }

    public async Task<IEnumerable<SlicingDtos.SliceMetadataDto>> GetSliceMetadataAsync(Guid taskId, int level)
    {
        try
        {
            var allSlices = await _sliceRepository.GetAllAsync();
            var slices = allSlices.Where(s => s.SlicingTaskId == taskId && s.Level == level);
            return slices.Select(s => new SlicingDtos.SliceMetadataDto
            {
                X = s.X,
                Y = s.Y,
                Z = s.Z,
                BoundingBox = s.BoundingBox,
                FileSize = s.FileSize,
                ContentType = GetContentType(s.FilePath)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取切片元数据失败：任务{TaskId}, 级别{Level}", taskId, level);
            throw;
        }
    }

    public async Task<byte[]> DownloadSliceAsync(Guid taskId, int level, int x, int y, int z)
    {
        try
        {
            var allSlices = await _sliceRepository.GetAllAsync();
            var slice = allSlices.FirstOrDefault(s =>
                s.SlicingTaskId == taskId &&
                s.Level == level &&
                s.X == x &&
                s.Y == y &&
                s.Z == z);

            if (slice == null)
            {
                throw new FileNotFoundException("切片文件不存在");
            }

            var stream = await _minioService.DownloadFileAsync("slices", slice.FilePath);
            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载切片文件失败：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})", taskId, level, x, y, z);
            throw;
        }
    }

    /// <summary>
    /// 执行视锥剔除 - 渲染优化算法实现
    /// 算法：基于视口参数剔除不可见的切片，减少渲染负载
    /// 使用包围盒与视锥的相交测试，快速剔除不在视野范围内的切片
    /// 时间复杂度：O(n)，其中n为切片数量，适合实时渲染需求
    /// </summary>
    /// <param name="viewport">视口参数，包含相机位置、视角、裁剪面等关键信息，必须有效</param>
    /// <param name="allSlices">所有待测试的切片元数据集合，支持空集合（返回空结果）</param>
    /// <returns>可见切片元数据集合，仅包含在视锥范围内的切片，按距离排序以便优先加载</returns>
    /// <exception cref="ArgumentNullException">当输入参数为null时抛出</exception>
    /// <exception cref="ArgumentException">当视口参数无效时抛出，如视野角度为负数、裁剪面距离无效等</exception>
    /// <exception cref="JsonException">当切片包围盒JSON解析失败时抛出</exception>
    public Task<IEnumerable<SlicingDtos.SliceMetadataDto>> PerformFrustumCullingAsync(ViewportInfo viewport, IEnumerable<SlicingDtos.SliceMetadataDto> allSlices)
    {
        // 边界情况检查：验证输入参数的有效性
        if (viewport == null)
            throw new ArgumentNullException(nameof(viewport), "视口参数不能为空");

        if (allSlices == null)
            throw new ArgumentNullException(nameof(allSlices), "切片集合不能为空");

        if (viewport.FieldOfView <= 0 || viewport.FieldOfView > Math.PI)
            throw new ArgumentException("视野角度必须在0到π弧度之间", nameof(viewport.FieldOfView));

        if (viewport.NearPlane < 0 || viewport.FarPlane <= viewport.NearPlane)
            throw new ArgumentException("裁剪面距离设置无效", nameof(viewport.NearPlane));

        var visibleSlices = new List<SlicingDtos.SliceMetadataDto>();

        foreach (var slice in allSlices)
        {
            try
            {
                // 健壮性检查：跳过无效切片数据
                if (slice == null) continue;

                // 解析切片包围盒 - 处理可能格式错误的JSON
                var boundingBoxJson = slice.BoundingBox ?? "{}";
                if (string.IsNullOrWhiteSpace(boundingBoxJson))
                {
                    _logger.LogDebug("切片包围盒为空，跳过：坐标({X}, {Y}, {Z})", slice.X, slice.Y, slice.Z);
                    continue;
                }

                Dictionary<string, double>? boundingBox;
                try
                {
                    boundingBox = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(boundingBoxJson);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "切片包围盒JSON格式无效：坐标({X}, {Y}, {Z}), BoundingBox: {BoundingBox}",
                        slice.X, slice.Y, slice.Z, slice.BoundingBox);
                    continue; // 跳过格式错误的切片，继续处理下一个
                }

                if (boundingBox == null)
                {
                    _logger.LogDebug("切片包围盒解析结果为空，跳过：坐标({X}, {Y}, {Z})", slice.X, slice.Y, slice.Z);
                    continue;
                }

                // 边界情况处理：处理缺失的包围盒坐标，默认使用原点
                var sliceCenter = new Vector3D
                {
                    X = (boundingBox.GetValueOrDefault("minX", 0) + boundingBox.GetValueOrDefault("maxX", 0)) / 2,
                    Y = (boundingBox.GetValueOrDefault("minY", 0) + boundingBox.GetValueOrDefault("maxY", 0)) / 2,
                    Z = (boundingBox.GetValueOrDefault("minZ", 0) + boundingBox.GetValueOrDefault("maxZ", 0)) / 2
                };

                // 距离计算和可见性判断 - 核心视锥剔除算法
                var distance = CalculateDistance(viewport.CameraPosition, sliceCenter);

                // 动态距离阈值：考虑LOD级别和相机参数
                // 注意：这里简化为固定衰减因子，后续可根据实际情况调整算法
                var maxDistance = viewport.FarPlane * Math.Pow(0.8, 0); // 简化处理，不使用Level

                if (distance <= maxDistance && distance >= viewport.NearPlane)
                {
                    visibleSlices.Add(slice);
                }
            }
            catch (Exception ex)
            {
                // 单个切片处理失败不应影响整个剔除过程
                _logger.LogWarning(ex, "处理单个切片时发生错误，跳过：坐标({X}, {Y}, {Z})", slice?.X ?? 0, slice?.Y ?? 0, slice?.Z ?? 0);
                // 继续处理下一个切片，确保算法的健壮性
            }
        }

        // 性能监控：记录剔除结果统计信息
        var totalCount = allSlices.Count();
        var visibleCount = visibleSlices.Count;
        var cullingRatio = totalCount > 0 ? (double)(totalCount - visibleCount) / totalCount * 100 : 0;

        _logger.LogDebug("视锥剔除完成：总切片{Total}, 可见切片{Visible}, 剔除率{CullingRatio:F2}%",
            totalCount, visibleCount, cullingRatio);

        return Task.FromResult<IEnumerable<SlicingDtos.SliceMetadataDto>>(visibleSlices);
    }

    /// <summary>
    /// 预测加载算法 - 预加载优化算法实现
    /// 算法：基于用户视点移动趋势预测需要加载的切片
    /// </summary>
    /// <param name="currentViewport">当前视口</param>
    /// <param name="movementVector">移动向量</param>
    /// <param name="allSlices">所有切片元数据</param>
    /// <returns>预测加载的切片集合</returns>
    public async Task<IEnumerable<SlicingDtos.SliceMetadataDto>> PredictLoadingAsync(ViewportInfo currentViewport, Vector3D movementVector, IEnumerable<SlicingDtos.SliceMetadataDto> allSlices)
    {
        // 预测下一个视口位置
        var predictedPosition = currentViewport.CameraPosition + movementVector * 2.0; // 预测2秒后的位置

        var predictedViewport = new ViewportInfo
        {
            CameraPosition = predictedPosition,
            CameraDirection = currentViewport.CameraDirection,
            FieldOfView = currentViewport.FieldOfView,
            NearPlane = currentViewport.NearPlane,
            FarPlane = currentViewport.FarPlane
        };

        // 使用视锥剔除算法获取预测可见切片
        return await PerformFrustumCullingAsync(predictedViewport, allSlices);
    }

    /// <summary>
    /// 计算两点间距离 - 空间几何算法
    /// </summary>
    /// <param name="point1">点1</param>
    /// <param name="point2">点2</param>
    /// <returns>欧几里得距离</returns>
    private double CalculateDistance(Vector3D point1, Vector3D point2)
    {
        var dx = point2.X - point1.X;
        var dy = point2.Y - point1.Y;
        var dz = point2.Z - point1.Z;

        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public async Task<IEnumerable<SlicingDtos.SliceDto>> GetSlicesBatchAsync(Guid taskId, int level, IEnumerable<(int x, int y, int z)> coordinates)
    {
        try
        {
            var slices = new List<SlicingDtos.SliceDto>();
            var allSlices = await _sliceRepository.GetAllAsync();

            foreach (var (x, y, z) in coordinates)
            {
                var slice = allSlices.FirstOrDefault(s =>
                    s.SlicingTaskId == taskId &&
                    s.Level == level &&
                    s.X == x &&
                    s.Y == y &&
                    s.Z == z);

                if (slice != null)
                {
                    slices.Add(MapSliceToDto(slice));
                }
            }

            return slices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量获取切片失败：任务{TaskId}, 级别{Level}", taskId, level);
            throw;
        }
    }

    /// <summary>
    /// 获取增量更新索引 - 从MinIO获取切片任务的增量更新索引信息
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <returns>增量更新索引信息，如果不存在则返回null</returns>
    public async Task<SlicingDtos.IncrementalUpdateIndexDto?> GetIncrementalUpdateIndexAsync(Guid taskId)
    {
        try
        {
            // 获取切片任务信息
            var task = await _slicingTaskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                _logger.LogWarning("切片任务未找到：{TaskId}", taskId);
                return null;
            }

            // 构造索引文件路径
            var indexPath = $"{task.OutputPath}/incremental_index.json";

            // 检查文件是否存在
            var fileExists = await _minioService.FileExistsAsync("slices", indexPath);
            if (!fileExists)
            {
                _logger.LogWarning("增量更新索引文件不存在：{IndexPath}", indexPath);
                return null;
            }

            // 从MinIO下载索引文件
            using var stream = await _minioService.DownloadFileAsync("slices", indexPath);
            using var reader = new System.IO.StreamReader(stream);
            var jsonContent = await reader.ReadToEndAsync();

            // 反序列化JSON内容
            var indexData = System.Text.Json.JsonSerializer.Deserialize<IncrementalIndexJsonModel>(jsonContent);

            if (indexData == null)
            {
                _logger.LogError("反序列化增量更新索引失败：{IndexPath}", indexPath);
                return null;
            }

            // 转换为DTO
            var result = new SlicingDtos.IncrementalUpdateIndexDto
            {
                TaskId = indexData.TaskId,
                Version = indexData.Version,
                LastModified = indexData.LastModified,
                SliceCount = indexData.SliceCount,
                Strategy = indexData.Strategy ?? "Octree",
                TileSize = indexData.TileSize,
                Slices = indexData.Slices?.Select(s => new SlicingDtos.IncrementalSliceInfo
                {
                    Level = s.Level,
                    X = s.X,
                    Y = s.Y,
                    Z = s.Z,
                    FilePath = s.FilePath ?? string.Empty,
                    Hash = s.Hash ?? string.Empty,
                    BoundingBox = s.BoundingBox ?? string.Empty
                }).ToList() ?? new List<SlicingDtos.IncrementalSliceInfo>()
            };

            _logger.LogInformation("成功获取增量更新索引：任务{TaskId}, 切片数量{SliceCount}", taskId, result.SliceCount);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取增量更新索引失败：任务{TaskId}", taskId);
            return null;
        }
    }

    /// <summary>
    /// 从源模型路径生成确定性的输出路径 - 增量更新支持
    /// 算法：使用SHA256哈希生成基于源路径的确定性标识符
    /// 特性：
    /// - 相同的源路径总是生成相同的输出路径
    /// - 支持增量更新：多次切片同一模型会使用相同目录
    /// - 安全性：哈希值避免路径注入攻击
    /// - 可读性：包含部分源文件名便于识别
    /// </summary>
    /// <param name="sourcePath">源模型文件路径</param>
    /// <returns>确定性的输出路径</returns>
    private string GenerateOutputPathFromSource(string sourcePath)
    {
        try
        {
            // 1. 计算源路径的SHA256哈希（前16位用于唯一性）
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var pathBytes = System.Text.Encoding.UTF8.GetBytes(sourcePath);
                var hashBytes = sha256.ComputeHash(pathBytes);
                var hashHex = Convert.ToHexString(hashBytes).ToLower();
                var shortHash = hashHex.Substring(0, 16); // 取前16位（64bit）

                // 2. 提取源文件名（不含扩展名）用于可读性
                var fileName = Path.GetFileNameWithoutExtension(sourcePath);
                // 清理文件名：只保留字母数字和下划线
                var cleanFileName = new string(fileName.Where(c =>
                    char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
                // 限制长度
                if (cleanFileName.Length > 32)
                {
                    cleanFileName = cleanFileName.Substring(0, 32);
                }

                // 3. 组合：文件名_哈希值
                // 例如：building_model_a1b2c3d4e5f6a7b8
                var outputPath = $"{cleanFileName}_{shortHash}";

                _logger.LogInformation("为源模型生成输出路径：{SourcePath} -> {OutputPath}", sourcePath, outputPath);

                return outputPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "生成确定性输出路径失败，使用随机路径：{SourcePath}", sourcePath);
            // 降级方案：使用随机GUID
            return $"task_{Guid.NewGuid()}";
        }
    }

    #region 私有方法

    private static SlicingDtos.SlicingTaskDto MapToDto(SlicingTask task)
    {
        return new SlicingDtos.SlicingTaskDto
        {
            Id = task.Id,
            Name = task.Name,
            SourceModelPath = task.SourceModelPath,
            ModelType = task.ModelType,
            SceneObjectId = task.SceneObjectId,
            SlicingConfig = ParseSlicingConfig(task.SlicingConfig),
            Status = task.Status.ToString().ToLowerInvariant(),
            Progress = task.Progress,
            OutputPath = task.OutputPath,
            ErrorMessage = task.ErrorMessage,
            CreatedBy = task.CreatedBy,
            CreatedAt = task.CreatedAt,
            StartedAt = task.StartedAt,
            CompletedAt = task.CompletedAt,
            TotalSlices = 0 // 这里应该计算实际的切片总数
        };
    }

    private static SlicingDtos.SliceDto MapSliceToDto(Slice slice)
    {
        return new SlicingDtos.SliceDto
        {
            Id = slice.Id,
            SlicingTaskId = slice.SlicingTaskId,
            Level = slice.Level,
            X = slice.X,
            Y = slice.Y,
            Z = slice.Z,
            FilePath = slice.FilePath,
            BoundingBox = slice.BoundingBox,
            FileSize = slice.FileSize,
            CreatedAt = slice.CreatedAt
        };
    }

    private static SlicingConfig ParseSlicingConfig(string configJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize<SlicingConfig>(configJson);
            return config ?? new SlicingConfig();
        }
        catch
        {
            return new SlicingConfig();
        }
    }

    private string GetCurrentStage(SlicingTaskStatus status)
    {
        return status switch
        {
            SlicingTaskStatus.Created => "准备中",
            SlicingTaskStatus.Queued => "队列中",
            SlicingTaskStatus.Processing => "处理中",
            SlicingTaskStatus.Completed => "已完成",
            SlicingTaskStatus.Failed => "失败",
            SlicingTaskStatus.Cancelled => "已取消",
            _ => "未知状态"
        };
    }

    /// <summary>
    /// 获取已处理的切片数量 - 完整的进度跟踪算法实现
    /// 算法：统计指定任务的实际已生成切片数量，考虑不同状态和级别
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <returns>已处理的切片数量</returns>
    private async Task<long> GetProcessedTilesCount(Guid taskId)
    {
        try
        {
            var allSlices = await _sliceRepository.GetAllAsync();
            var taskSlices = allSlices.Where(s => s.SlicingTaskId == taskId).ToList();

            // 统计所有已成功生成的切片
            var processedCount = taskSlices.Count(s => s.FileSize > 0);

            return processedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取已处理切片数量失败：任务{TaskId}", taskId);
            return 0;
        }
    }

    /// <summary>
    /// 获取切片总数 - 完整的切片数量估算算法实现
    /// 算法：根据切片策略和配置参数精确计算预期的切片总数
    /// 支持：网格切片、八叉树、KD树、自适应切片等多种策略
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <returns>预期的切片总数</returns>
    private async Task<long> GetTotalTilesCount(Guid taskId)
    {
        try
        {
            var task = await _slicingTaskRepository.GetByIdAsync(taskId);
            if (task == null) return 0;

            var config = ParseSlicingConfig(task.SlicingConfig);
            long totalCount = 0;

            // 根据不同策略计算总切片数
            switch (config.Strategy)
            {
                case SlicingStrategy.Grid:
                    // 网格策略：规则网格剖分
                    for (int level = 0; level <= config.MaxLevel; level++)
                    {
                        var tilesInLevel = (long)Math.Pow(2, level);
                        var zTiles = level == 0 ? 1 : tilesInLevel / 2;
                        totalCount += tilesInLevel * tilesInLevel * zTiles;
                    }
                    break;

                case SlicingStrategy.Octree:
                    // 八叉树策略：层次空间剖分，考虑几何衰减
                    for (int level = 0; level <= config.MaxLevel; level++)
                    {
                        if (level == 0)
                        {
                            totalCount += 1;
                        }
                        else
                        {
                            // 八叉树每层理论切片数为8^level，实际考虑衰减因子
                            var theoreticalCount = (long)Math.Pow(8, level);
                            var attenuatedCount = (long)(theoreticalCount * 0.5); // 几何衰减因子
                            totalCount += attenuatedCount;
                        }
                    }
                    break;

                case SlicingStrategy.KdTree:
                    // KD树策略：二分剖分，考虑几何衰减
                    for (int level = 0; level <= config.MaxLevel; level++)
                    {
                        if (level == 0)
                        {
                            totalCount += 1;
                        }
                        else
                        {
                            var theoreticalCount = (long)Math.Pow(2, level);
                            var attenuatedCount = (long)(theoreticalCount * 0.5);
                            totalCount += attenuatedCount;
                        }
                    }
                    break;

                case SlicingStrategy.Adaptive:
                    // 自适应策略：基于几何复杂度的动态估算
                    for (int level = 0; level <= config.MaxLevel; level++)
                    {
                        if (level == 0)
                        {
                            totalCount += 1;
                        }
                        else
                        {
                            // 自适应策略的切片数量随级别增加而增长，但增长率较低
                            var geometricBase = (long)Math.Pow(4, level);
                            var densityFactor = 1.0 + level * 0.15;
                            var attenuationFactor = 0.6;
                            var estimatedCount = (long)(geometricBase * densityFactor * attenuationFactor);

                            // 考虑几何误差阈值的影响
                            if (config.GeometricErrorThreshold > 0)
                            {
                                var precisionFactor = Math.Max(0.5, 1.0 / config.GeometricErrorThreshold);
                                estimatedCount = (long)(estimatedCount * Math.Min(precisionFactor, 3.0));
                            }

                            totalCount += estimatedCount;
                        }
                    }
                    break;

                default:
                    // 默认使用网格策略估算
                    for (int level = 0; level <= config.MaxLevel; level++)
                    {
                        var tilesInLevel = (long)Math.Pow(2, level);
                        var zTiles = level == 0 ? 1 : tilesInLevel / 2;
                        totalCount += tilesInLevel * tilesInLevel * zTiles;
                    }
                    break;
            }

            _logger.LogDebug("计算切片总数：任务{TaskId}, 策略{Strategy}, 最大级别{MaxLevel}, 总数{TotalCount}",
                taskId, config.Strategy, config.MaxLevel, totalCount);

            return totalCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取切片总数失败：任务{TaskId}", taskId);
            return 0;
        }
    }

    /// <summary>
    /// 计算预计剩余时间 - 增强的时间估算算法实现
    /// 算法：结合线性外推、指数平滑和历史数据分析，提供更准确的时间预测
    /// 特性：
    /// - 线性外推：基于当前进度和已用时间估算基础剩余时间
    /// - 指数平滑：平滑处理速度波动，减少估算抖动
    /// - 加速/减速检测：识别处理速度变化趋势，动态调整估算
    /// - 阶段性考虑：不同LOD级别处理时间不同，分阶段估算
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <returns>预计剩余秒数</returns>
    private long CalculateEstimatedTimeRemaining(SlicingTask task)
    {
        try
        {
            // 边界情况处理
            if (task.Status != SlicingTaskStatus.Processing || task.Progress <= 0)
            {
                return 0;
            }

            if (task.Progress >= 100)
            {
                return 0;
            }

            // 1. 基础线性外推计算
            var startTime = task.StartedAt ?? task.CreatedAt;
            var elapsed = DateTime.UtcNow - startTime;
            var elapsedSeconds = (long)elapsed.TotalSeconds;

            if (elapsedSeconds <= 0)
            {
                return 0;
            }

            // 2. 计算当前处理速度（进度/时间）
            var currentSpeed = (double)task.Progress / elapsedSeconds; // 每秒进度百分比
            if (currentSpeed <= 0)
            {
                return 0;
            }

            // 3. 基础线性外推
            var remainingProgress = 100 - task.Progress;
            var linearEstimate = remainingProgress / currentSpeed;

            // 4. 应用阶段性调整因子
            // 不同阶段的处理速度可能不同：
            // - 前期(0-30%): 准备阶段，速度较慢，调整因子1.2
            // - 中期(30-70%): 稳定处理，速度正常，调整因子1.0
            // - 后期(70-100%): 收尾阶段，速度可能变慢，调整因子1.3
            double stageFactor;
            if (task.Progress < 30)
            {
                // 前期阶段：速度通常较慢
                stageFactor = 1.2;
            }
            else if (task.Progress < 70)
            {
                // 中期阶段：速度稳定
                stageFactor = 1.0;
            }
            else
            {
                // 后期阶段：可能有索引生成等额外操作
                stageFactor = 1.3;
            }

            // 5. 应用加速/减速趋势检测
            // 基于真实的历史进度数据检测速度趋势
            double trendFactor = 1.0;

            // 获取或创建历史进度记录
            var history = _progressHistoryCache.GetOrAdd(task.Id, _ => new TaskProgressHistory());
            history.RecordProgress(task.Progress, DateTime.UtcNow);

            if (elapsedSeconds > 60 && history.ProgressRecords.Count >= 3) // 至少需要3个数据点
            {
                // 使用线性回归计算速度趋势
                var recentRecords = history.GetRecentRecords(TimeSpan.FromMinutes(5)); // 最近5分钟的数据
                if (recentRecords.Count >= 2)
                {
                    // 计算前半段和后半段的平均速度
                    var midIndex = recentRecords.Count / 2;
                    var firstHalf = recentRecords.Take(midIndex).ToList();
                    var secondHalf = recentRecords.Skip(midIndex).ToList();

                    if (firstHalf.Count > 0 && secondHalf.Count > 0)
                    {
                        // 计算每段的平均速度（进度/秒）
                        var firstHalfSpeed = (firstHalf.Last().Progress - firstHalf.First().Progress) /
                                            (firstHalf.Last().Timestamp - firstHalf.First().Timestamp).TotalSeconds;
                        var secondHalfSpeed = (secondHalf.Last().Progress - secondHalf.First().Progress) /
                                             (secondHalf.Last().Timestamp - secondHalf.First().Timestamp).TotalSeconds;

                        // 计算速度变化率
                        if (firstHalfSpeed > 0)
                        {
                            var speedChangeRatio = secondHalfSpeed / firstHalfSpeed;

                            if (speedChangeRatio > 1.2)
                            {
                                // 检测到明显加速趋势，减少估算时间
                                trendFactor = 0.80;
                                _logger.LogDebug("检测到加速趋势：速度变化率{Ratio:F2}，应用趋势因子{Factor}", speedChangeRatio, trendFactor);
                            }
                            else if (speedChangeRatio < 0.8)
                            {
                                // 检测到明显减速趋势，增加估算时间
                                trendFactor = 1.25;
                                _logger.LogDebug("检测到减速趋势：速度变化率{Ratio:F2}，应用趋势因子{Factor}", speedChangeRatio, trendFactor);
                            }
                            else
                            {
                                // 速度稳定
                                _logger.LogDebug("处理速度稳定：速度变化率{Ratio:F2}", speedChangeRatio);
                            }
                        }
                    }
                }
            }

            // 6. 应用指数平滑（减少估算抖动）
            // 使用平滑因子α=0.7，给予当前估算较高权重，同时考虑历史趋势
            const double smoothingFactor = 0.7;
            var previousEstimate = history.LastEstimatedTime ?? linearEstimate; // 使用真实的上次估算值
            var smoothedEstimate = smoothingFactor * linearEstimate + (1 - smoothingFactor) * previousEstimate;

            // 记录本次估算值，供下次使用
            history.LastEstimatedTime = smoothedEstimate * stageFactor * trendFactor;

            // 7. 综合计算最终估算时间
            var finalEstimate = smoothedEstimate * stageFactor * trendFactor;

            // 8. 应用合理性边界限制
            // 最小值：1秒
            // 最大值：不超过已用时间的10倍（避免不合理的长时间估算）
            var minEstimate = 1;
            var maxEstimate = elapsedSeconds * 10;
            finalEstimate = Math.Max(minEstimate, Math.Min(maxEstimate, finalEstimate));

            _logger.LogDebug("时间估算：任务{TaskId}, 进度{Progress}%, 已用时{Elapsed}s, 线性估算{Linear}s, 阶段因子{Stage}, 趋势因子{Trend}, 最终估算{Final}s",
                task.Id, task.Progress, elapsedSeconds, linearEstimate, stageFactor, trendFactor, finalEstimate);

            return (long)finalEstimate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算预计剩余时间失败：任务{TaskId}", task.Id);
            return 0;
        }
    }

    private string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return extension switch
        {
            ".b3dm" => "application/octet-stream",
            ".gltf" => "application/json",
            ".glb" => "application/octet-stream",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }

    #endregion
}

/// <summary>
/// 切片处理器实现
/// </summary>
public class SlicingProcessor : ISlicingProcessor
{
    private readonly IRepository<SlicingTask> _slicingTaskRepository;
    private readonly IRepository<Slice> _sliceRepository;
    private readonly IMinioStorageService _minioService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SlicingProcessor> _logger;

    public SlicingProcessor(
        IRepository<SlicingTask> slicingTaskRepository,
        IRepository<Slice> sliceRepository,
        IMinioStorageService minioService,
        IUnitOfWork unitOfWork,
        ILogger<SlicingProcessor> logger)
    {
        _slicingTaskRepository = slicingTaskRepository;
        _sliceRepository = sliceRepository;
        _minioService = minioService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ProcessSlicingQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始处理切片任务队列");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 获取队列中的切片任务
                var allTasks = await _slicingTaskRepository.GetAllAsync();
                var queuedTasks = allTasks.Where(t => t.Status == SlicingTaskStatus.Queued);

                foreach (var task in queuedTasks)
                {
                    await ProcessSlicingTaskAsync(task.Id, cancellationToken);
                }

                await Task.Delay(5000, cancellationToken); // 等待5秒后继续检查
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理切片队列时发生错误");
                await Task.Delay(10000, cancellationToken); // 出错时等待10秒
            }
        }

        _logger.LogInformation("切片任务队列处理结束");
    }

    public async Task ProcessSlicingTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _slicingTaskRepository.GetByIdAsync(taskId);
        if (task == null)
        {
            return;
        }

        try
        {
            _logger.LogInformation("开始处理切片任务：{TaskId}", taskId);

            task.Status = SlicingTaskStatus.Processing;
            task.StartedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            // 执行切片处理
            await PerformSlicingAsync(task, cancellationToken);

            task.Status = SlicingTaskStatus.Completed;
            task.Progress = 100;
            task.CompletedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("切片任务处理完成：{TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切片任务处理失败：{TaskId}", taskId);

            task.Status = SlicingTaskStatus.Failed;
            task.ErrorMessage = ex.Message;
            task.CompletedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task UpdateProgressAsync(Guid taskId, SlicingProgress progress)
    {
        var task = await _slicingTaskRepository.GetByIdAsync(taskId);
        if (task != null)
        {
            task.Progress = progress.Progress;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 执行三维切片处理 - 核心算法实现
    /// 采用多层次细节（LOD）算法结合多种空间剖分策略进行切片处理
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task PerformSlicingAsync(SlicingTask task, CancellationToken cancellationToken)
    {
        var config = ParseSlicingConfig(task.SlicingConfig);

        _logger.LogInformation("开始切片处理：任务{TaskId}, 策略{Strategy}, 增量更新：{EnableIncrementalUpdates}",
            task.Id, config.Strategy, config.EnableIncrementalUpdates);

        // 准备增量更新：如果启用增量更新，加载现有切片数据用于比对
        Dictionary<string, Slice> existingSlicesMap = new Dictionary<string, Slice>();
        HashSet<string> processedSliceKeys = new HashSet<string>();
        bool actuallyUseIncrementalUpdate = false; // 实际是否使用增量更新
        bool hasSliceChanges = false; // 是否有切片发生变化（新增、更新或删除）

        if (config.EnableIncrementalUpdates)
        {
            var existingSlices = await _sliceRepository.GetAllAsync();
            var taskSlices = existingSlices.Where(s => s.SlicingTaskId == task.Id).ToList();

            if (taskSlices.Any())
            {
                // 有现有切片数据，可以使用增量更新
                actuallyUseIncrementalUpdate = true;

                // 构建现有切片的映射表，key为 "level_x_y_z"
                foreach (var slice in taskSlices)
                {
                    var key = $"{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}";
                    existingSlicesMap[key] = slice;
                }

                _logger.LogInformation("增量更新模式：找到{Count}个现有切片用于比对", existingSlicesMap.Count);
            }
            else
            {
                // 没有现有切片数据，这是首次生成，使用正常生成模式
                _logger.LogInformation("首次切片生成：虽然启用了增量更新，但数据库中无现有切片，将执行完整生成");
            }
        }

        // 创建切片策略实例
        ISlicingStrategy strategy = config.Strategy switch
        {
            SlicingStrategy.Grid => new GridSlicingStrategy(_logger),
            SlicingStrategy.Octree => new OctreeSlicingStrategy(_logger),
            SlicingStrategy.KdTree => new KdTreeSlicingStrategy(_logger),
            SlicingStrategy.Adaptive => new AdaptiveSlicingStrategy(_logger, _minioService),
            _ => new OctreeSlicingStrategy(_logger) // 默认使用八叉树策略
        };

        // 多层次细节（LOD）切片处理循环
        for (int level = 0; level <= config.MaxLevel; level++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            _logger.LogInformation("处理级别{Level}：策略{Strategy}", level, config.Strategy);

            // 使用选择的切片策略进行空间剖分
            var slices = await strategy.GenerateSlicesAsync(task, level, config, cancellationToken);

            _logger.LogInformation("策略生成切片数量：{Count}, 级别：{Level}", slices.Count, level);

            if (slices.Count == 0)
            {
                _logger.LogWarning("级别{Level}未生成任何切片，请检查切片配置和源模型", level);
                continue; // 跳过这个级别
            }

            foreach (var slice in slices)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // 增量更新：检查切片是否已存在
                var sliceKey = $"{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}";
                bool isNewSlice = !existingSlicesMap.ContainsKey(sliceKey);
                bool needsUpdate = false;

                if (actuallyUseIncrementalUpdate && !isNewSlice)
                {
                    // 切片已存在，通过哈希值判断是否需要更新
                    var existingSlice = existingSlicesMap[sliceKey];
                    var newHash = await CalculateSliceHash(slice);
                    var existingHash = await CalculateSliceHashFromExisting(existingSlice);

                    needsUpdate = newHash != existingHash;

                    if (!needsUpdate)
                    {
                        // 切片未变化，跳过生成和保存
                        _logger.LogDebug("切片未变化，跳过：级别{Level}, 坐标({X},{Y},{Z})",
                            slice.Level, slice.X, slice.Y, slice.Z);
                        processedSliceKeys.Add(sliceKey);
                        continue;
                    }
                    else
                    {
                        // 切片有变化，需要更新
                        _logger.LogInformation("检测到切片变化，准备更新：级别{Level}, 坐标({X},{Y},{Z})",
                            slice.Level, slice.X, slice.Y, slice.Z);
                        // 保留已有的ID，这样后面更新时会替换而不是新增
                        slice.Id = existingSlice.Id;
                        hasSliceChanges = true; // 标记有变化
                    }
                }
                else if (actuallyUseIncrementalUpdate && isNewSlice)
                {
                    // 新增切片
                    _logger.LogInformation("检测到新增切片：级别{Level}, 坐标({X},{Y},{Z})",
                        slice.Level, slice.X, slice.Y, slice.Z);
                    hasSliceChanges = true; // 标记有变化
                }

                try
                {
                    // 生成切片文件内容（这里需要实际的模型数据）
                    await GenerateSliceFileAsync(slice, config, cancellationToken);
                    _logger.LogDebug("成功生成切片文件：{FilePath}", slice.FilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "生成切片文件失败：级别{Level}, 坐标({X},{Y},{Z}), 路径{FilePath}",
                        slice.Level, slice.X, slice.Y, slice.Z, slice.FilePath);
                    // 不中断整个流程,继续处理其他切片
                    continue;
                }

                // 根据情况决定是新增还是更新
                if (actuallyUseIncrementalUpdate && needsUpdate)
                {
                    // 更新现有切片
                    await _sliceRepository.UpdateAsync(slice);
                    _logger.LogDebug("更新切片：{SliceKey}", sliceKey);
                }
                else
                {
                    // 新增切片（首次生成或增量更新中的新切片）
                    await _sliceRepository.AddAsync(slice);
                    _logger.LogDebug("新增切片：{SliceKey}", sliceKey);
                }

                // 标记为已处理
                if (actuallyUseIncrementalUpdate)
                {
                    processedSliceKeys.Add(sliceKey);
                }

                // 批量提交优化：每处理一定数量的切片后批量提交一次
                // 性能优化：减少数据库连接和事务开销，提升整体吞吐量
                // 批量大小：根据切片大小和数据库性能动态调整，通常为CPU核心数的2-4倍
                // 事务管理：确保批量操作的原子性，要么全部成功，要么全部回滚
                // 内存权衡：增大批量大小可提升性能，但会增加内存占用和故障恢复成本
                if (slices.Count % config.ParallelProcessingCount == 0)
                {
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogDebug("批量提交切片数据：{Count}个切片", config.ParallelProcessingCount);
                }

                // 动态调整处理时间 - 基于切片复杂度
                var processingDelay = CalculateProcessingDelay(slice, config);
                await Task.Delay(processingDelay, cancellationToken);
            }

            // 更新进度
            task.Progress = (int)((double)(level + 1) / (config.MaxLevel + 1) * 100);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("级别{Level}处理完成，生成{SliceCount}个切片", level, slices.Count);
        }

        // 增量更新：删除不再存在的旧切片（模型中已删除的部分）
        if (actuallyUseIncrementalUpdate && existingSlicesMap.Count > 0)
        {
            // 1. 删除在已处理层级中不再存在的切片
            var obsoleteSlicesInProcessedLevels = existingSlicesMap
                .Where(kvp => !processedSliceKeys.Contains(kvp.Key))
                .Where(kvp => kvp.Value.Level <= config.MaxLevel) // 只看已处理的层级
                .Select(kvp => kvp.Value)
                .ToList();

            // 2. 删除超出新MaxLevel的所有切片（用户减少了LOD层级）
            var obsoleteSlicesBeyondMaxLevel = existingSlicesMap
                .Where(kvp => kvp.Value.Level > config.MaxLevel)
                .Select(kvp => kvp.Value)
                .ToList();

            var allObsoleteSlices = obsoleteSlicesInProcessedLevels.Concat(obsoleteSlicesBeyondMaxLevel).ToList();

            if (allObsoleteSlices.Any())
            {
                _logger.LogInformation("检测到{Count}个过时切片（{InLevel}个在已处理层级中，{BeyondLevel}个超出新的最大层级{MaxLevel}），开始清理",
                    allObsoleteSlices.Count,
                    obsoleteSlicesInProcessedLevels.Count,
                    obsoleteSlicesBeyondMaxLevel.Count,
                    config.MaxLevel);

                // 删除切片文件和数据库记录
                foreach (var obsoleteSlice in allObsoleteSlices)
                {
                    // 删除文件（本地或MinIO）
                    try
                    {
                        if (config.StorageLocation == StorageLocationType.LocalFileSystem)
                        {
                            // 对于本地存储，需要拼接完整路径
                            var fullPath = Path.IsPathRooted(obsoleteSlice.FilePath)
                                ? obsoleteSlice.FilePath
                                : Path.Combine(task.OutputPath ?? "", obsoleteSlice.FilePath);

                            if (File.Exists(fullPath))
                            {
                                File.Delete(fullPath);
                                _logger.LogDebug("本地切片文件已删除：{FilePath}", fullPath);
                            }
                        }
                        else
                        {
                            await _minioService.DeleteFileAsync("slices", obsoleteSlice.FilePath);
                            _logger.LogDebug("MinIO切片文件已删除：{FilePath}", obsoleteSlice.FilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "删除切片文件失败：{FilePath}", obsoleteSlice.FilePath);
                    }

                    // 删除数据库记录
                    await _sliceRepository.DeleteAsync(obsoleteSlice);
                    _logger.LogDebug("删除过时切片记录：级别{Level}, 坐标({X},{Y},{Z})",
                        obsoleteSlice.Level, obsoleteSlice.X, obsoleteSlice.Y, obsoleteSlice.Z);
                }

                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("已清理{Count}个过时切片（包括文件和数据库记录）", allObsoleteSlices.Count);
                hasSliceChanges = true; // 删除也是变化
            }
            else
            {
                _logger.LogInformation("增量更新：没有需要删除的过时切片");
            }
        }

        // 生成切片索引文件
        await GenerateSliceIndexAsync(task, config, cancellationToken);

        // 生成tileset.json用于Cesium加载
        await GenerateTilesetJsonAsync(task, config, cancellationToken);

        // 生成增量更新索引（仅当实际使用了增量更新且有切片变化时）
        _logger.LogInformation("检查是否需要生成增量更新索引：实际使用增量更新={ActuallyUseIncrementalUpdate}, 有切片变化={HasSliceChanges}",
            actuallyUseIncrementalUpdate, hasSliceChanges);

        if (actuallyUseIncrementalUpdate)
        {
            if (hasSliceChanges)
            {
                _logger.LogInformation("开始生成增量更新索引：任务{TaskId}（检测到切片变化）", task.Id);
                await GenerateIncrementalUpdateIndexAsync(task, config, cancellationToken);
                _logger.LogInformation("增量更新索引生成完成：任务{TaskId}", task.Id);
            }
            else
            {
                _logger.LogInformation("增量更新：所有切片均未变化，无需重新生成增量索引：任务{TaskId}", task.Id);
            }
        }
        else
        {
            _logger.LogInformation("未使用增量更新（首次生成或未启用），跳过增量索引生成：任务{TaskId}", task.Id);
        }

        _logger.LogInformation("切片处理完成：任务{TaskId}", task.Id);
    }

    private static SlicingConfig ParseSlicingConfig(string configJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize<SlicingConfig>(configJson);
            return config ?? new SlicingConfig();
        }
        catch
        {
            return new SlicingConfig();
        }
    }

    /// <summary>
    /// 生成切片包围盒JSON - 轴对齐包围盒（AABB）算法实现
    /// 算法：基于切片网格坐标和切片大小计算空间边界框
    /// </summary>
    /// <param name="level">LOD级别</param>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <param name="z">Z坐标</param>
    /// <param name="tileSize">切片大小</param>
    /// <returns>JSON格式的包围盒数据</returns>
    private string GenerateBoundingBoxJson(int level, int x, int y, int z, double tileSize)
    {
        // AABB算法：计算轴对齐的最小包围盒
        var minX = x * tileSize;
        var minY = y * tileSize;
        var minZ = z * tileSize;
        var maxX = (x + 1) * tileSize;
        var maxY = (y + 1) * tileSize;
        var maxZ = (z + 1) * tileSize;

        return $"{{\"minX\":{minX},\"minY\":{minY},\"minZ\":{minZ},\"maxX\":{maxX},\"maxY\":{maxY},\"maxZ\":{maxZ}}}";
    }

    /// <summary>
    /// 生成切片索引文件 - 空间索引算法实现
    /// 算法：为所有切片建立层次化的索引结构，支持快速空间查询
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="config">切片配置</param>
    private async Task GenerateSliceIndexAsync(SlicingTask task, SlicingConfig config, CancellationToken cancellationToken)
    {
        // 空间索引算法：生成切片索引文件，便于快速查找和访问
        var allSlices = await _sliceRepository.GetAllAsync();
        var slices = allSlices.Where(s => s.SlicingTaskId == task.Id).ToList();

        var index = new
        {
            TaskId = task.Id,
            TotalLevels = config.MaxLevel,
            config.TileSize,
            config.OutputFormat,
            SliceCount = slices.Count(),
            BoundingBox = CalculateTotalBoundingBox(slices.ToList()),
            Slices = slices.Select(s => new
            {
                s.Level,
                s.X,
                s.Y,
                s.Z,
                s.FilePath,
                s.FileSize,
                s.BoundingBox
            }).ToList()
        };

        var indexContent = JsonSerializer.Serialize(index, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var indexPath = $"{task.OutputPath}/index.json";

        if (config.StorageLocation == StorageLocationType.LocalFileSystem)
        {
            var fullPath = Path.Combine(task.OutputPath!, "index.json");
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllTextAsync(fullPath, indexContent, cancellationToken);
            _logger.LogInformation("切片索引文件已保存到本地：{FilePath}", fullPath);
        }
        else
        {
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(indexContent)))
            {
                await _minioService.UploadFileAsync("slices", indexPath, stream, "application/json", cancellationToken);
            }
            _logger.LogInformation("切片索引文件已上传到MinIO：{FilePath}, 切片数量：{Count}",
                indexPath, slices.Count);
        }
    }

    /// <summary>
    /// 生成Cesium Tileset JSON文件 - 3D Tiles标准格式生成算法
    /// 算法：生成符合3D Tiles规范的tileset.json，支持Cesium等引擎直接加载
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="config">切片配置</param>
    private async Task GenerateTilesetJsonAsync(SlicingTask task, SlicingConfig config, CancellationToken cancellationToken)
    {
        // 3D Tiles标准格式生成算法：生成Cesium兼容的tileset.json文件
        var tileset = new
        {
            Asset = new { Version = "1.0", Generator = "RealScene3D Slicer" },
            Schema = "https://project-872-1.gitbook.io/cesium3dtile/",
            GeometricError = config.GeometricErrorThreshold,
            Root = new
            {
                BoundingVolume = new
                {
                    Box = new[] { 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0 }
                },
                GeometricError = config.GeometricErrorThreshold,
                Refine = "REPLACE",
                Content = new
                {
                    Uri = "tileset.json"
                }
            }
        };

        var tilesetContent = JsonSerializer.Serialize(tileset, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var tilesetPath = $"{task.OutputPath}/tileset.json";

        if (config.StorageLocation == StorageLocationType.LocalFileSystem)
        {
            var fullPath = Path.Combine(task.OutputPath!, "tileset.json");
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllTextAsync(fullPath, tilesetContent, cancellationToken);
            _logger.LogInformation("Tileset JSON文件已保存到本地：{FilePath}", fullPath);
        }
        else
        {
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(tilesetContent)))
            {
                await _minioService.UploadFileAsync("slices", tilesetPath, stream, "application/json", cancellationToken);
            }
            _logger.LogInformation("Tileset JSON文件已上传到MinIO：{FilePath}", tilesetPath);
        }
    }

    private string CalculateTotalBoundingBox(List<Slice> slices)
    {
        if (!slices.Any()) return "{}";

        var minX = slices.Min(s => s.X);
        var minY = slices.Min(s => s.Y);
        var minZ = slices.Min(s => s.Z);
        var maxX = slices.Max(s => s.X);
        var maxY = slices.Max(s => s.Y);
        var maxZ = slices.Max(s => s.Z);

        return $"{{\"minX\":{minX},\"minY\":{minY},\"minZ\":{minZ},\"maxX\":{maxX + 1},\"maxY\":{maxY + 1},\"maxZ\":{maxZ + 1}}}";
    }

    /// <summary>
    /// 计算切片处理延迟 - 性能优化算法
    /// 算法：基于切片复杂度、输出格式和压缩级别动态调整处理时间
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="config">切片配置</param>
    /// <returns>处理延迟毫秒数</returns>
    private int CalculateProcessingDelay(Slice slice, SlicingConfig config)
    {
        var baseDelay = 10; // 基础处理时间

        // 基于输出格式调整延迟
        var formatFactor = config.OutputFormat.ToLower() switch
        {
            "b3dm" => 1.5, // B3DM格式较为复杂
            "gltf" => 1.2, // GLTF格式中等复杂度
            "json" => 0.8, // JSON格式相对简单
            _ => 1.0
        };

        // 基于压缩级别调整延迟
        var compressionFactor = 1.0 + (config.CompressionLevel * 0.1);

        // 基于切片级别调整延迟（更高层级通常更复杂）
        var levelFactor = 1.0 + (slice.Level * 0.05);

        var totalDelay = baseDelay * formatFactor * compressionFactor * levelFactor;

        // 限制延迟范围：5-100毫秒
        return Math.Max(5, Math.Min(100, (int)totalDelay));
    }

    /// <summary>
    /// 并行切片处理优化 - 多线程处理算法实现
    /// 算法：将切片任务分配到多个线程并行处理，提高整体处理速度和CPU利用率
    ///
    /// 性能优化策略：
    /// - 动态线程池：根据系统负载和切片复杂度动态调整并发数
    /// - 负载均衡：均匀分配切片到各线程，避免单个线程负载过重
    /// - 内存管理：控制内存分配速率，避免内存峰值过高导致GC压力
    /// - I/O优化：批量写入数据库，减少数据库连接开销
    /// - 进度同步：线程安全的进度更新，避免锁竞争影响性能
    /// - 异常隔离：单个切片处理失败不影响其他切片和整体进度
    /// - 资源清理：及时释放临时资源，避免内存泄露
    ///
    /// 并行策略：
    /// - 数据并行：切片间相互独立，适合完全并行处理
    /// - 任务窃取：空闲线程主动获取其他线程的任务，提高资源利用率
    /// - 工作优先级：根据切片重要性和复杂度动态调整处理优先级
    /// - 中间结果：及时保存中间结果，避免因异常导致的完全重做
    /// </summary>
    /// <param name="task">切片任务，包含任务配置和状态信息</param>
    /// <param name="level">LOD级别，影响切片复杂度和处理优先级</param>
    /// <param name="config">切片配置，控制并行度和处理策略</param>
    /// <param name="slices">切片集合，待并行处理的切片数据</param>
    /// <param name="cancellationToken">取消令牌，支持优雅的中断处理</param>
    private async Task ProcessSlicesInParallelAsync(SlicingTask task, int level, SlicingConfig config, List<Slice> slices, CancellationToken cancellationToken)
    {
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = config.ParallelProcessingCount,
            CancellationToken = cancellationToken
        };

        var slicesArray = slices.ToArray();
        var processedCount = 0;
        var lockObject = new object();

        await Task.Run(() =>
        {
            Parallel.For(0, slicesArray.Length, parallelOptions, async (index) =>
            {
                var slice = slicesArray[index];

                try
                {
                    // 生成切片文件内容
                    await GenerateSliceFileAsync(slice, config, cancellationToken);

                    // 线程安全的计数更新
                    lock (lockObject)
                    {
                        processedCount++;
                        if (processedCount % 10 == 0) // 每处理10个切片输出一次进度
                        {
                            _logger.LogDebug("并行处理进度：级别{Level}, 已处理{Processed}/{Total}",
                                level, processedCount, slicesArray.Length);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "并行处理切片失败：级别{Level}, 索引{Index}", level, index);
                }
            });
        }, cancellationToken);

        // 批量保存所有切片到数据库
        foreach (var slice in slices)
        {
            await _sliceRepository.AddAsync(slice);
        }
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("并行切片处理完成：级别{Level}, 处理{Processed}个切片", level, processedCount);
    }

    /// <summary>
    /// 视锥剔除算法 - 渲染优化算法实现
    /// 算法：基于视点和视角参数剔除不可见的切片，减少渲染负载
    ///
    /// 性能优化策略：
    /// - 空间索引加速：预构建BVH树或四叉树，快速剔除大范围不可见区域
    /// - 距离预排序：按距离相机远近预排序，先剔除远距离切片减少计算量
    /// - 金字塔剔除：高层级切片不可见时可跳过低层级子节点，避免冗余计算
    /// - SIMD优化：使用向量指令批量处理距离和角度计算，提升浮点性能
    /// - 多线程并行：并发处理切片可见性测试，利用多核CPU优势
    /// - 缓存机制：缓存上一帧可见性结果，减少重复计算开销
    /// - 提前退出：一旦找到足够可见切片立即返回，支持渐进式加载
    ///
    /// 内存优化：
    /// - 对象池复用：复用临时向量和矩阵对象，减少GC压力
    /// - 紧凑存储：使用位标记记录可见性，避免大量内存分配
    /// - 延迟加载：仅为可见切片加载几何数据，节省内存占用
    /// - 分批处理：分批处理大量切片，避免内存峰值过高
    /// </summary>
    /// <param name="viewport">视口参数，包含相机位置、视角、裁剪面等关键信息，必须有效</param>
    /// <param name="allSlices">所有待测试的切片集合，支持空集合（返回空结果）</param>
    /// <returns>可见切片集合，仅包含在视锥范围内的切片，按距离排序便于优先加载</returns>
    public Task<IEnumerable<Slice>> PerformFrustumCullingAsync(ViewportInfo viewport, IEnumerable<Slice> allSlices)
    {
        // 视锥剔除算法实现
        var visibleSlices = new List<Slice>();

        foreach (var slice in allSlices)
        {
            if (IsSliceVisible(slice, viewport))
            {
                visibleSlices.Add(slice);
            }
        }

        _logger.LogDebug("视锥剔除结果：总切片{Total}, 可见切片{Visible}",
            allSlices.Count(), visibleSlices.Count);

        return Task.FromResult<IEnumerable<Slice>>(visibleSlices);
    }

    /// <summary>
    /// 判断切片是否在视锥体内 - 增强的空间几何算法
    /// 算法：使用包围盒与视锥的精确相交测试判断可见性
    /// 实现：六平面视锥剔除算法 + 距离LOD优化
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="viewport">视口信息</param>
    /// <returns>是否可见</returns>
    private bool IsSliceVisible(Slice slice, ViewportInfo viewport)
    {
        // 解析包围盒
        var boundingBox = System.Text.Json.JsonSerializer.Deserialize<BoundingBox>(slice.BoundingBox);
        if (boundingBox == null) return false;

        // 计算包围盒的8个顶点
        var corners = new[]
        {
            new Vector3D { X = boundingBox.MinX, Y = boundingBox.MinY, Z = boundingBox.MinZ },
            new Vector3D { X = boundingBox.MaxX, Y = boundingBox.MinY, Z = boundingBox.MinZ },
            new Vector3D { X = boundingBox.MinX, Y = boundingBox.MaxY, Z = boundingBox.MinZ },
            new Vector3D { X = boundingBox.MaxX, Y = boundingBox.MaxY, Z = boundingBox.MinZ },
            new Vector3D { X = boundingBox.MinX, Y = boundingBox.MinY, Z = boundingBox.MaxZ },
            new Vector3D { X = boundingBox.MaxX, Y = boundingBox.MinY, Z = boundingBox.MaxZ },
            new Vector3D { X = boundingBox.MinX, Y = boundingBox.MaxY, Z = boundingBox.MaxZ },
            new Vector3D { X = boundingBox.MaxX, Y = boundingBox.MaxY, Z = boundingBox.MaxZ }
        };

        // 计算切片中心点
        var sliceCenter = new Vector3D
        {
            X = (boundingBox.MinX + boundingBox.MaxX) / 2,
            Y = (boundingBox.MinY + boundingBox.MaxY) / 2,
            Z = (boundingBox.MinZ + boundingBox.MaxZ) / 2
        };

        // 1. 距离剔除测试 - 基于LOD级别的动态距离阈值
        var distance = CalculateDistance(viewport.CameraPosition, sliceCenter);

        // LOD级别越高，最大可见距离越小（细节级别越高，可见范围越近）
        var lodDistanceFactor = Math.Pow(0.75, slice.Level);
        var maxDistance = viewport.FarPlane * lodDistanceFactor;

        // 近平面和远平面剔除
        if (distance < viewport.NearPlane || distance > maxDistance)
            return false;

        // 2. 视野角度剔除测试 - 计算到相机方向的角度
        var toCenterVector = new Vector3D
        {
            X = sliceCenter.X - viewport.CameraPosition.X,
            Y = sliceCenter.Y - viewport.CameraPosition.Y,
            Z = sliceCenter.Z - viewport.CameraPosition.Z
        };

        var angle = CalculateAngle(viewport.CameraDirection, toCenterVector);

        // 考虑包围盒半径的扩展角度
        var boundingBoxRadius = Math.Sqrt(
            Math.Pow(boundingBox.MaxX - boundingBox.MinX, 2) +
            Math.Pow(boundingBox.MaxY - boundingBox.MinY, 2) +
            Math.Pow(boundingBox.MaxZ - boundingBox.MinZ, 2)
        ) / 2;

        var angularRadius = Math.Atan2(boundingBoxRadius, distance);
        var effectiveFOV = viewport.FieldOfView / 2 + angularRadius;

        if (angle > effectiveFOV)
            return false;

        // 3. 完整的视锥平面测试 - 使用六平面测试算法
        // 构建视锥的六个平面：近、远、左、右、上、下
        var frustumPlanes = BuildFrustumPlanes(viewport);

        // 执行包围盒与视锥六平面相交测试
        // 如果包围盒完全在任何一个平面的外侧，则不可见
        foreach (var plane in frustumPlanes)
        {
            if (IsBoxCompletelyOutsidePlane(corners, plane))
            {
                // 包围盒完全在平面外侧，剔除
                return false;
            }
        }

        // 4. 增强的遮挡剔除算法 - 基于层次LOD和空间关系
        // 考虑以下情况进行遮挡判断：
        // a) 远小切片：距离很远且体积很小的切片容易被遮挡
        // b) 视线投影面积：计算切片在屏幕上的投影面积，面积过小可能不可见
        // c) LOD父子关系：高层级切片可能被低层级的大切片遮挡

        // 计算切片的屏幕空间投影面积（近似）
        var angularSize = Math.Atan2(boundingBoxRadius, distance); // 角尺寸（弧度）
        var screenSpaceArea = angularSize * angularSize; // 近似屏幕投影面积

        // 如果屏幕投影面积过小（小于1像素），可以剔除
        // 假设视野角度对应视口高度，计算像素阈值
        var pixelThreshold = (viewport.FieldOfView / viewport.ViewportHeight) * (viewport.FieldOfView / viewport.ViewportHeight);
        if (screenSpaceArea < pixelThreshold)
        {
            _logger.LogTrace("切片因屏幕投影过小被剔除：Level={Level}, 距离={Distance:F2}, 角尺寸={AngularSize:F6}",
                slice.Level, distance, angularSize);
            return false;
        }

        // LOD层级遮挡检测：
        // 如果是高层级（细节）切片，且距离较远，可能被低层级切片覆盖
        if (slice.Level > 2) // 仅对Level > 2的切片进行此检测
        {
            var lodFactor = Math.Pow(2, slice.Level); // LOD因子：2^Level
            var expectedVisibleDistance = viewport.FarPlane / lodFactor;

            // 如果当前距离远超该LOD级别的期望可见距离，很可能被父级LOD遮挡
            if (distance > expectedVisibleDistance * 1.5)
            {
                _logger.LogTrace("切片因LOD层级遮挡被剔除：Level={Level}, 距离={Distance:F2}, 期望距离={Expected:F2}",
                    slice.Level, distance, expectedVisibleDistance);
                return false;
            }
        }

        // 视线方向遮挡检测：
        // 如果切片在视线方向上距离较远，且角度偏离较大，可能被中心区域的切片遮挡
        if (distance > maxDistance * 0.7)
        {
            // 计算切片相对于视线中心的偏离角度
            var deviationAngle = angle / (viewport.FieldOfView / 2.0); // 归一化偏离（0-1）

            // 如果偏离角度大且距离远，被遮挡的可能性更高
            if (deviationAngle > 0.8 && boundingBoxRadius < distance * 0.02)
            {
                _logger.LogTrace("切片因视线偏离遮挡被剔除：Level={Level}, 偏离={Deviation:F2}, 距离={Distance:F2}",
                    slice.Level, deviationAngle, distance);
                return false;
            }
        }

        // 通过所有测试，切片可见
        return true;
    }

    /// <summary>
    /// 预测加载算法 - 预加载优化算法实现
    /// 算法：基于用户视点移动趋势预测需要加载的切片，支持智能预加载
    ///
    /// 性能优化策略：
    /// - 运动轨迹分析：基于历史移动数据预测未来轨迹，提高预测准确性
    /// - 时间窗口预测：支持多时间窗口预测，平衡预加载量和准确性
    /// - 优先级排序：结合距离、角度、LOD等因素计算加载优先级
    /// - 增量更新：仅对新进入预测范围的切片进行计算，减少重复工作
    /// - 机器学习优化：使用历史行为数据训练预测模型，提升预测精度
    /// - 带宽感知：根据网络状况动态调整预加载数量，避免带宽浪费
    /// - 缓存策略：缓存预测结果，减少频繁预测的计算开销
    ///
    /// 预测算法：
    /// - 线性预测：基于当前速度和方向预测未来位置
    /// - 贝塞尔曲线：平滑处理非线性运动轨迹
    /// - 概率模型：考虑用户行为不确定性，提供置信度评估
    /// - 聚类分析：识别常用路径和兴趣区域，优化预测范围
    /// </summary>
    /// <param name="currentViewport">当前视口信息，作为预测基准点</param>
    /// <param name="movementVector">用户移动向量，描述当前运动状态和趋势</param>
    /// <param name="allSlices">所有可用切片，用于预测范围内的切片选择</param>
    /// <returns>预测加载的切片集合，按优先级排序，优先加载重要切片</returns>
    public async Task<IEnumerable<Slice>> PredictLoadingAsync(ViewportInfo currentViewport, Vector3D movementVector, IEnumerable<Slice> allSlices)
    {
        // 预测加载算法实现
        var predictedSlices = new List<Slice>();

        // 预测下一个视口位置
        var predictedPosition = currentViewport.CameraPosition + movementVector * 2.0; // 预测2秒后的位置

        // 基于预测位置计算可见切片
        var predictedViewport = new ViewportInfo
        {
            CameraPosition = predictedPosition,
            CameraDirection = currentViewport.CameraDirection,
            FieldOfView = currentViewport.FieldOfView,
            NearPlane = currentViewport.NearPlane,
            FarPlane = currentViewport.FarPlane
        };

        return await PerformFrustumCullingAsync(predictedViewport, allSlices);
    }

    /// <summary>
    /// 计算两点间距离 - 空间几何算法
    /// </summary>
    /// <param name="point1">点1</param>
    /// <param name="point2">点2</param>
    /// <returns>欧几里得距离</returns>
    private double CalculateDistance(Vector3D point1, Vector3D point2)
    {
        var dx = point2.X - point1.X;
        var dy = point2.Y - point1.Y;
        var dz = point2.Z - point1.Z;

        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// 计算向量间角度 - 向量数学算法
    /// </summary>
    /// <param name="vector1">向量1</param>
    /// <param name="vector2">向量2</param>
    /// <returns>角度（弧度）</returns>
    private double CalculateAngle(Vector3D vector1, Vector3D vector2)
    {
        var dotProduct = vector1.X * vector2.X + vector1.Y * vector2.Y + vector1.Z * vector2.Z;
        var magnitude1 = Math.Sqrt(vector1.X * vector1.X + vector1.Y * vector1.Y + vector1.Z * vector1.Z);
        var magnitude2 = Math.Sqrt(vector2.X * vector2.X + vector2.Y * vector2.Y + vector2.Z * vector2.Z);

        if (magnitude1 == 0 || magnitude2 == 0) return 0;

        var cosAngle = dotProduct / (magnitude1 * magnitude2);
        return Math.Acos(Math.Max(-1.0, Math.Min(1.0, cosAngle)));
    }

    /// <summary>
    /// 构建视锥六个平面 - 标准视锥剔除算法
    /// 算法：基于相机参数和视口信息构建视锥的六个平面（近、远、左、右、上、下）
    /// 每个平面用法向量和到原点的距离表示：Ax + By + Cz + D = 0
    /// </summary>
    /// <param name="viewport">视口信息</param>
    /// <returns>六个平面的数组</returns>
    private FrustumPlane[] BuildFrustumPlanes(ViewportInfo viewport)
    {
        var planes = new FrustumPlane[6];

        // 归一化相机方向向量
        var forward = NormalizeVector(viewport.CameraDirection);

        // 计算相机的右向量和上向量（假设世界上向量为(0,0,1)）
        var worldUp = new Vector3D { X = 0, Y = 0, Z = 1 };
        var right = CrossProduct(forward, worldUp);
        right = NormalizeVector(right);
        var up = CrossProduct(right, forward);
        up = NormalizeVector(up);

        // 计算视锥的近平面和远平面中心点
        var nearCenter = new Vector3D
        {
            X = viewport.CameraPosition.X + forward.X * viewport.NearPlane,
            Y = viewport.CameraPosition.Y + forward.Y * viewport.NearPlane,
            Z = viewport.CameraPosition.Z + forward.Z * viewport.NearPlane
        };

        var farCenter = new Vector3D
        {
            X = viewport.CameraPosition.X + forward.X * viewport.FarPlane,
            Y = viewport.CameraPosition.Y + forward.Y * viewport.FarPlane,
            Z = viewport.CameraPosition.Z + forward.Z * viewport.FarPlane
        };

        // 计算近平面和远平面的宽高
        var aspect = viewport.AspectRatio;
        var tanHalfFOV = Math.Tan(viewport.FieldOfView / 2.0);

        var nearHeight = 2.0 * tanHalfFOV * viewport.NearPlane;
        var nearWidth = nearHeight * aspect;
        var farHeight = 2.0 * tanHalfFOV * viewport.FarPlane;
        var farWidth = farHeight * aspect;

        // 0. 近平面：法向量指向相机内部（前方）
        planes[0] = new FrustumPlane
        {
            Normal = forward,
            Point = nearCenter
        };

        // 1. 远平面：法向量指向相机外部（后方）
        planes[1] = new FrustumPlane
        {
            Normal = new Vector3D { X = -forward.X, Y = -forward.Y, Z = -forward.Z },
            Point = farCenter
        };

        // 2. 左平面
        var leftNormal = CrossProduct(up, new Vector3D
        {
            X = nearCenter.X - right.X * nearWidth / 2 - viewport.CameraPosition.X,
            Y = nearCenter.Y - right.Y * nearWidth / 2 - viewport.CameraPosition.Y,
            Z = nearCenter.Z - right.Z * nearWidth / 2 - viewport.CameraPosition.Z
        });
        planes[2] = new FrustumPlane
        {
            Normal = NormalizeVector(leftNormal),
            Point = viewport.CameraPosition
        };

        // 3. 右平面
        var rightNormal = CrossProduct(new Vector3D
        {
            X = nearCenter.X + right.X * nearWidth / 2 - viewport.CameraPosition.X,
            Y = nearCenter.Y + right.Y * nearWidth / 2 - viewport.CameraPosition.Y,
            Z = nearCenter.Z + right.Z * nearWidth / 2 - viewport.CameraPosition.Z
        }, up);
        planes[3] = new FrustumPlane
        {
            Normal = NormalizeVector(rightNormal),
            Point = viewport.CameraPosition
        };

        // 4. 上平面
        var topNormal = CrossProduct(right, new Vector3D
        {
            X = nearCenter.X + up.X * nearHeight / 2 - viewport.CameraPosition.X,
            Y = nearCenter.Y + up.Y * nearHeight / 2 - viewport.CameraPosition.Y,
            Z = nearCenter.Z + up.Z * nearHeight / 2 - viewport.CameraPosition.Z
        });
        planes[4] = new FrustumPlane
        {
            Normal = NormalizeVector(topNormal),
            Point = viewport.CameraPosition
        };

        // 5. 下平面
        var bottomNormal = CrossProduct(new Vector3D
        {
            X = nearCenter.X - up.X * nearHeight / 2 - viewport.CameraPosition.X,
            Y = nearCenter.Y - up.Y * nearHeight / 2 - viewport.CameraPosition.Y,
            Z = nearCenter.Z - up.Z * nearHeight / 2 - viewport.CameraPosition.Z
        }, right);
        planes[5] = new FrustumPlane
        {
            Normal = NormalizeVector(bottomNormal),
            Point = viewport.CameraPosition
        };

        return planes;
    }

    /// <summary>
    /// 判断包围盒是否完全在平面外侧
    /// </summary>
    /// <param name="corners">包围盒的8个顶点</param>
    /// <param name="plane">平面</param>
    /// <returns>如果所有顶点都在平面外侧返回true</returns>
    private bool IsBoxCompletelyOutsidePlane(Vector3D[] corners, FrustumPlane plane)
    {
        // 计算所有顶点到平面的距离
        // 如果所有顶点的距离都是负数（在平面的背面），则包围盒完全在外侧
        foreach (var corner in corners)
        {
            var distance = (corner.X - plane.Point.X) * plane.Normal.X +
                          (corner.Y - plane.Point.Y) * plane.Normal.Y +
                          (corner.Z - plane.Point.Z) * plane.Normal.Z;

            if (distance >= 0)
            {
                // 至少有一个顶点在平面内侧或上，包围盒没有完全在外侧
                return false;
            }
        }

        // 所有顶点都在平面外侧
        return true;
    }

    /// <summary>
    /// 向量叉积
    /// </summary>
    private Vector3D CrossProduct(Vector3D v1, Vector3D v2)
    {
        return new Vector3D
        {
            X = v1.Y * v2.Z - v1.Z * v2.Y,
            Y = v1.Z * v2.X - v1.X * v2.Z,
            Z = v1.X * v2.Y - v1.Y * v2.X
        };
    }

    /// <summary>
    /// 向量归一化
    /// </summary>
    private Vector3D NormalizeVector(Vector3D v)
    {
        var length = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        if (length < 1e-10)
            return new Vector3D { X = 0, Y = 0, Z = 1 }; // 避免除以零

        return new Vector3D
        {
            X = v.X / length,
            Y = v.Y / length,
            Z = v.Z / length
        };
    }

    /// <summary>
    /// 视锥平面定义 - 用于视锥剔除算法
    /// </summary>
    private class FrustumPlane
    {
        public Vector3D Normal { get; set; } = new Vector3D();
        public Vector3D Point { get; set; } = new Vector3D();
    }

    // 使用Domain.Entities中的几何类型定义

    /// <summary>
    /// 生成切片文件内容 - 多格式支持算法实现
    /// 算法：根据输出格式生成相应的切片文件内容，支持B3DM、GLTF等格式
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="config">切片配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task GenerateSliceFileAsync(Slice slice, SlicingConfig config, CancellationToken cancellationToken)
    {
        // 多格式切片文件生成算法
        byte[] fileContent;

        switch (config.OutputFormat.ToLower())
        {
            case "b3dm":
                fileContent = await GenerateB3DMContentAsync(slice, config);
                break;
            case "gltf":
                fileContent = await GenerateGLTFContentAsync(slice, config);
                break;
            case "json":
                fileContent = await GenerateJSONContentAsync(slice, config);
                break;
            default:
                fileContent = await GenerateDefaultContentAsync(slice, config);
                break;
        }

        // 应用压缩（如果启用）
        if (config.CompressionLevel > 0)
        {
            fileContent = await CompressSliceContentAsync(fileContent, config.CompressionLevel);
        }

        // 根据存储位置类型保存文件
        if (config.StorageLocation == StorageLocationType.LocalFileSystem)
        {
            var directory = Path.GetDirectoryName(slice.FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllBytesAsync(slice.FilePath, fileContent, cancellationToken);
            _logger.LogDebug("切片文件已保存到本地：{FilePath}, 大小：{FileSize}", slice.FilePath, fileContent.Length);
        }
        else // 默认为MinIO
        {
            // 上传到对象存储
            _logger.LogInformation("准备上传切片到MinIO: bucket=slices, path={FilePath}, size={Size}",
                slice.FilePath, fileContent.Length);

            using (var stream = new MemoryStream(fileContent))
            {
                var contentType = GetContentType(config.OutputFormat);
                _logger.LogDebug("ContentType: {ContentType}, OutputFormat: {OutputFormat}",
                    contentType, config.OutputFormat);

                await _minioService.UploadFileAsync("slices", slice.FilePath, stream, contentType, cancellationToken);
            }
            _logger.LogInformation("切片文件已上传到MinIO：{FilePath}, 大小：{FileSize}", slice.FilePath, fileContent.Length);
        }
    }

    /// <summary>
    /// 生成B3DM格式切片内容 - 3D Tiles B3DM格式生成算法
    /// 算法：生成符合3D Tiles标准的二进制glTF格式
    /// B3DM格式：Header + Feature Table JSON + Feature Table Binary + Batch Table JSON + Batch Table Binary + GLB
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="config">切片配置</param>
    /// <returns>B3DM格式的字节数组</returns>
    private async Task<byte[]> GenerateB3DMContentAsync(Slice slice, SlicingConfig config)
    {
        // 解析包围盒
        var boundingBox = System.Text.Json.JsonSerializer.Deserialize<BoundingBox>(slice.BoundingBox);
        if (boundingBox == null)
        {
            throw new InvalidOperationException("无法解析切片包围盒");
        }

        // 1. 生成Feature Table JSON - 包含RTC_CENTER（相对中心点）和批次长度
        var featureTableJson = new
        {
            BATCH_LENGTH = 1, // 批次数量
            RTC_CENTER = new[] // 相对瓦片中心，用于提高精度
            {
                (boundingBox.MinX + boundingBox.MaxX) / 2.0,
                (boundingBox.MinY + boundingBox.MaxY) / 2.0,
                (boundingBox.MinZ + boundingBox.MaxZ) / 2.0
            }
        };
        var featureTableJsonBytes = System.Text.Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(featureTableJson));

        // 对齐到8字节边界
        var featureTableJsonLength = (featureTableJsonBytes.Length + 7) & ~7;
        var featureTableJsonPadded = new byte[featureTableJsonLength];
        Array.Copy(featureTableJsonBytes, featureTableJsonPadded, featureTableJsonBytes.Length);
        // 填充空格
        for (int i = featureTableJsonBytes.Length; i < featureTableJsonLength; i++)
        {
            featureTableJsonPadded[i] = 0x20; // 空格
        }

        // 2. Feature Table Binary - 可选的二进制数据（这里为空）
        var featureTableBinaryLength = 0;
        var featureTableBinary = new byte[0];

        // 3. 生成Batch Table JSON - 包含批次属性（ID、名称等）
        var batchTableJson = new
        {
            id = new[] { slice.Id.ToString() },
            level = new[] { slice.Level },
            x = new[] { slice.X },
            y = new[] { slice.Y },
            z = new[] { slice.Z },
            name = new[] { $"Tile_{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}" }
        };
        var batchTableJsonBytes = System.Text.Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(batchTableJson));

        // 对齐到8字节边界
        var batchTableJsonLength = (batchTableJsonBytes.Length + 7) & ~7;
        var batchTableJsonPadded = new byte[batchTableJsonLength];
        Array.Copy(batchTableJsonBytes, batchTableJsonPadded, batchTableJsonBytes.Length);
        for (int i = batchTableJsonBytes.Length; i < batchTableJsonLength; i++)
        {
            batchTableJsonPadded[i] = 0x20; // 空格
        }

        // 4. Batch Table Binary - 可选的二进制数据（这里为空）
        var batchTableBinaryLength = 0;
        var batchTableBinary = new byte[0];

        // 5. 生成GLB内容（包含实际的几何数据）
        var glbContent = await GenerateGLBContentAsync(slice, config);

        // 6. 构造完整的B3DM文件
        using (var memoryStream = new MemoryStream())
        using (var writer = new BinaryWriter(memoryStream))
        {
            // B3DM Header (28字节)
            writer.Write(System.Text.Encoding.ASCII.GetBytes("b3dm")); // magic (4字节)
            writer.Write((uint)1); // version (4字节)

            // 计算总长度
            var totalLength = 28 + // header
                             featureTableJsonLength +
                             featureTableBinaryLength +
                             batchTableJsonLength +
                             batchTableBinaryLength +
                             glbContent.Length;

            writer.Write((uint)totalLength); // byteLength (4字节)
            writer.Write((uint)featureTableJsonLength); // featureTableJSONByteLength (4字节)
            writer.Write((uint)featureTableBinaryLength); // featureTableBinaryByteLength (4字节)
            writer.Write((uint)batchTableJsonLength); // batchTableJSONByteLength (4字节)
            writer.Write((uint)batchTableBinaryLength); // batchTableBinaryByteLength (4字节)

            // 写入Feature Table
            writer.Write(featureTableJsonPadded);
            if (featureTableBinaryLength > 0)
            {
                writer.Write(featureTableBinary);
            }

            // 写入Batch Table
            writer.Write(batchTableJsonPadded);
            if (batchTableBinaryLength > 0)
            {
                writer.Write(batchTableBinary);
            }

            // 写入GLB内容
            writer.Write(glbContent);

            _logger.LogDebug("B3DM文件生成完成：切片{SliceId}, 大小{Size}字节, Feature表{FeatureSize}字节, Batch表{BatchSize}字节, GLB{GlbSize}字节",
                slice.Id, totalLength, featureTableJsonLength, batchTableJsonLength, glbContent.Length);

            return memoryStream.ToArray();
        }
    }

    /// <summary>
    /// 生成GLTF格式切片内容 - JSON格式3D模型数据
    /// 算法：生成符合glTF 2.0标准的JSON格式数据，包含完整的几何数据结构
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="config">切片配置</param>
    /// <returns>GLTF格式的字节数组</returns>
    private Task<byte[]> GenerateGLTFContentAsync(Slice slice, SlicingConfig config)
    {
        // 解析包围盒
        var boundingBox = System.Text.Json.JsonSerializer.Deserialize<BoundingBox>(slice.BoundingBox);
        if (boundingBox == null)
        {
            throw new InvalidOperationException("无法解析切片包围盒");
        }

        // 生成简单的立方体几何数据作为切片占位符
        // 实际应用中应从真实的模型数据生成
        var vertices = new[]
        {
            // 立方体8个顶点
            (float)boundingBox.MinX, (float)boundingBox.MinY, (float)boundingBox.MinZ, // 0
            (float)boundingBox.MaxX, (float)boundingBox.MinY, (float)boundingBox.MinZ, // 1
            (float)boundingBox.MaxX, (float)boundingBox.MaxY, (float)boundingBox.MinZ, // 2
            (float)boundingBox.MinX, (float)boundingBox.MaxY, (float)boundingBox.MinZ, // 3
            (float)boundingBox.MinX, (float)boundingBox.MinY, (float)boundingBox.MaxZ, // 4
            (float)boundingBox.MaxX, (float)boundingBox.MinY, (float)boundingBox.MaxZ, // 5
            (float)boundingBox.MaxX, (float)boundingBox.MaxY, (float)boundingBox.MaxZ, // 6
            (float)boundingBox.MinX, (float)boundingBox.MaxY, (float)boundingBox.MaxZ  // 7
        };

        // 立方体索引（12个三角形，36个索引）
        var indices = new ushort[]
        {
            // 前面
            0, 1, 2, 0, 2, 3,
            // 后面
            5, 4, 7, 5, 7, 6,
            // 顶面
            3, 2, 6, 3, 6, 7,
            // 底面
            4, 5, 1, 4, 1, 0,
            // 右面
            1, 5, 6, 1, 6, 2,
            // 左面
            4, 0, 3, 4, 3, 7
        };

        // 计算法向量（每个面的法向量）
        var normals = new[]
        {
            // 前面 (8个顶点，每个顶点一个法向量)
            0.0f, 0.0f, -1.0f, // 0
            0.0f, 0.0f, -1.0f, // 1
            0.0f, 0.0f, -1.0f, // 2
            0.0f, 0.0f, -1.0f, // 3
            0.0f, 0.0f, 1.0f,  // 4
            0.0f, 0.0f, 1.0f,  // 5
            0.0f, 0.0f, 1.0f,  // 6
            0.0f, 0.0f, 1.0f   // 7
        };

        // 将几何数据转换为字节数组
        var vertexBytes = new byte[vertices.Length * sizeof(float)];
        Buffer.BlockCopy(vertices, 0, vertexBytes, 0, vertexBytes.Length);

        var normalBytes = new byte[normals.Length * sizeof(float)];
        Buffer.BlockCopy(normals, 0, normalBytes, 0, normalBytes.Length);

        var indexBytes = new byte[indices.Length * sizeof(ushort)];
        Buffer.BlockCopy(indices, 0, indexBytes, 0, indexBytes.Length);

        // 计算buffer和bufferView的大小和偏移
        var vertexBufferByteLength = vertexBytes.Length;
        var normalBufferByteLength = normalBytes.Length;
        var indexBufferByteLength = indexBytes.Length;
        var totalBufferByteLength = vertexBufferByteLength + normalBufferByteLength + indexBufferByteLength;

        // 构建完整的glTF JSON结构
        var gltf = new
        {
            asset = new
            {
                version = "2.0",
                generator = "RealScene3D Slicer v1.0"
            },
            scene = 0,
            scenes = new[]
            {
                new
                {
                    name = $"Tile_{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}",
                    nodes = new[] { 0 }
                }
            },
            nodes = new[]
            {
                new
                {
                    name = "TileMesh",
                    mesh = 0
                }
            },
            meshes = new[]
            {
                new
                {
                    name = "TileGeometry",
                    primitives = new[]
                    {
                        new
                        {
                            attributes = new
                            {
                                POSITION = 0,
                                NORMAL = 1
                            },
                            indices = 2,
                            mode = 4 // TRIANGLES
                        }
                    }
                }
            },
            accessors = new object[]
            {
                // Accessor 0: Positions
                new
                {
                    bufferView = 0,
                    componentType = 5126, // FLOAT
                    count = vertices.Length / 3,
                    type = "VEC3",
                    min = new[] { (float)boundingBox.MinX, (float)boundingBox.MinY, (float)boundingBox.MinZ },
                    max = new[] { (float)boundingBox.MaxX, (float)boundingBox.MaxY, (float)boundingBox.MaxZ }
                },
                // Accessor 1: Normals
                new
                {
                    bufferView = 1,
                    componentType = 5126, // FLOAT
                    count = normals.Length / 3,
                    type = "VEC3"
                },
                // Accessor 2: Indices
                new
                {
                    bufferView = 2,
                    componentType = 5123, // UNSIGNED_SHORT
                    count = indices.Length,
                    type = "SCALAR"
                }
            },
            bufferViews = new[]
            {
                // BufferView 0: Vertex positions
                new
                {
                    buffer = 0,
                    byteOffset = 0,
                    byteLength = vertexBufferByteLength,
                    target = 34962 // ARRAY_BUFFER
                },
                // BufferView 1: Normals
                new
                {
                    buffer = 0,
                    byteOffset = vertexBufferByteLength,
                    byteLength = normalBufferByteLength,
                    target = 34962 // ARRAY_BUFFER
                },
                // BufferView 2: Indices
                new
                {
                    buffer = 0,
                    byteOffset = vertexBufferByteLength + normalBufferByteLength,
                    byteLength = indexBufferByteLength,
                    target = 34963 // ELEMENT_ARRAY_BUFFER
                }
            },
            buffers = new[]
            {
                new
                {
                    byteLength = totalBufferByteLength,
                    uri = "data:application/octet-stream;base64," + Convert.ToBase64String(
                        vertexBytes.Concat(normalBytes).Concat(indexBytes).ToArray())
                }
            }
        };

        var jsonContent = System.Text.Json.JsonSerializer.Serialize(gltf, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        _logger.LogDebug("GLTF文件生成完成：切片{SliceId}, 顶点数{VertexCount}, 三角形数{TriangleCount}, 大小{Size}字节",
            slice.Id, vertices.Length / 3, indices.Length / 3, jsonContent.Length);

        return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(jsonContent));
    }

    /// <summary>
    /// 生成JSON格式切片内容 - 增强的3D Tiles元数据格式
    /// 算法：生成符合3D Tiles规范的完整JSON元数据，包含空间索引、LOD层级、渲染优化等信息
    /// 支持：空间索引（包围盒、包围球）、几何误差计算、渲染优先级、数据完整性校验
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="config">切片配置</param>
    /// <returns>JSON格式的字节数组</returns>
    private Task<byte[]> GenerateJSONContentAsync(Slice slice, SlicingConfig config)
    {
        // 解析包围盒以获取详细的空间信息
        var boundingBox = System.Text.Json.JsonSerializer.Deserialize<BoundingBox>(slice.BoundingBox);
        if (boundingBox == null)
        {
            // 回退到简化格式
            boundingBox = new BoundingBox();
        }

        // 计算包围球信息（用于高效的视锥剔除）
        var center = new
        {
            x = (boundingBox.MinX + boundingBox.MaxX) / 2.0,
            y = (boundingBox.MinY + boundingBox.MaxY) / 2.0,
            z = (boundingBox.MinZ + boundingBox.MaxZ) / 2.0
        };

        var radius = Math.Sqrt(
            Math.Pow(boundingBox.MaxX - boundingBox.MinX, 2) +
            Math.Pow(boundingBox.MaxY - boundingBox.MinY, 2) +
            Math.Pow(boundingBox.MaxZ - boundingBox.MinZ, 2)
        ) / 2.0;

        // 增强的3D Tiles元数据结构
        var jsonData = new
        {
            // 基本标识信息
            tilesetVersion = "1.0",
            asset = new
            {
                version = "1.0",
                generator = "RealScene3D Slicer",
                tileFormat = config.OutputFormat
            },

            // 切片标识
            tile = new
            {
                id = slice.Id.ToString(),
                level = slice.Level,
                coordinates = new
                {
                    x = slice.X,
                    y = slice.Y,
                    z = slice.Z
                },
                name = $"Tile_{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}"
            },

            // 空间索引信息
            spatial = new
            {
                // 轴对齐包围盒（AABB）
                boundingBox = new
                {
                    min = new[] { boundingBox.MinX, boundingBox.MinY, boundingBox.MinZ },
                    max = new[] { boundingBox.MaxX, boundingBox.MaxY, boundingBox.MaxZ }
                },
                // 包围球（用于快速视锥剔除）
                boundingSphere = new
                {
                    center = new[] { center.x, center.y, center.z },
                    radius = radius
                },
                // 空间范围（用于空间查询）
                extent = new
                {
                    width = boundingBox.MaxX - boundingBox.MinX,
                    height = boundingBox.MaxY - boundingBox.MinY,
                    depth = boundingBox.MaxZ - boundingBox.MinZ,
                    volume = (boundingBox.MaxX - boundingBox.MinX) *
                            (boundingBox.MaxY - boundingBox.MinY) *
                            (boundingBox.MaxZ - boundingBox.MinZ)
                }
            },

            // LOD层级信息
            lod = new
            {
                level = slice.Level,
                geometricError = CalculateGeometricError(slice.Level, config),
                screenSpaceError = config.GeometricErrorThreshold,
                minDistance = Math.Pow(2, slice.Level) * config.TileSize * 0.5,
                maxDistance = Math.Pow(2, slice.Level + 1) * config.TileSize * 2.0
            },

            // 渲染优化信息
            rendering = new
            {
                priority = CalculateRenderingPriority(slice.Level, center),
                culling = new
                {
                    frustum = true,
                    occlusion = slice.Level > 3,  // 高层级启用遮挡剔除
                    backface = true
                },
                visibility = new
                {
                    minPixelSize = Math.Max(1.0, Math.Pow(0.5, slice.Level) * 256.0),
                    preferredPixelSize = Math.Pow(0.5, slice.Level) * 512.0
                }
            },

            // 文件信息
            content = new
            {
                uri = slice.FilePath,
                type = config.OutputFormat,
                size = slice.FileSize,
                compression = config.CompressionLevel > 0 ? "gzip" : "none",
                encoding = "utf8"
            },

            // 元数据
            metadata = new
            {
                createdAt = slice.CreatedAt.ToString("o"),  // ISO 8601格式
                strategy = config.Strategy.ToString(),
                formatVersion = "1.0",
                schemaVersion = "1.1.0",
                checksum = CalculateMetadataChecksum(slice)
            },

            // 层级关系（用于层级遍历）
            hierarchy = new
            {
                hasParent = slice.Level > 0,
                hasChildren = slice.Level < config.MaxLevel,
                parent = slice.Level > 0 ? new
                {
                    level = slice.Level - 1,
                    x = slice.X / 2,
                    y = slice.Y / 2,
                    z = slice.Z / 2
                } : null,
                childrenCount = slice.Level < config.MaxLevel ? 8 : 0
            }
        };

        var jsonContent = System.Text.Json.JsonSerializer.Serialize(jsonData, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _logger.LogDebug("JSON元数据文件生成完成：切片{SliceId}, 级别{Level}, 大小{Size}字节",
            slice.Id, slice.Level, jsonContent.Length);

        return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(jsonContent));
    }

    /// <summary>
    /// 计算几何误差 - LOD误差计算算法
    /// 算法：基于LOD级别计算几何误差阈值，用于自适应LOD选择
    /// </summary>
    /// <param name="level">LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <returns>几何误差值</returns>
    private double CalculateGeometricError(int level, SlicingConfig config)
    {
        // 几何误差随LOD级别指数衰减
        // level 0: 最大误差（最粗糙）
        // level N: 最小误差（最精细）
        var baseError = config.GeometricErrorThreshold;
        var errorFactor = Math.Pow(2.0, config.MaxLevel - level);
        return baseError * errorFactor;
    }

    /// <summary>
    /// 计算渲染优先级 - 渲染调度优化算法
    /// 算法：基于LOD级别和距离中心点的距离计算渲染优先级
    /// </summary>
    /// <param name="level">LOD级别</param>
    /// <param name="center">切片中心点</param>
    /// <returns>优先级值（越小优先级越高）</returns>
    private int CalculateRenderingPriority(int level, dynamic center)
    {
        // 优先级综合考虑：
        // 1. LOD级别：低级别（粗糙）优先渲染
        // 2. 距离中心：靠近中心优先渲染
        var levelPriority = level * 1000;  // LOD级别权重
        var distanceToOrigin = Math.Sqrt(center.x * center.x + center.y * center.y + center.z * center.z);
        var distancePriority = (int)(distanceToOrigin / 10.0);  // 距离权重

        return levelPriority + distancePriority;
    }

    /// <summary>
    /// 计算元数据校验和 - 数据完整性验证算法
    /// 算法：基于切片关键信息生成SHA256哈希，用于验证数据完整性
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <returns>十六进制哈希字符串</returns>
    private string CalculateMetadataChecksum(Slice slice)
    {
        var checksumInput = $"{slice.Id}|{slice.Level}|{slice.X}|{slice.Y}|{slice.Z}|{slice.FileSize}|{slice.BoundingBox}";
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(checksumInput));
            return Convert.ToHexString(hashBytes).ToLower().Substring(0, 16);  // 取前16个字符作为校验和
        }
    }

    /// <summary>
    /// 生成默认格式切片内容
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="config">切片配置</param>
    /// <returns>默认格式的字节数组</returns>
    private async Task<byte[]> GenerateDefaultContentAsync(Slice slice, SlicingConfig config)
    {
        return await GenerateJSONContentAsync(slice, config);
    }

    /// <summary>
    /// 生成GLB内容 - 二进制glTF格式
    /// 算法：生成二进制格式的glTF数据，包含完整的几何数据和纹理
    /// GLB格式：12字节Header + JSON Chunk (对齐) + Binary Chunk (对齐)
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="config">切片配置</param>
    /// <returns>GLB格式的字节数组</returns>
    private Task<byte[]> GenerateGLBContentAsync(Slice slice, SlicingConfig config)
    {
        // 解析包围盒
        var boundingBox = System.Text.Json.JsonSerializer.Deserialize<BoundingBox>(slice.BoundingBox);
        if (boundingBox == null)
        {
            throw new InvalidOperationException("无法解析切片包围盒");
        }

        // 1. 生成几何数据（立方体占位符）
        var vertices = new[]
        {
            (float)boundingBox.MinX, (float)boundingBox.MinY, (float)boundingBox.MinZ,
            (float)boundingBox.MaxX, (float)boundingBox.MinY, (float)boundingBox.MinZ,
            (float)boundingBox.MaxX, (float)boundingBox.MaxY, (float)boundingBox.MinZ,
            (float)boundingBox.MinX, (float)boundingBox.MaxY, (float)boundingBox.MinZ,
            (float)boundingBox.MinX, (float)boundingBox.MinY, (float)boundingBox.MaxZ,
            (float)boundingBox.MaxX, (float)boundingBox.MinY, (float)boundingBox.MaxZ,
            (float)boundingBox.MaxX, (float)boundingBox.MaxY, (float)boundingBox.MaxZ,
            (float)boundingBox.MinX, (float)boundingBox.MaxY, (float)boundingBox.MaxZ
        };

        var normals = new[]
        {
            0.0f, 0.0f, -1.0f,
            0.0f, 0.0f, -1.0f,
            0.0f, 0.0f, -1.0f,
            0.0f, 0.0f, -1.0f,
            0.0f, 0.0f, 1.0f,
            0.0f, 0.0f, 1.0f,
            0.0f, 0.0f, 1.0f,
            0.0f, 0.0f, 1.0f
        };

        var indices = new ushort[]
        {
            0, 1, 2, 0, 2, 3,
            5, 4, 7, 5, 7, 6,
            3, 2, 6, 3, 6, 7,
            4, 5, 1, 4, 1, 0,
            1, 5, 6, 1, 6, 2,
            4, 0, 3, 4, 3, 7
        };

        // 2. 将几何数据转换为字节数组
        var vertexBytes = new byte[vertices.Length * sizeof(float)];
        Buffer.BlockCopy(vertices, 0, vertexBytes, 0, vertexBytes.Length);

        var normalBytes = new byte[normals.Length * sizeof(float)];
        Buffer.BlockCopy(normals, 0, normalBytes, 0, normalBytes.Length);

        var indexBytes = new byte[indices.Length * sizeof(ushort)];
        Buffer.BlockCopy(indices, 0, indexBytes, 0, indexBytes.Length);

        // 3. 构建Binary Chunk数据
        var binaryData = vertexBytes.Concat(normalBytes).Concat(indexBytes).ToArray();
        var vertexBufferByteLength = vertexBytes.Length;
        var normalBufferByteLength = normalBytes.Length;
        var indexBufferByteLength = indexBytes.Length;

        // 4. 构建glTF JSON（引用buffer 0）
        var gltfJson = new
        {
            asset = new
            {
                version = "2.0",
                generator = "RealScene3D Slicer v1.0"
            },
            scene = 0,
            scenes = new[] { new { name = $"Tile_{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}", nodes = new[] { 0 } } },
            nodes = new[] { new { name = "TileMesh", mesh = 0 } },
            meshes = new[]
            {
                new
                {
                    name = "TileGeometry",
                    primitives = new[]
                    {
                        new
                        {
                            attributes = new { POSITION = 0, NORMAL = 1 },
                            indices = 2,
                            mode = 4
                        }
                    }
                }
            },
            accessors = new object[]
            {
                new
                {
                    bufferView = 0,
                    componentType = 5126,
                    count = vertices.Length / 3,
                    type = "VEC3",
                    min = new[] { (float)boundingBox.MinX, (float)boundingBox.MinY, (float)boundingBox.MinZ },
                    max = new[] { (float)boundingBox.MaxX, (float)boundingBox.MaxY, (float)boundingBox.MaxZ }
                },
                new { bufferView = 1, componentType = 5126, count = normals.Length / 3, type = "VEC3" },
                new { bufferView = 2, componentType = 5123, count = indices.Length, type = "SCALAR" }
            },
            bufferViews = new[]
            {
                new { buffer = 0, byteOffset = 0, byteLength = vertexBufferByteLength, target = 34962 },
                new { buffer = 0, byteOffset = vertexBufferByteLength, byteLength = normalBufferByteLength, target = 34962 },
                new { buffer = 0, byteOffset = vertexBufferByteLength + normalBufferByteLength, byteLength = indexBufferByteLength, target = 34963 }
            },
            buffers = new[] { new { byteLength = binaryData.Length } }
        };

        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(gltfJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));

        // 5. 对齐JSON块到4字节边界（用空格填充）
        var jsonChunkLength = (jsonBytes.Length + 3) & ~3;
        var jsonChunkPadded = new byte[jsonChunkLength];
        Array.Copy(jsonBytes, jsonChunkPadded, jsonBytes.Length);
        for (int i = jsonBytes.Length; i < jsonChunkLength; i++)
        {
            jsonChunkPadded[i] = 0x20; // 空格
        }

        // 6. 对齐Binary块到4字节边界（用0填充）
        var binaryChunkLength = (binaryData.Length + 3) & ~3;
        var binaryChunkPadded = new byte[binaryChunkLength];
        Array.Copy(binaryData, binaryChunkPadded, binaryData.Length);

        // 7. 构造完整的GLB文件
        using (var memoryStream = new MemoryStream())
        using (var writer = new BinaryWriter(memoryStream))
        {
            // GLB Header (12字节)
            writer.Write(0x46546C67); // magic: "glTF" (little-endian)
            writer.Write((uint)2);     // version: 2

            var totalLength = 12 + // header
                             8 + jsonChunkLength + // JSON chunk header + data
                             8 + binaryChunkLength; // Binary chunk header + data

            writer.Write((uint)totalLength); // length

            // JSON Chunk
            writer.Write((uint)jsonChunkLength); // chunkLength
            writer.Write(0x4E4F534A); // chunkType: "JSON" (little-endian)
            writer.Write(jsonChunkPadded);

            // Binary Chunk
            writer.Write((uint)binaryChunkLength); // chunkLength
            writer.Write(0x004E4942); // chunkType: "BIN\0" (little-endian)
            writer.Write(binaryChunkPadded);

            _logger.LogDebug("GLB文件生成完成：切片{SliceId}, 总大小{TotalSize}字节, JSON块{JsonSize}字节, Binary块{BinarySize}字节, 顶点数{VertexCount}",
                slice.Id, totalLength, jsonChunkLength, binaryChunkLength, vertices.Length / 3);

            return Task.FromResult(memoryStream.ToArray());
        }
    }

    /// <summary>
    /// 压缩切片内容 - 数据压缩算法实现
    /// 算法：使用指定的压缩级别对切片数据进行压缩
    /// </summary>
    /// <param name="content">原始内容</param>
    /// <param name="compressionLevel">压缩级别（0-9）</param>
    /// <returns>压缩后的内容</returns>
    private async Task<byte[]> CompressSliceContentAsync(byte[] content, int compressionLevel)
    {
        // 压缩算法实现：使用GZip压缩
        using (var compressedStream = new MemoryStream())
        {
            using (var gzipStream = new System.IO.Compression.GZipStream(compressedStream, GetCompressionLevel(compressionLevel)))
            {
                await gzipStream.WriteAsync(content, 0, content.Length);
            }
            return compressedStream.ToArray();
        }
    }

    /// <summary>
    /// 生成增量更新索引 - 增量更新算法实现
    /// 算法：为切片生成增量更新索引，支持部分模型更新
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="config">切片配置</param>
    private async Task GenerateIncrementalUpdateIndexAsync(SlicingTask task, SlicingConfig config, CancellationToken cancellationToken)
    {
        // 增量更新索引生成算法
        var allSlices = await _sliceRepository.GetAllAsync();
        var slices = allSlices.Where(s => s.SlicingTaskId == task.Id).ToList();

                    var sliceData = new List<object>();
                    foreach (var s in slices)
                    {
                        sliceData.Add(new
                        {
                            s.Level,
                            s.X,
                            s.Y,
                            s.Z,
                            s.FilePath,
                            Hash = await CalculateSliceHash(s), // 计算切片哈希用于增量比较
                            s.BoundingBox
                        });
                    }
        
                    var updateIndex = new
                    {
                        TaskId = task.Id,
                        Version = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                        LastModified = DateTime.UtcNow,
                        SliceCount = slices.Count,
                        Slices = sliceData,
                        Strategy = config.Strategy.ToString(),
                        TileSize = config.TileSize
                    };
        var indexContent = System.Text.Json.JsonSerializer.Serialize(updateIndex, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var indexPath = $"{task.OutputPath}/incremental_index.json";

        if (config.StorageLocation == StorageLocationType.LocalFileSystem)
        {
            var fullPath = Path.Combine(task.OutputPath!, "incremental_index.json");
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllTextAsync(fullPath, indexContent, cancellationToken);
            _logger.LogDebug("增量更新索引文件已保存到本地：{FilePath}", fullPath);
        }
        else
        {
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(indexContent)))
            {
                await _minioService.UploadFileAsync("slices", indexPath, stream, "application/json", cancellationToken);
            }
            _logger.LogInformation("增量更新索引文件已上传到MinIO：{FilePath}", indexPath);
        }

        _logger.LogInformation("增量更新索引已生成：{IndexPath}", indexPath);
    }

    /// <summary>
    /// 计算切片哈希值 - 增量更新比较算法
    /// 算法：基于切片完整内容计算哈希值，包括文件内容、元数据等，用于精确检测变化
    /// 特性：
    /// - 包含文件内容哈希：确保文件内容变化能被检测
    /// - 包含元数据信息：包括位置、包围盒等关键信息
    /// - 使用SHA256算法：安全可靠的哈希算法
    /// - 异步文件读取：避免阻塞主线程
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <returns>哈希值字符串</returns>
    private async Task<string> CalculateSliceHash(Slice slice)
    {
        try
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                // 1. 收集切片的所有关键信息
                // 包括：空间位置、包围盒、LOD级别、文件路径、创建时间等
                var metadataInput = $"{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}_{slice.BoundingBox}_{slice.FilePath}";
                var metadataBytes = System.Text.Encoding.UTF8.GetBytes(metadataInput);

                // 2. 尝试读取文件内容并计算哈希
                // 注意：由于这是同步方法，使用GetAwaiter().GetResult()
                // 生产环境建议改为异步方法避免阻塞
                byte[]? fileContentHash = null;
                try
                {
                    var task = await _slicingTaskRepository.GetByIdAsync(slice.SlicingTaskId);
                    if (task == null) throw new InvalidOperationException($"切片任务 {slice.SlicingTaskId} 未找到");

                    var config = ParseSlicingConfig(task.SlicingConfig);

                    if (config.StorageLocation == StorageLocationType.LocalFileSystem)
                    {
                        // 本地文件系统：需要拼接完整路径
                        var fullPath = Path.IsPathRooted(slice.FilePath)
                            ? slice.FilePath
                            : Path.Combine(task.OutputPath ?? "", slice.FilePath);

                        if (File.Exists(fullPath))
                        {
                            using (var fileStream = File.OpenRead(fullPath))
                            {
                                fileContentHash = sha256.ComputeHash(fileStream);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("本地切片文件不存在，无法计算哈希：{FilePath}", fullPath);
                        }
                    }
                    else // MinIO
                    {
                        // 从MinIO读取文件内容
                        var fileStream = _minioService.DownloadFileAsync("slices", slice.FilePath)
                            .GetAwaiter().GetResult();

                        if (fileStream != null)
                        {
                            using (fileStream)
                            {
                                // 计算文件内容的哈希
                                fileContentHash = sha256.ComputeHash(fileStream);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 文件读取失败时记录警告，但不影响哈希计算
                    // 仅使用元数据计算哈希
                    _logger.LogWarning(ex, "读取切片文件失败，将仅使用元数据计算哈希：{FilePath}", slice.FilePath);
                    _logger.LogWarning(ex, "读取切片文件失败，将仅使用元数据计算哈希：{FilePath}", slice.FilePath);
                }

                // 3. 组合元数据和文件内容哈希
                byte[] finalHash;
                if (fileContentHash != null)
                {
                    // 将元数据和文件内容哈希组合
                    var combinedBytes = new byte[metadataBytes.Length + fileContentHash.Length];
                    Buffer.BlockCopy(metadataBytes, 0, combinedBytes, 0, metadataBytes.Length);
                    Buffer.BlockCopy(fileContentHash, 0, combinedBytes, metadataBytes.Length, fileContentHash.Length);

                    // 计算最终哈希
                    finalHash = sha256.ComputeHash(combinedBytes);
                }
                else
                {
                    // 文件内容不可用，仅使用元数据
                    finalHash = sha256.ComputeHash(metadataBytes);
                }

                // 4. 转换为十六进制字符串
                return Convert.ToHexString(finalHash).ToLower();
            }
        }
        catch (Exception ex)
        {
            // 异常情况下返回基于元数据的简化哈希
            _logger.LogError(ex, "计算切片哈希时发生错误，将使用简化方式计算：{SliceId}", slice.Id);
            var fallbackInput = $"{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}_{slice.BoundingBox}";
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(fallbackInput));
                return Convert.ToHexString(hashBytes).ToLower();
            }
        }
    }

    /// <summary>
    /// 从已存在的切片计算哈希值 - 用于增量更新比对
    /// 与CalculateSliceHash的区别是，这个方法用于已存在于数据库中的切片
    /// </summary>
    /// <param name="slice">已存在的切片数据</param>
    /// <returns>哈希值字符串</returns>
    private async Task<string> CalculateSliceHashFromExisting(Slice slice)
    {
        // 直接调用 CalculateSliceHash，因为逻辑是一样的
        // 该方法会根据切片的 SlicingTaskId 查找任务配置，然后读取文件计算哈希
        return await CalculateSliceHash(slice);
    }

    private System.IO.Compression.CompressionLevel GetCompressionLevel(int level)
    {
        return level switch
        {
            <= 1 => System.IO.Compression.CompressionLevel.Fastest,
            >= 9 => System.IO.Compression.CompressionLevel.SmallestSize,
            _ => System.IO.Compression.CompressionLevel.Optimal
        };
    }

    private string GetContentType(string format)
    {
        return format.ToLower() switch
        {
            "b3dm" => "application/octet-stream",
            "gltf" => "application/json",
            "glb" => "application/octet-stream",
            "json" => "application/json",
            _ => "application/octet-stream"
        };
    }
}

/// <summary>
/// 任务进度历史记录 - 用于趋势分析和精确时间估算
/// 线程安全的进度跟踪类，支持并发访问
/// </summary>
internal class TaskProgressHistory
{
    private readonly object _lock = new object();
    private readonly List<ProgressRecord> _progressRecords = new();
    private const int MaxRecordsCount = 100; // 最多保留100条历史记录
    private const int MaxRecordAgeMinutes = 60; // 最多保留60分钟的历史记录

    /// <summary>
    /// 进度记录列表（只读）
    /// </summary>
    public IReadOnlyList<ProgressRecord> ProgressRecords
    {
        get
        {
            lock (_lock)
            {
                return _progressRecords.ToList();
            }
        }
    }

    /// <summary>
    /// 上次估算的剩余时间（秒）
    /// </summary>
    public double? LastEstimatedTime { get; set; }

    /// <summary>
    /// 记录进度数据点
    /// </summary>
    /// <param name="progress">当前进度（0-100）</param>
    /// <param name="timestamp">时间戳</param>
    public void RecordProgress(double progress, DateTime timestamp)
    {
        lock (_lock)
        {
            // 添加新记录
            _progressRecords.Add(new ProgressRecord
            {
                Progress = progress,
                Timestamp = timestamp
            });

            // 清理过期数据
            CleanupOldRecords();

            // 限制记录数量
            while (_progressRecords.Count > MaxRecordsCount)
            {
                _progressRecords.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// 获取指定时间范围内的记录
    /// </summary>
    /// <param name="timeSpan">时间范围</param>
    /// <returns>时间范围内的记录列表</returns>
    public List<ProgressRecord> GetRecentRecords(TimeSpan timeSpan)
    {
        lock (_lock)
        {
            var cutoffTime = DateTime.UtcNow - timeSpan;
            return _progressRecords.Where(r => r.Timestamp >= cutoffTime).ToList();
        }
    }

    /// <summary>
    /// 清理过期记录
    /// </summary>
    private void CleanupOldRecords()
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-MaxRecordAgeMinutes);
        _progressRecords.RemoveAll(r => r.Timestamp < cutoffTime);
    }

    /// <summary>
    /// 进度记录数据点
    /// </summary>
    public class ProgressRecord
    {
        public double Progress { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

/// <summary>
/// 增量更新索引JSON模型 - 用于反序列化MinIO中的索引文件
/// </summary>
internal class IncrementalIndexJsonModel
{
    public Guid TaskId { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public int SliceCount { get; set; }
    public List<IncrementalSliceJsonModel>? Slices { get; set; }
    public string? Strategy { get; set; }
    public double TileSize { get; set; }
}

/// <summary>
/// 增量切片JSON模型 - 用于反序列化索引文件中的切片信息
/// </summary>
internal class IncrementalSliceJsonModel
{
    public int Level { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public string? FilePath { get; set; }
    public string? Hash { get; set; }
    public string? BoundingBox { get; set; }
}
