using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Enums;

namespace RealScene3D.Domain.Interfaces;

/// <summary>
/// 切片策略接口 - 定义3D模型切片算法的统一规范
/// 支持多种切片算法：网格切片、八叉树切片、KD树切片、自适应切片等
/// 提供标准化的切片生成和估算接口
/// </summary>
public interface ISlicingStrategy
{
    /// <summary>
    /// 生成切片集合 - 核心切片算法接口
    /// 根据指定的LOD级别和配置参数，生成三维空间切片
    /// 支持异步处理、进度监控、取消操作等高级功能
    /// </summary>
    /// <param name="task">切片任务，包含任务配置和状态信息</param>
    /// <param name="level">LOD级别，控制切片的细节程度和数量</param>
    /// <param name="config">切片配置，定义算法参数和输出格式</param>
    /// <param name="modelBounds">模型的实际包围盒，用于确定切片的空间范围</param>
    /// <param name="cancellationToken">取消令牌，支持优雅中断切片过程</param>
    /// <returns>生成的切片集合，按空间位置排序</returns>
    Task<List<Slice>> GenerateSlicesAsync(
        SlicingTask task, 
        int level, 
        SlicingConfig config, 
        BoundingBox3D modelBounds, 
        CancellationToken cancellationToken);

    /// <summary>
    /// 估算切片数量 - 预估算法复杂度接口
    /// 在实际执行切片之前，估算指定级别下的切片数量
    /// 用于资源规划、进度预估、存储空间计算等
    /// </summary>
    /// <param name="level">LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <returns>估算的切片数量</returns>
    int EstimateSliceCount(int level, SlicingConfig config);
}

/// <summary>
/// 切片配置基类 - 定义切片算法的参数配置
/// 包含切片策略、输出格式、并行处理等核心配置项
/// 支持扩展配置和自定义参数
/// </summary>
public class SlicingConfig
{
    /// <summary>
    /// 切片策略名称，如："Grid", "Octree", "KdTree", "Adaptive", "Recursive"
    /// </summary>
    public SlicingStrategy Strategy { get; set; } = SlicingStrategy.Grid;

    /// <summary>
    /// 瓦片格式（枚举，推荐使用）
    /// 指定输出瓦片的格式类型，支持B3DM、I3DM、GLTF、PNTS、CMPT
    /// </summary>
    public TileFormat TileFormat { get; set; } = TileFormat.B3DM;

    /// <summary>
    /// 输出格式字符串（向后兼容，自动同步TileFormat）
    /// 支持: "b3dm", "i3dm", "gltf", "pnts", "cmpt"
    /// </summary>
    public string OutputFormat
    {
        get => TileFormat.ToString().ToLower();
        set
        {
            if (Enum.TryParse<TileFormat>(value, true, out var format))
                TileFormat = format;
        }
    }

    /// <summary>
    /// 基础瓦片尺寸，用于计算切片的空间大小
    /// </summary>
    public double TileSize { get; set; } = 100.0;

    /// <summary>
    /// 最大LOD级别，限制切片的最大细分程度
    /// </summary>
    public int MaxLevel { get; set; } = 10;

    /// <summary>
    /// 并行处理线程数，控制切片生成的并发度
    /// </summary>
    public int ParallelProcessingCount { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// 是否启用增量更新，支持切片的增量生成
    /// </summary>
    public bool EnableIncrementalUpdates { get; set; } = false;

    /// <summary>
    /// 坐标系，如："EPSG:4326", "EPSG:3857"
    /// </summary>
    public string CoordinateSystem { get; set; } = "EPSG:4326";

    /// <summary>
    /// 存储位置，指定切片文件的存储路径
    /// </summary>
    public StorageLocationType StorageLocation { get; set; } = StorageLocationType.MinIO;

    /// <summary>
    /// 自定义配置，JSON格式的扩展参数
    /// </summary>
    public string CustomSettings { get; set; } = "{}";

    /// <summary>
    /// 几何误差阈值 - LOD切换精度控制
    /// </summary>
    public double GeometricErrorThreshold { get; set; } = 1.0;

    /// <summary>
    /// 切片压缩级别 - 输出数据的压缩级别（0-9）
    /// </summary>
    public int CompressionLevel { get; set; } = 6;

    /// <summary>
    /// PNTS格式：点云采样策略
    /// 选项: "VerticesOnly"（仅顶点，最快）, "UniformSampling"（均匀采样）, "DenseSampling"（密集采样，最高质量）
    /// </summary>
    public string PointCloudSamplingStrategy { get; set; } = "VerticesOnly";

    /// <summary>
    /// PNTS格式：点云采样密度
    /// 控制每个三角形的采样点数（仅对UniformSampling和DenseSampling有效）
    /// </summary>
    public int PointCloudSamplingDensity { get; set; } = 10;

    /// <summary>
    /// I3DM格式：实例数量
    /// 指定要生成的实例化对象数量（仅对I3DM格式有效）
    /// </summary>
    public int InstanceCount { get; set; } = 1;

    /// <summary>
    /// 是否启用LOD网格简化
    /// 使用QEM（Quadric Error Metric）算法进行网格简化，生成真正的多层次细节
    /// </summary>
    public bool EnableMeshDecimation { get; set; } = false;

    /// <summary>
    /// LOD级别数量
    /// 生成的LOD层数（仅当EnableMeshDecimation=true时有效）
    /// </summary>
    public int LodLevels { get; set; } = 3;

    /// <summary>
    /// 是否保留边界顶点（用于网格简化）
    /// 保护模型轮廓，避免简化时边界变形
    /// </summary>
    public bool PreserveBoundary { get; set; } = true;

    /// <summary>
    /// 是否自动生成tileset.json
    /// 完成切片后自动生成Cesium 3D Tiles的层次结构文件
    /// </summary>
    public bool GenerateTileset { get; set; } = true;

    /// <summary>
    /// 验证配置参数的有效性
    /// </summary>
    /// <returns>验证结果和错误信息</returns>
    public (bool IsValid, string ErrorMessage) Validate()
    {
        if (TileSize <= 0)
            return (false, "瓦片尺寸必须大于0");

        if (MaxLevel < 0 || MaxLevel > 20)
            return (false, "最大LOD级别必须在0到20之间");

        if (ParallelProcessingCount < 1)
            return (false, "并行处理数量至少为1");

        if (string.IsNullOrWhiteSpace(Strategy.ToString()))
            return (false, "策略不能为空");

        // 验证格式特定参数
        if (TileFormat == TileFormat.I3DM && InstanceCount < 1)
            return (false, "I3DM格式的实例数量至少为1");

        if (TileFormat == TileFormat.PNTS && PointCloudSamplingDensity < 1)
            return (false, "PNTS格式的点云采样密度至少为1");

        if (EnableMeshDecimation && LodLevels < 1)
            return (false, "启用网格简化时LOD级别至少为1");

        return (true, string.Empty);
    }
}