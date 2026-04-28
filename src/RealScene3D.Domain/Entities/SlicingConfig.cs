using RealScene3D.Domain.Enums;

namespace RealScene3D.Domain.Entities;

/// <summary>
/// 切片配置 - 控制三维切片处理的核心参数集合
/// 提供完整的切片处理参数配置，支持多种算法和优化选项
/// </summary>
public class SlicingConfig
{
    /// <summary>
    /// 空间分割递归深度
    /// </summary>
    public int Divisions { get; set; } = 2;

    /// <summary>
    /// LOD级别数量
    /// </summary>
    public int LodLevels { get; set; } = 3;

    /// <summary>
    /// 是否启用网格简化
    /// </summary>
    public bool EnableMeshDecimation { get; set; } = true;

    /// <summary>
    /// 是否生成 tileset.json
    /// </summary>
    public bool GenerateTileset { get; set; } = true;

    /// <summary>
    /// 输出格式
    /// </summary>
    public string OutputFormat { get; set; } = "b3dm";

    /// <summary>
    /// 是否启用增量更新
    /// </summary>
    public bool EnableIncrementalUpdates { get; set; } = false;

    /// <summary>
    /// 存储位置
    /// </summary>
    public StorageLocationType StorageLocation { get; set; } = StorageLocationType.MinIO;

    /// <summary>
    /// 纹理处理策略
    /// </summary>
    public TexturesStrategy TextureStrategy { get; set; } = TexturesStrategy.Repack;

    /// <summary>
    /// 几何误差阈值
    /// </summary>
    public double GeometricErrorThreshold { get; set; } = 0.001;

    /// <summary>
    /// 空间参考系统（坐标系）
    /// 例如：EPSG:4326, EPSG:3857, EPSG:4978等
    /// 主要用于倾斜摄影数据
    /// </summary>
    public string? SpatialReference { get; set; }

    /// <summary>
    /// 零点坐标X（中心点经度或X坐标）
    /// 主要用于倾斜摄影数据的坐标偏移
    /// </summary>
    public double? CenterX { get; set; }

    /// <summary>
    /// 零点坐标Y（中心点纬度或Y坐标）
    /// 主要用于倾斜摄影数据的坐标偏移
    /// </summary>
    public double? CenterY { get; set; }

    /// <summary>
    /// 零点坐标Z（中心点高度或Z坐标）
    /// 主要用于倾斜摄影数据的坐标偏移
    /// </summary>
    public double? CenterZ { get; set; }

    /// <summary>
    /// 是否启用纹理压缩
    /// </summary>
    public bool EnableTextureCompression { get; set; } = false;

    /// <summary>
    /// 是否启用网格优化
    /// </summary>
    public bool EnableMeshOptimization { get; set; } = false;

    /// <summary>
    /// 是否启用Draco压缩
    /// </summary>
    public bool EnableDracoCompression { get; set; } = false;

    /// <summary>
    /// 3D Tiles版本（1.0或1.1）
    /// </summary>
    public string TilesVersion { get; set; } = "1.0";

    /// <summary>
    /// MinIO 服务端点 (如 "localhost:9000")
    /// </summary>
    public string? MinioEndpoint { get; set; }

    /// <summary>
    /// MinIO 访问密钥
    /// </summary>
    public string? MinioAccessKey { get; set; }

    /// <summary>
    /// MinIO 秘密密钥
    /// </summary>
    public string? MinioSecretKey { get; set; }

    /// <summary>
    /// MinIO 是否使用 HTTPS
    /// </summary>
    public bool MinioUseSSL { get; set; } = false;
}
