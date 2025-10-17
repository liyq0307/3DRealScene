using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealScene3D.Infrastructure.MongoDB.Entities;

/// <summary>
/// MongoDB BIM 模型元数据实体
/// </summary>
public class BimModelMetadata
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("sceneId")]
    public Guid SceneId { get; set; }

    [BsonElement("projectName")]
    public string ProjectName { get; set; } = string.Empty;

    [BsonElement("modelName")]
    public string ModelName { get; set; } = string.Empty;

    [BsonElement("discipline")]
    public string Discipline { get; set; } = string.Empty; // "Architecture", "Structure", "MEP"

    [BsonElement("format")]
    public string Format { get; set; } = string.Empty; // "IFC", "RVT", "GLTF"

    [BsonElement("version")]
    public string Version { get; set; } = string.Empty;

    [BsonElement("fileSize")]
    public long FileSize { get; set; }

    [BsonElement("storagePath")]
    public string StoragePath { get; set; } = string.Empty;

    [BsonElement("elements")]
    public BimElementStats Elements { get; set; } = new();

    [BsonElement("levels")]
    public List<BimLevel> Levels { get; set; } = new();

    [BsonElement("systems")]
    public List<BimSystem> Systems { get; set; } = new();

    [BsonElement("properties")]
    public Dictionary<string, object> Properties { get; set; } = new();

    [BsonElement("uploadedBy")]
    public Guid UploadedBy { get; set; }

    [BsonElement("uploadedAt")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("lastModified")]
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

public class BimElementStats
{
    [BsonElement("totalCount")]
    public int TotalCount { get; set; }

    [BsonElement("walls")]
    public int Walls { get; set; }

    [BsonElement("floors")]
    public int Floors { get; set; }

    [BsonElement("doors")]
    public int Doors { get; set; }

    [BsonElement("windows")]
    public int Windows { get; set; }

    [BsonElement("columns")]
    public int Columns { get; set; }

    [BsonElement("beams")]
    public int Beams { get; set; }
}

public class BimLevel
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("elevation")]
    public double Elevation { get; set; }

    [BsonElement("height")]
    public double Height { get; set; }
}

public class BimSystem
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("elementCount")]
    public int ElementCount { get; set; }
}
