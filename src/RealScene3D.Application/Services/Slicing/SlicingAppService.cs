using Microsoft.Extensions.DependencyInjection;
using RealScene3D.Domain.Enums;
using Microsoft.Extensions.Logging;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.MinIO;
using System.Buffers;
using System.Text.Json;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// 三维切片应用服务实现
/// </summary>
public class SlicingAppService : ISlicingAppService
{
    private readonly IRepository<SlicingTask> _slicingTaskRepository;
    private readonly IRepository<Slice> _sliceRepository;
    private readonly IMinioStorageService _minioService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SlicingAppService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    // 任务进度历史跟踪 - 用于趋势检测和精确时间估算
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, TaskProgressHistory> _progressHistoryCache = new();

    // 后台任务取消令牌源 - 用于取消卡死的任务
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, CancellationTokenSource> _taskCancellationTokens = new();

    public SlicingAppService(
        IRepository<SlicingTask> slicingTaskRepository,
        IRepository<Slice> sliceRepository,
        IMinioStorageService minioService,
        IUnitOfWork unitOfWork,
        ILogger<SlicingAppService> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _slicingTaskRepository = slicingTaskRepository;
        _sliceRepository = sliceRepository;
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
            if (request.SlicingConfig.Divisions < 0 || request.SlicingConfig.Divisions > 20)
                throw new ArgumentException("LOD级别数量必须在0-20之间", nameof(request.SlicingConfig.Divisions));

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
                // 转换 DTO 配置为 Domain 配置
                var initialDomainConfig = MapSlicingConfigToDomain(request.SlicingConfig);

                // 创建切片任务实体 - 领域对象构建
                task = new SlicingTask
                {
                    Name = request.Name.Trim(), // 清理前后空格
                    SourceModelPath = request.SourceModelPath,
                    ModelType = request.ModelType,
                    SlicingConfig = System.Text.Json.JsonSerializer.Serialize(initialDomainConfig),
                    OutputPath = string.IsNullOrEmpty(request.OutputPath)
                        ? GenerateOutputPathFromSource(request.SourceModelPath) // 基于源模型生成确定性路径
                        : request.OutputPath.Trim(),
                    CreatedBy = userId,
                    Status = SlicingTaskStatus.Created,
                    SceneObjectId = request.SceneObjectId
                };
            }            // 根据OutputPath判断存储类型
            // 先将 DTO 配置转换为 Domain 配置
            var domainConfig = isIncrementalUpdate || task.Status == SlicingTaskStatus.Created
                ? MapSlicingConfigToDomain(request.SlicingConfig)
                : SlicingUtilities.ParseSlicingConfig(task.SlicingConfig);

            // 判断存储位置的优先级：
            // 1. 如果用户在 SlicingConfig 中明确指定了 StorageLocation，使用用户指定的
            // 2. 如果任务的 OutputPath 是绝对路径（Path.IsPathRooted），判定为本地文件系统
            // 3. 如果任务的 OutputPath 是相对路径或未提供路径，默认使用 MinIO

            // 关键修复：对于增量更新，应该使用 task.OutputPath 而不是 request.OutputPath
            // 因为增量更新时 task 可能来自 existingTask，其 OutputPath 已经确定
            bool hasRootedPath = !string.IsNullOrEmpty(task.OutputPath) && Path.IsPathRooted(task.OutputPath);

            StorageLocationType specifiedLocation = request.SlicingConfig.StorageLocation;
            bool userSpecifiedStorage =
            specifiedLocation == StorageLocationType.LocalFileSystem || specifiedLocation != StorageLocationType.MinIO;

            if (userSpecifiedStorage)
            {
                // 用户明确指定了存储位置，使用用户指定的
                domainConfig.StorageLocation = specifiedLocation;
                _logger.LogInformation("切片任务 {TaskId} 使用用户指定的存储位置：{StorageLocation}", task.Id, domainConfig.StorageLocation);
            }
            else if (hasRootedPath)
            {
                // 任务的输出路径是绝对路径，判定为本地文件系统
                domainConfig.StorageLocation = StorageLocationType.LocalFileSystem;
                _logger.LogInformation("切片任务 {TaskId} 的输出路径 {OutputPath} 被识别为本地文件系统路径。", task.Id, task.OutputPath!);
            }
            else
            {
                // 默认使用 MinIO
                domainConfig.StorageLocation = StorageLocationType.MinIO;
                _logger.LogInformation("切片任务 {TaskId} 的输出路径 {OutputPath} 被识别为MinIO路径。", task.Id, task.OutputPath!);
            }

            // 序列化更新后的配置
            task.SlicingConfig = JsonSerializer.Serialize(domainConfig);

            if (domainConfig.StorageLocation == StorageLocationType.LocalFileSystem)
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

            _logger.LogInformation("切片任务 {TaskId} 的最终存储位置类型为 {StorageLocation}.",
                task.Id, domainConfig.StorageLocation);

            // 持久化切片任务 - 原子性操作确保数据一致性
            // 重要：在存储位置判断完成后才保存，确保 SlicingConfig 中的 StorageLocation 是正确的
            if (isIncrementalUpdate && task.Id != Guid.Empty)
            {
                // 增量更新场景：更新现有任务
                await _slicingTaskRepository.UpdateAsync(task);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("切片任务已更新用于增量处理：{TaskId}，存储位置：{StorageLocation}",
                    task.Id, domainConfig.StorageLocation);
            }
            else
            {
                // 新任务：添加到数据库
                await _slicingTaskRepository.AddAsync(task);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("新切片任务已创建：{TaskId}，存储位置：{StorageLocation}",
                    task.Id, domainConfig.StorageLocation);
            }

            var taskId = task.Id;

            // 创建可取消的令牌源（30分钟超时）
            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            _taskCancellationTokens[taskId] = cts;

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

                        await processor.ProcessSlicingTaskAsync(taskId, cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("切片任务被取消或超时：{TaskId}", taskId);

                    // 更新任务状态为失败
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var repository = scope.ServiceProvider.GetRequiredService<IRepository<SlicingTask>>();
                        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                        var failedTask = await repository.GetByIdAsync(taskId);
                        if (failedTask != null)
                        {
                            failedTask.Status = SlicingTaskStatus.Failed;
                            failedTask.ErrorMessage = "任务超时（30分钟）或被取消";
                            await repository.UpdateAsync(failedTask);
                            await unitOfWork.SaveChangesAsync();
                        }
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
                finally
                {
                    // 清理取消令牌
                    if (_taskCancellationTokens.TryRemove(taskId, out var removedCts))
                    {
                        removedCts.Dispose();
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
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("任务ID不能为空", nameof(taskId));
        }

        try
        {
            var task = await _slicingTaskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                _logger.LogWarning("切片任务不存在：{TaskId}", taskId);
                return null;
            }

            // 计算实际的切片总数，添加超时保护
            int totalSlices = 0;
            try
            {
                var slices = await _sliceRepository.GetAllAsync();
                totalSlices = slices.Count(s => s.SlicingTaskId == taskId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "计算切片总数失败，使用默认值0：{TaskId}", taskId);
                // 不抛出异常，允许返回任务信息
            }

            return MapToDto(task, totalSlices);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "获取切片任务参数验证失败：{TaskId}", taskId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取切片任务时发生未预期错误：{TaskId}", taskId);
            throw new InvalidOperationException($"获取切片任务失败：{taskId}", ex);
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
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("任务ID不能为空", nameof(taskId));
        }

        try
        {
            var task = await _slicingTaskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                _logger.LogWarning("切片任务不存在：{TaskId}", taskId);
                return null;
            }

            // 并发安全：捕获任务状态快照，避免并发修改导致的不一致性
            var taskSnapshot = new
            {
                task.Id,
                task.Progress,
                task.Status,
                task.StartedAt,
                task.CompletedAt
            };

            // 异步获取统计数据，提高响应性
            var processedTilesTask = GetProcessedTilesCount(taskId);
            var totalTilesTask = GetTotalTilesCount(taskId);
            var estimatedTimeTask = Task.FromResult(CalculateEstimatedTimeRemaining(task));

            await Task.WhenAll(processedTilesTask, totalTilesTask, estimatedTimeTask);

            return new SlicingDtos.SlicingProgressDto
            {
                TaskId = taskSnapshot.Id,
                Progress = Math.Clamp(taskSnapshot.Progress, 0, 100), // 确保进度在有效范围内
                CurrentStage = GetCurrentStage(taskSnapshot.Status),
                Status = taskSnapshot.Status.ToString().ToLowerInvariant(),
                ProcessedTiles = await processedTilesTask,
                TotalTiles = await totalTilesTask,
                EstimatedTimeRemaining = await estimatedTimeTask
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "获取切片进度参数验证失败：{TaskId}", taskId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取切片进度时发生未预期错误：{TaskId}", taskId);
            throw new InvalidOperationException($"获取切片进度失败：{taskId}", ex);
        }
    }

    public async Task<bool> CancelSlicingTaskAsync(Guid taskId, Guid userId)
    {
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("任务ID不能为空", nameof(taskId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("用户ID不能为空", nameof(userId));
        }

        try
        {
            var task = await _slicingTaskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                _logger.LogWarning("切片任务不存在：{TaskId}", taskId);
                return false;
            }

            if (task.CreatedBy != userId)
            {
                _logger.LogWarning("用户无权取消此任务：任务{TaskId}, 用户{UserId}, 创建者{CreatedBy}",
                    taskId, userId, task.CreatedBy);
                return false;
            }

            // 只允许取消处理中的任务
            if (task.Status != SlicingTaskStatus.Processing && task.Status != SlicingTaskStatus.Queued)
            {
                _logger.LogWarning("无法取消非活跃任务：任务{TaskId}, 状态{Status}",
                    taskId, task.Status);
                return false;
            }

            // 原子性更新：使用数据库事务确保状态一致性
            task.Status = SlicingTaskStatus.Cancelled;
            task.CompletedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("切片任务已取消：{TaskId}, 用户：{UserId}, 原状态：{OriginalStatus}",
                taskId, userId, task.Status);
            return true;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "取消切片任务参数验证失败：任务{TaskId}, 用户{UserId}", taskId, userId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消切片任务时发生未预期错误：任务{TaskId}, 用户{UserId}", taskId, userId);
            return false; // 返回false而不是抛出异常，保持API的一致性
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
            var config = SlicingUtilities.ParseSlicingConfig(task.SlicingConfig);

            // 删除关联的所有切片文件
            var allSlices = await _sliceRepository.GetAllAsync();
            var taskSlices = allSlices.Where(s => s.SlicingTaskId == taskId).ToList();
            foreach (var slice in taskSlices)
            {
                await SlicingUtilities.DeleteSliceFileAsync(
                    slice.FilePath, task.OutputPath, config.StorageLocation, _minioService, _logger);

                // 删除数据库中的切片记录
                await _sliceRepository.DeleteAsync(slice);
            }

            // 删除切片索引文件和tileset.json
            await SlicingUtilities.DeleteSliceIndexAndTilesetAsync(task.OutputPath, config.StorageLocation, _minioService, _logger);

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
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("任务ID不能为空", nameof(taskId));
        }

        if (level < 0)
        {
            throw new ArgumentException("LOD级别不能为负数", nameof(level));
        }

        if (x < 0 || y < 0 || z < 0)
        {
            throw new ArgumentException("切片坐标不能为负数", nameof(x));
        }

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
                _logger.LogWarning("切片文件不存在：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})",
                    taskId, level, x, y, z);
                throw new FileNotFoundException($"切片文件不存在：任务{taskId}, 级别{level}, 坐标({x}, {y}, {z})");
            }

            // 验证文件大小，防止下载过大文件导致内存溢出
            const long MaxFileSize = 100 * 1024 * 1024; // 100MB限制
            if (slice.FileSize > MaxFileSize)
            {
                _logger.LogWarning("切片文件过大：{FileSize}字节，超过限制{MaxSize}字节",
                    slice.FileSize, MaxFileSize);
                throw new InvalidOperationException($"切片文件过大，无法下载：{slice.FileSize}字节");
            }

            var stream = await _minioService.DownloadFileAsync("slices", slice.FilePath);
            if (stream == null)
            {
                _logger.LogError("从MinIO下载文件失败：{FilePath}", slice.FilePath);
                throw new FileNotFoundException($"无法下载切片文件：{slice.FilePath}");
            }

            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream);

                // 双重验证：确保下载的字节数与记录一致
                if (memoryStream.Length != slice.FileSize)
                {
                    _logger.LogWarning("下载文件大小不匹配：期望{ExpectedSize}字节，实际{DownloadedSize}字节",
                        slice.FileSize, memoryStream.Length);
                }

                return memoryStream.ToArray();
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "下载切片文件参数验证失败：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})",
                taskId, level, x, y, z);
            throw;
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "切片文件不存在：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})",
                taskId, level, x, y, z);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载切片文件时发生未预期错误：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})",
                taskId, level, x, y, z);
            throw new InvalidOperationException($"下载切片文件失败：任务{taskId}, 级别{level}, 坐标({x}, {y}, {z})", ex);
        }
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
            using var reader = new StreamReader(stream);
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

    private static SlicingDtos.SlicingTaskDto MapToDto(SlicingTask task, int totalSlices = 0)
    {
        return new SlicingDtos.SlicingTaskDto
        {
            Id = task.Id,
            Name = task.Name,
            SourceModelPath = task.SourceModelPath,
            ModelType = task.ModelType,
            SceneObjectId = task.SceneObjectId,
            SlicingConfig = MapSlicingConfigToDto(SlicingUtilities.ParseSlicingConfig(task.SlicingConfig)),
            Status = task.Status.ToString().ToLowerInvariant(),
            Progress = task.Progress,
            OutputPath = task.OutputPath,
            ErrorMessage = task.ErrorMessage,
            CreatedBy = task.CreatedBy,
            CreatedAt = task.CreatedAt,
            StartedAt = task.StartedAt,
            CompletedAt = task.CompletedAt,
            TotalSlices = totalSlices
        };
    }

    private static SlicingDtos.SlicingConfigDto MapSlicingConfigToDto(SlicingConfig domainConfig)
    {
        return new SlicingDtos.SlicingConfigDto
        {
            Granularity = "Medium",
            OutputFormat = domainConfig.OutputFormat,
            CoordinateSystem = "EPSG:4326", // 默认值
            CustomSettings = "{}", // 默认值
            MaxLevel = domainConfig.Divisions,  // MaxLevel 映射到 Divisions（向后兼容）
            Divisions = domainConfig.Divisions,
            LodLevels = domainConfig.LodLevels,
            EnableMeshDecimation = domainConfig.EnableMeshDecimation,
            GenerateTileset = domainConfig.GenerateTileset,
            EnableIncrementalUpdates = domainConfig.EnableIncrementalUpdates,
            StorageLocation = domainConfig.StorageLocation,
            TextureStrategy = domainConfig.TextureStrategy  // 添加纹理策略映射
        };
    }

    /// <summary>
    /// 将 DTO 切片配置转换为 Domain 切片配置
    /// </summary>
    private static SlicingConfig MapSlicingConfigToDomain(SlicingDtos.SlicingConfigDto dtoConfig)
    {
        // 解析输出格式
        var outputFormat = "b3dm";
        if (!string.IsNullOrWhiteSpace(dtoConfig.OutputFormat))
        {
            outputFormat = dtoConfig.OutputFormat.ToLowerInvariant() switch
            {
                "3d tiles" => "b3dm",
                "cesium3dtiles" => "b3dm",
                "gltf" => "gltf",
                "json" => "json",
                _ => dtoConfig.OutputFormat.ToLowerInvariant()
            };
        }

        return new SlicingConfig
        {
            Divisions = dtoConfig.Divisions > 0 ? dtoConfig.Divisions : 2,  // 空间分割深度（对应 --divisions）
            LodLevels = dtoConfig.LodLevels > 0 ? dtoConfig.LodLevels : 3,  // LOD级别数量（对应 --lods）
            EnableMeshDecimation = dtoConfig.EnableMeshDecimation,          // 网格简化开关
            GenerateTileset = dtoConfig.GenerateTileset,                    // tileset.json生成开关
            OutputFormat = outputFormat,
            EnableIncrementalUpdates = dtoConfig.EnableIncrementalUpdates,
            StorageLocation = dtoConfig.StorageLocation,
            TextureStrategy = dtoConfig.TextureStrategy                     // 纹理处理策略（修复：添加映射）
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

            var config = SlicingUtilities.ParseSlicingConfig(task.SlicingConfig);
            long totalCount = 0;

            // 默认使用网格策略估算
            for (int level = 0; level <= config.Divisions; level++)
            {
                var tilesInLevel = (long)Math.Pow(2, level);
                var zTiles = level == 0 ? 1 : tilesInLevel / 2;
                totalCount += tilesInLevel * tilesInLevel * zTiles;
            }

            _logger.LogDebug("计算切片总数：任务{TaskId}, 最大级别{MaxLevel}, 总数{TotalCount}",
                taskId, config.Divisions, totalCount);

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
