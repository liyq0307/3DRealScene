using MongoDB.Bson;
using MongoDB.Driver;
using RealScene3D.Infrastructure.MongoDB.Entities;

namespace RealScene3D.Infrastructure.MongoDB.Repositories;

/// <summary>
/// 倾斜摄影元数据仓储接口
/// </summary>
public interface ITiltPhotographyMetadataRepository : Domain.Interfaces.IMongoRepository<TiltPhotographyMetadata>
{
    /// <summary>
    /// 根据项目名称搜索
    /// </summary>
    Task<IEnumerable<TiltPhotographyMetadata>> SearchByProjectNameAsync(
        string projectName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据采集日期范围查询
    /// </summary>
    Task<IEnumerable<TiltPhotographyMetadata>> GetByCaptureDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据覆盖面积查询
    /// </summary>
    Task<IEnumerable<TiltPhotographyMetadata>> GetByCoverageAreaAsync(
        double minAreaKm2,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据输出格式查询
    /// </summary>
    Task<IEnumerable<TiltPhotographyMetadata>> GetByOutputFormatAsync(
        string format,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据地理边界查询 (空间查询)
    /// </summary>
    Task<IEnumerable<TiltPhotographyMetadata>> GetByBoundsAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 确保所有索引已创建
    /// </summary>
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 倾斜摄影元数据仓储实现
/// </summary>
public class TiltPhotographyMetadataRepository : MongoRepositoryBase<TiltPhotographyMetadata>, ITiltPhotographyMetadataRepository
{
    public TiltPhotographyMetadataRepository(MongoDbContext context)
        : base(context.TiltPhotographyMetadata)
    {
    }

    public async Task<IEnumerable<TiltPhotographyMetadata>> SearchByProjectNameAsync(
        string projectName,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<TiltPhotographyMetadata>.Filter.Regex(
            t => t.ProjectName,
            new BsonRegularExpression(projectName, "i"));

        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<TiltPhotographyMetadata>> GetByCaptureDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<TiltPhotographyMetadata>.Filter.And(
            Builders<TiltPhotographyMetadata>.Filter.Gte(t => t.CaptureDate, startDate),
            Builders<TiltPhotographyMetadata>.Filter.Lte(t => t.CaptureDate, endDate)
        );

        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<TiltPhotographyMetadata>> GetByCoverageAreaAsync(
        double minAreaKm2,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<TiltPhotographyMetadata>.Filter.Gte("coverage.areaKm2", minAreaKm2);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<TiltPhotographyMetadata>> GetByOutputFormatAsync(
        string format,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<TiltPhotographyMetadata>.Filter.AnyEq(t => t.OutputFormats, format);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<TiltPhotographyMetadata>> GetByBoundsAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        CancellationToken cancellationToken = default)
    {
        // 使用地理空间查询 - 查找边界框相交的数据
        var filter = Builders<TiltPhotographyMetadata>.Filter.And(
            Builders<TiltPhotographyMetadata>.Filter.Lte("coverage.bounds.0", maxLon),  // minLon <= maxLon
            Builders<TiltPhotographyMetadata>.Filter.Gte("coverage.bounds.2", minLon),  // maxLon >= minLon
            Builders<TiltPhotographyMetadata>.Filter.Lte("coverage.bounds.1", maxLat),  // minLat <= maxLat
            Builders<TiltPhotographyMetadata>.Filter.Gte("coverage.bounds.3", minLat)   // maxLat >= minLat
        );

        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 创建倾斜摄影元数据集合的所有索引
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        // 1. SceneId 索引 - 用于按场景查询
        await CreateIndexAsync(t => t.SceneId, cancellationToken: cancellationToken);

        // 2. ProjectName 索引 - 用于项目查询
        await CreateIndexAsync(t => t.ProjectName, cancellationToken: cancellationToken);

        // 3. CaptureDate 索引 - 用于时间范围查询
        await CreateIndexAsync(t => t.CaptureDate, cancellationToken: cancellationToken);

        // 4. CreatedAt 索引 - 用于创建时间排序
        await CreateIndexAsync(t => t.CreatedAt, cancellationToken: cancellationToken);

        // 5. OutputFormats 索引 - 用于格式筛选
        await CreateIndexAsync(t => t.OutputFormats, cancellationToken: cancellationToken);

        // 6. 地理空间索引 - 用于边界查询
        var geoIndexKeys = Builders<TiltPhotographyMetadata>.IndexKeys
            .Ascending("coverage.bounds");
        await CreateCompoundIndexAsync(geoIndexKeys, cancellationToken);

        // 7. 复合索引: SceneId + CaptureDate (常见查询组合)
        var compoundIndex1 = Builders<TiltPhotographyMetadata>.IndexKeys
            .Ascending(t => t.SceneId)
            .Descending(t => t.CaptureDate);
        await CreateCompoundIndexAsync(compoundIndex1, cancellationToken);

        // 8. 复合索引: ProjectName + CreatedAt (项目视图排序)
        var compoundIndex2 = Builders<TiltPhotographyMetadata>.IndexKeys
            .Ascending(t => t.ProjectName)
            .Descending(t => t.CreatedAt);
        await CreateCompoundIndexAsync(compoundIndex2, cancellationToken);

        // 9. 覆盖面积索引 - 用于面积筛选
        var areaIndexKeys = Builders<TiltPhotographyMetadata>.IndexKeys
            .Ascending("coverage.areaKm2");
        await CreateCompoundIndexAsync(areaIndexKeys, cancellationToken);
    }
}
