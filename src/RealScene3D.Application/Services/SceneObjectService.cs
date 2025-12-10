using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.Geometries;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.Data;
using RealScene3D.Infrastructure.MinIO;

namespace RealScene3D.Application.Services;

/// <summary>
/// 3D场景对象应用服务实现类
/// 负责场景对象的创建、查询、删除等业务操作
/// 支持3D对象的位置管理、属性配置和空间数据处理
/// </summary>
public class SceneObjectService : ISceneObjectService
{
    private readonly IRepository<SceneObject> _objectRepository;
    private readonly IRepository<SlicingTask> _slicingTaskRepository;
    private readonly ISliceRepository _sliceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _context;
    private readonly GeometryFactory _geometryFactory;
    private readonly ILogger<SceneObjectService> _logger;
    private readonly IMinioStorageService? _minioStorageService;
    private readonly IConfiguration _configuration;


    /// <summary>
    /// 构造函数，注入所需的依赖服务
    /// </summary>
    /// <param name="objectRepository">场景对象仓储</param>
    /// <param name="slicingTaskRepository">切片任务仓储</param>
    /// <param name="sliceRepository">切片仓储</param>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="context">应用数据库上下文</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="minioStorageService">MinIO存储服务</param>
    /// <param name="configuration">配置服务</param>
    public SceneObjectService(
        IRepository<SceneObject> objectRepository,
        IRepository<SlicingTask> slicingTaskRepository,
        ISliceRepository sliceRepository,
        IUnitOfWork unitOfWork,
        ApplicationDbContext context,
        ILogger<SceneObjectService> logger,
        IMinioStorageService minioStorageService,
        IConfiguration configuration)
    {
        _objectRepository = objectRepository;
        _slicingTaskRepository = slicingTaskRepository;
        _sliceRepository = sliceRepository;
        _unitOfWork = unitOfWork;
        _context = context;
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        _logger = logger;
        _minioStorageService = minioStorageService;
        _configuration = configuration;
    }

    /// <summary>
    /// 创建场景对象
    /// </summary>
    /// <param name="request">场景对象创建请求，包含对象的基本信息和3D属性</param>
    /// <returns>创建成功的场景对象完整信息</returns>
    public async Task<SceneDtos.SceneObjectDto> CreateObjectAsync(SceneDtos.CreateSceneObjectRequest request)
    {
        var sceneObject = new SceneObject
        {
            SceneId = request.SceneId,
            Name = request.Name,
            Type = request.Type,
            Rotation = request.Rotation,
            Scale = request.Scale,
            ModelPath = request.ModelPath,
            MaterialData = request.MaterialData,
            Properties = request.Properties
        };

        // 创建位置点
        if (request.Position.Length >= 3)
        {
            sceneObject.Position = _geometryFactory.CreatePoint(
                new CoordinateZ(request.Position[0], request.Position[1], request.Position[2]));
        }

        await _objectRepository.AddAsync(sceneObject);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(sceneObject);
    }

    /// <summary>
    /// 根据ID获取场景对象详情
    /// </summary>
    /// <param name="id">场景对象唯一标识符</param>
    /// <returns>场景对象详情，如果不存在则返回null</returns>
    public async Task<SceneDtos.SceneObjectDto?> GetObjectByIdAsync(Guid id)
    {
        var obj = await _context.SceneObjects
            .Include(so => so.SlicingTask)
            .FirstOrDefaultAsync(so => so.Id == id && !so.IsDeleted);
        return obj != null ? MapToDto(obj) : null;
    }

    /// <summary>
    /// 获取指定场景的所有对象列表
    /// </summary>
    /// <param name="sceneId">场景ID</param>
    /// <returns>该场景下的所有对象列表，按创建时间倒序排列</returns>
    public async Task<IEnumerable<SceneDtos.SceneObjectDto>> GetSceneObjectsAsync(Guid sceneId)
    {
        var objects = await _context.SceneObjects
            .Include(o => o.SlicingTask)
            .Where(o => o.SceneId == sceneId && !o.IsDeleted)
            .ToListAsync();

        return objects.Select(MapToDto);
    }

    /// <summary>
    /// 更新场景对象
    /// </summary>
    /// <param name="id">场景对象唯一标识符</param>
    /// <param name="request">场景对象更新请求，包含要更新的属性</param>
    /// <returns>更新成功的场景对象完整信息，如果不存在则返回null</returns>
    public async Task<SceneDtos.SceneObjectDto?> UpdateObjectAsync(Guid id, SceneDtos.UpdateSceneObjectRequest request)
    {
        var sceneObject = await _objectRepository.GetByIdAsync(id);
        if (sceneObject == null)
        {
            return null;
        }

        // 更新对象属性（仅更新提供的字段）
        if (request.Name != null)
        {
            sceneObject.Name = request.Name;
        }

        if (request.Type != null)
        {
            sceneObject.Type = request.Type;
        }

        if (request.Position != null && request.Position.Length >= 3)
        {
            sceneObject.Position = _geometryFactory.CreatePoint(
                new CoordinateZ(request.Position[0], request.Position[1], request.Position[2]));
        }

        if (request.Rotation != null)
        {
            sceneObject.Rotation = request.Rotation;
        }

        if (request.Scale != null)
        {
            sceneObject.Scale = request.Scale;
        }

        if (request.ModelPath != null)
        {
            sceneObject.ModelPath = request.ModelPath;
        }

        if (request.MaterialData != null)
        {
            sceneObject.MaterialData = request.MaterialData;
        }

        if (request.Properties != null)
        {
            sceneObject.Properties = request.Properties;
        }

        sceneObject.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(sceneObject);
    }

    /// <summary>
    /// 删除场景对象（软删除）
    /// 同时删除关联的切片任务、切片数据以及MinIO中的模型文件
    /// </summary>
    /// <param name="id">要删除的场景对象ID</param>
    /// <returns>删除是否成功</returns>
    public async Task<bool> DeleteObjectAsync(Guid id)
    {
        var obj = await _objectRepository.GetByIdAsync(id);
        if (obj == null)
        {
            return false;
        }

        try
        {
            _logger.LogInformation("开始删除场景对象 {ObjectId}, ModelPath: {ModelPath}", id, obj.ModelPath);

            // 1. 删除MinIO中的模型文件（如果是MinIO路径）
            await DeleteMinioFileIfNeededAsync(obj.ModelPath);

            // 2. 查找并删除关联的切片任务
            var allTasks = await _slicingTaskRepository.GetAllAsync();
            var relatedTasks = allTasks.Where(t => t.SceneObjectId == id).ToList();

            if (relatedTasks.Count != 0)
            {
                _logger.LogInformation("删除场景对象 {ObjectId} 关联的 {Count} 个切片任务", id, relatedTasks.Count);

                foreach (var task in relatedTasks)
                {
                    // 获取切片任务的配置信息，以确定存储位置
                    var config = SlicingUtilities.ParseSlicingConfig(task.SlicingConfig);
                    var storageLocation = config.StorageLocation;

                    // 查找并删除所有关联的切片文件
                    var slices = await _sliceRepository.GetByTaskIdAsync(task.Id);
                    if (slices.Any())
                    {
                        _logger.LogInformation("删除切片任务 {TaskId} 关联的 {Count} 个切片文件，存储位置：{StorageLocation}",
                            task.Id, slices.Count(), storageLocation);
                        foreach (var slice in slices)
                        {
                            await SlicingUtilities.DeleteSliceFileAsync(
                                slice.FilePath, task.OutputPath, storageLocation, _minioStorageService, _logger);

                            // 删除数据库中的切片记录
                            await _sliceRepository.DeleteAsync(slice);
                        }
                    }

                    // 从数据库中删除切片记录
                    var deletedCount = await _sliceRepository.DeleteByTaskIdAsync(task.Id);
                    if (deletedCount > 0)
                    {
                        _logger.LogInformation("从数据库中删除了 {Count} 条切片记录", deletedCount);
                    }

                    // 删除切片任务
                    await _slicingTaskRepository.DeleteAsync(task);

                    // 删除切片索引文件和tileset.json
                    await SlicingUtilities.DeleteSliceIndexAndTilesetAsync(
                        task.OutputPath, storageLocation, _minioStorageService, _logger);
                }
            }

            // 3. 物理删除场景对象
            await _objectRepository.DeleteAsync(obj);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("成功删除场景对象 {ObjectId} 及其关联的切片数据", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除场景对象 {ObjectId} 时发生错误", id);
            throw;
        }
    }

    /// <summary>
    /// 删除MinIO文件（如果路径指向MinIO存储）
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns></returns>
    private async Task DeleteMinioFileIfNeededAsync(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogDebug("文件路径为空，跳过MinIO文件删除");
            return;
        }

        if (_minioStorageService == null)
        {
            _logger.LogWarning("MinIO存储服务未注入，无法删除MinIO文件");
            return;
        }

        try
        {
            _logger.LogInformation("检查文件路径类型: {FilePath}", filePath);

            string? bucket = null;
            string? objectName = null;

            // 已知的MinIO bucket列表
            var knownBuckets = new[]
            {
                MinioBuckets.MODELS_3D,
                MinioBuckets.BIM_MODELS,
                MinioBuckets.VIDEOS,
                MinioBuckets.TEXTURES,
                MinioBuckets.THUMBNAILS,
                MinioBuckets.TILT_PHOTOGRAPHY,
                MinioBuckets.DOCUMENTS,
                MinioBuckets.TEMP
            };

            // 从配置中读取MinIO endpoint
            var minioEndpoint = _configuration["MinIO:Endpoint"];
            var minioUseSSLStr = _configuration["MinIO:UseSSL"];
            var minioUseSSL = !string.IsNullOrEmpty(minioUseSSLStr) && bool.Parse(minioUseSSLStr);

            // 情况1: MinIO URL格式 (http://localhost:9000/bucket/object 或 https://...)
            // 支持预签名URL（包含查询参数）
            if (filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var uri = new Uri(filePath);
                    var fileHost = $"{uri.Host}:{uri.Port}";

                    // 检查是否是配置的MinIO endpoint
                    bool isMinioUrl = false;
                    if (!string.IsNullOrEmpty(minioEndpoint))
                    {
                        // 规范化endpoint进行比较
                        var normalizedMinioEndpoint = minioEndpoint.Replace("http://", "").Replace("https://", "");
                        isMinioUrl = fileHost.Equals(normalizedMinioEndpoint, StringComparison.OrdinalIgnoreCase) ||
                                    uri.Host.Equals(normalizedMinioEndpoint.Split(':')[0], StringComparison.OrdinalIgnoreCase);

                        _logger.LogInformation("URL Host比对 - 文件Host: {FileHost}, 配置Endpoint: {MinioEndpoint}, 是否匹配: {IsMatch}",
                            fileHost, minioEndpoint, isMinioUrl);
                    }

                    if (isMinioUrl)
                    {
                        // 提取路径部分（忽略查询参数），去掉开头的 /
                        // 预签名URL格式: http://localhost:9000/bucket/object?X-Amz-Algorithm=...
                        // AbsolutePath会自动去除查询参数，只保留路径部分
                        var path = uri.AbsolutePath.TrimStart('/');

                        // URL解码（处理 %2B 等编码字符）
                        path = Uri.UnescapeDataString(path);

                        var parts = path.Split('/', 2);

                        if (parts.Length == 2)
                        {
                            var possibleBucket = parts[0];
                            if (knownBuckets.Contains(possibleBucket, StringComparer.OrdinalIgnoreCase))
                            {
                                bucket = possibleBucket;
                                objectName = parts[1];
                                _logger.LogInformation("检测到MinIO URL格式（含签名参数） - Host: {Host}, Bucket: {Bucket}, ObjectName: {ObjectName}",
                                    uri.Host, bucket, objectName);
                            }
                            else
                            {
                                _logger.LogInformation("URL的bucket不在已知列表中，跳过删除: {Bucket}", possibleBucket);
                                return;
                            }
                        }
                        else
                        {
                            _logger.LogInformation("MinIO URL无法解析bucket/object结构，跳过删除: {FilePath}", filePath);
                            return;
                        }
                    }
                    else
                    {
                        _logger.LogInformation("检测到非MinIO的远程URL（host不匹配配置的endpoint），跳过删除: {FilePath}", filePath);
                        return;
                    }
                }
                catch (UriFormatException)
                {
                    _logger.LogWarning("URL格式无效，跳过删除: {FilePath}", filePath);
                    return;
                }
            }
            // 情况2: Windows本地路径 (C:\..., D:\...) 或者 Unix绝对路径
            else if (filePath.Contains(":\\") || filePath.StartsWith("/"))
            {
                _logger.LogInformation("检测到本地路径，跳过删除: {FilePath}", filePath);
                return;
            }
            // 情况3: 相对路径 (./, ../)
            else if (filePath.StartsWith("./") || filePath.StartsWith("../"))
            {
                _logger.LogInformation("检测到相对路径，跳过删除: {FilePath}", filePath);
                return;
            }
            // 情况4: MinIO相对路径
            else
            {
                var normalizedPath = filePath.TrimStart('/');
                var parts = normalizedPath.Split('/', 2);

                if (parts.Length == 2)
                {
                    var possibleBucket = parts[0];

                    if (knownBuckets.Contains(possibleBucket, StringComparer.OrdinalIgnoreCase))
                    {
                        bucket = possibleBucket;
                        objectName = parts[1];
                        _logger.LogInformation("检测到MinIO相对路径 - Bucket: {Bucket}, ObjectName: {ObjectName}", bucket, objectName);
                    }
                    else
                    {
                        _logger.LogInformation("检测到Unix本地路径（非MinIO bucket），跳过删除: {FilePath}", filePath);
                        return;
                    }
                }
                else
                {
                    _logger.LogInformation("无法解析为MinIO路径（缺少bucket/object结构），跳过删除: {FilePath}", filePath);
                    return;
                }
            }

            // 执行删除操作
            if (!string.IsNullOrEmpty(bucket) && !string.IsNullOrEmpty(objectName))
            {
                // 检查是否是文件夹路径（批量上传的文件）
                // 例如: "HouseName/house.obj" 或 "HouseName/house.mtl"
                var lastSlashIndex = objectName.LastIndexOf('/');

                if (lastSlashIndex > 0)
                {
                    // 这是文件夹中的文件，提取文件夹路径
                    var folderPath = objectName.Substring(0, lastSlashIndex + 1);

                    _logger.LogInformation("检测到文件夹路径，准备删除整个文件夹: {Bucket}/{FolderPath}", bucket, folderPath);

                    try
                    {
                        // 列出文件夹中的所有文件
                        var filesInFolder = await _minioStorageService.ListFilesAsync(bucket, folderPath);

                        if (filesInFolder.Any())
                        {
                            _logger.LogInformation("文件夹 {FolderPath} 包含 {Count} 个文件，开始批量删除", folderPath, filesInFolder.Count);

                            int successCount = 0;
                            int failCount = 0;

                            // 删除文件夹中的每个文件
                            foreach (var fileKey in filesInFolder)
                            {
                                try
                                {
                                    var deleted = await _minioStorageService.DeleteFileAsync(bucket, fileKey);

                                    if (deleted)
                                    {
                                        successCount++;
                                        _logger.LogInformation("  ✓ 已删除: {Bucket}/{FileKey}", bucket, fileKey);
                                    }
                                    else
                                    {
                                        failCount++;
                                        _logger.LogWarning("  ✗ 删除失败: {Bucket}/{FileKey}", bucket, fileKey);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    failCount++;
                                    _logger.LogError(ex, "  ✗ 删除文件时发生异常: {Bucket}/{FileKey}", bucket, fileKey);
                                }
                            }

                            _logger.LogInformation("文件夹删除完成: 成功 {SuccessCount}/{TotalCount}, 失败 {FailCount}",
                                successCount, filesInFolder.Count, failCount);
                        }
                        else
                        {
                            _logger.LogInformation("文件夹 {FolderPath} 为空或不存在", folderPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "列出或删除文件夹内容时发生错误: {Bucket}/{FolderPath}", bucket, folderPath);

                        // 如果文件夹删除失败，尝试删除单个文件作为后备方案
                        _logger.LogInformation("回退到单文件删除模式: {Bucket}/{ObjectName}", bucket, objectName);
                        var deleted = await _minioStorageService.DeleteFileAsync(bucket, objectName);

                        if (deleted)
                        {
                            _logger.LogInformation("✓ 成功删除单个文件: {Bucket}/{ObjectName}", bucket, objectName);
                        }
                        else
                        {
                            _logger.LogWarning("✗ 单个文件删除也失败: {Bucket}/{ObjectName}", bucket, objectName);
                        }
                    }
                }
                else
                {
                    // 这是单个文件（不在文件夹中），直接删除
                    _logger.LogInformation("尝试删除单个文件: {Bucket}/{ObjectName}", bucket, objectName);

                    var deleted = await _minioStorageService.DeleteFileAsync(bucket, objectName);

                    if (deleted)
                    {
                        _logger.LogInformation("✓ 成功删除MinIO文件: {Bucket}/{ObjectName}", bucket, objectName);
                    }
                    else
                    {
                        _logger.LogWarning("✗ MinIO文件删除失败或文件不存在: {Bucket}/{ObjectName}", bucket, objectName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // 不抛出异常，只记录日志，避免影响主流程
            _logger.LogError(ex, "删除MinIO文件时发生错误: {FilePath}", filePath);
        }
    }

    /// <summary>
    /// 将领域对象映射为DTO对象
    /// </summary>
    /// <param name="obj">场景对象领域实体</param>
    /// <returns>场景对象DTO，包含前端所需的所有信息</returns>
    private static SceneDtos.SceneObjectDto MapToDto(SceneObject obj)
    {
        return new SceneDtos.SceneObjectDto
        {
            Id = obj.Id,
            SceneId = obj.SceneId,
            Name = obj.Name,
            Type = obj.Type,
            Position = obj.Position != null
                ? new[] { obj.Position.X, obj.Position.Y, obj.Position.Z }
                : Array.Empty<double>(),
            Rotation = obj.Rotation,
            Scale = obj.Scale,
            ModelPath = obj.ModelPath,
            MaterialData = obj.MaterialData,
            Properties = obj.Properties,
            CreatedAt = obj.CreatedAt,
            SlicingTaskId = obj.SlicingTask?.Id,
            SlicingTaskStatus = obj.SlicingTask?.Status.ToString()
        };
    }
}
