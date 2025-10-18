using MongoDB.Bson;
using MongoDB.Driver;
using RealScene3D.Infrastructure.MongoDB.Entities;

namespace RealScene3D.Infrastructure.MongoDB.Repositories;

/// <summary>
/// BIM 模型元数据仓储接口
/// </summary>
public interface IBimModelMetadataRepository : Domain.Interfaces.IMongoRepository<BimModelMetadata>
{
    /// <summary>
    /// 根据项目名称搜索
    /// </summary>
    Task<IEnumerable<BimModelMetadata>> SearchByProjectNameAsync(
        string projectName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据学科类型查询
    /// </summary>
    Task<IEnumerable<BimModelMetadata>> GetByDisciplineAsync(
        string discipline,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据格式查询
    /// </summary>
    Task<IEnumerable<BimModelMetadata>> GetByFormatAsync(
        string format,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据场景 ID 和学科查询
    /// </summary>
    Task<IEnumerable<BimModelMetadata>> GetBySceneAndDisciplineAsync(
        Guid sceneId,
        string discipline,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取构件统计信息
    /// </summary>
    Task<BimElementStats> GetElementStatsAsync(
        string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 确保所有索引已创建
    /// </summary>
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// BIM 模型元数据仓储实现
/// </summary>
public class BimModelMetadataRepository : MongoRepositoryBase<BimModelMetadata>, IBimModelMetadataRepository
{
    public BimModelMetadataRepository(MongoDbContext context)
        : base(context.BimModelMetadata)
    {
    }

    public async Task<IEnumerable<BimModelMetadata>> SearchByProjectNameAsync(
        string projectName,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<BimModelMetadata>.Filter.Regex(
            b => b.ProjectName,
            new BsonRegularExpression(projectName, "i"));

        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<BimModelMetadata>> GetByDisciplineAsync(
        string discipline,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<BimModelMetadata>.Filter.Eq(b => b.Discipline, discipline);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<BimModelMetadata>> GetByFormatAsync(
        string format,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<BimModelMetadata>.Filter.Eq(b => b.Format, format);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<BimModelMetadata>> GetBySceneAndDisciplineAsync(
        Guid sceneId,
        string discipline,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<BimModelMetadata>.Filter.And(
            Builders<BimModelMetadata>.Filter.Eq(b => b.SceneId, sceneId),
            Builders<BimModelMetadata>.Filter.Eq(b => b.Discipline, discipline)
        );

        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<BimElementStats> GetElementStatsAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var model = await GetByIdAsync(id, cancellationToken);
        return model?.Elements ?? new BimElementStats();
    }

    /// <summary>
    /// 创建 BIM 模型元数据集合的所有索引
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        // 1. SceneId 索引 - 用于按场景查询
        await CreateIndexAsync(b => b.SceneId, cancellationToken: cancellationToken);

        // 2. ProjectName 索引 - 用于项目查询
        await CreateIndexAsync(b => b.ProjectName, cancellationToken: cancellationToken);

        // 3. Discipline 索引 - 用于按学科筛选
        await CreateIndexAsync(b => b.Discipline, cancellationToken: cancellationToken);

        // 4. Format 索引 - 用于按格式筛选
        await CreateIndexAsync(b => b.Format, cancellationToken: cancellationToken);

        // 5. UploadedBy 索引 - 用于按用户查询
        await CreateIndexAsync(b => b.UploadedBy, cancellationToken: cancellationToken);

        // 6. UploadedAt 索引 - 用于时间排序
        await CreateIndexAsync(b => b.UploadedAt, cancellationToken: cancellationToken);

        // 7. ModelName 文本索引 - 用于全文搜索
        await CreateTextIndexAsync(b => b.ModelName, cancellationToken);

        // 8. 复合索引: SceneId + Discipline (常见查询组合)
        var compoundIndex1 = Builders<BimModelMetadata>.IndexKeys
            .Ascending(b => b.SceneId)
            .Ascending(b => b.Discipline);
        await CreateCompoundIndexAsync(compoundIndex1, cancellationToken);

        // 9. 复合索引: ProjectName + UploadedAt (项目视图排序)
        var compoundIndex2 = Builders<BimModelMetadata>.IndexKeys
            .Ascending(b => b.ProjectName)
            .Descending(b => b.UploadedAt);
        await CreateCompoundIndexAsync(compoundIndex2, cancellationToken);
    }
}
