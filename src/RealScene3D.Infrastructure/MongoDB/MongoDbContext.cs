using MongoDB.Driver;
using RealScene3D.Infrastructure.MongoDB.Entities;

namespace RealScene3D.Infrastructure.MongoDB;

/// <summary>
/// MongoDB 数据库上下文
/// </summary>
public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IMongoClient mongoClient, string databaseName)
    {
        _database = mongoClient.GetDatabase(databaseName);
    }

    public IMongoCollection<VideoMetadata> VideoMetadata =>
        _database.GetCollection<VideoMetadata>("video_metadata");

    public IMongoCollection<TiltPhotographyMetadata> TiltPhotographyMetadata =>
        _database.GetCollection<TiltPhotographyMetadata>("tilt_photography_metadata");

    public IMongoCollection<BimModelMetadata> BimModelMetadata =>
        _database.GetCollection<BimModelMetadata>("bim_model_metadata");
}
