using RealScene3D.Domain.Enums;

namespace RealScene3D.Domain.Entities;

/// <summary>
/// 切片配置 - 控制三维切片处理的核心参数集合
/// 提供完整的切片处理参数配置，支持多种算法和优化选项
/// </summary>
public class SlicingConfig
{
    /// <summary>
    /// 瓦片大小
    /// </summary>
    public double TileSize { get; set; } = 100.0;

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
    public TextureStrategy TextureStrategy { get; set; } = TextureStrategy.Repack;

    /// <summary>
    /// 几何误差阈值
    /// </summary>
    public double GeometricErrorThreshold { get; set; } = 0.001;
}
