using Microsoft.EntityFrameworkCore;
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
            Metadata = request.Metadata
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
    /// 删除场景（软删除）
    /// </summary>
    /// <param name="id">要删除的场景ID</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>删除是否成功</returns>
    public async Task<bool> DeleteSceneAsync(Guid id, Guid userId)
    {
        var scene = await _sceneRepository.GetByIdAsync(id);
        if (scene == null || scene.OwnerId != userId)
        {
            return false;
        }

        scene.IsDeleted = true;
        scene.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        return true;
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
