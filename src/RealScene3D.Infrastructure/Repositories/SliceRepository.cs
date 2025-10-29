using Microsoft.EntityFrameworkCore;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Infrastructure.Repositories;

/// <summary>
/// 切片仓储实现
/// 提供切片特定的高效查询方法
/// </summary>
public class SliceRepository : Repository<Slice>, ISliceRepository
{
    public SliceRepository(DbContext context) : base(context)
    {
    }

    /// <summary>
    /// 根据切片任务ID获取所有切片
    /// 性能优化：使用索引查询，避免加载所有切片到内存
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <returns>属于指定任务的所有切片</returns>
    public async Task<IEnumerable<Slice>> GetByTaskIdAsync(Guid taskId)
    {
        return await _dbSet
            .Where(s => s.SlicingTaskId == taskId)
            .AsNoTracking() // 性能优化：不跟踪实体变化
            .ToListAsync();
    }

    /// <summary>
    /// 根据切片任务ID和层级获取切片
    /// 用于更精细的查询优化
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="level">LOD层级</param>
    /// <returns>属于指定任务和层级的切片</returns>
    public async Task<IEnumerable<Slice>> GetByTaskIdAndLevelAsync(Guid taskId, int level)
    {
        return await _dbSet
            .Where(s => s.SlicingTaskId == taskId && s.Level == level)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// 批量删除指定任务的切片
    /// 用于清理或重新生成切片
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <returns>删除的切片数量</returns>
    public async Task<int> DeleteByTaskIdAsync(Guid taskId)
    {
        var slicesToDelete = await _dbSet
            .Where(s => s.SlicingTaskId == taskId)
            .ToListAsync();

        _dbSet.RemoveRange(slicesToDelete);

        return slicesToDelete.Count;
    }
}
