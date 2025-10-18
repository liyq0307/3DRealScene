using System.Linq.Expressions;

namespace RealScene3D.Domain.Interfaces;

/// <summary>
/// MongoDB 通用仓储接口
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public interface IMongoRepository<T> where T : class
{
    /// <summary>
    /// 根据 ID 获取文档
    /// </summary>
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据场景 ID 获取文档
    /// </summary>
    Task<T?> GetBySceneIdAsync(Guid sceneId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据场景 ID 获取所有文档
    /// </summary>
    Task<IEnumerable<T>> GetAllBySceneIdAsync(Guid sceneId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有文档
    /// </summary>
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件查询文档
    /// </summary>
    Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询文档
    /// </summary>
    Task<(IEnumerable<T> Items, long TotalCount)> FindPagedAsync(
        Expression<Func<T, bool>> filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加文档
    /// </summary>
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量添加文档
    /// </summary>
    Task AddManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新文档
    /// </summary>
    Task<bool> UpdateAsync(string id, T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 部分更新文档
    /// </summary>
    Task<bool> UpdateFieldsAsync<TField>(
        string id,
        Expression<Func<T, TField>> field,
        TField value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除文档
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件删除文档
    /// </summary>
    Task<long> DeleteManyAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// 统计文档数量
    /// </summary>
    Task<long> CountAsync(
        Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查文档是否存在
    /// </summary>
    Task<bool> ExistsAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据标签查询(用于视频元数据)
    /// </summary>
    Task<IEnumerable<T>> FindByTagsAsync(
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default);
}
