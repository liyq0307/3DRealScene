using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealScene3D.Infrastructure.MongoDB.Entities;

/// <summary>
/// MongoDB 倾斜摄影元数据实体
/// </summary>
public class TiltPhotographyMetadata
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("sceneId")]
    public Guid SceneId { get; set; }

    [BsonElement("projectName")]
    public string ProjectName { get; set; } = string.Empty;

    [BsonElement("captureDate")]
    public DateTime CaptureDate { get; set; }

    [BsonElement("totalImages")]
    public int TotalImages { get; set; }

    [BsonElement("coverage")]
    public CoverageArea Coverage { get; set; } = new();

    [BsonElement("camera")]
    public CameraInfo Camera { get; set; } = new();

    [BsonElement("processingInfo")]
    public ProcessingInfo Processing { get; set; } = new();

    [BsonElement("outputFormats")]
    public List<string> OutputFormats { get; set; } = new();

    [BsonElement("storagePath")]
    public string StoragePath { get; set; } = string.Empty;

    [BsonElement("tilesets")]
    public List<TilesetInfo> Tilesets { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class CoverageArea
{
    [BsonElement("bounds")]
    public double[] Bounds { get; set; } = Array.Empty<double>(); // [minLon, minLat, maxLon, maxLat]

    [BsonElement("centerPoint")]
    public double[] CenterPoint { get; set; } = Array.Empty<double>(); // [lon, lat, alt]

    [BsonElement("areaKm2")]
    public double AreaKm2 { get; set; }
}

public class CameraInfo
{
    [BsonElement("model")]
    public string Model { get; set; } = string.Empty;

    [BsonElement("focalLength")]
    public double FocalLength { get; set; }

    [BsonElement("sensorSize")]
    public double[] SensorSize { get; set; } = Array.Empty<double>(); // [width, height] in mm
}

public class ProcessingInfo
{
    [BsonElement("software")]
    public string Software { get; set; } = string.Empty;

    [BsonElement("version")]
    public string Version { get; set; } = string.Empty;

    [BsonElement("processedDate")]
    public DateTime ProcessedDate { get; set; }

    [BsonElement("quality")]
    public string Quality { get; set; } = string.Empty;

    [BsonElement("resolution")]
    public double Resolution { get; set; } // cm/pixel
}

public class TilesetInfo
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("format")]
    public string Format { get; set; } = string.Empty; // "3dtiles", "osgb", etc.

    [BsonElement("path")]
    public string Path { get; set; } = string.Empty;

    [BsonElement("lodLevels")]
    public int LodLevels { get; set; }

    [BsonElement("fileSize")]
    public long FileSize { get; set; }
}
