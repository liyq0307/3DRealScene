using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Application.Services;

/// <summary>
/// 网格切片策略 - 规则网格剖分算法
/// 适用于规则地形和均匀分布的数据，计算简单，内存占用规律
/// </summary>
/// <remarks>
/// 构造函数 - 注入日志记录器
/// </remarks>
/// <param name="logger">日志记录器</param>
public class GridSlicingStrategy(ILogger logger) : ISlicingStrategy
{
    // 日志记录器
    private readonly ILogger _logger = logger;

    /// <summary>
    /// 生成切片集合 - 增强的网格切片策略算法实现
    /// 算法：基于规则网格进行三维空间剖分，生成LOD层级切片
    /// 支持：并行处理、内存优化、进度监控、边界条件处理
    /// </summary>
    /// <param name="task">切片任务，包含任务配置和状态</param>
    /// <param name="level">LOD级别，影响网格密度和切片数量</param>
    /// <param name="config">切片配置，控制剖分策略和输出格式</param>
    /// <param name="modelBounds">模型的实际包围盒，用于确定切片的空间范围</param>
    /// <param name="cancellationToken">取消令牌，支持优雅中断</param>
    /// <returns>生成的切片集合，按空间位置排序</returns>
    public async Task<List<Slice>> GenerateSlicesAsync(SlicingTask task, int level, SlicingConfig config, BoundingBox3D modelBounds, CancellationToken cancellationToken)
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
            slices = await GenerateSlicesInParallelAsync(task, level, config, tilesInLevel, zTilesCount, modelBounds, cancellationToken);
        }
        else
        {
            // 串行生成 - 适用于小规模切片
            slices = await GenerateSlicesSequentiallyAsync(task, level, config, tilesInLevel, zTilesCount, modelBounds, cancellationToken);
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
    /// <param name="modelBounds">模型的实际包围盒</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生成的切片集合</returns>
    private Task<List<Slice>> GenerateSlicesSequentiallyAsync(
        SlicingTask task, int level, SlicingConfig config,
        int tilesInLevel, int zTilesCount, BoundingBox3D modelBounds, CancellationToken cancellationToken)
    {
        var slices = new List<Slice>();

        for (int z = 0; z < zTilesCount; z++)
        {
            for (int y = 0; y < tilesInLevel; y++)
            {
                for (int x = 0; x < tilesInLevel; x++)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var slice = CreateSlice(task, level, config, x, y, z, modelBounds);
                    if (slice != null)
                    {
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
        }

        return Task.FromResult(slices);
    }

    /// <summary>
    /// 并行切片生成 - 适用于大规模切片的高性能处理（内存优化版本）
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
        int tilesInLevel, int zTilesCount, BoundingBox3D modelBounds, CancellationToken cancellationToken)
    {
        // 使用线程安全的集合
        var slices = new System.Collections.Concurrent.ConcurrentBag<Slice>();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Min(config.ParallelProcessingCount, Environment.ProcessorCount),
            CancellationToken = cancellationToken
        };

        var processedCount = 0;
        var totalSlices = tilesInLevel * tilesInLevel * zTilesCount;

        await Task.Run(() =>
        {
            Parallel.For(0, zTilesCount, parallelOptions, z =>
            {
                for (int y = 0; y < tilesInLevel; y++)
                {
                    for (int x = 0; x < tilesInLevel; x++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var slice = CreateSlice(task, level, config, x, y, z, modelBounds);
                        if (slice != null)
                        {
                            slices.Add(slice);

                            var currentCount = Interlocked.Increment(ref processedCount);

                            // 并行进度监控（减少日志频率）
                            if (currentCount % 500 == 0)
                            {
                                _logger.LogDebug("并行网格切片生成进度：级别{Level}，已生成{Processed}/{Total}",
                                    level, currentCount, totalSlices);

                                // 定期触发GC以控制内存增长
                                if (currentCount % 2000 == 0)
                                {
                                    GC.Collect(0, GCCollectionMode.Optimized);
                                }
                            }
                        }
                    }
                }
            });
        }, cancellationToken);

        return slices.ToList();
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
    /// <param name="modelBounds">模型的实际包围盒</param>
    /// <returns>生成的切片实例，如果切片不与模型相交则返回null</returns>
    private Slice? CreateSlice(SlicingTask task, int level, SlicingConfig config, int x, int y, int z, BoundingBox3D modelBounds)
    {
        var boundingBox = GenerateGridBoundingBox(level, x, y, z, config.TileSize, modelBounds);
        if (boundingBox == null)
        {
            // 切片不与模型相交，跳过
            return null;
        }

        return new Slice
        {
            SlicingTaskId = task.Id,
            Level = level,
            X = x,
            Y = y,
            Z = z,
            FilePath = GenerateSliceFilePath(task, level, x, y, z, config.OutputFormat),
            BoundingBox = boundingBox,
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
    /// **关键改进：基于模型实际包围盒进行空间剖分**
    /// </summary>
    /// <param name="level">LOD级别，用于计算缩放因子</param>
    /// <param name="x">X轴网格坐标</param>
    /// <param name="y">Y轴网格坐标</param>
    /// <param name="z">Z轴网格坐标</param>
    /// <param name="tileSize">基础切片尺寸</param>
    /// <param name="modelBounds">模型的实际包围盒</param>
    /// <returns>标准化的JSON格式包围盒字符串，如果切片不与模型相交则返回null</returns>
    private string? GenerateGridBoundingBox(int level, int x, int y, int z, double tileSize, BoundingBox3D modelBounds)
    {
        // 1. 计算模型的实际尺寸
        var modelSizeX = modelBounds.MaxX - modelBounds.MinX;
        var modelSizeY = modelBounds.MaxY - modelBounds.MinY;
        var modelSizeZ = modelBounds.MaxZ - modelBounds.MinZ;

        // 使用最大尺寸作为基准，确保所有维度使用统一的缩放
        var maxModelSize = Math.Max(modelSizeX, Math.Max(modelSizeY, modelSizeZ));

        // 2. 计算LOD缩放因子
        // level 0: 基础尺寸，整个模型空间被划分为 1x1x1 网格
        // level N: 尺寸减半的N次方，空间被划分为 2^N x 2^N x 2^N 网格
        var tilesInLevel = Math.Pow(2, level);
        
        // 对Z轴使用不同的计算方法，与生成逻辑保持一致
        var zTilesCount = level == 0 ? 1 : Math.Max(1, (int)tilesInLevel / 2);
        
        // 计算各个轴的真实尺寸
        var xScaledTileSize = modelSizeX / tilesInLevel;
        var yScaledTileSize = modelSizeY / tilesInLevel;
        var zScaledTileSize = modelSizeZ / zTilesCount;

        // 3. 计算切片在模型空间中的实际位置
        // 将网格坐标映射到模型的实际坐标范围
        var relativeX = x * xScaledTileSize;
        var relativeY = y * yScaledTileSize;
        var relativeZ = z * zScaledTileSize;

        // 4. 计算轴对齐包围盒（AABB）- 映射到模型坐标系
        var minX = modelBounds.MinX + relativeX;
        var minY = modelBounds.MinY + relativeY;
        var minZ = modelBounds.MinZ + relativeZ;
        var maxX = modelBounds.MinX + relativeX + xScaledTileSize;
        var maxY = modelBounds.MinY + relativeY + yScaledTileSize;
        var maxZ = modelBounds.MinZ + relativeZ + zScaledTileSize;

        _logger.LogDebug("切片({Level},{X},{Y},{Z})计算包围盒：原始=[{OrigMinX:F3},{OrigMinY:F3},{OrigMinZ:F3}]-[{OrigMaxX:F3},{OrigMaxY:F3},{OrigMaxZ:F3}] (XSize={XSize:F6}, YSize={YSize:F6}, ZSize={ZSize:F6})",
            level, x, y, z, minX, minY, minZ, maxX, maxY, maxZ, xScaledTileSize, yScaledTileSize, zScaledTileSize);

        // 5. 边界裁剪 - 确保不超出模型范围
        // 注意：需要同时裁剪min和max，确保切片与模型的交集
        minX = Math.Max(minX, modelBounds.MinX);
        minY = Math.Max(minY, modelBounds.MinY);
        minZ = Math.Max(minZ, modelBounds.MinZ);
        maxX = Math.Min(maxX, modelBounds.MaxX);
        maxY = Math.Min(maxY, modelBounds.MaxY);
        maxZ = Math.Min(maxZ, modelBounds.MaxZ);

        _logger.LogDebug("切片({Level},{X},{Y},{Z})裁剪后：裁剪后=[{ClipMinX:F3},{ClipMinY:F3},{ClipMinZ:F3}]-[{ClipMaxX:F3},{ClipMaxY:F3},{ClipMaxZ:F3}]",
            level, x, y, z, minX, minY, minZ, maxX, maxY, maxZ);

        // 6. 验证切片是否与模型有有效交集
        // 检查是否有负尺寸（边界计算错误）或尺寸过小
        var minBoxSize = 1e-6;
        
        // 修复：如果尺寸为负值，说明边界裁剪后顺序错误，需要修复
        if (maxX < minX || maxY < minY || maxZ < minZ)
        {
            _logger.LogInformation("切片({Level},{X},{Y},{Z})被跳过：边界计算错误导致负尺寸，XSize={XSize:F6}, YSize={YSize:F6}, ZSize={ZSize:F6}",
                level, x, y, z, maxX - minX, maxY - minY, maxZ - minZ);
            return null;
        }
        
        if (maxX - minX < minBoxSize || maxY - minY < minBoxSize || maxZ - minZ < minBoxSize)
        {
            _logger.LogInformation("切片({Level},{X},{Y},{Z})被跳过：尺寸太小或无交集，XSize={XSize:F6}, YSize={YSize:F6}, ZSize={ZSize:F6}",
                level, x, y, z, maxX - minX, maxY - minY, maxZ - minZ);
            return null; // 切片不与模型相交，不生成
        }

        _logger.LogInformation("切片({Level},{X},{Y},{Z})生成成功：包围盒=[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}]",
            level, x, y, z, minX, minY, minZ, maxX, maxY, maxZ);

        // 7. 生成标准化JSON格式
        // 使用不变区域性格式确保跨平台兼容性
        // 注意：属性名首字母大写，与BoundingBox类的属性名匹配
        return $"{{\"MinX\":{minX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},\"MinY\":{minY.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},\"MinZ\":{minZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},\"MaxX\":{maxX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},\"MaxY\":{maxY.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},\"MaxZ\":{maxZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}}}";
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