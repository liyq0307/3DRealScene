using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealScene3D.Infrastructure.MongoDB.Entities;

/// <summary>
/// MongoDB 视频元数据实体
/// </summary>
public class VideoMetadata
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("sceneId")]
    public Guid SceneId { get; set; }

    [BsonElement("fileName")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("fileSize")]
    public long FileSize { get; set; }

    [BsonElement("duration")]
    public double Duration { get; set; }

    [BsonElement("resolution")]
    public VideoResolution Resolution { get; set; } = new();

    [BsonElement("codec")]
    public string Codec { get; set; } = string.Empty;

    [BsonElement("bitrate")]
    public int Bitrate { get; set; }

    [BsonElement("frameRate")]
    public double FrameRate { get; set; }

    [BsonElement("thumbnailPath")]
    public string ThumbnailPath { get; set; } = string.Empty;

    [BsonElement("storagePath")]
    public string StoragePath { get; set; } = string.Empty;

    [BsonElement("uploadedBy")]
    public Guid UploadedBy { get; set; }

    [BsonElement("uploadedAt")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("tags")]
    public List<string> Tags { get; set; } = new();

    [BsonElement("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class VideoResolution
{
    [BsonElement("width")]
    public int Width { get; set; }

    [BsonElement("height")]
    public int Height { get; set; }
}
