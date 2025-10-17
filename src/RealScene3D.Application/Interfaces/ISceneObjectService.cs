using RealScene3D.Application.DTOs;

namespace RealScene3D.Application.Interfaces;

/// <summary>
/// 3D场景对象应用服务接口
/// 提供场景对象的创建、查询、删除等业务操作
/// </summary>
public interface ISceneObjectService
{
    /// <summary>
    /// 创建场景对象
    /// </summary>
    /// <param name="request">场景对象创建请求，包含对象的基本信息和3D属性</param>
    /// <returns>创建成功的场景对象完整信息</returns>
    Task<SceneDtos.SceneObjectDto> CreateObjectAsync(SceneDtos.CreateSceneObjectRequest request);

    /// <summary>
    /// 根据ID获取场景对象详情
    /// </summary>
    /// <param name="id">场景对象唯一标识符</param>
    /// <returns>场景对象详情，如果不存在则返回null</returns>
    Task<SceneDtos.SceneObjectDto?> GetObjectByIdAsync(Guid id);

    /// <summary>
    /// 获取指定场景的所有对象列表
    /// </summary>
    /// <param name="sceneId">场景ID</param>
    /// <returns>该场景下的所有对象列表</returns>
    Task<IEnumerable<SceneDtos.SceneObjectDto>> GetSceneObjectsAsync(Guid sceneId);

    /// <summary>
    /// 删除场景对象
    /// </summary>
    /// <param name="id">要删除的场景对象ID</param>
    /// <returns>删除是否成功</returns>
    Task<bool> DeleteObjectAsync(Guid id);
}
