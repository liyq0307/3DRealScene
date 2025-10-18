using MongoDB.Bson;
using MongoDB.Driver;
using RealScene3D.Infrastructure.MongoDB.Entities;

namespace RealScene3D.Infrastructure.MongoDB.Repositories;

/// <summary>
/// 视频元数据仓储接口
/// </summary>
public interface IVideoMetadataRepository : Domain.Interfaces.IMongoRepository<VideoMetadata>
{
    /// <summary>
    /// 根据文件名搜索
    /// </summary>
    Task<IEnumerable<VideoMetadata>> SearchByFileNameAsync(
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据上传用户获取视频
    /// </summary>
    Task<IEnumerable<VideoMetadata>> GetByUploaderAsync(
        Guid uploaderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据时间范围查询
    /// </summary>
    Task<IEnumerable<VideoMetadata>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据分辨率查询
    /// </summary>
    Task<IEnumerable<VideoMetadata>> GetByResolutionAsync(
        int minWidth,
        int minHeight,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 确保所有索引已创建
    /// </summary>
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 视频元数据仓储实现
/// </summary>
public class VideoMetadataRepository : MongoRepositoryBase<VideoMetadata>, IVideoMetadataRepository
{
    public VideoMetadataRepository(MongoDbContext context)
        : base(context.VideoMetadata)
    {
    }

    public async Task<IEnumerable<VideoMetadata>> SearchByFileNameAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<VideoMetadata>.Filter.Regex(
            v => v.FileName,
            new BsonRegularExpression(fileName, "i"));

        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<VideoMetadata>> GetByUploaderAsync(
        Guid uploaderId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<VideoMetadata>.Filter.Eq(v => v.UploadedBy, uploaderId);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<VideoMetadata>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<VideoMetadata>.Filter.And(
            Builders<VideoMetadata>.Filter.Gte(v => v.UploadedAt, startDate),
            Builders<VideoMetadata>.Filter.Lte(v => v.UploadedAt, endDate)
        );

        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<VideoMetadata>> GetByResolutionAsync(
        int minWidth,
        int minHeight,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<VideoMetadata>.Filter.And(
            Builders<VideoMetadata>.Filter.Gte("resolution.width", minWidth),
            Builders<VideoMetadata>.Filter.Gte("resolution.height", minHeight)
        );

        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 创建视频元数据集合的所有索引
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        // 1. SceneId 索引 - 用于按场景查询
        await CreateIndexAsync(v => v.SceneId, cancellationToken: cancellationToken);

        // 2. UploadedBy 索引 - 用于按用户查询
        await CreateIndexAsync(v => v.UploadedBy, cancellationToken: cancellationToken);

        // 3. UploadedAt 索引 - 用于时间范围查询和排序
        await CreateIndexAsync(v => v.UploadedAt, cancellationToken: cancellationToken);

        // 4. Tags 索引 - 用于标签查询
        await CreateIndexAsync(v => v.Tags, cancellationToken: cancellationToken);

        // 5. FileName 文本索引 - 用于全文搜索
        await CreateTextIndexAsync(v => v.FileName, cancellationToken);

        // 6. 复合索引: SceneId + UploadedAt (常见查询组合)
        var compoundIndex = Builders<VideoMetadata>.IndexKeys
            .Ascending(v => v.SceneId)
            .Descending(v => v.UploadedAt);
        await CreateCompoundIndexAsync(compoundIndex, cancellationToken);
    }
}
