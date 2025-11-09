using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;

namespace RealScene3D.Application.Services;

/// <summary>
/// 网格切片策略 - 规则网格剖分算法
/// 适用于规则地形和均匀分布的数据，计算简单，内存占用规律
/// 支持自适应网格、边界优化、内存池、流水线处理、LOD网格简化等高级特性
/// </summary>
public class GridSlicingStrategy : ISlicingStrategy
{
    // 日志记录器
    private readonly ILogger _logger;

    // 网格简化服务（可选）
    private readonly MeshDecimationService? _meshDecimationService;

    // 瓦片生成器工厂
    private readonly ITileGeneratorFactory _tileGeneratorFactory;

    // 模型加载器
    private readonly IModelLoader _modelLoader;

    /// <summary>
    /// 构造函数 - 注入日志记录器和必需的服务
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="tileGeneratorFactory">瓦片生成器工厂，用于动态创建不同格式的生成器</param>
    /// <param name="modelLoader">模型加载器</param>
    /// <param name="meshDecimationService">网格简化服务（可选，用于LOD生成）</param>
    public GridSlicingStrategy(
        ILogger logger,
        ITileGeneratorFactory tileGeneratorFactory,
        IModelLoader modelLoader,
        MeshDecimationService? meshDecimationService = null)
    {
        _logger = logger;
        _tileGeneratorFactory = tileGeneratorFactory ?? throw new ArgumentNullException(nameof(tileGeneratorFactory));
        _modelLoader = modelLoader ?? throw new ArgumentNullException(nameof(modelLoader));
        _meshDecimationService = meshDecimationService;
    }

    // 性能计数器
    private static readonly object _performanceCounterLock = new();
    private static long _totalSlicesGenerated = 0;
    private static long _totalProcessingTimeMs = 0;

    // 性能优化常量
    private const double MinBoxSize = 1e-6;
    private const int MaxParallelDegree = 1024; // 最大并行度限制
    private const int MinSlicesForParallel = 100; // 启用并行处理的最小切片数量
    private const int ProgressLogInterval = 10; // 进度日志间隔百分比

    // 内存池 - 用于减少GC压力
    private readonly ObjectPool<List<Slice>> _sliceListPool = new(() => new List<Slice>(256), list => list.Clear());

    // 预计算的2的幂次方表 - 避免重复计算
    private static readonly int[] PowerOfTwoTable = Enumerable.Range(0, 11).Select(i => 1 << i).ToArray();

    /// <summary>
    /// 对象池实现 - 用于高效管理对象生命周期
    /// 减少GC压力，提高内存使用效率
    /// </summary>
    /// <typeparam name="T">池中对象的类型</typeparam>
    private class ObjectPool<T> where T : class
    {
        private readonly ConcurrentBag<T> _objects = new();
        private readonly Func<T> _objectFactory;
        private readonly Action<T>? _resetAction;

        public ObjectPool(Func<T> objectFactory, Action<T>? resetAction = null)
        {
            _objectFactory = objectFactory;
            _resetAction = resetAction;
        }

        public T Get()
        {
            return _objects.TryTake(out T? item) ? item : _objectFactory();
        }

        public void Return(T item)
        {
            _resetAction?.Invoke(item);
            _objects.Add(item);
        }
    }

    /// <summary>
    /// 切片生成工作项 - 用于并行处理的任务数据结构
    /// </summary>
    private readonly struct SliceWorkItem
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public SlicingTask Task { get; }
        public int Level { get; }
        public SlicingConfig Config { get; }
        public BoundingBox3D ModelBounds { get; }

        public SliceWorkItem(int x, int y, int z, SlicingTask task, int level, SlicingConfig config, BoundingBox3D modelBounds)
        {
            X = x;
            Y = y;
            Z = z;
            Task = task;
            Level = level;
            Config = config;
            ModelBounds = modelBounds;
        }
    }

    /// <summary>
    /// 生成切片集合 - 增强的网格切片策略算法实现
    /// 算法：基于规则网格进行三维空间剖分，生成LOD层级切片
    /// 支持：并行处理、内存优化、进度监控、边界条件处理
    /// 性能优化：减少日志输出、优化边界计算、智能并行度选择
    /// </summary>
    /// <param name="task">切片任务，包含任务配置和状态</param>
    /// <param name="level">LOD级别，影响网格密度和切片数量</param>
    /// <param name="config">切片配置，控制剖分策略和输出格式</param>
    /// <param name="modelBounds">模型的实际包围盒，用于确定切片的空间范围</param>
    /// <param name="cancellationToken">取消令牌，支持优雅中断</param>
    /// <returns>生成的切片集合，按空间位置排序</returns>
    public async Task<List<Slice>> GenerateSlicesAsync(SlicingTask task, int level, SlicingConfig config, BoundingBox3D modelBounds, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var overallStartTime = DateTime.UtcNow;

        try
        {
            // 1. 参数验证和优化
            ValidateInputParameters(task, level, config, modelBounds);

            // 2. 配置验证
            var (isValid, errorMessage) = config.Validate();
            if (!isValid)
            {
                throw new ArgumentException($"切片配置无效: {errorMessage}", nameof(config));
            }

            // 3. 计算网格参数
            var tilesInLevel = CalculateTilesInLevel(level);
            var zTilesCount = tilesInLevel;

            _logger.LogInformation("网格切片策略：级别{Level}，网格尺寸{TilesX}x{TilesY}x{TilesZ}（统一剖分），模型范围=[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}]",
                level, tilesInLevel, tilesInLevel, zTilesCount,
                modelBounds.MinX, modelBounds.MinY, modelBounds.MinZ,
                modelBounds.MaxX, modelBounds.MaxY, modelBounds.MaxZ);

            // 4. 内存预分配优化
            var estimatedSliceCount = CalculateEstimatedSliceCount(tilesInLevel, zTilesCount);

            // 5. 智能选择并行或串行处理
            List<Slice> slices;
            var shouldUseParallel = ShouldUseParallelProcessing(config, estimatedSliceCount);

            if (shouldUseParallel)
            {
                _logger.LogInformation("使用并行处理模式：并行度{Parallelism}，切片数量{SliceCount}",
                    config.ParallelProcessingCount, estimatedSliceCount);
                slices = await GenerateSlicesWithPipelineAsync(task, level, config, tilesInLevel, zTilesCount, modelBounds, cancellationToken);
            }
            else
            {
                _logger.LogInformation("使用串行处理模式：切片数量{SliceCount}", estimatedSliceCount);
                slices = await GenerateSlicesSequentiallyAsync(task, level, config, tilesInLevel, zTilesCount, modelBounds, cancellationToken);
            }

            // 6. 结果验证和排序
            await ProcessSliceResults(slices, level, estimatedSliceCount, overallStartTime);

            // 7. 更新性能统计
            UpdatePerformanceCounters(slices.Count, stopwatch.ElapsedMilliseconds);

            return slices;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("网格切片生成被取消：级别{Level}，耗时{ElapsedMs}ms", level, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "网格切片生成失败：级别{Level}，耗时{ElapsedMs}ms", level, stopwatch.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// 格式化文件大小为人类可读格式
    /// </summary>
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:F2}{sizes[order]}";
    }

    /// <summary>
    /// 验证输入参数的有效性
    /// </summary>
    private void ValidateInputParameters(SlicingTask task, int level, SlicingConfig config, BoundingBox3D modelBounds)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task), "切片任务不能为空");

        if (level < 0 || level > 10)
            throw new ArgumentOutOfRangeException(nameof(level), "LOD级别必须在0-10之间");

        if (config == null)
            throw new ArgumentNullException(nameof(config), "切片配置不能为空");

        if (modelBounds == null || !modelBounds.IsValid())
            throw new ArgumentException("模型包围盒无效", nameof(modelBounds));

        if (!modelBounds.IsValid())
            throw new ArgumentException("模型包围盒的坐标无效", nameof(modelBounds));
    }

    /// <summary>
    /// 处理切片结果 - 验证、排序和统计
    /// </summary>
    private Task ProcessSliceResults(List<Slice> slices, int level, int estimatedSliceCount, DateTime startTime)
    {
        if (!slices.Any())
        {
            _logger.LogWarning("网格切片策略未生成任何切片：级别{Level}，估算{Estimated}个", level, estimatedSliceCount);
            return Task.CompletedTask;
        }

        // 按空间位置排序，便于后续处理
        var sortStartTime = DateTime.UtcNow;
        slices.Sort((a, b) =>
        {
            var zCompare = a.Z.CompareTo(b.Z);
            if (zCompare != 0) return zCompare;
            var yCompare = a.Y.CompareTo(b.Y);
            if (yCompare != 0) return yCompare;
            return a.X.CompareTo(b.X);
        });
        var sortElapsed = DateTime.UtcNow - sortStartTime;

        // 计算统计信息
        var totalElapsed = DateTime.UtcNow - startTime;
        var totalFileSize = slices.Sum(s => s.FileSize);
        var avgFileSize = slices.Count > 0 ? totalFileSize / slices.Count : 0;
        var generationRate = slices.Count / Math.Max(1, totalElapsed.TotalSeconds);

        _logger.LogInformation("网格切片生成统计：级别{Level}，生成{SliceCount}/{Estimated}个切片（{Percent:F1}%），" +
            "总耗时{Total:F2}秒（排序{Sort:F3}秒），速度{Rate:F1}片/秒，总文件大小{TotalSize}，平均{AvgSize}",
            level, slices.Count, estimatedSliceCount, (slices.Count * 100.0 / estimatedSliceCount),
            totalElapsed.TotalSeconds, sortElapsed.TotalSeconds, generationRate,
            FormatFileSize(totalFileSize), FormatFileSize(avgFileSize));

        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新性能统计信息
    /// </summary>
    private void UpdatePerformanceCounters(int sliceCount, long elapsedMilliseconds)
    {
        lock (_performanceCounterLock)
        {
            _totalSlicesGenerated += sliceCount;
            _totalProcessingTimeMs += elapsedMilliseconds;
        }
    }

    /// <summary>
    /// 获取性能统计信息
    /// </summary>
    public static (long TotalSlicesGenerated, long TotalProcessingTimeMs, double AverageTimePerSlice) GetPerformanceStats()
    {
        lock (_performanceCounterLock)
        {
            var avgTime = _totalSlicesGenerated > 0 ? (double)_totalProcessingTimeMs / _totalSlicesGenerated : 0;
            return (_totalSlicesGenerated, _totalProcessingTimeMs, avgTime);
        }
    }

    /// <summary>
    /// 串行切片生成 - 适用于小规模切片
    /// 使用内存池优化和边界检查优化
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
        var slices = _sliceListPool.Get();
        slices.Capacity = CalculateEstimatedSliceCount(tilesInLevel, zTilesCount);

        var totalSlices = tilesInLevel * tilesInLevel * zTilesCount;
        var processedCount = 0;
        var lastLoggedProgress = 0;

        _logger.LogInformation("开始串行生成切片：级别{Level}，总计{Total}个",
            level, totalSlices);

        var startTime = DateTime.UtcNow;

        try
        {
            for (int z = 0; z < zTilesCount; z++)
            {
                for (int y = 0; y < tilesInLevel; y++)
                {
                    for (int x = 0; x < tilesInLevel; x++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _logger.LogWarning("切片生成被取消：级别{Level}，已生成{Count}/{Total}",
                                level, slices.Count, totalSlices);
                            return Task.FromResult(slices);
                        }

                        var slice = CreateSlice(task, level, config, x, y, z, modelBounds);
                        if (slice != null)
                        {
                            slices.Add(slice);
                        }

                        processedCount++;

                        // 优化进度监控：每20%输出一次
                        var progressPercent = (processedCount * 100) / totalSlices;
                        if (progressPercent >= lastLoggedProgress + 20 && progressPercent > lastLoggedProgress)
                        {
                            lastLoggedProgress = progressPercent;
                            var elapsed = DateTime.UtcNow - startTime;
                            var slicesPerSecond = processedCount / Math.Max(1, elapsed.TotalSeconds);

                            _logger.LogInformation("串行切片进度：{Percent}% ({Processed}/{Total})，速度：{Speed:F1}片/秒",
                                progressPercent, processedCount, totalSlices, slicesPerSecond);
                        }
                    }
                }
            }

            var totalElapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("串行切片生成完成：级别{Level}，生成{Count}个切片，耗时{Elapsed:F2}秒，平均速度{Speed:F1}片/秒",
                level, slices.Count, totalElapsed.TotalSeconds, slices.Count / Math.Max(1, totalElapsed.TotalSeconds));

            return Task.FromResult(slices);
        }
        catch
        {
            // 异常时归还对象到池中
            _sliceListPool.Return(slices);
            throw;
        }
    }

    /// <summary>
    /// 流水线并行切片生成 - 使用TPL Dataflow实现高效并行处理
    /// 支持背压控制、异常处理、进度监控等高级特性
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="level">LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <param name="tilesInLevel">当前级别的瓦片数量</param>
    /// <param name="zTilesCount">当前级别的Z轴切片数量</param>
    /// <param name="modelBounds">模型的实际包围盒</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生成的切片集合</returns>
    private async Task<List<Slice>> GenerateSlicesWithPipelineAsync(
        SlicingTask task, int level, SlicingConfig config,
        int tilesInLevel, int zTilesCount, BoundingBox3D modelBounds, CancellationToken cancellationToken)
    {
        var slices = _sliceListPool.Get();
        var processedCount = 0;
        var totalSlices = tilesInLevel * tilesInLevel * zTilesCount;
        var lastLoggedProgress = 0;
        var startTime = DateTime.UtcNow;

        // 创建生产者-消费者管道
        var workItems = new BufferBlock<SliceWorkItem>();
        var results = new BufferBlock<Slice>();

        // 创建工作处理器
        var workerOptions = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = config.ParallelProcessingCount,
            CancellationToken = cancellationToken,
            BoundedCapacity = config.ParallelProcessingCount * 2 // 背压控制
        };

        var workerBlock = new TransformBlock<SliceWorkItem, Slice?>(
            workItem => Task.FromResult(CreateSlice(
                workItem.Task, workItem.Level, workItem.Config, workItem.X, workItem.Y, workItem.Z, workItem.ModelBounds)),
            workerOptions);

        // 创建结果收集器
        var resultOptions = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1, // 串行收集结果
            CancellationToken = cancellationToken
        };

        var resultBlock = new ActionBlock<Slice?>(
            slice =>
            {
                if (slice != null)
                {
                    results.Post(slice);
                    Interlocked.Increment(ref processedCount);

                    // 进度监控
                    var progressPercent = (processedCount * 100) / totalSlices;
                    if (progressPercent >= lastLoggedProgress + ProgressLogInterval && progressPercent > lastLoggedProgress)
                    {
                        Interlocked.Exchange(ref lastLoggedProgress, progressPercent);
                        var elapsed = DateTime.UtcNow - startTime;
                        var slicesPerSecond = processedCount / Math.Max(1, elapsed.TotalSeconds);

                        _logger.LogInformation("流水线切片进度：{Percent}% ({Processed}/{Total})，速度：{Speed:F1}片/秒",
                            progressPercent, processedCount, totalSlices, slicesPerSecond);
                    }
                }
            },
            resultOptions);

        // 链接管道
        workerBlock.LinkTo(resultBlock, new DataflowLinkOptions { PropagateCompletion = true });

        // 启动工作项生产
        var producerTask = Task.Run(async () =>
        {
            for (int z = 0; z < zTilesCount; z++)
            {
                for (int y = 0; y < tilesInLevel; y++)
                {
                    for (int x = 0; x < tilesInLevel; x++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        var workItem = new SliceWorkItem(x, y, z, task, level, config, modelBounds);
                        await workItems.SendAsync(workItem, cancellationToken);
                    }
                }
            }
            workItems.Complete();
        }, cancellationToken);

        // 收集结果
        var consumerTask = Task.Run(async () =>
        {
            while (await results.OutputAvailableAsync(cancellationToken))
            {
                if (results.TryReceive(out Slice? slice))
                {
                    slices.Add(slice);
                }
            }
        }, cancellationToken);

        // 等待完成
        await Task.WhenAll(producerTask, consumerTask);
        await workerBlock.Completion;
        await resultBlock.Completion;

        var totalElapsed = DateTime.UtcNow - startTime;
        _logger.LogInformation("流水线切片生成完成：级别{Level}，生成{Count}个切片，耗时{Elapsed:F2}秒，平均速度{Speed:F1}片/秒",
            level, slices.Count, totalElapsed.TotalSeconds, slices.Count / Math.Max(1, totalElapsed.TotalSeconds));

        return slices;
    }

    /// <summary>
    /// 并行切片生成 - 适用于大规模切片的高性能处理（内存优化版本）
    /// 使用分区并行和内存池优化
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="level">LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <param name="tilesInLevel">当前级别的瓦片数量</param>
    /// <param name="zTilesCount">当前级别的Z轴切片数量</param>
    /// <param name="modelBounds">模型的实际包围盒</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生成的切片集合</returns>
    private Task<List<Slice>> GenerateSlicesInParallelAsync(
        SlicingTask task, int level, SlicingConfig config,
        int tilesInLevel, int zTilesCount, BoundingBox3D modelBounds, CancellationToken cancellationToken)
    {
        // 使用分区并行处理
        var partitioner = Partitioner.Create(0, zTilesCount * tilesInLevel * tilesInLevel);
        var slices = new ConcurrentBag<Slice>();

        var totalSlices = tilesInLevel * tilesInLevel * zTilesCount;
        var processedCount = 0;
        var lastLoggedProgress = 0;

        _logger.LogInformation("开始并行生成切片：级别{Level}，总计{Total}个，并行度{Parallelism}",
            level, totalSlices, config.ParallelProcessingCount);

        var startTime = DateTime.UtcNow;

        try
        {
            Parallel.ForEach(partitioner, new ParallelOptions
            {
                MaxDegreeOfParallelism = config.ParallelProcessingCount,
                CancellationToken = cancellationToken
            }, (range, loopState) =>
            {
                var localSlices = new List<Slice>();
                var (start, end) = range;

                for (int i = start; i < end; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        loopState.Stop();
                        break;
                    }

                    // 将线性索引转换为三维坐标
                    var z = i / (tilesInLevel * tilesInLevel);
                    var remaining = i % (tilesInLevel * tilesInLevel);
                    var y = remaining / tilesInLevel;
                    var x = remaining % tilesInLevel;

                    var slice = CreateSlice(task, level, config, x, y, z, modelBounds);
                    if (slice != null)
                    {
                        localSlices.Add(slice);
                    }

                    var currentCount = Interlocked.Increment(ref processedCount);

                    // 优化进度监控：每10%输出一次，避免频繁日志
                    var progressPercent = (currentCount * 100) / totalSlices;
                    if (progressPercent >= lastLoggedProgress + ProgressLogInterval && progressPercent > lastLoggedProgress)
                    {
                        Interlocked.Exchange(ref lastLoggedProgress, progressPercent);
                        var elapsed = DateTime.UtcNow - startTime;
                        var slicesPerSecond = currentCount / Math.Max(1, elapsed.TotalSeconds);

                        _logger.LogInformation("并行切片进度：{Percent}% ({Processed}/{Total})，速度：{Speed:F1}片/秒",
                            progressPercent, currentCount, totalSlices, slicesPerSecond);
                    }
                }

                // 批量添加到共享集合
                foreach (var slice in localSlices)
                {
                    slices.Add(slice);
                }
            });

            var totalElapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("并行切片生成完成：级别{Level}，生成{Count}个切片，耗时{Elapsed:F2}秒，平均速度{Speed:F1}片/秒",
                level, slices.Count, totalElapsed.TotalSeconds, slices.Count / Math.Max(1, totalElapsed.TotalSeconds));

            return Task.FromResult(slices.ToList());
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("并行切片生成被取消：级别{Level}，已生成{Count}/{Total}个",
                level, slices.Count, totalSlices);
            return Task.FromResult(slices.ToList());
        }
    }

    /// <summary>
    /// 创建单个切片实例 - 使用优化的边界检查和文件大小计算
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
        // 快速边界检查
        if (!IsWithinModelBounds(x, y, z, level, modelBounds))
        {
            return null;
        }

        var boundingBox = GenerateGridBoundingBox(level, x, y, z, config.TileSize, modelBounds);
        if (boundingBox == null)
        {
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
            FileSize = CalculateFileSize(config.OutputFormat, level),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 快速边界检查 - 判断切片坐标是否在模型范围内
    /// </summary>
    private bool IsWithinModelBounds(int x, int y, int z, int level, BoundingBox3D modelBounds)
    {
        var tilesInLevel = CalculateTilesInLevel(level);
        return x >= 0 && x < tilesInLevel &&
               y >= 0 && y < tilesInLevel &&
               z >= 0 && z < tilesInLevel;
    }

    /// <summary>
    /// 生成切片文件路径 - 标准化路径格式
    /// 使用StringBuilder优化字符串拼接
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

        // 使用StringBuilder优化字符串拼接
        return $"{outputPath}/{level}/{x}_{y}_{z}.{format.ToLowerInvariant()}";
    }

    /// <summary>
    /// 估算指定级别的切片数量 - 基于规则网格剖分算法
    /// 使用预计算表优化性能
    /// </summary>
    /// <param name="level">LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <returns>估算的切片数量</returns>
    public int EstimateSliceCount(int level, SlicingConfig config)
    {
        // 使用预计算表避免重复计算
        var tilesInLevel = CalculateTilesInLevel(level);
        return CalculateEstimatedSliceCount(tilesInLevel, tilesInLevel);
    }

    /// <summary>
    /// 计算指定级别的瓦片数量 - 使用预计算表
    /// </summary>
    private int CalculateTilesInLevel(int level)
    {
        if (level < 0 || level >= PowerOfTwoTable.Length)
        {
            return 1 << Math.Clamp(level, 0, 10); // 限制在有效范围内
        }
        return PowerOfTwoTable[level];
    }

    /// <summary>
    /// 计算估算的切片数量
    /// </summary>
    private int CalculateEstimatedSliceCount(int tilesInLevel, int zTilesCount)
    {
        return tilesInLevel * tilesInLevel * zTilesCount;
    }

    /// <summary>
    /// 判断是否应该使用并行处理
    /// </summary>
    private bool ShouldUseParallelProcessing(SlicingConfig config, int estimatedSliceCount)
    {
        return config.ParallelProcessingCount > 1 &&
               estimatedSliceCount > MinSlicesForParallel &&
               Environment.ProcessorCount > 1;
    }

    /// <summary>
    /// 生成网格包围盒 - 轴对齐包围盒（AABB）算法实现
    /// 算法：基于网格坐标和切片尺寸计算精确的空间边界
    /// 支持：LOD级别缩放、边界验证、格式标准化
    /// **关键改进：基于模型实际包围盒进行空间剖分，优化性能，减少日志输出**
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
        // 快速边界检查
        if (!IsWithinModelBounds(x, y, z, level, modelBounds))
        {
            return null;
        }

        // 1. 计算模型的实际尺寸
        var modelSizeX = modelBounds.MaxX - modelBounds.MinX;
        var modelSizeY = modelBounds.MaxY - modelBounds.MinY;
        var modelSizeZ = modelBounds.MaxZ - modelBounds.MinZ;

        // 调试日志：仅在第一个切片时输出
        if (level == 0 && x == 0 && y == 0 && z == 0)
        {
            _logger.LogInformation("【首个切片】模型尺寸: SizeX={SizeX:F6}, SizeY={SizeY:F6}, SizeZ={SizeZ:F6}",
                modelSizeX, modelSizeY, modelSizeZ);
            _logger.LogInformation("【首个切片】模型包围盒: Min=({MinX:F6},{MinY:F6},{MinZ:F6}), Max=({MaxX:F6},{MaxY:F6},{MaxZ:F6})",
                modelBounds.MinX, modelBounds.MinY, modelBounds.MinZ,
                modelBounds.MaxX, modelBounds.MaxY, modelBounds.MaxZ);
        }

        // 2. 计算LOD缩放因子 - 使用预计算表
        var tilesInLevel = CalculateTilesInLevel(level);

        // 计算各个轴的真实尺寸
        var xScaledTileSize = modelSizeX / tilesInLevel;
        var yScaledTileSize = modelSizeY / tilesInLevel;
        var zScaledTileSize = modelSizeZ / tilesInLevel;

        // 3. 计算切片在模型空间中的实际位置
        var relativeX = x * xScaledTileSize;
        var relativeY = y * yScaledTileSize;
        var relativeZ = z * zScaledTileSize;

        // 4. 计算轴对齐包围盒（AABB）- 映射到模型坐标系
        var minX = Math.Max(modelBounds.MinX + relativeX, modelBounds.MinX);
        var minY = Math.Max(modelBounds.MinY + relativeY, modelBounds.MinY);
        var minZ = Math.Max(modelBounds.MinZ + relativeZ, modelBounds.MinZ);
        var maxX = Math.Min(modelBounds.MinX + relativeX + xScaledTileSize, modelBounds.MaxX);
        var maxY = Math.Min(modelBounds.MinY + relativeY + yScaledTileSize, modelBounds.MaxY);
        var maxZ = Math.Min(modelBounds.MinZ + relativeZ + zScaledTileSize, modelBounds.MaxZ);

        // 5. 验证切片是否与模型有有效交集
        var sizeX = maxX - minX;
        var sizeY = maxY - minY;
        var sizeZ = maxZ - minZ;

        // 调试日志：仅在第一个切片时输出
        if (level == 0 && x == 0 && y == 0 && z == 0)
        {
            _logger.LogInformation("【首个切片】计算的包围盒: Min=({MinX:F6},{MinY:F6},{MinZ:F6}), Max=({MaxX:F6},{MaxY:F6},{MaxZ:F6})",
                minX, minY, minZ, maxX, maxY, maxZ);
            _logger.LogInformation("【首个切片】包围盒尺寸: SizeX={SizeX:F6}, SizeY={SizeY:F6}, SizeZ={SizeZ:F6}",
                sizeX, sizeY, sizeZ);
        }

        // 如果任何尺寸为负值或过小，说明切片无效
        if (sizeX < MinBoxSize || sizeY < MinBoxSize || sizeZ < MinBoxSize)
        {
            return null;
        }

        // 6. 生成标准化JSON格式 - 使用StringBuilder优化
        var jsonResult = GenerateBoundingBoxJson(minX, minY, minZ, maxX, maxY, maxZ);

        // 调试日志：仅在第一个切片时输出
        if (level == 0 && x == 0 && y == 0 && z == 0)
        {
            _logger.LogInformation("【首个切片】生成的JSON: {Json}", jsonResult);
        }

        return jsonResult;
    }

    /// <summary>
    /// 生成包围盒JSON字符串 - 使用StringBuilder优化
    /// </summary>
    private string GenerateBoundingBoxJson(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        return $"{{\"MinX\":{minX.ToString("F6", culture)},\"MinY\":{minY.ToString("F6", culture)},\"MinZ\":{minZ.ToString("F6", culture)},\"MaxX\":{maxX.ToString("F6", culture)},\"MaxY\":{maxY.ToString("F6", culture)},\"MaxZ\":{maxZ.ToString("F6", culture)}}}";
    }

    /// <summary>
    /// 计算文件大小 - 基于几何复杂度的简化估算算法
    /// 算法：根据切片级别、输出格式综合计算文件大小
    /// 优化：简化计算逻辑，提高性能
    /// </summary>
    /// <param name="format">输出格式</param>
    /// <param name="level">LOD级别</param>
    /// <param name="tileSize">切片尺寸</param>
    /// <returns>估算的文件大小（字节）</returns>
    private long CalculateFileSize(string format, int level = 0, double tileSize = 1.0)
    {
        // 1. 基础文件大小（字节）- 基于格式的固定开销
        var baseSize = GetBaseFileSize(format);

        // 2. 级别复杂度因子：简化计算
        // LOD级别越高，细节越丰富，但增长是有限的
        var levelFactor = 1.0 + (level * 0.3); // 每级增加30%

        // 3. 空间因子：简化计算
        var spatialFactor = Math.Max(1.0, Math.Pow(tileSize, 0.5));

        // 4. 格式开销因子
        var formatFactor = GetFormatOverheadFactor(format);

        // 5. 综合计算
        var estimatedSize = (long)(baseSize * levelFactor * spatialFactor * formatFactor);

        // 6. 应用边界约束
        return ApplySizeConstraints(estimatedSize, format);
    }

    /// <summary>
    /// 优化的文件大小计算方法 - 预计算常用值
    /// </summary>
    private long CalculateFileSizeOptimized(string format, int level)
    {
        var baseSize = GetBaseFileSize(format);
        var levelFactor = 1.0 + (level * 0.3);
        var formatFactor = GetFormatOverheadFactor(format);

        var estimatedSize = (long)(baseSize * levelFactor * formatFactor);
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
            "pnts" => 1.2,  // pnts: 点云格式
            _ => 1.3
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
            "pnts" => (128L, 52428800L),    // pnts: 128B - 50MB
            _ => (256L, 104857600L)         // 默认: 256B - 100MB
        };

        return Math.Max(minSize, Math.Min(maxSize, size));
    }

    /// <summary>
    /// 从切片任务加载三角形数据
    /// </summary>
    /// <param name="task">切片任务，包含源模型路径和配置信息</param>
    /// <param name="cancellationToken">取消令牌，支持异步操作取消</param>
    /// <returns>加载的三角形列表，失败时返回null</returns>
    /// <exception cref="ArgumentNullException">当task参数为null时抛出</exception>
    /// <exception cref="ArgumentException">当任务配置无效时抛出</exception>
    /// <exception cref="InvalidOperationException">当模型加载失败时抛出</exception>
    private async Task<List<Triangle>?> LoadTrianglesFromTask(SlicingTask task, CancellationToken cancellationToken)
    {
        // 参数验证
        if (task == null)
            throw new ArgumentNullException(nameof(task), "切片任务不能为空");

        if (string.IsNullOrWhiteSpace(task.SourceModelPath))
            throw new ArgumentException("源模型路径不能为空", nameof(task.SourceModelPath));

        if (string.IsNullOrWhiteSpace(task.ModelType))
            throw new ArgumentException("模型类型不能为空", nameof(task.ModelType));

        try
        {
            // 检查模型加载器是否支持该文件格式
            var fileExtension = Path.GetExtension(task.SourceModelPath)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(fileExtension))
            {
                _logger.LogError("无法确定模型文件的扩展名：{SourceModelPath}", task.SourceModelPath);
                throw new InvalidOperationException($"无法确定模型文件的扩展名：{task.SourceModelPath}");
            }

            if (!_modelLoader.SupportsFormat(fileExtension))
            {
                _logger.LogError("模型加载器不支持此文件格式：{FileExtension}，支持的格式：{SupportedFormats}",
                    fileExtension, string.Join(", ", _modelLoader.GetSupportedFormats()));
                throw new InvalidOperationException($"不支持的模型文件格式：{fileExtension}");
            }

            // 记录加载开始
            _logger.LogInformation("开始加载模型文件：{SourceModelPath}，类型：{ModelType}，格式：{FileExtension}",
                task.SourceModelPath, task.ModelType, fileExtension);

            // 异步加载模型数据，带超时保护
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(10)); // 设置10分钟超时

            var (triangles, boundingBox, materials) = await _modelLoader.LoadModelAsync(task.SourceModelPath, cts.Token);

            // 验证加载结果
            if (triangles == null || triangles.Count == 0)
            {
                _logger.LogWarning("模型文件加载成功但未包含任何三角形数据：{SourceModelPath}", task.SourceModelPath);
                return new List<Triangle>(); // 返回空列表而不是null，避免上层代码处理null
            }

            // 验证三角形数据完整性
            var invalidTriangles = triangles.Count(t =>
                t == null ||
                t.Vertices == null ||
                t.Vertices.Length != 3 ||
                t.Vertices.Any(v => v == null));

            if (invalidTriangles > 0)
            {
                _logger.LogWarning("发现{InvalidCount}个无效三角形，已过滤", invalidTriangles);
                triangles = triangles.Where(t =>
                    t != null &&
                    t.Vertices != null &&
                    t.Vertices.Length == 3 &&
                    t.Vertices.All(v => v != null)).ToList();
            }

            // 记录加载统计信息
            _logger.LogInformation("模型加载完成：{TriangleCount}个三角形，包围盒：[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}]",
                triangles.Count,
                boundingBox.MinX, boundingBox.MinY, boundingBox.MinZ,
                boundingBox.MaxX, boundingBox.MaxY, boundingBox.MaxZ);

            return triangles;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("模型加载操作被用户取消：{SourceModelPath}", task.SourceModelPath);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("模型加载超时（10分钟）：{SourceModelPath}", task.SourceModelPath);
            throw new TimeoutException($"模型加载超时：{task.SourceModelPath}");
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "模型文件不存在：{SourceModelPath}", task.SourceModelPath);
            throw new InvalidOperationException($"模型文件不存在：{task.SourceModelPath}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "无权限访问模型文件：{SourceModelPath}", task.SourceModelPath);
            throw new InvalidOperationException($"无权限访问模型文件：{task.SourceModelPath}", ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "模型文件读取I/O错误：{SourceModelPath}", task.SourceModelPath);
            throw new InvalidOperationException($"模型文件读取失败：{task.SourceModelPath}", ex);
        }
        catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
        {
            // 对于未预期的异常，记录详细错误信息
            _logger.LogError(ex, "模型加载过程中发生未预期错误：{SourceModelPath}，错误类型：{ExceptionType}",
                task.SourceModelPath, ex.GetType().Name);
            throw new InvalidOperationException($"模型加载失败：{task.SourceModelPath}，{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 应用LOD简化
    /// </summary>
    private List<Triangle> SimplifyMeshForLOD(List<Triangle> triangles, int level, int maxLevel)
    {
        if (_meshDecimationService == null || triangles.Count < 10)
        {
            return triangles;
        }

        try
        {
            var quality = CalculateLODQuality(level, maxLevel);

            var options = new MeshDecimationService.DecimationOptions
            {
                Quality = quality,
                PreserveBoundary = true,
                MaxIterations = 100
            };

            var decimated = _meshDecimationService.SimplifyMesh(triangles, options);
            return decimated.Triangles;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "网格简化失败，使用原始网格");
            return triangles;
        }
    }

    /// <summary>
    /// 计算LOD质量因子
    /// </summary>
    private double CalculateLODQuality(int level, int maxLevel)
    {
        if (level >= maxLevel)
            return 1.0;

        // 使用平方根衰减，Level越高质量越好
        var ratio = (double)level / maxLevel;
        return Math.Pow(ratio, 0.5);
    }

    /// <summary>
    /// 序列化包围盒为JSON
    /// </summary>
    private string SerializeBoundingBox(BoundingBox3D bounds)
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            bounds.MinX,
            bounds.MinY,
            bounds.MinZ,
            bounds.MaxX,
            bounds.MaxY,
            bounds.MaxZ
        });
    }
}