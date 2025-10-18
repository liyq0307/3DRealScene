using MongoDB.Bson;
using MongoDB.Driver;
using RealScene3D.Domain.Interfaces;
using System.Linq.Expressions;

namespace RealScene3D.Infrastructure.MongoDB.Repositories;

/// <summary>
/// MongoDB 通用仓储基类
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public abstract class MongoRepositoryBase<T> : IMongoRepository<T> where T : class
{
    protected readonly IMongoCollection<T> _collection;

    protected MongoRepositoryBase(IMongoCollection<T> collection)
    {
        _collection = collection;
    }

    public virtual async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<T?> GetBySceneIdAsync(Guid sceneId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Eq("sceneId", sceneId);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> GetAllBySceneIdAsync(Guid sceneId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Eq("sceneId", sceneId);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(Builders<T>.Filter.Empty).ToListAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default)
    {
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public virtual async Task<(IEnumerable<T> Items, long TotalCount)> FindPagedAsync(
        Expression<Func<T, bool>> filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var items = await _collection
            .Find(filter)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public virtual async Task AddManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await _collection.InsertManyAsync(entities, cancellationToken: cancellationToken);
    }

    public virtual async Task<bool> UpdateAsync(string id, T entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
        var result = await _collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public virtual async Task<bool> UpdateFieldsAsync<TField>(
        string id,
        Expression<Func<T, TField>> field,
        TField value,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
        var update = Builders<T>.Update.Set(field, value);
        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public virtual async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
        var result = await _collection.DeleteOneAsync(filter, cancellationToken);
        return result.DeletedCount > 0;
    }

    public virtual async Task<long> DeleteManyAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default)
    {
        var result = await _collection.DeleteManyAsync(filter, cancellationToken);
        return result.DeletedCount;
    }

    public virtual async Task<long> CountAsync(
        Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            return await _collection.CountDocumentsAsync(Builders<T>.Filter.Empty, cancellationToken: cancellationToken);
        }
        return await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    public virtual async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default)
    {
        var count = await _collection.CountDocumentsAsync(filter, new CountOptions { Limit = 1 }, cancellationToken);
        return count > 0;
    }

    public virtual async Task<IEnumerable<T>> FindByTagsAsync(
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.AnyIn("tags", tags);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 创建索引的辅助方法
    /// </summary>
    protected async Task CreateIndexAsync(
        Expression<Func<T, object>> field,
        bool unique = false,
        CancellationToken cancellationToken = default)
    {
        var indexKeys = Builders<T>.IndexKeys.Ascending(field);
        var indexOptions = new CreateIndexOptions { Unique = unique };
        var indexModel = new CreateIndexModel<T>(indexKeys, indexOptions);
        await _collection.Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 创建文本索引的辅助方法
    /// </summary>
    protected async Task CreateTextIndexAsync(
        Expression<Func<T, object>> field,
        CancellationToken cancellationToken = default)
    {
        var indexKeys = Builders<T>.IndexKeys.Text(field);
        var indexModel = new CreateIndexModel<T>(indexKeys);
        await _collection.Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 创建复合索引的辅助方法
    /// </summary>
    protected async Task CreateCompoundIndexAsync(
        IndexKeysDefinition<T> indexKeys,
        CancellationToken cancellationToken = default)
    {
        var indexModel = new CreateIndexModel<T>(indexKeys);
        await _collection.Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
    }
}
