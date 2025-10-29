namespace RealScene3D.Domain.Interfaces;

/// <summary>
/// 切片仓储接口
/// 扩展通用仓储接口，添加切片特定的查询方法
/// </summary>
public interface ISliceRepository : IRepository<Entities.Slice>
{
    /// <summary>
    /// 根据切片任务ID获取所有切片
    /// 性能优化：避免加载所有切片到内存，只加载指定任务的切片
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <returns>属于指定任务的所有切片</returns>
    Task<IEnumerable<Entities.Slice>> GetByTaskIdAsync(Guid taskId);

    /// <summary>
    /// 根据切片任务ID和层级获取切片
    /// 用于更精细的查询优化
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="level">LOD层级</param>
    /// <returns>属于指定任务和层级的切片</returns>
    Task<IEnumerable<Entities.Slice>> GetByTaskIdAndLevelAsync(Guid taskId, int level);

    /// <summary>
    /// 批量删除指定任务的切片
    /// 用于清理或重新生成切片
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <returns>删除的切片数量</returns>
    Task<int> DeleteByTaskIdAsync(Guid taskId);
}
