using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.MinIO;
using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace RealScene3D.Application.Services.Slicing;

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

/// <summary>
/// 增量更新服务 - 负责生成和管理增量更新索引
/// 提供增量更新算法实现，支持部分模型更新
/// </summary>
public class IncrementalUpdateService
{
    private readonly IRepository<SlicingTask> _slicingTaskRepository;
    private readonly IRepository<Slice> _sliceRepository;
    private readonly IMinioStorageService _minioService;
    private readonly ILogger<IncrementalUpdateService> _logger;

    // 任务配置缓存
    private static readonly ConcurrentDictionary<Guid, (SlicingConfig config, DateTime timestamp)> _taskConfigCache =
        new ConcurrentDictionary<Guid, (SlicingConfig config, DateTime timestamp)>();

    public IncrementalUpdateService(
        IRepository<SlicingTask> slicingTaskRepository,
        IRepository<Slice> sliceRepository,
        IMinioStorageService minioService,
        ILogger<IncrementalUpdateService> logger)
    {
        _slicingTaskRepository = slicingTaskRepository;
        _sliceRepository = sliceRepository;
        _minioService = minioService;
        _logger = logger;
    }

    /// <summary>
    /// 生成增量更新索引 - 增量更新算法实现
    /// 算法：为切片生成增量更新索引，支持部分模型更新
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="config">切片配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task GenerateIncrementalUpdateIndexAsync(
        SlicingTask task,
        SlicingConfig config,
        CancellationToken cancellationToken)
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
                Hash = await CalculateSliceHashAsync(s), // 计算切片哈希用于增量比较
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
            Strategy = "Default",
            TileSize = config.TileSize
        };

        var indexContent = JsonSerializer.Serialize(updateIndex, new JsonSerializerOptions
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
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(indexContent)))
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
    /// 性能优化：
    /// - 预分配缓冲区避免重复内存分配
    /// - 异步文件I/O避免阻塞
    /// - 缓存任务配置避免重复查询
    /// </summary>
    public async Task<string> CalculateSliceHashAsync(Slice slice)
    {
        try
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            {
                // 1. 收集切片的关键信息，使用StringBuilder避免字符串拼接开销
                var metadataBuilder = new StringBuilder(256);
                metadataBuilder.Append(slice.Level).Append('_')
                              .Append(slice.X).Append('_')
                              .Append(slice.Y).Append('_')
                              .Append(slice.Z).Append('_')
                              .Append(slice.BoundingBox).Append('_')
                              .Append(slice.FilePath);

                var metadataBytes = Encoding.UTF8.GetBytes(metadataBuilder.ToString());

                // 2. 获取任务配置（带缓存）
                var config = await GetSlicingConfigCachedAsync(slice.SlicingTaskId);

                // 3. 异步读取文件内容并计算哈希
                byte[]? fileContentHash = null;
                try
                {
                    if (config.StorageLocation == StorageLocationType.LocalFileSystem)
                    {
                        var fullPath = Path.IsPathRooted(slice.FilePath)
                            ? slice.FilePath
                            : Path.Combine(await GetTaskOutputPathAsync(slice.SlicingTaskId), slice.FilePath);

                        if (File.Exists(fullPath))
                        {
                            await using var fileStream = File.OpenRead(fullPath);
                            fileContentHash = await sha256.ComputeHashAsync(fileStream);
                        }
                    }
                    else // MinIO
                    {
                        await using var fileStream = await _minioService.DownloadFileAsync("slices", slice.FilePath);
                        if (fileStream != null)
                        {
                            fileContentHash = await sha256.ComputeHashAsync(fileStream);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "读取切片文件失败，将仅使用元数据计算哈希：{FilePath}", slice.FilePath);
                }

                // 4. 计算最终哈希
                byte[] finalHash;
                if (fileContentHash != null)
                {
                    // 预分配缓冲区
                    var combinedLength = metadataBytes.Length + fileContentHash.Length;
                    var combinedBytes = ArrayPool<byte>.Shared.Rent(combinedLength);
                    try
                    {
                        Buffer.BlockCopy(metadataBytes, 0, combinedBytes, 0, metadataBytes.Length);
                        Buffer.BlockCopy(fileContentHash, 0, combinedBytes, metadataBytes.Length, fileContentHash.Length);
                        finalHash = sha256.ComputeHash(combinedBytes, 0, combinedLength);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(combinedBytes);
                    }
                }
                else
                {
                    finalHash = sha256.ComputeHash(metadataBytes);
                }

                // 5. 转换为十六进制字符串
                return Convert.ToHexString(finalHash).ToLower();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算切片哈希时发生错误，将使用简化方式计算：{SliceId}", slice.Id);
            var fallbackInput = $"{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}_{slice.BoundingBox}";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(fallbackInput));
                return Convert.ToHexString(hashBytes).ToLower();
            }
        }
    }

    /// <summary>
    /// 从已存在的切片计算哈希值 - 用于增量更新比对
    /// 与CalculateSliceHashAsync的区别是，这个方法用于已存在于数据库中的切片
    /// </summary>
    /// <param name="slice">已存在的切片数据</param>
    /// <returns>哈希值字符串</returns>
    public async Task<string> CalculateSliceHashFromExistingAsync(Slice slice)
    {
        // 直接调用 CalculateSliceHashAsync，因为逻辑是一样的
        // 该方法会根据切片的 SlicingTaskId 查找任务配置，然后读取文件计算哈希
        return await CalculateSliceHashAsync(slice);
    }

    /// <summary>
    /// 缓存获取切片配置，避免重复数据库查询
    /// </summary>
    private async Task<SlicingConfig> GetSlicingConfigCachedAsync(Guid taskId)
    {
        if (_taskConfigCache.TryGetValue(taskId, out var cached) &&
            (DateTime.UtcNow - cached.timestamp) < TimeSpan.FromMinutes(5))
        {
            return cached.config;
        }

        var task = await _slicingTaskRepository.GetByIdAsync(taskId);
        if (task == null)
            throw new InvalidOperationException($"切片任务 {taskId} 未找到");

        var config = ParseSlicingConfig(task.SlicingConfig);
        _taskConfigCache[taskId] = (config, DateTime.UtcNow);
        return config;
    }

    /// <summary>
    /// 获取任务输出路径（带缓存）
    /// </summary>
    private async Task<string> GetTaskOutputPathAsync(Guid taskId)
    {
        var task = await _slicingTaskRepository.GetByIdAsync(taskId);
        return task?.OutputPath ?? string.Empty;
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
