using RealScene3D.Application.DTOs;

namespace RealScene3D.Application.Interfaces;

/// <summary>
/// 3D场景应用服务接口
/// 提供场景的创建、查询、删除等核心业务操作
/// 支持多用户场景管理和权限控制
/// </summary>
public interface ISceneService
{
    /// <summary>
    /// 创建3D场景
    /// </summary>
    /// <param name="request">场景创建请求，包含场景基本信息和地理数据</param>
    /// <param name="ownerId">场景所有者用户ID</param>
    /// <returns>创建成功的场景完整信息</returns>
    Task<SceneDtos.SceneDto> CreateSceneAsync(SceneDtos.CreateSceneRequest request, Guid ownerId);

    /// <summary>
    /// 根据ID获取场景详情
    /// </summary>
    /// <param name="id">场景唯一标识符</param>
    /// <returns>场景详情，如果不存在则返回null</returns>
    Task<SceneDtos.SceneDto?> GetSceneByIdAsync(Guid id);

    /// <summary>
    /// 获取指定用户的所有场景列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户拥有的场景列表</returns>
    Task<IEnumerable<SceneDtos.SceneDto>> GetUserScenesAsync(Guid userId);

    /// <summary>
    /// 获取所有公开场景列表
    /// </summary>
    /// <returns>所有公开可访问的场景列表</returns>
    Task<IEnumerable<SceneDtos.SceneDto>> GetAllScenesAsync();

    /// <summary>
    /// 删除场景
    /// </summary>
    /// <param name="id">要删除的场景ID</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>删除是否成功</returns>
    Task<bool> DeleteSceneAsync(Guid id, Guid userId);

    /// <summary>
    /// 更新场景信息
    /// </summary>
    /// <param name="id">场景ID</param>
    /// <param name="request">更新请求，包含需要更新的场景信息</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>更新后的场景信息，如果场景不存在或无权限则返回null</returns>
    Task<SceneDtos.SceneDto?> UpdateSceneAsync(Guid id, SceneDtos.UpdateSceneRequest request, Guid userId);
}
