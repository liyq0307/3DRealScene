using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// 三维切片处理器 - 简化版
/// 使用 TileGenerationPipeline 进行切片处理
///
/// 主要职责：
/// - 管理切片任务队列
/// - 调度切片任务到瓦片生成流水线
/// - 保存切片结果到数据库
/// - 支持增量更新（可选）
/// </summary>
public class SlicingProcessor : ISlicingProcessor
{
    private readonly IRepository<SlicingTask> _slicingTaskRepository;
    private readonly IRepository<Slice> _sliceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SlicingProcessor> _logger;
    private readonly TileGenerationPipeline _tileGenerationPipeline;
    private readonly IncrementalUpdateService _incrementalUpdateService;

    public SlicingProcessor(
        IRepository<SlicingTask> slicingTaskRepository,
        IRepository<Slice> sliceRepository,
        IUnitOfWork unitOfWork,
        ILogger<SlicingProcessor> logger,
        TileGenerationPipeline tileGenerationPipeline,
        IncrementalUpdateService incrementalUpdateService)
    {
        _slicingTaskRepository = slicingTaskRepository ?? throw new ArgumentNullException(nameof(slicingTaskRepository));
        _sliceRepository = sliceRepository ?? throw new ArgumentNullException(nameof(sliceRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tileGenerationPipeline = tileGenerationPipeline ?? throw new ArgumentNullException(nameof(tileGenerationPipeline));
        _incrementalUpdateService = incrementalUpdateService ?? throw new ArgumentNullException(nameof(incrementalUpdateService));
    }

    /// <summary>
    /// 处理切片任务队列
    /// </summary>
    public async Task ProcessSlicingQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始处理切片任务队列");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var allTasks = await _slicingTaskRepository.GetAllAsync();
                var queuedTasks = allTasks.Where(t => t.Status == SlicingTaskStatus.Queued);

                foreach (var task in queuedTasks)
                {
                    await ProcessSlicingTaskAsync(task.Id, cancellationToken);
                }

                await Task.Delay(5000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理切片队列时发生错误");
                await Task.Delay(10000, cancellationToken);
            }
        }

        _logger.LogInformation("切片任务队列处理结束");
    }

    /// <summary>
    /// 处理单个切片任务
    /// </summary>
    public async Task ProcessSlicingTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _slicingTaskRepository.GetByIdAsync(taskId);
        if (task == null)
        {
            _logger.LogWarning("切片任务不存在：{TaskId}", taskId);
            return;
        }

        try
        {
            _logger.LogInformation("开始处理切片任务：{TaskId} ({TaskName})", taskId, task.Name);

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
    /// 执行切片处理 - 使用瓦片生成流水线
    /// </summary>
    private async Task PerformSlicingAsync(SlicingTask task, CancellationToken cancellationToken)
    {
        var config = ParseSlicingConfig(task.SlicingConfig);

        _logger.LogInformation("开始切片处理：任务{TaskId}, 格式={Format}, LOD级别={LodLevels}, 最大深度={MaxLevel}",
            task.Id, config.OutputFormat, config.LodLevels, config.Divisions);

        // 使用瓦片生成流水线处理
        var result = await _tileGenerationPipeline.ProcessAsync(task, config, cancellationToken);

        _logger.LogInformation("瓦片生成流水线处理完成：生成{TotalSlices}个切片，耗时{Time:F2}秒",
            result.TotalSlices, result.ProcessingTime.TotalSeconds);

        // 保存切片到数据库
        await SaveSlicesToDatabaseAsync(result.Slices, task, config, cancellationToken);

        // 生成增量更新索引（如果需要）
        if (config.EnableIncrementalUpdates)
        {
            _logger.LogInformation("生成增量更新索引：任务{TaskId}", task.Id);
            await _incrementalUpdateService.GenerateIncrementalUpdateIndexAsync(task, config, cancellationToken);
        }
    }

    /// <summary>
    /// 保存切片到数据库
    /// </summary>
    private async Task SaveSlicesToDatabaseAsync(
        List<Slice> slices,
        SlicingTask task,
        SlicingConfig config,
        CancellationToken cancellationToken)
    {
        const int batchSize = 50;
        int savedCount = 0;

        for (int i = 0; i < slices.Count; i += batchSize)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var batch = slices.Skip(i).Take(batchSize).ToList();

            foreach (var slice in batch)
            {
                await _sliceRepository.AddAsync(slice);
            }

            await _unitOfWork.SaveChangesAsync();
            savedCount += batch.Count;

            _logger.LogDebug("保存切片进度：{Saved}/{Total}", savedCount, slices.Count);
        }

        _logger.LogInformation("切片保存完成：共{Count}个切片", savedCount);
    }

    /// <summary>
    /// 解析切片配置
    /// </summary>
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
}
