using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.Data;

namespace RealScene3D.Application.Services;

/// <summary>
/// 3D场景对象应用服务实现类
/// 负责场景对象的创建、查询、删除等业务操作
/// 支持3D对象的位置管理、属性配置和空间数据处理
/// </summary>
public class SceneObjectService : ISceneObjectService
{
    private readonly IRepository<SceneObject> _objectRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _context;
    private readonly GeometryFactory _geometryFactory;

    /// <summary>
    /// 构造函数 - 依赖注入仓储和数据库上下文
    /// </summary>
    /// <param name="objectRepository">场景对象仓储接口</param>
    /// <param name="unitOfWork">工作单元接口</param>
    /// <param name="context">应用数据库上下文</param>
    public SceneObjectService(
        IRepository<SceneObject> objectRepository,
        IUnitOfWork unitOfWork,
        ApplicationDbContext context)
    {
        _objectRepository = objectRepository;
        _unitOfWork = unitOfWork;
        _context = context;
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
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
        var obj = await _objectRepository.GetByIdAsync(id);
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
            .Where(o => o.SceneId == sceneId)
            .ToListAsync();

        return objects.Select(MapToDto);
    }

    /// <summary>
    /// 删除场景对象（软删除）
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

        obj.IsDeleted = true;
        obj.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        return true;
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
            CreatedAt = obj.CreatedAt
        };
    }
}
