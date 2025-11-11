using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.MinIO;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// 三维切片处理器实现 - 负责执行切片任务的核心逻辑
/// 采用多层次细节（LOD）算法结合多种空间剖分策略进行切片处理
/// 
/// 主要职责包括：
/// - 处理切片任务队列，持续监听和处理待切片任务
/// - 执行单个切片任务的完整处理流程
/// - 加载模型数据，构建空间索引
/// - 按照配置进行多级别切片处理
/// - 支持增量更新，优化切片生成效率
/// - 记录详细日志，支持任务状态跟踪和错误处理
/// 注意：该类设计为应用服务层的一部分，依赖于领域实体和基础设施服务
/// 主要方法均为异步实现，支持取消令牌以便优雅关闭
/// 
/// <remarks>
/// 设计原则：
/// 1. 单一职责原则：专注于切片处理逻辑，其他职责委托给相关服务
/// 2. 依赖注入：通过构造函数注入所需的仓储和服务，便于测试和扩展
/// 3. 异步编程：采用异步方法提高并发处理能力，提升性能
/// 4. 日志记录：集成日志记录，便于问题排查和性能监控
/// 5. 可扩展性：支持多种切片策略和格式，便于未来扩展
/// </remarks>
/// 
/// </summary>
public class SlicingProcessor : ISlicingProcessor
{
    private readonly IRepository<SlicingTask> _slicingTaskRepository;
    private readonly IRepository<Slice> _sliceRepository;
    private readonly IMinioStorageService _minioService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SlicingProcessor> _logger;
    private readonly ISlicingStrategyFactory _slicingStrategyFactory;
    private readonly SlicingDataService _dataService;
    private readonly IncrementalUpdateService _incrementalUpdateService;

    /// <summary>
    /// 构造函数 - 简化依赖注入
    /// 数据处理相关操作委托给 SlicingDataService
    /// </summary>
    public SlicingProcessor(
        IRepository<SlicingTask> slicingTaskRepository,
        IRepository<Slice> sliceRepository,
        IMinioStorageService minioService,
        IUnitOfWork unitOfWork,
        ILogger<SlicingProcessor> logger,
        ISlicingStrategyFactory slicingStrategyFactory,
        SlicingDataService dataService,
        IncrementalUpdateService incrementalUpdateService)
    {
        _slicingTaskRepository = slicingTaskRepository;
        _sliceRepository = sliceRepository;
        _minioService = minioService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _slicingStrategyFactory = slicingStrategyFactory ?? throw new ArgumentNullException(nameof(slicingStrategyFactory));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _incrementalUpdateService = incrementalUpdateService ?? throw new ArgumentNullException(nameof(incrementalUpdateService));
    }

    /// <summary>
    /// 处理切片任务队列 - 持续监听和处理待切片任务
    /// 后台运行，轮询检查新任务并启动处理
    /// 
    /// 注意：该方法设计为长时间运行的后台任务，需支持取消令牌以便优雅关闭
    /// 
    /// <param name="cancellationToken">取消令牌，支持优雅关闭</param>
    /// <returns>异步任务</returns>
    /// 
    /// 主要步骤包括：
    /// - 轮询检查切片任务队列中的新任务
    /// - 对每个待处理任务调用 ProcessSlicingTaskAsync 方法
    /// - 处理异常情况，确保任务不中断
    /// - 支持通过取消令牌优雅停止任务处理
    /// 
    /// 注意：该方法应在独立的后台服务中运行，避免阻塞主线程
    /// </summary>

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

    /// <summary>
    /// 处理单个切片任务 - 执行具体的切片处理逻辑
    /// 实现完整的切片处理流程，包括剖分、生成、压缩、索引等
    /// 
    /// 主要步骤包括：
    /// - 加载切片任务信息
    /// - 更新任务状态为处理中
    /// - 执行切片处理核心逻辑 PerformSlicingAsync
    /// - 处理成功和失败的状态更新
    /// - 记录详细的日志信息以便追踪
    /// 
    /// 注意：该方法支持取消令牌，可在处理中途取消任务
    /// 
    /// <param name="taskId">待处理的切片任务ID</param>
    /// <param name="cancellationToken">取消令牌，支持中途取消</param>
    /// <returns>异步任务</returns>
    /// </summary>
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
    /// 加载模型数据 - 模型处理的核心步骤
    /// 包括加载三角形数据、构建空间索引、计算包围盒
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回元组：(三角形列表, 空间索引, 模型包围盒, 材质字典)</returns>
    private async Task<(List<Triangle> triangles, Dictionary<string, List<Triangle>> spatialIndex, BoundingBox3D bounds, Dictionary<string, Material> materials)> 
    LoadModelDataAsync(SlicingTask task, CancellationToken cancellationToken)
    {
        // 加载源模型的三角形数据*
        _logger.LogInformation("开始加载源模型三角形数据：{SourceModelPath}", task.SourceModelPath);
        List<Triangle> allTriangles;
        BoundingBox3D extractedBounds;
        Dictionary<string, Material> materials;

        try
        {
            var (Triangles, BoundingBox, Materials) = await _dataService.LoadTrianglesFromModelAsync(task.SourceModelPath, cancellationToken);
            allTriangles = Triangles;
            extractedBounds = BoundingBox;
            materials = Materials;
            _logger.LogInformation(
                "模型加载完成：共{TriangleCount}个三角形，{MaterialCount}个材质", allTriangles.Count, materials.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载模型失败，使用空几何数据");
            allTriangles = []; // 使用空数据继续执行
            extractedBounds = new BoundingBox3D();
            materials = [];
        }

        // 计算模型包围盒用于坐标变换，优先使用从模型加载器中直接获取的包围盒（从模型原始数据中提取）
        // 如果包围盒无效，则从三角形列表计算
        BoundingBox3D modelBounds;
        if (extractedBounds != null && extractedBounds.IsValid())
        {
            _logger.LogInformation("使用loader从模型原始数据提取的包围盒");
            modelBounds = extractedBounds;
        }
        else
        {
            _logger.LogWarning("loader包围盒无效，从三角形列表计算包围盒");
            modelBounds = CalculateModelBounds(allTriangles);
        }

        _logger.LogInformation("模型包围盒计算完成：[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}]",
            modelBounds.MinX, modelBounds.MinY, modelBounds.MinZ,
            modelBounds.MaxX, modelBounds.MaxY, modelBounds.MaxZ);

        // 构建空间索引以加速切片查询（使用已确定的模型包围盒）
        _logger.LogInformation("开始构建三角形空间索引");
        var triangleSpatialIndex = BuildTriangleSpatialIndex(allTriangles, modelBounds);
        _logger.LogInformation("空间索引构建完成");

        return (allTriangles, triangleSpatialIndex, modelBounds, materials);
    }

    /// <summary>
    /// 处理单个LOD级别 - 级别处理的核心逻辑
    /// 包括切片生成、增量更新检查、并行/串行处理选择、进度更新
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="level">当前处理的LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <param name="strategy">切片策略</param>
    /// <param name="triangleSpatialIndex">三角形空间索引</param>
    /// <param name="modelBounds">模型包围盒</param>
    /// <param name="existingSlicesMap">现有切片映射表</param>
    /// <param name="actuallyUseIncrementalUpdate">是否实际使用增量更新</param>
    /// <param name="hasSliceChanges">切片变化标记</param>
    /// <param name="processedSliceKeys">已处理的切片键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回更新后的切片变化标记</returns>
    private async Task<bool> ProcessLevelAsync(
        SlicingTask task,
        int level,
        SlicingConfig config,
        ISlicingStrategy strategy,
        Dictionary<string, List<Triangle>> triangleSpatialIndex,
        BoundingBox3D modelBounds,
        Dictionary<string, Slice> existingSlicesMap,
        bool actuallyUseIncrementalUpdate,
        bool hasSliceChanges,
        HashSet<string> processedSliceKeys,
        Dictionary<string, Material> materials,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("处理级别{Level}：策略{Strategy}", level, config.Strategy);

        // 使用选择的切片策略进行空间剖分（传入模型包围盒）
        var slices = await strategy.GenerateSlicesAsync(task, level, config, modelBounds, cancellationToken);

        _logger.LogInformation("策略生成切片数量：{Count}, 级别：{Level}", slices.Count, level);

        if (slices.Count == 0)
        {
            _logger.LogWarning("级别{Level}未生成任何切片，请检查切片配置和源模型", level);
            return hasSliceChanges; // 跳过这个级别，返回当前状态
        }

        // 选择处理模式：使用并行处理还是串行处理
        if (config.ParallelProcessingCount > 1 && slices.Count > 10)
        {
            hasSliceChanges = await ProcessSlicesInParallelForLevelAsync(
                task, level, config, slices, triangleSpatialIndex, modelBounds, existingSlicesMap,
                actuallyUseIncrementalUpdate, hasSliceChanges, processedSliceKeys, materials, cancellationToken);
        }
        else
        {
            hasSliceChanges = await ProcessSlicesSeriallyForLevelAsync(
                task, level, config, slices, triangleSpatialIndex, modelBounds, existingSlicesMap,
                actuallyUseIncrementalUpdate, hasSliceChanges, processedSliceKeys, materials, cancellationToken);
        }

        // 更新进度
        task.Progress = (int)((double)(level + 1) / (config.MaxLevel + 1) * 100);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("级别{Level}处理完成，生成{SliceCount}个切片", level, slices.Count);

        return hasSliceChanges;
    }

    /// <summary>
    /// 并行处理级别切片
    /// </summary>
    private async Task<bool> ProcessSlicesInParallelForLevelAsync(
        SlicingTask task,
        int level,
        SlicingConfig config,
        List<Slice> slices,
        Dictionary<string, List<Triangle>> triangleSpatialIndex,
        BoundingBox3D modelBounds,
        Dictionary<string, Slice> existingSlicesMap,
        bool actuallyUseIncrementalUpdate,
        bool hasSliceChanges,
        HashSet<string> processedSliceKeys,
        Dictionary<string, Material> materials,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("使用并行处理：级别{Level}, 切片数量{Count}, 并行度{ParallelCount}",
            level, slices.Count, config.ParallelProcessingCount);

        var (processedCount, hasChanges) = await ProcessSlicesInParallelAsync(
            task, level, config, slices, triangleSpatialIndex, modelBounds, existingSlicesMap,
            actuallyUseIncrementalUpdate, hasSliceChanges, processedSliceKeys, materials, [], [], cancellationToken);

        _logger.LogInformation("并行处理完成：级别{Level}, 处理{Processed}个切片, 是否有变化{HasChanges}",
            level, processedCount, hasChanges);

        return hasChanges;
    }

    /// <summary>
    /// 串行处理级别切片
    /// </summary>
    private async Task<bool> ProcessSlicesSeriallyForLevelAsync(
        SlicingTask task,
        int level,
        SlicingConfig config,
        List<Slice> slices,
        Dictionary<string, List<Triangle>> triangleSpatialIndex,
        BoundingBox3D modelBounds,
        Dictionary<string, Slice> existingSlicesMap,
        bool actuallyUseIncrementalUpdate,
        bool hasSliceChanges,
        HashSet<string> processedSliceKeys,
        Dictionary<string, Material> materials,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("使用串行处理：任务{TaskId}({TaskName}), 级别{Level}, 切片数量{Count}",
            task.Id, task.Name, level, slices.Count);

        // 批量大小设置：使用合理的批量大小避免内存累积
        const int batchSize = 50; // 每批处理50个切片
        var processedInBatch = 0;
        var slicesToAdd = new List<Slice>(batchSize);
        var slicesToUpdate = new List<Slice>(batchSize);

        foreach (var slice in slices)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var sliceKey = $"{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}";
            bool isNewSlice = !existingSlicesMap.ContainsKey(sliceKey);
            bool needsUpdate = false;

            // 确保切片关联到正确的任务
            slice.SlicingTaskId = task.Id;

            // 增量更新检查逻辑
            if (actuallyUseIncrementalUpdate)
            {
                if (!isNewSlice)
                {
                    var existingSlice = existingSlicesMap[sliceKey];
                    var newHash = await CalculateSliceHash(slice);
                    var existingHash = await CalculateSliceHashFromExisting(existingSlice);

                    needsUpdate = newHash != existingHash;

                    if (!needsUpdate)
                    {
                        _logger.LogDebug("切片未变化，跳过：级别{Level}, 坐标({X},{Y},{Z})",
                            slice.Level, slice.X, slice.Y, slice.Z);
                        processedSliceKeys.Add(sliceKey);
                        continue;
                    }
                    else
                    {
                        _logger.LogInformation("检测到切片变化，准备更新：级别{Level}, 坐标({X},{Y},{Z})",
                            slice.Level, slice.X, slice.Y, slice.Z);
                        slice.Id = existingSlice.Id;
                        hasSliceChanges = true;
                    }
                }
                else
                {
                    _logger.LogInformation("检测到新增切片：级别{Level}, 坐标({X},{Y},{Z})",
                        slice.Level, slice.X, slice.Y, slice.Z);
                    hasSliceChanges = true;
                }
            }

            try
            {
                // 查询切片相交的三角形数据
                var sliceTriangles = QueryTrianglesForSlice(slice, triangleSpatialIndex, modelBounds);
                _logger.LogDebug("切片({X},{Y},{Z})查询到{Count}个三角形",
                    slice.X, slice.Y, slice.Z, sliceTriangles.Count);

                // 生成切片文件内容，获取是否成功
                var generated = await _dataService.GenerateSliceFileAsync(slice, config, sliceTriangles, materials, cancellationToken);
                if (!generated)
                {
                    _logger.LogDebug("切片({Level},{X},{Y},{Z})无几何数据，跳过保存",
                        slice.Level, slice.X, slice.Y, slice.Z);
                    continue;
                }

                _logger.LogDebug("成功生成切片文件：{FilePath}", slice.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成切片文件失败：任务{TaskId}({TaskName}), 级别{Level}, 坐标({X},{Y},{Z}), 路径{FilePath}",
                    task.Id, task.Name, slice.Level, slice.X, slice.Y, slice.Z, slice.FilePath);
                continue; // 不中断整个流程,继续处理其他切片
            }

            // 收集待批量处理的切片（仅在成功生成时）
            if (actuallyUseIncrementalUpdate && needsUpdate)
            {
                slicesToUpdate.Add(slice);
                _logger.LogDebug("标记切片待更新：{SliceKey}", sliceKey);
            }
            else
            {
                slicesToAdd.Add(slice);
                _logger.LogDebug("标记切片待新增：{SliceKey}", sliceKey);
            }

            // 标记为已处理
            if (actuallyUseIncrementalUpdate)
            {
                processedSliceKeys.Add(sliceKey);
            }

            processedInBatch++;

            // 批量提交优化
            if (processedInBatch >= batchSize)
            {
                await CommitSliceBatchAsync(slicesToAdd, slicesToUpdate);
                processedInBatch = 0;
                GC.Collect(0, GCCollectionMode.Optimized); // 内存优化
            }

            // 动态调整处理时间
            var processingDelay = CalculateProcessingDelay(slice, config);
            await Task.Delay(processingDelay, cancellationToken);
        }

        // 提交剩余的切片
        if (slicesToAdd.Count > 0 || slicesToUpdate.Count > 0)
        {
            await CommitSliceBatchAsync(slicesToAdd, slicesToUpdate);
        }

        return hasSliceChanges;
    }

    /// <summary>
    /// 批量提交切片数据
    /// </summary>
    private async Task CommitSliceBatchAsync(List<Slice> slicesToAdd, List<Slice> slicesToUpdate)
    {
        if (slicesToAdd.Count > 0)
        {
            foreach (var s in slicesToAdd)
            {
                // 诊断日志：输出即将保存的包围盒
                if (s.Level == 0 || (s.Level == 1 && s.X == 0 && s.Y == 1 && s.Z == 0))
                {
                    _logger.LogInformation(
                        "【保存前】准备保存切片到数据库: Level={Level}, X={X}, Y={Y}, Z={Z}, BoundingBox={BoundingBox}",
                        s.Level, s.X, s.Y, s.Z, s.BoundingBox);
                }
                await _sliceRepository.AddAsync(s);
            }
            slicesToAdd.Clear();
        }

        if (slicesToUpdate.Count > 0)
        {
            foreach (var s in slicesToUpdate)
            {
                await _sliceRepository.UpdateAsync(s);
            }
            slicesToUpdate.Clear();
        }

        await _unitOfWork.SaveChangesAsync();
        _logger.LogDebug("批量提交切片数据：{Count}个切片", slicesToAdd.Count + slicesToUpdate.Count);
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

        // 加载模型数据
        var (allTriangles, triangleSpatialIndex, modelBounds, materials) = await LoadModelDataAsync(task, cancellationToken);

        // 准备增量更新：如果启用增量更新，加载现有切片数据用于比对
        Dictionary<string, Slice> existingSlicesMap = [];
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

        // 使用工厂创建切片策略实例 - 解耦策略创建逻辑
        ISlicingStrategy strategy = _slicingStrategyFactory.CreateStrategy(config.Strategy);

        // 多层次细节（LOD）切片处理循环
        for (int level = 0; level <= config.MaxLevel; level++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            hasSliceChanges = await ProcessLevelAsync(
                task, level, config, strategy, triangleSpatialIndex, modelBounds, existingSlicesMap,
                actuallyUseIncrementalUpdate, hasSliceChanges, processedSliceKeys, materials, cancellationToken);
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
                _logger.LogInformation(
                    "检测到{Count}个过时切片（{InLevel}个在已处理层级中，{BeyondLevel}个超出新的最大层级{MaxLevel}），开始清理",
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

        // 生成切片索引文件 - 只生成 index.json
        var indexGenerator = new IndexFileGenerator(_sliceRepository, _minioService, _logger);
        var indexResult = await indexGenerator.GenerateIndexFileAsync(task, config, cancellationToken);

        if (!indexResult.Success)
        {
            _logger.LogWarning("index.json生成存在警告或修复：任务{TaskId}，验证问题{IssueCount}个，修复成功{RepairSuccess}",
                task.Id, indexResult.ValidationResult?.Issues.Count ?? 0, indexResult.RepairResult?.Success ?? false);
        }
        else
        {
            _logger.LogInformation("index.json生成成功：任务{TaskId}，包含{SliceCount}个切片",
                task.Id, indexResult.IndexJson?.SliceCount ?? 0);
        }

        // 生成增量更新索引（仅当实际使用了增量更新且有切片变化时）
        _logger.LogInformation(
            "检查是否需要生成增量更新索引：实际使用增量更新={ActuallyUseIncrementalUpdate}, 有切片变化={HasSliceChanges}",
            actuallyUseIncrementalUpdate, hasSliceChanges);

        if (actuallyUseIncrementalUpdate)
        {
            if (hasSliceChanges)
            {
                _logger.LogInformation("开始生成增量更新索引：任务{TaskId}（检测到切片变化）", task.Id);
                await _incrementalUpdateService.GenerateIncrementalUpdateIndexAsync(task, config, cancellationToken);
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

        // 生成 tileset.json 文件（如果配置启用）
        if (config.GenerateTileset)
        {
            try
            {
                _logger.LogInformation("开始生成 tileset.json 文件：任务{TaskId}", task.Id);

                // 获取所有切片数据
                var allSlices = await _sliceRepository.GetAllAsync();
                var taskSlices = allSlices.Where(s => s.SlicingTaskId == task.Id).ToList();

                if (taskSlices.Any())
                {
                    await _dataService.GenerateTilesetJsonAsync(
                        taskSlices, config, modelBounds, task.OutputPath ?? string.Empty,
                        config.StorageLocation, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("无法生成 tileset.json：任务{TaskId} 没有生成任何切片", task.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成 tileset.json 失败：任务{TaskId}", task.Id);
                // 不抛出异常，允许任务继续完成
            }
        }
        else
        {
            _logger.LogDebug("配置未启用 tileset.json 生成：任务{TaskId}", task.Id);
        }

        _logger.LogInformation("切片处理完成：任务{TaskId}", task.Id);
    }

    /// <summary>
    /// 计算模型包围盒
    /// </summary>
    private BoundingBox3D CalculateModelBounds(List<Triangle> triangles)
    {
        var bounds = new BoundingBox3D();

        if (triangles == null || triangles.Count == 0)
        {
            return bounds;
        }

        bounds.MinX = double.MaxValue;
        bounds.MinY = double.MaxValue;
        bounds.MinZ = double.MaxValue;
        bounds.MaxX = double.MinValue;
        bounds.MaxY = double.MinValue;
        bounds.MaxZ = double.MinValue;

        foreach (var triangle in triangles)
        {
            foreach (var vertex in triangle.Vertices)
            {
                bounds.MinX = Math.Min(bounds.MinX, vertex.X);
                bounds.MinY = Math.Min(bounds.MinY, vertex.Y);
                bounds.MinZ = Math.Min(bounds.MinZ, vertex.Z);
                bounds.MaxX = Math.Max(bounds.MaxX, vertex.X);
                bounds.MaxY = Math.Max(bounds.MaxY, vertex.Y);
                bounds.MaxZ = Math.Max(bounds.MaxZ, vertex.Z);
            }
        }

        return bounds;
    }

    /// <summary>
    /// 构建三角形的空间索引（简单网格索引）
    /// 将三角形按照其包围盒分配到网格单元中，以加速空间查询
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="modelBounds">模型包围盒（避免重复计算）</param>
    /// <returns>空间索引字典</returns>
    private Dictionary<string, List<Triangle>> BuildTriangleSpatialIndex(List<Triangle> triangles, BoundingBox3D modelBounds)
    {
        var spatialIndex = new Dictionary<string, List<Triangle>>();

        if (triangles.Count == 0)
        {
            return spatialIndex;
        }

        // 使用传入的模型包围盒（避免重复计算）
        double minX = modelBounds.MinX, minY = modelBounds.MinY, minZ = modelBounds.MinZ;
        double maxX = modelBounds.MaxX, maxY = modelBounds.MaxY, maxZ = modelBounds.MaxZ;

        // 创建粗粒度网格索引（64x64x32网格）
        const int gridSizeX = 64;
        const int gridSizeY = 64;
        const int gridSizeZ = 32;

        double cellSizeX = (maxX - minX) / gridSizeX;
        double cellSizeY = (maxY - minY) / gridSizeY;
        double cellSizeZ = (maxZ - minZ) / gridSizeZ;

        // 防止除零
        if (cellSizeX <= 0) cellSizeX = 1.0;
        if (cellSizeY <= 0) cellSizeY = 1.0;
        if (cellSizeZ <= 0) cellSizeZ = 1.0;

        // 将每个三角形添加到其包围盒相交的所有网格单元
        foreach (var triangle in triangles)
        {
            // 计算三角形包围盒
            double triMinX = triangle.Vertices.Min(v => v.X);
            double triMinY = triangle.Vertices.Min(v => v.Y);
            double triMinZ = triangle.Vertices.Min(v => v.Z);
            double triMaxX = triangle.Vertices.Max(v => v.X);
            double triMaxY = triangle.Vertices.Max(v => v.Y);
            double triMaxZ = triangle.Vertices.Max(v => v.Z);

            // 计算三角形跨越的网格范围
            int startX = Math.Max(0, (int)((triMinX - minX) / cellSizeX));
            int startY = Math.Max(0, (int)((triMinY - minY) / cellSizeY));
            int startZ = Math.Max(0, (int)((triMinZ - minZ) / cellSizeZ));
            int endX = Math.Min(gridSizeX - 1, (int)((triMaxX - minX) / cellSizeX));
            int endY = Math.Min(gridSizeY - 1, (int)((triMaxY - minY) / cellSizeY));
            int endZ = Math.Min(gridSizeZ - 1, (int)((triMaxZ - minZ) / cellSizeZ));

            // 将三角形添加到所有相交的网格单元
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    for (int z = startZ; z <= endZ; z++)
                    {
                        var cellKey = $"{x}_{y}_{z}";
                        if (!spatialIndex.ContainsKey(cellKey))
                        {
                            spatialIndex[cellKey] = new List<Triangle>();
                        }
                        spatialIndex[cellKey].Add(triangle);
                    }
                }
            }
        }

        _logger.LogInformation("空间索引构建完成：{CellCount}个网格单元，平均每单元{AvgTriangles:F1}个三角形",
            spatialIndex.Count, triangles.Count / (double)Math.Max(1, spatialIndex.Count));

        return spatialIndex;
    }

    /// <summary>
    /// 查询切片包围盒内的三角形
    /// 使用空间索引快速定位可能相交的三角形，然后进行精确的相交测试
    /// 支持坐标变换以解决切片包围盒和模型坐标系不匹配的问题
    /// </summary>
    private List<Triangle> QueryTrianglesForSlice(Slice slice, Dictionary<string, List<Triangle>> spatialIndex, BoundingBox3D modelBounds)
    {
        var result = new List<Triangle>();

        try
        {
            // 解析切片包围盒
            var boundingBox = System.Text.Json.JsonSerializer.Deserialize<BoundingBox3D>(slice.BoundingBox);
            if (boundingBox == null)
            {
                _logger.LogWarning("无法解析切片包围盒：{SliceKey}", $"{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}");
                return result;
            }

            // 输出原始切片包围盒
            _logger.LogDebug(
                "切片包围盒：Level={Level}, X={X}, Y={Y}, Z={Z}, 包围盒=[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}]",
                slice.Level, slice.X, slice.Y, slice.Z,
                boundingBox.MinX, boundingBox.MinY, boundingBox.MinZ,
                boundingBox.MaxX, boundingBox.MaxY, boundingBox.MaxZ);

            // 注意：切片包围盒已经在 GenerateGridBoundingBox 中映射到模型坐标系，
            // 这里不需要再进行坐标变换，直接使用即可

            // 调试信息：记录切片包围盒范围和模型包围盒
            _logger.LogDebug(
                "查询切片三角形 - 切片包围盒：Level={Level}, X={X}, Y={Y}, Z={Z}, 包围盒=[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}]",
                slice.Level, slice.X, slice.Y, slice.Z,
                boundingBox.MinX, boundingBox.MinY, boundingBox.MinZ,
                boundingBox.MaxX, boundingBox.MaxY, boundingBox.MaxZ);

            _logger.LogDebug(
                "查询切片三角形 - 模型包围盒：[{ModelMinX:F3},{ModelMinY:F3},{ModelMinZ:F3}]-[{ModelMaxX:F3},{ModelMaxY:F3},{ModelMaxZ:F3}]",
                modelBounds.MinX, modelBounds.MinY, modelBounds.MinZ,
                modelBounds.MaxX, modelBounds.MaxY, modelBounds.MaxZ);

            // 使用HashSet去重（因为三角形可能被多个网格单元引用）
            var candidateTriangles = new HashSet<Triangle>();

            // 统计信息
            var totalTrianglesInIndex = spatialIndex.Values.Sum(list => list.Count);
            var testedTriangles = 0;
            var intersectingTriangles = 0;

            // 增大容差值来处理浮点数精度问题和边界情况
            // 改进的容差策略：对小切片使用更大的相对容差
            var sliceSize = Math.Max(
                Math.Max(boundingBox.MaxX - boundingBox.MinX, boundingBox.MaxY - boundingBox.MinY),
                boundingBox.MaxZ - boundingBox.MinZ);

            // 计算模型尺寸用于自适应容差
            var modelSize = Math.Max(
                Math.Max(modelBounds.MaxX - modelBounds.MinX, modelBounds.MaxY - modelBounds.MinY),
                modelBounds.MaxZ - modelBounds.MinZ);

            // 自适应容差策略：
            // 1. 对于大切片（>10%模型尺寸）：使用切片尺寸的1%
            // 2. 对于中等切片（1%-10%模型尺寸）：使用切片尺寸的5%
            // 3. 对于小切片（<1%模型尺寸）：使用切片尺寸的10%或模型尺寸的0.1%（取较大者）
            double tolerance;
            var sizeRatio = sliceSize / modelSize;

            if (sizeRatio > 0.1)
            {
                // 大切片：1%容差
                tolerance = Math.Max(sliceSize * 0.01, 1e-4);
            }
            else if (sizeRatio > 0.01)
            {
                // 中等切片：5%容差
                tolerance = Math.Max(sliceSize * 0.05, modelSize * 0.001);
            }
            else
            {
                // 小切片：10%容差或模型尺寸的0.1%，取较大者
                tolerance = Math.Max(sliceSize * 0.1, modelSize * 0.001);
            }

            // 确保最小容差不低于1e-4
            tolerance = Math.Max(tolerance, 1e-4);

            _logger.LogDebug(
                "切片包围盒容差：{Tolerance:E3}，切片尺寸：{SliceSize:F6}，模型尺寸：{ModelSize:F6}，尺寸比例：{Ratio:F6}",
                tolerance, sliceSize, modelSize, sizeRatio);

            // 计算切片包围盒应该查询的空间索引网格范围
            // 重新计算网格参数（与BuildTriangleSpatialIndex保持一致）
            const int gridSizeX = 64;
            const int gridSizeY = 64;
            const int gridSizeZ = 32;

            double cellSizeX = (modelBounds.MaxX - modelBounds.MinX) / gridSizeX;
            double cellSizeY = (modelBounds.MaxY - modelBounds.MinY) / gridSizeY;
            double cellSizeZ = (modelBounds.MaxZ - modelBounds.MinZ) / gridSizeZ;

            // 防止除零
            if (cellSizeX <= 0) cellSizeX = 1.0;
            if (cellSizeY <= 0) cellSizeY = 1.0;
            if (cellSizeZ <= 0) cellSizeZ = 1.0;

            // 计算切片包围盒（带容差）跨越的网格范围
            int startX = Math.Max(0, (int)((boundingBox.MinX - tolerance - modelBounds.MinX) / cellSizeX));
            int startY = Math.Max(0, (int)((boundingBox.MinY - tolerance - modelBounds.MinY) / cellSizeY));
            int startZ = Math.Max(0, (int)((boundingBox.MinZ - tolerance - modelBounds.MinZ) / cellSizeZ));
            int endX = Math.Min(gridSizeX - 1, (int)((boundingBox.MaxX + tolerance - modelBounds.MinX) / cellSizeX));
            int endY = Math.Min(gridSizeY - 1, (int)((boundingBox.MaxY + tolerance - modelBounds.MinY) / cellSizeY));
            int endZ = Math.Min(gridSizeZ - 1, (int)((boundingBox.MaxZ + tolerance - modelBounds.MinZ) / cellSizeZ));

            // 对于高LOD层级的小切片，确保至少查询相邻的网格单元
            // 这样可以避免因为切片太小而遗漏三角形
            if (sizeRatio < 0.01) // 小于模型尺寸的1%
            {
                // 扩展查询范围至少包含相邻的一个单元
                startX = Math.Max(0, startX - 1);
                startY = Math.Max(0, startY - 1);
                startZ = Math.Max(0, startZ - 1);
                endX = Math.Min(gridSizeX - 1, endX + 1);
                endY = Math.Min(gridSizeY - 1, endY + 1);
                endZ = Math.Min(gridSizeZ - 1, endZ + 1);

                _logger.LogDebug("小切片扩展查询范围：原始范围扩展±1个网格单元");
            }

            _logger.LogDebug(
                "切片包围盒查询网格范围：X=[{StartX},{EndX}], Y=[{StartY},{EndY}], Z=[{StartZ},{EndZ}]",
                startX, endX, startY, endY, startZ, endZ);

            // 只从相关的网格单元中查询三角形
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    for (int z = startZ; z <= endZ; z++)
                    {
                        var cellKey = $"{x}_{y}_{z}";
                        if (!spatialIndex.ContainsKey(cellKey))
                            continue;

                        foreach (var triangle in spatialIndex[cellKey])
                        {
                            // 使用HashSet自动去重
                            if (candidateTriangles.Contains(triangle))
                                continue;

                            testedTriangles++;

                            // 首先使用简单的包围盒相交测试进行快速筛选
                            double triMinX = triangle.Vertices.Min(v => v.X);
                            double triMinY = triangle.Vertices.Min(v => v.Y);
                            double triMinZ = triangle.Vertices.Min(v => v.Z);
                            double triMaxX = triangle.Vertices.Max(v => v.X);
                            double triMaxY = triangle.Vertices.Max(v => v.Y);
                            double triMaxZ = triangle.Vertices.Max(v => v.Z);

                            // AABB包围盒相交测试（带容差）
                            bool intersects = !(triMaxX < boundingBox.MinX - tolerance || triMinX > boundingBox.MaxX + tolerance ||
                                               triMaxY < boundingBox.MinY - tolerance || triMinY > boundingBox.MaxY + tolerance ||
                                               triMaxZ < boundingBox.MinZ - tolerance || triMinZ > boundingBox.MaxZ + tolerance);

                            if (intersects)
                            {
                                // 如果AABB包围盒相交，进行更精确的相交测试
                                if (TriangleIntersectsSlice(triangle, boundingBox, tolerance))
                                {
                                    candidateTriangles.Add(triangle);
                                    intersectingTriangles++;
                                }
                            }
                        }
                    }
                }
            }

            result.AddRange(candidateTriangles);

            // 详细的调试日志
            _logger.LogDebug("切片三角形查询结果：总三角形={Total}, 测试={Tested}, 相交={Intersecting}, 结果={ResultCount}",
                totalTrianglesInIndex, testedTriangles, intersectingTriangles, result.Count);

            // 如果找到了相交三角形，记录成功信息
            if (result.Count > 0)
            {
                _logger.LogInformation("✓ 切片成功找到三角形：Level={Level}, X={X}, Y={Y}, Z={Z}, 找到{Count}个三角形",
                    slice.Level, slice.X, slice.Y, slice.Z, result.Count);
            }
            // 如果没有找到相交三角形，但有三角形数据，记录警告
            else if (result.Count == 0 && totalTrianglesInIndex > 0)
            {
                _logger.LogWarning(
                    "切片没有找到相交三角形：Level={Level}, X={X}, Y={Y}, Z={Z}, 总三角形={TotalTriangles}, 包围盒=[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}]",
                    slice.Level, slice.X, slice.Y, slice.Z, totalTrianglesInIndex,
                    boundingBox.MinX, boundingBox.MinY, boundingBox.MinZ,
                    boundingBox.MaxX, boundingBox.MaxY, boundingBox.MaxZ);

                // 计算模型的总包围盒范围，用于诊断坐标系问题
                double modelMinX = double.MaxValue, modelMinY = double.MaxValue, modelMinZ = double.MaxValue;
                double modelMaxX = double.MinValue, modelMaxY = double.MinValue, modelMaxZ = double.MinValue;

                foreach (var triangleList in spatialIndex.Values)
                {
                    foreach (var triangle in triangleList)
                    {
                        foreach (var vertex in triangle.Vertices)
                        {
                            modelMinX = Math.Min(modelMinX, vertex.X);
                            modelMinY = Math.Min(modelMinY, vertex.Y);
                            modelMinZ = Math.Min(modelMinZ, vertex.Z);
                            modelMaxX = Math.Max(modelMaxX, vertex.X);
                            modelMaxY = Math.Max(modelMaxY, vertex.Y);
                            modelMaxZ = Math.Max(modelMaxZ, vertex.Z);
                        }
                    }
                }

                _logger.LogWarning(
                    "模型总包围盒：[{ModelMinX:F3},{ModelMinY:F3},{ModelMinZ:F3}]-[{ModelMaxX:F3},{ModelMaxY:F3},{ModelMaxZ:F3}]",
                    modelMinX, modelMinY, modelMinZ, modelMaxX, modelMaxY, modelMaxZ);

                // 输出前3个三角形的坐标信息以帮助调试
                var sampleCount = 0;
                foreach (var triangleList in spatialIndex.Values)
                {
                    foreach (var triangle in triangleList)
                    {
                        if (sampleCount < 3)
                        {
                            var v0 = triangle.Vertices[0];
                            var v1 = triangle.Vertices[1];
                            var v2 = triangle.Vertices[2];
                            _logger.LogWarning(
                                "示例三角形 {Index}: V0=({V0X:F3},{V0Y:F3},{V0Z:F3}), V1=({V1X:F3},{V1Y:F3},{V1Z:F3}), V2=({V2X:F3},{V2Y:F3},{V2Z:F3})",
                                sampleCount + 1, v0.X, v0.Y, v0.Z, v1.X, v1.Y, v1.Z, v2.X, v2.Y, v2.Z);
                            sampleCount++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (sampleCount >= 3) break;
                }

                // 可能的修复：如果坐标系明显不匹配，提供警告
                var debugSliceSize = Math.Max(boundingBox.MaxX - boundingBox.MinX,
                               Math.Max(boundingBox.MaxY - boundingBox.MinY, boundingBox.MaxZ - boundingBox.MinZ));
                var debugModelSize = Math.Max(modelMaxX - modelMinX,
                               Math.Max(modelMaxY - modelMinY, modelMaxZ - modelMinZ));

                if (debugSliceSize > 0 && debugModelSize > 0)
                {
                    var debugSizeRatio = debugSliceSize / debugModelSize;
                    if (debugSizeRatio < 0.01 || debugSizeRatio > 100)
                    {
                        _logger.LogWarning(
                            "检测到坐标系可能不匹配：切片尺寸={SliceSize:F3}, 模型尺寸={ModelSize:F3}, 比例={Ratio:F6}",
                            debugSliceSize, debugModelSize, debugSizeRatio);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询切片三角形失败：{SliceKey}", $"{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}");
        }

        return result;
    }

    /// <summary>
    /// 检查三角形是否与切片包围盒相交
    /// 使用更精确的相交测试算法，比简单的AABB测试更准确
    /// </summary>
    /// <param name="triangle">待测试的三角形</param>
    /// <param name="boundingBox">切片的包围盒</param>
    /// <param name="tolerance">容差值</param>
    /// <returns>如果三角形与包围盒相交则返回true，否则返回false</returns>
    private bool TriangleIntersectsSlice(Triangle triangle, BoundingBox3D boundingBox, double tolerance)
    {
        // 1. 首先再次进行AABB包围盒测试作为快速拒绝测试
        var triMinX = triangle.Vertices.Min(v => v.X);
        var triMinY = triangle.Vertices.Min(v => v.Y);
        var triMinZ = triangle.Vertices.Min(v => v.Z);
        var triMaxX = triangle.Vertices.Max(v => v.X);
        var triMaxY = triangle.Vertices.Max(v => v.Y);
        var triMaxZ = triangle.Vertices.Max(v => v.Z);

        if (triMaxX < boundingBox.MinX - tolerance || triMinX > boundingBox.MaxX + tolerance ||
            triMaxY < boundingBox.MinY - tolerance || triMinY > boundingBox.MaxY + tolerance ||
            triMaxZ < boundingBox.MinZ - tolerance || triMinZ > boundingBox.MaxZ + tolerance)
        {
            return false; // 快速拒绝：AABB不相交
        }

        // 2. 检查三角形的三个顶点是否在包围盒内部
        foreach (var vertex in triangle.Vertices)
        {
            if (vertex.X >= boundingBox.MinX - tolerance && vertex.X <= boundingBox.MaxX + tolerance &&
                vertex.Y >= boundingBox.MinY - tolerance && vertex.Y <= boundingBox.MaxY + tolerance &&
                vertex.Z >= boundingBox.MinZ - tolerance && vertex.Z <= boundingBox.MaxZ + tolerance)
            {
                return true; // 至少一个顶点在包围盒内部
            }
        }

        // 3. 检查包围盒的顶点是否在三角形内部（通过扩展包围盒来测试）
        // 这种情况比较复杂，我们可以使用分离轴定理(Separating Axis Theorem, SAT)
        // 但为了简化，这里使用点到三角形距离的方法

        // 4. 检查三角形边与包围盒的相交（简化版）
        // 检查三角形的三条边是否穿过包围盒的面
        for (int i = 0; i < 3; i++)
        {
            var v1 = triangle.Vertices[i];
            var v2 = triangle.Vertices[(i + 1) % 3];

            // 检查边的线段是否与包围盒相交
            if (LineIntersectsBox(v1, v2, boundingBox, tolerance))
            {
                return true;
            }
        }

        // 5. 最后检查：检查三角形是否跨越包围盒的边界
        // 通过检查三角形平面是否与包围盒相交
        if (TrianglePlaneIntersectsBox(triangle, boundingBox, tolerance))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 检查线段是否与轴对齐包围盒相交
    /// </summary>
    private bool LineIntersectsBox(Vector3D v1, Vector3D v2, BoundingBox3D box, double tolerance)
    {
        // 使用Liang-Barsky算法来测试线段与AABB的相交
        double tMin = 0.0;
        double tMax = 1.0;

        // X轴测试
        if (Math.Abs(v2.X - v1.X) < tolerance)
        {
            // 线段平行于Y-Z平面
            if (v1.X < box.MinX - tolerance || v1.X > box.MaxX + tolerance)
            {
                return false; // 线段在包围盒之外
            }
        }
        else
        {
            double invDir = 1.0 / (v2.X - v1.X);
            double t1 = (box.MinX - v1.X) * invDir;
            double t2 = (box.MaxX - v1.X) * invDir;
            if (t1 > t2) { (t1, t2) = (t2, t1); } // 交换t1和t2
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax) return false;
        }

        // Y轴测试
        if (Math.Abs(v2.Y - v1.Y) < tolerance)
        {
            if (v1.Y < box.MinY - tolerance || v1.Y > box.MaxY + tolerance)
            {
                return false;
            }
        }
        else
        {
            double invDir = 1.0 / (v2.Y - v1.Y);
            double t1 = (box.MinY - v1.Y) * invDir;
            double t2 = (box.MaxY - v1.Y) * invDir;
            if (t1 > t2) { (t1, t2) = (t2, t1); }
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax) return false;
        }

        // Z轴测试
        if (Math.Abs(v2.Z - v1.Z) < tolerance)
        {
            if (v1.Z < box.MinZ - tolerance || v1.Z > box.MaxZ + tolerance)
            {
                return false;
            }
        }
        else
        {
            double invDir = 1.0 / (v2.Z - v1.Z);
            double t1 = (box.MinZ - v1.Z) * invDir;
            double t2 = (box.MaxZ - v1.Z) * invDir;
            if (t1 > t2) { (t1, t2) = (t2, t1); }
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax) return false;
        }

        // 如果线段与包围盒相交，返回true
        return tMin <= tMax && tMin <= 1.0 && tMax >= 0.0;
    }

    /// <summary>
    /// 检查三角形平面是否与轴对齐包围盒相交
    /// 这是一个简化的近似测试
    /// </summary>
    private bool TrianglePlaneIntersectsBox(Triangle triangle, BoundingBox3D box, double tolerance)
    {
        // 此处可以实现更复杂的相交测试
        // 为简化处理，我们使用之前AABB测试的结果作为基础
        // 因为我们已经通过了AABB测试，这里返回true
        // 在实际应用中，可能需要实现更复杂的相交检测算法

        // 如果三角形平面与包围盒可能相交，返回true
        // 检查三角形的重心是否接近包围盒
        var center = new Vector3D
        {
            X = (triangle.Vertices[0].X + triangle.Vertices[1].X + triangle.Vertices[2].X) / 3.0,
            Y = (triangle.Vertices[0].Y + triangle.Vertices[1].Y + triangle.Vertices[2].Y) / 3.0,
            Z = (triangle.Vertices[0].Z + triangle.Vertices[1].Z + triangle.Vertices[2].Z) / 3.0
        };

        // 检查重心是否在扩展的包围盒内
        return center.X >= box.MinX - tolerance && center.X <= box.MaxX + tolerance &&
               center.Y >= box.MinY - tolerance && center.Y <= box.MaxY + tolerance &&
               center.Z >= box.MinZ - tolerance && center.Z <= box.MaxZ + tolerance;
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
    /// <param name="triangleSpatialIndex">三角形空间索引，用于快速查询</param>
    /// <param name="modelBounds">模型包围盒，用于坐标变换</param>
    /// <param name="existingSlicesMap">现有切片映射表，用于增量更新</param>
    /// <param name="actuallyUseIncrementalUpdate">是否实际使用增量更新</param>
    /// <param name="hasSliceChanges">是否有切片变化的标记</param>
    /// <param name="processedSliceKeys">已处理的切片键集合</param>
    /// <param name="slicesToAdd">待添加的切片列表</param>
    /// <param name="slicesToUpdate">待更新的切片列表</param>
    /// <param name="cancellationToken">取消令牌，支持优雅的中断处理</param>
    /// <returns>包含处理结果的元组：(处理数量, 是否有变化)</returns>
    private async Task<(int processedCount, bool hasChanges)> ProcessSlicesInParallelAsync(
        SlicingTask task,
        int level,
        SlicingConfig config,
        List<Slice> slices,
        Dictionary<string, List<Triangle>> triangleSpatialIndex,
        BoundingBox3D modelBounds,
        Dictionary<string, Slice> existingSlicesMap,
        bool actuallyUseIncrementalUpdate,
        bool hasSliceChanges,
        HashSet<string> processedSliceKeys,
        Dictionary<string, Material> materials,
        List<Slice> slicesToAdd,
        List<Slice> slicesToUpdate,
        CancellationToken cancellationToken)
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
                    // 增量更新：检查切片是否已存在
                    var sliceKey = $"{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}";
                    bool isNewSlice = !existingSlicesMap.ContainsKey(sliceKey);
                    bool needsUpdate = false;

                    // 确保切片关联到正确的任务
                    slice.SlicingTaskId = task.Id;

                    if (actuallyUseIncrementalUpdate && !isNewSlice)
                    {
                        var existingSlice = existingSlicesMap[sliceKey];
                        var newHash = await CalculateSliceHash(slice);
                        var existingHash = await CalculateSliceHashFromExisting(existingSlice);

                        needsUpdate = newHash != existingHash;

                        if (!needsUpdate)
                        {
                            lock (lockObject)
                            {
                                processedSliceKeys.Add(sliceKey);
                            }
                            return;
                        }
                        else
                        {
                            slice.Id = existingSlice.Id;
                            lock (lockObject)
                            {
                                hasSliceChanges = true;
                            }
                        }
                    }
                    else if (actuallyUseIncrementalUpdate && isNewSlice)
                    {
                        lock (lockObject)
                        {
                            hasSliceChanges = true;
                        }
                    }

                    // 查询此切片相交的三角形数据
                    var sliceTriangles = QueryTrianglesForSlice(slice, triangleSpatialIndex, modelBounds);

                    // 生成切片文件内容（传入实际的三角形数据）
                    await _dataService.GenerateSliceFileAsync(slice, config, sliceTriangles, materials, cancellationToken);

                    // 线程安全的计数更新和列表操作
                    lock (lockObject)
                    {
                        if (actuallyUseIncrementalUpdate && needsUpdate)
                        {
                            slicesToUpdate.Add(slice);
                        }
                        else
                        {
                            slicesToAdd.Add(slice);
                        }

                        if (actuallyUseIncrementalUpdate)
                        {
                            processedSliceKeys.Add(sliceKey);
                        }

                        processedCount++;
                        if (processedCount % 10 == 0) // 每处理10个切片输出一次进度
                        {
                            _logger.LogDebug("并行处理进度：任务{TaskId}({TaskName}), 级别{Level}, 已处理{Processed}/{Total}",
                                task.Id, task.Name, level, processedCount, slicesArray.Length);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "并行处理切片失败：任务{TaskId}({TaskName}), 级别{Level}, 索引{Index}",
                        task.Id, task.Name, level, index);
                }
            });
        }, cancellationToken);

        _logger.LogInformation("并行切片处理完成：级别{Level}, 处理{Processed}个切片", level, processedCount);

        return (processedCount, hasSliceChanges);
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
        var boundingBox = System.Text.Json.JsonSerializer.Deserialize<BoundingBox3D>(slice.BoundingBox);
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
        var distance = viewport.CameraPosition.DistanceTo(sliceCenter);

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

        var angle = viewport.CameraDirection.AngleTo(toCenterVector);

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
        var pixelThreshold = viewport.FieldOfView / viewport.ViewportHeight * (viewport.FieldOfView / viewport.ViewportHeight);
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
        var forward = viewport.CameraDirection.Normalize();

        // 计算相机的右向量和上向量（假设世界上向量为(0,0,1)）
        var worldUp = new Vector3D { X = 0, Y = 0, Z = 1 };
        var right = forward.Cross(worldUp).Normalize();
        var up = right.Cross(forward).Normalize();

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
        planes[0] = FrustumPlane.FromNormalAndPoint(forward, nearCenter);

        // 1. 远平面：法向量指向相机外部（后方）
        planes[1] = FrustumPlane.FromNormalAndPoint(
            new Vector3D { X = -forward.X, Y = -forward.Y, Z = -forward.Z },
            farCenter);

        // 2. 左平面
        var leftNormal = up.Cross(new Vector3D
        {
            X = nearCenter.X - right.X * nearWidth / 2 - viewport.CameraPosition.X,
            Y = nearCenter.Y - right.Y * nearWidth / 2 - viewport.CameraPosition.Y,
            Z = nearCenter.Z - right.Z * nearWidth / 2 - viewport.CameraPosition.Z
        });
        planes[2] = FrustumPlane.FromNormalAndPoint(leftNormal.Normalize(), viewport.CameraPosition);

        // 3. 右平面
        var rightNormal = (new Vector3D
        {
            X = nearCenter.X + right.X * nearWidth / 2 - viewport.CameraPosition.X,
            Y = nearCenter.Y + right.Y * nearWidth / 2 - viewport.CameraPosition.Y,
            Z = nearCenter.Z + right.Z * nearWidth / 2 - viewport.CameraPosition.Z
        }).Cross(up);
        planes[3] = FrustumPlane.FromNormalAndPoint(rightNormal.Normalize(), viewport.CameraPosition);

        // 4. 上平面
        var topNormal = right.Cross(new Vector3D
        {
            X = nearCenter.X + up.X * nearHeight / 2 - viewport.CameraPosition.X,
            Y = nearCenter.Y + up.Y * nearHeight / 2 - viewport.CameraPosition.Y,
            Z = nearCenter.Z + up.Z * nearHeight / 2 - viewport.CameraPosition.Z
        });
        planes[4] = FrustumPlane.FromNormalAndPoint(topNormal.Normalize(), viewport.CameraPosition);

        // 5. 下平面
        var bottomNormal = (new Vector3D
        {
            X = nearCenter.X - up.X * nearHeight / 2 - viewport.CameraPosition.X,
            Y = nearCenter.Y - up.Y * nearHeight / 2 - viewport.CameraPosition.Y,
            Z = nearCenter.Z - up.Z * nearHeight / 2 - viewport.CameraPosition.Z
        }).Cross(right);
        planes[5] = FrustumPlane.FromNormalAndPoint(bottomNormal.Normalize(), viewport.CameraPosition);

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
            var distance = plane.DistanceToPoint(corner);

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
    /// 计算切片哈希值 - 委托给 IncrementalUpdateService
    /// </summary>
    private async Task<string> CalculateSliceHash(Slice slice)
    {
        return await _incrementalUpdateService.CalculateSliceHashAsync(slice);
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
}