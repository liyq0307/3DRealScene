using RealScene3D.Domain.Entities;

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
/// 存储位置类型枚举
/// </summary>
public enum StorageLocationType
{
    /// <summary>
    /// MinIO对象存储
    /// </summary>
    MinIO = 0,
    /// <summary>
    /// 本地文件系统
    /// </summary>
    LocalFileSystem = 1
}

/// <summary>
/// 切片策略枚举 - 不同空间剖分算法的选择
/// 定义支持的切片算法类型，每种算法适用于不同的数据特征和使用场景
/// </summary>
public enum SlicingStrategy
{
    /// <summary>
    /// 规则网格切片算法
    /// 算法：均匀网格剖分，适用于规则分布的数据，计算简单，内存占用规律
    /// 优点：处理速度快，内存使用可预测
    /// 缺点：对不规则数据适应性差，可能产生过多冗余切片
    /// 使用场景：规则地形、城市网格数据
    /// </summary>
    Grid = 0,

    /// <summary>
    /// 八叉树切片算法（默认）
    /// 算法：层次递归剖分，适用于非均匀分布的数据，自适应精度，平衡细节和性能
    /// 优点：自适应精度，减少冗余切片，层次结构利于LOD
    /// 缺点：剖分算法较复杂，实现难度较高
    /// 使用场景：倾斜摄影、BIM模型、复杂地形
    /// </summary>
    Octree = 1,

    /// <summary>
    /// KD树切片算法
    /// 算法：基于方差的二分剖分，适用于高维空间查询优化，剖分轴交替选择
    /// 优点：最优空间查询性能，自适应数据分布
    /// 缺点：构造复杂度高，内存开销较大
    /// 使用场景：激光点云、海量空间数据、查询密集场景
    /// </summary>
    KdTree = 2,

    /// <summary>
    /// 自适应切片算法
    /// 算法：基于数据密度和几何特征的智能剖分，自动调整切片大小和LOD级别
    /// 优点：最佳质量和性能平衡，完全自适应
    /// 缺点：算法复杂度最高，实现和调试困难
    /// 使用场景：高精度需求、混合类型数据、质量优先场景
    /// </summary>
    Adaptive = 3
}

/// <summary>
/// 切片配置基类 - 定义切片算法的参数配置
/// 包含切片策略、输出格式、并行处理等核心配置项
/// 支持扩展配置和自定义参数
/// </summary>
public class SlicingConfig
{
    /// <summary>
    /// 切片策略名称，如："Grid", "Octree", "KdTree", "Adaptive"
    /// </summary>
    public SlicingStrategy Strategy { get; set; } = SlicingStrategy.Grid;

    /// <summary>
    /// 输出格式，如："b3dm", "gltf", "glb", "json"
    /// </summary>
    public string OutputFormat { get; set; } = "b3dm";

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
    /// 验证配置参数的有效性
    /// </summary>
    /// <returns>验证结果和错误信息</returns>
    public (bool IsValid, string ErrorMessage) Validate()
    {
        if (TileSize <= 0)
            return (false, "TileSize must be greater than 0");

        if (MaxLevel < 0 || MaxLevel > 20)
            return (false, "MaxLevel must be between 0 and 20");

        if (ParallelProcessingCount < 1)
            return (false, "ParallelProcessingCount must be at least 1");

        if (string.IsNullOrWhiteSpace(Strategy.ToString()))
            return (false, "Strategy cannot be empty");

        if (string.IsNullOrWhiteSpace(OutputFormat))
            return (false, "OutputFormat cannot be empty");

        return (true, string.Empty);
    }
}

/// <summary>
/// 三维包围盒类 - 轴对齐包围盒（AABB）
/// 用于定义三维空间中的矩形区域，支持空间查询和碰撞检测
/// 提供高效的包围体表示和相交测试算法
/// </summary>
public class BoundingBox3D
{
    /// <summary>
    /// 最小X坐标 - 包围盒在X轴的最小值
    /// </summary>
    public double MinX { get; set; }

    /// <summary>
    /// 最小Y坐标 - 包围盒在Y轴的最小值
    /// </summary>
    public double MinY { get; set; }

    /// <summary>
    /// 最小Z坐标 - 包围盒在Z轴的最小值
    /// </summary>
    public double MinZ { get; set; }

    /// <summary>
    /// 最大X坐标 - 包围盒在X轴的最大值
    /// </summary>
    public double MaxX { get; set; }

    /// <summary>
    /// 最大Y坐标 - 包围盒在Y轴的最大值
    /// </summary>
    public double MaxY { get; set; }

    /// <summary>
    /// 最大Z坐标 - 包围盒在Z轴的最大值
    /// </summary>
    public double MaxZ { get; set; }

    /// <summary>
    /// 默认构造函数
    /// </summary>
    public BoundingBox3D() { }

    /// <summary>
    /// 带参数构造函数
    /// </summary>
    public BoundingBox3D(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        MinX = minX;
        MinY = minY;
        MinZ = minZ;
        MaxX = maxX;
        MaxY = maxY;
        MaxZ = maxZ;
    }

    /// <summary>
    /// 计算包围盒的中心点
    /// </summary>
    public Vector3D GetCenter()
    {
        return new Vector3D
        {
            X = (MinX + MaxX) / 2,
            Y = (MinY + MaxY) / 2,
            Z = (MinZ + MaxZ) / 2
        };
    }

    /// <summary>
    /// 计算包围盒的尺寸
    /// </summary>
    public (double Width, double Height, double Depth) GetSize()
    {
        return (MaxX - MinX, MaxY - MinY, MaxZ - MinZ);
    }

    /// <summary>
    /// 判断点是否在包围盒内
    /// </summary>
    public bool Contains(Vector3D point)
    {
        return point.X >= MinX && point.X <= MaxX &&
               point.Y >= MinY && point.Y <= MaxY &&
               point.Z >= MinZ && point.Z <= MaxZ;
    }

    /// <summary>
    /// 判断两个包围盒是否相交
    /// </summary>
    public bool Intersects(BoundingBox3D other)
    {
        return !(MaxX < other.MinX || MinX > other.MaxX ||
                 MaxY < other.MinY || MinY > other.MaxY ||
                 MaxZ < other.MinZ || MinZ > other.MaxZ);
    }

    /// <summary>
    /// 计算包围盒的体积
    /// </summary>
    public double GetVolume()
    {
        var size = GetSize();
        return size.Width * size.Height * size.Depth;
    }

    /// <summary>
    /// 验证包围盒的有效性
    /// 要求包围盒至少在一个维度上有非零尺寸
    /// </summary>
    public bool IsValid()
    {
        // 检查坐标顺序是否正确
        if (!(MinX <= MaxX && MinY <= MaxY && MinZ <= MaxZ))
            return false;

        // 检查是否至少有一个维度的尺寸大于0（避免退化为点或线）
        // 对于有效的3D包围盒，应该在所有维度上都有非零尺寸
        var sizeX = MaxX - MinX;
        var sizeY = MaxY - MinY;
        var sizeZ = MaxZ - MinZ;

        // 至少需要在一个维度上有尺寸（避免全零包围盒）
        return sizeX > 0 || sizeY > 0 || sizeZ > 0;
    }

    /// <summary>
    /// 扩展包围盒以包含指定点
    /// </summary>
    public void Expand(Vector3D point)
    {
        MinX = Math.Min(MinX, point.X);
        MinY = Math.Min(MinY, point.Y);
        MinZ = Math.Min(MinZ, point.Z);
        MaxX = Math.Max(MaxX, point.X);
        MaxY = Math.Max(MaxY, point.Y);
        MaxZ = Math.Max(MaxZ, point.Z);
    }

    /// <summary>
    /// 扩展包围盒以包含另一个包围盒
    /// </summary>
    public void Expand(BoundingBox3D other)
    {
        MinX = Math.Min(MinX, other.MinX);
        MinY = Math.Min(MinY, other.MinY);
        MinZ = Math.Min(MinZ, other.MinZ);
        MaxX = Math.Max(MaxX, other.MaxX);
        MaxY = Math.Max(MaxY, other.MaxY);
        MaxZ = Math.Max(MaxZ, other.MaxZ);
    }
}