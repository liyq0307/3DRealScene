using Microsoft.EntityFrameworkCore;
using RealScene3D.Domain.Enums;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.Data;
using RealScene3D.Infrastructure.MinIO;

namespace RealScene3D.Application.Services;

/// <summary>
/// 场景服务实现
/// </summary>
public class SceneService : ISceneService
{
    private readonly IRepository<Scene3D> _sceneRepository;
    private readonly IRepository<SceneObject> _sceneObjectRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _context;
    private readonly GeometryFactory _geometryFactory;
    private readonly GeoJsonReader _geoJsonReader;
    private readonly IMinioStorageService _minioStorageService;

    public SceneService(
        IRepository<Scene3D> sceneRepository,
        IRepository<SceneObject> sceneObjectRepository,
        IUnitOfWork unitOfWork,
        ApplicationDbContext context,
        IMinioStorageService minioStorageService)
    {
        _sceneRepository = sceneRepository;
        _sceneObjectRepository = sceneObjectRepository;
        _unitOfWork = unitOfWork;
        _context = context;
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        _geoJsonReader = new GeoJsonReader();
        _minioStorageService = minioStorageService;
    }

    /// <summary>
    /// 创建3D场景
    /// </summary>
    /// <param name="request">场景创建请求，包含场景基本信息和地理数据</param>
    /// <param name="ownerId">场景所有者用户ID</param>
    /// <returns>创建成功的场景完整信息</returns>
    public async Task<SceneDtos.SceneDto> CreateSceneAsync(SceneDtos.CreateSceneRequest request, Guid ownerId)
    {
        var scene = new Scene3D
        {
            Name = request.Name,
            Description = request.Description,
            OwnerId = ownerId,
            Metadata = request.Metadata,
            RenderEngine = request.RenderEngine
        };

        // 解析GeoJSON边界
        if (!string.IsNullOrEmpty(request.BoundaryGeoJson))
        {
            try
            {
                var geometry = _geoJsonReader.Read<Polygon>(request.BoundaryGeoJson);
                scene.Boundary = geometry;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Invalid boundary GeoJSON: {ex.Message}");
            }
        }

        // 创建中心点
        if (request.CenterPoint != null && request.CenterPoint.Length >= 2)
        {
            var z = request.CenterPoint.Length > 2 ? request.CenterPoint[2] : 0;
            scene.CenterPoint = _geometryFactory.CreatePoint(
                new CoordinateZ(request.CenterPoint[0], request.CenterPoint[1], z));
        }

        await _sceneRepository.AddAsync(scene);

        foreach (var objRequest in request.SceneObjects)
        {
            var sceneObject = new SceneObject
            {
                SceneId = scene.Id,
                Name = objRequest.Name,
                Type = objRequest.Type,
                Position = _geometryFactory.CreatePoint(new CoordinateZ(objRequest.Position[0], objRequest.Position[1], objRequest.Position.Length > 2 ? objRequest.Position[2] : 0)),
                Rotation = objRequest.Rotation,
                Scale = objRequest.Scale,
                ModelPath = objRequest.ModelPath,
                MaterialData = objRequest.MaterialData,
                Properties = objRequest.Properties
            };
            scene.SceneObjects.Add(sceneObject);
        }

        await _unitOfWork.SaveChangesAsync();

        return MapToDto(scene);
    }

    /// <summary>
    /// 根据ID获取场景详情
    /// </summary>
    /// <param name="id">场景唯一标识符</param>
    /// <returns>场景详情，如果不存在则返回null</returns>
    public async Task<SceneDtos.SceneDto?> GetSceneByIdAsync(Guid id)
    {
        var scene = await _context.Scenes
            .Include(s => s.SceneObjects)
                .ThenInclude(so => so.SlicingTask)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (scene == null)
            return null;

        var sceneDto = MapToDto(scene);
        return EnrichWithPresignedUrlsAsync(sceneDto);
    }

    /// <summary>
    /// 获取指定用户的所有场景列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户拥有的场景列表，按创建时间倒序排列</returns>
    public async Task<IEnumerable<SceneDtos.SceneDto>> GetUserScenesAsync(Guid userId)
    {
        var scenes = await _context.Scenes
            .Include(s => s.SceneObjects)
                .ThenInclude(so => so.SlicingTask)
            .Where(s => s.OwnerId == userId)
            .ToListAsync();

        return scenes.Select(MapToDto);
    }

    /// <summary>
    /// 获取所有公开场景列表
    /// </summary>
    /// <returns>所有公开可访问的场景列表</returns>
    public async Task<IEnumerable<SceneDtos.SceneDto>> GetAllScenesAsync()
    {
        var scenes = await _context.Scenes
            .Include(s => s.SceneObjects)
                .ThenInclude(so => so.SlicingTask)
            .ToListAsync();
        return scenes.Select(MapToDto);
    }

    /// <summary>
    /// 删除场景（物理删除）
    /// </summary>
    /// <param name="id">要删除的场景ID</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>删除是否成功</returns>
    public async Task<bool> DeleteSceneAsync(Guid id, Guid userId)
    {
        var scene = await _context.Scenes
            .Include(s => s.SceneObjects)
                .ThenInclude(so => so.SlicingTask)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (scene == null || scene.OwnerId != userId)
        {
            return false;
        }

        // 1. 删除场景下的所有对象（包括MinIO文件和切片任务）
        if (scene.SceneObjects.Any())
        {
            foreach (var sceneObject in scene.SceneObjects.ToList())
            {
                // 删除MinIO中的模型文件
                await DeleteMinioFileIfNeededAsync(sceneObject.ModelPath);

                // 删除关联的切片任务和切片文件
                if (sceneObject.SlicingTask != null)
                {
                    await DeleteSlicingTaskAndFilesAsync(sceneObject.SlicingTask.Id);
                }

                // 从数据库删除场景对象
                _context.SceneObjects.Remove(sceneObject);
            }
        }

        // 2. 从数据库物理删除场景
        _context.Scenes.Remove(scene);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// 删除MinIO文件（如果路径指向MinIO存储）
    /// 支持删除单个文件或整个文件夹（批量上传的OBJ+MTL+纹理）
    /// </summary>
    private async Task DeleteMinioFileIfNeededAsync(string? modelPath)
    {
        if (string.IsNullOrEmpty(modelPath))
        {
            Console.WriteLine("[SceneService] ModelPath为空，跳过删除");
            return;
        }

        Console.WriteLine($"[SceneService] 开始处理文件删除: {modelPath}");

        try
        {
            // 提取bucket和文件路径
            string? bucketName = null;
            string? objectName = null;

            // 情况1: /api/files/proxy/bucket/object 格式
            if (modelPath.Contains("/api/files/proxy/"))
            {
                Console.WriteLine("[SceneService] 检测到代理路径格式");
                var parts = modelPath.Split(new[] { "/api/files/proxy/" }, StringSplitOptions.None);
                if (parts.Length > 1)
                {
                    var pathParts = parts[1].Split('/', 2);
                    bucketName = pathParts[0];
                    objectName = pathParts.Length > 1 ? pathParts[1] : "";
                    Console.WriteLine($"[SceneService] 解析结果: Bucket={bucketName}, Object={objectName}");
                }
            }
            // 情况2: 预签名URL或直接MinIO URL (http://localhost:9000/bucket/object 或 https://...)
            else if (modelPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     modelPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[SceneService] 检测到HTTP(S) URL格式");
                try
                {
                    var uri = new Uri(modelPath);
                    // 提取路径部分（去除查询参数）
                    var path = uri.AbsolutePath.TrimStart('/');
                    Console.WriteLine($"[SceneService] URL路径: {path}");

                    var pathParts = path.Split('/', 2);
                    if (pathParts.Length >= 2)
                    {
                        bucketName = pathParts[0];
                        objectName = pathParts[1];
                        Console.WriteLine($"[SceneService] 解析结果: Bucket={bucketName}, Object={objectName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SceneService] URL解析失败: {ex.Message}");
                }
            }
            // 情况3: 相对路径 bucket/object 格式
            else if (modelPath.Contains("/"))
            {
                Console.WriteLine("[SceneService] 检测到相对路径格式");
                var pathParts = modelPath.TrimStart('/').Split('/', 2);
                if (pathParts.Length >= 2)
                {
                    bucketName = pathParts[0];
                    objectName = pathParts[1];
                    Console.WriteLine($"[SceneService] 解析结果: Bucket={bucketName}, Object={objectName}");
                }
            }

            if (string.IsNullOrEmpty(bucketName) || string.IsNullOrEmpty(objectName))
            {
                Console.WriteLine($"[SceneService] 无法解析路径，跳过删除: {modelPath}");
                return;
            }

            // 执行删除操作
            if (!string.IsNullOrEmpty(objectName))
            {
                // 检查是否是文件夹路径（批量上传的文件）
                var lastSlashIndex = objectName.LastIndexOf('/');

                if (lastSlashIndex > 0)
                {
                    // 这是文件夹中的文件，提取文件夹路径并删除整个文件夹
                    var folderPath = objectName.Substring(0, lastSlashIndex + 1);

                    Console.WriteLine($"[SceneService] 检测到文件夹路径，准备删除整个文件夹: {bucketName}/{folderPath}");

                    try
                    {
                        // 列出文件夹中的所有文件
                        var filesInFolder = await _minioStorageService.ListFilesAsync(bucketName, folderPath);

                        if (filesInFolder.Any())
                        {
                            Console.WriteLine($"[SceneService] 文件夹 {folderPath} 包含 {filesInFolder.Count} 个文件，开始批量删除");

                            int successCount = 0;
                            foreach (var fileKey in filesInFolder)
                            {
                                var deleted = await _minioStorageService.DeleteFileAsync(bucketName, fileKey);
                                if (deleted)
                                {
                                    successCount++;
                                    Console.WriteLine($"[SceneService]   ✓ 已删除: {bucketName}/{fileKey}");
                                }
                            }

                            Console.WriteLine($"[SceneService] 文件夹删除完成: 成功 {successCount}/{filesInFolder.Count}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SceneService] 列出或删除文件夹内容时发生错误: {bucketName}/{folderPath}, 错误: {ex.Message}");

                        // 回退到单文件删除
                        await _minioStorageService.DeleteFileAsync(bucketName, objectName);
                    }
                }
                else
                {
                    // 单个文件，直接删除
                    Console.WriteLine($"[SceneService] 删除单个文件: {bucketName}/{objectName}");
                    var deleted = await _minioStorageService.DeleteFileAsync(bucketName, objectName);
                    if (deleted)
                    {
                        Console.WriteLine($"[SceneService] ✓ 成功删除: {bucketName}/{objectName}");
                    }
                    else
                    {
                        Console.WriteLine($"[SceneService] ✗ 删除失败: {bucketName}/{objectName}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SceneService] 删除MinIO文件失败: {modelPath}, 错误: {ex.Message}");
            Console.WriteLine($"[SceneService] 错误堆栈: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 删除切片任务及其所有切片文件
    /// </summary>
    private async Task DeleteSlicingTaskAndFilesAsync(Guid taskId)
    {
        var task = await _context.SlicingTasks
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null)
            return;

        // 查找并删除所有切片文件
        var slices = await _context.Slices
            .Where(s => s.SlicingTaskId == taskId)
            .ToListAsync();

        if (slices.Any())
        {
            foreach (var slice in slices)
            {
                try
                {
                    // 从MinIO删除切片文件
                    if (!string.IsNullOrEmpty(slice.FilePath))
                    {
                        await _minioStorageService.DeleteFileAsync("slices", slice.FilePath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"删除切片文件失败: {slice.FilePath}, 错误: {ex.Message}");
                }

                // 从数据库删除切片记录
                _context.Slices.Remove(slice);
            }
        }

        // 删除tileset.json等索引文件
        if (!string.IsNullOrEmpty(task.OutputPath))
        {
            try
            {
                await _minioStorageService.DeleteFileAsync("slices", task.OutputPath + "/tileset.json");
            }
            catch { }
        }

        // 从数据库删除切片任务
        _context.SlicingTasks.Remove(task);
    }

    /// <summary>
    /// 更新场景信息
    /// </summary>
    /// <param name="id">场景ID</param>
    /// <param name="request">更新请求，包含需要更新的场景信息</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>更新后的场景信息，如果场景不存在或无权限则返回null</returns>
    public async Task<SceneDtos.SceneDto?> UpdateSceneAsync(Guid id, SceneDtos.UpdateSceneRequest request, Guid userId)
    {
        var scene = await _sceneRepository.GetByIdAsync(id);
        if (scene == null || scene.OwnerId != userId)
        {
            return null;
        }

        // 更新基本信息
        if (request.Name != null)
        {
            scene.Name = request.Name;
        }

        if (request.Description != null)
        {
            scene.Description = request.Description;
        }

        if (request.Metadata != null)
        {
            scene.Metadata = request.Metadata;
        }

        if (request.RenderEngine != null)
        {
            scene.RenderEngine = request.RenderEngine;
        }

        // 更新GeoJSON边界
        if (request.BoundaryGeoJson != null)
        {
            try
            {
                var geometry = _geoJsonReader.Read<Polygon>(request.BoundaryGeoJson);
                scene.Boundary = geometry;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Invalid boundary GeoJSON: {ex.Message}");
            }
        }

        // 更新中心点
        if (request.CenterPoint != null && request.CenterPoint.Length >= 2)
        {
            var z = request.CenterPoint.Length > 2 ? request.CenterPoint[2] : 0;
            scene.CenterPoint = _geometryFactory.CreatePoint(
                new CoordinateZ(request.CenterPoint[0], request.CenterPoint[1], z));
        }

        scene.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(scene);
    }

    /// <summary>
    /// 将领域对象映射为DTO对象
    /// </summary>
    /// <param name="scene">场景领域实体</param>
    /// <returns>场景DTO，包含前端所需的所有信息，包括地理数据转换</returns>
    private static SceneDtos.SceneDto MapToDto(Scene3D scene)
    {
        var geoJsonWriter = new GeoJsonWriter();

        return new SceneDtos.SceneDto
        {
            Id = scene.Id,
            Name = scene.Name,
            Description = scene.Description,
            BoundaryGeoJson = scene.Boundary != null ? geoJsonWriter.Write(scene.Boundary) : null,
            CenterPoint = scene.CenterPoint != null
                ? new[] { scene.CenterPoint.X, scene.CenterPoint.Y, scene.CenterPoint.Z }
                : null,
            Metadata = scene.Metadata,
            RenderEngine = scene.RenderEngine,
            OwnerId = scene.OwnerId,
            CreatedAt = scene.CreatedAt,
            SceneObjects = scene.SceneObjects.Select(so =>
            {
                var slicingOutputPath = so.SlicingTask?.Status == SlicingTaskStatus.Completed ? so.SlicingTask.OutputPath : null;

                // 如果切片任务完成，displayPath应该指向tileset.json
                string? displayPath = null;
                if (!string.IsNullOrEmpty(slicingOutputPath) && so.SlicingTask?.Status == SlicingTaskStatus.Completed)
                {
                    // 构建tileset.json路径
                    if (slicingOutputPath.EndsWith("tileset.json", StringComparison.OrdinalIgnoreCase))
                    {
                        displayPath = slicingOutputPath;
                    }
                    else if (slicingOutputPath.EndsWith('/') || slicingOutputPath.EndsWith('\\'))
                    {
                        displayPath = slicingOutputPath + "tileset.json";
                    }
                    else
                    {
                        displayPath = slicingOutputPath + "/tileset.json";
                    }
                }
                else
                {
                    // 如果没有切片输出或切片未完成，使用原始模型路径
                    displayPath = so.ModelPath;
                }

                Console.WriteLine($"[SceneService] 场景对象: {so.Name}");
                Console.WriteLine($"[SceneService]   ModelPath: {so.ModelPath}");
                Console.WriteLine($"[SceneService]   SlicingTask: {(so.SlicingTask != null ? so.SlicingTask.Id.ToString() : "null")}");
                Console.WriteLine($"[SceneService]   SlicingStatus: {so.SlicingTask?.Status.ToString() ?? "null"}");
                Console.WriteLine($"[SceneService]   SlicingOutputPath: {slicingOutputPath ?? "null"}");
                Console.WriteLine($"[SceneService]   DisplayPath: {displayPath ?? "null"}");

                return new SceneDtos.SceneObjectDto
                {
                    Id = so.Id,
                    SceneId = so.SceneId,
                    Name = so.Name,
                    Type = so.Type,
                    Position = so.Position != null ? new[] { so.Position.X, so.Position.Y, so.Position.Z } : Array.Empty<double>(),
                    Rotation = so.Rotation,
                    Scale = so.Scale,
                    ModelPath = so.ModelPath,
                    MaterialData = so.MaterialData,
                    Properties = so.Properties,
                    CreatedAt = so.CreatedAt,
                    SlicingTaskId = so.SlicingTask?.Id,
                    SlicingTaskStatus = so.SlicingTask?.Status.ToString(),
                    SlicingOutputPath = slicingOutputPath,
                    DisplayPath = displayPath
                };
            }).ToList()
        };
    }

    /// <summary>
    /// 为场景对象生成后端代理路径（用于访问MinIO存储）
    /// </summary>
    private SceneDtos.SceneDto EnrichWithPresignedUrlsAsync(SceneDtos.SceneDto sceneDto)
    {
        foreach (var obj in sceneDto.SceneObjects)
        {
            if (string.IsNullOrEmpty(obj.DisplayPath))
                continue;

            // 如果已经是完整URL（包含http或https），跳过
            if (obj.DisplayPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                obj.DisplayPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // 使用后端代理路径访问MinIO对象
            try
            {
                // 根据场景对象是否有完成的切片任务来决定使用哪个存储桶
                string bucketName;
                if (obj.SlicingTaskStatus == SlicingTaskStatus.Completed.ToString() && !string.IsNullOrEmpty(obj.SlicingOutputPath))
                {
                    // 切片输出使用 slices 存储桶
                    bucketName = MinioBuckets.SLICES;
                }
                else
                {
                    // 原始模型使用 models-3d 存储桶
                    bucketName = MinioBuckets.MODELS_3D;
                }

                obj.DisplayPath = $"/api/files/proxy/{bucketName}/{obj.DisplayPath}";
            }
            catch (Exception ex)
            {
                // 如果构建代理路径失败，保留原始路径
                Console.WriteLine($"Failed to build proxy path for {obj.Name}: {ex.Message}");
            }
        }

        return sceneDto;
    }
}
