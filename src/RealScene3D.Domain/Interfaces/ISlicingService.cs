using RealScene3D.Domain.Entities;

namespace RealScene3D.Domain.Interfaces;

/// <summary>
/// 三维切片服务接口 - 领域层核心业务接口
/// 定义三维模型切片处理的核心业务操作契约
/// 提供切片任务管理、数据获取、渲染优化等完整功能集
/// 支持多种空间剖分算法和渲染优化技术
/// </summary>
public interface ISlicingService
{
    /// <summary>
    /// 创建切片任务 - 启动三维模型切片处理流程
    /// </summary>
    /// <param name="name">切片任务名称，必须唯一且具有描述性</param>
    /// <param name="sourceModelPath">源3D模型文件路径，支持多种格式（OBJ、FBX、GLTF等）</param>
    /// <param name="modelType">模型类型，影响切片算法选择和参数配置</param>
    /// <param name="config">切片配置参数，控制切片粒度、输出格式等</param>
    /// <param name="userId">创建用户ID，用于权限验证和任务归属</param>
    /// <returns>新创建的切片任务ID</returns>
    /// <exception cref="ArgumentException">当参数无效时抛出</exception>
    /// <exception cref="InvalidOperationException">当源文件不存在时抛出</exception>
    Task<Guid> CreateSlicingTaskAsync(string name, string sourceModelPath, string modelType, SlicingConfig config, Guid userId);

    /// <summary>
    /// 获取切片任务详情 - 根据ID查询切片任务完整信息
    /// </summary>
    /// <param name="taskId">切片任务唯一标识符</param>
    /// <returns>切片任务详情，如果不存在则返回null</returns>
    Task<SlicingTask?> GetSlicingTaskAsync(Guid taskId);

    /// <summary>
    /// 获取用户的切片任务列表 - 分页查询指定用户的切片任务
    /// </summary>
    /// <param name="userId">用户ID，查询该用户创建的所有切片任务</param>
    /// <param name="page">页码，从1开始，默认为1</param>
    /// <param name="pageSize">每页大小，默认20条，最大不超过100条</param>
    /// <returns>用户的切片任务列表，按创建时间倒序排列</returns>
    Task<IEnumerable<SlicingTask>> GetUserSlicingTasksAsync(Guid userId, int page = 1, int pageSize = 20);

    /// <summary>
    /// 取消切片任务 - 中止正在进行的切片处理
    /// </summary>
    /// <param name="taskId">要取消的切片任务ID</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>取消是否成功</returns>
    /// <exception cref="UnauthorizedAccessException">当用户无权限操作时抛出</exception>
    Task<bool> CancelSlicingTaskAsync(Guid taskId, Guid userId);

    /// <summary>
    /// 获取切片数据 - 根据坐标获取特定切片
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="level">LOD层级，0表示最高细节级别</param>
    /// <param name="x">切片X坐标</param>
    /// <param name="y">切片Y坐标</param>
    /// <param name="z">切片Z坐标</param>
    /// <returns>切片数据，如果不存在则返回null</returns>
    Task<Slice?> GetSliceAsync(Guid taskId, int level, int x, int y, int z);

    /// <summary>
    /// 获取指定级别的所有切片 - 批量获取同一LOD层级的所有切片
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="level">LOD层级</param>
    /// <returns>该层级所有切片的集合</returns>
    Task<IEnumerable<Slice>> GetSlicesByLevelAsync(Guid taskId, int level);

    /// <summary>
    /// 删除切片任务及相关数据 - 清理切片任务和生成的文件
    /// </summary>
    /// <param name="taskId">要删除的切片任务ID</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>删除是否成功</returns>
    Task<bool> DeleteSlicingTaskAsync(Guid taskId, Guid userId);

    /// <summary>
    /// 执行视锥剔除 - 渲染优化算法实现
    /// 算法：基于视口参数剔除不可见的切片，减少渲染负载
    /// 使用包围盒与视锥相交测试，快速剔除不在视野范围内的切片
    /// </summary>
    /// <param name="viewport">视口参数，包含相机位置、视角、裁剪面等信息</param>
    /// <param name="allSlices">所有待测试的切片集合</param>
    /// <returns>可见切片集合，仅包含在视锥范围内的切片</returns>
    /// <exception cref="ArgumentNullException">当输入参数为null时抛出</exception>
    Task<IEnumerable<Slice>> PerformFrustumCullingAsync(ViewportInfo viewport, IEnumerable<Slice> allSlices);

    /// <summary>
    /// 预测加载算法 - 预加载优化算法实现
    /// 算法：基于用户视点移动趋势预测需要加载的切片
    /// 使用运动向量预测未来视口位置，提前加载可能需要的切片
    /// 支持时间预测和距离预测两种模式
    /// </summary>
    /// <param name="currentViewport">当前视口信息，作为预测基准</param>
    /// <param name="movementVector">用户移动向量，描述移动方向和速度</param>
    /// <param name="allSlices">所有可用切片，用于预测选择</param>
    /// <returns>预测加载的切片集合，优先级按需要程度排序</returns>
    /// <exception cref="ArgumentException">当运动向量无效时抛出</exception>
    Task<IEnumerable<Slice>> PredictLoadingAsync(ViewportInfo currentViewport, Vector3D movementVector, IEnumerable<Slice> allSlices);
}

/// <summary>
/// 切片配置 - 控制三维切片处理的核心参数集合
/// 提供完整的切片处理参数配置，支持多种算法和优化选项
/// 所有参数都有合理的默认值，支持运行时动态调整
/// </summary>
public class SlicingConfig
{
    /// <summary>
    /// 切片大小（米）- 基础空间剖分单位
    /// 算法影响：决定网格剖分的最小粒度，影响切片总数和处理性能
    /// 建议值：根据模型大小调整，典型值为10-1000米
    /// </summary>
    public double TileSize { get; set; } = 100.0;

    /// <summary>
    /// LOD级别数量 - 多层次细节级别数
    /// 算法影响：金字塔层数，每级切片数为2^level，影响渲染性能和数据量
    /// 建议值：4-12级别，根据模型复杂度选择
    /// </summary>
    public int MaxLevel { get; set; } = 10;

    /// <summary>
    /// 输出格式 - 切片文件格式选择
    /// 算法影响：B3DM（二进制）、GLTF（JSON）、JSON（文本）等格式的选择
    /// B3DM：Cesium优化格式，压缩率高
    /// GLTF：标准格式，兼容性好
    /// JSON：轻量级，自定义格式
    /// </summary>
    public string OutputFormat { get; set; } = "b3dm"; // b3dm, gltf, json

    /// <summary>
    /// 是否压缩输出 - 数据压缩优化
    /// 算法影响：启用压缩可减少存储空间，但会增加处理时间
    /// 建议：生产环境启用，开发环境可选择性启用
    /// </summary>
    public bool CompressOutput { get; set; } = true;

    /// <summary>
    /// 几何误差阈值 - LOD切换精度控制
    /// 算法影响：控制不同LOD级别间的切换距离，影响视觉连续性
    /// 单位：米，小于此距离的几何误差将被忽略
    /// </summary>
    public double GeometricErrorThreshold { get; set; } = 1.0;

    /// <summary>
    /// 纹理质量（0-1）- 纹理压缩质量参数
    /// 算法影响：平衡纹理质量和文件大小，影响视觉效果和传输性能
    /// 1.0：最高质量，最大文件
    /// 0.5：中等质量，平衡大小
    /// 0.1：最低质量，最小文件
    /// </summary>
    public double TextureQuality { get; set; } = 0.8;

    /// <summary>
    /// 切片策略 - 空间剖分算法选择
    /// 算法影响：选择不同的空间剖分策略（网格、八叉树、KD树、自适应）
    /// Grid：规则分布，计算简单
    /// Octree：层次剖分，自适应精度
    /// KdTree：轴对齐，最优查询
    /// Adaptive：智能剖分，最佳质量
    /// </summary>
    public SlicingStrategy Strategy { get; set; } = SlicingStrategy.Octree;

    /// <summary>
    /// 是否启用增量更新 - 支持模型的增量切片更新
    /// 算法影响：启用后将生成增量更新索引，支持部分模型更新而不需要完全重新切片
    /// 适用于经常变更的大型模型，能显著减少更新时间
    /// </summary>
    public bool EnableIncrementalUpdates { get; set; } = false;

    /// <summary>
    /// 视点优化阈值 - 自适应LOD的视点距离阈值
    /// 算法影响：控制不同LOD级别切换的视点距离，优化渲染性能
    /// 小于此距离使用高细节级别，大于此距离使用低细节级别
    /// </summary>
    public double ViewportOptimizationThreshold { get; set; } = 10.0;

    /// <summary>
    /// 密度分析采样率 - 几何密度分析的采样精度
    /// 算法影响：影响密度自适应切片的准确性和计算性能，0.1-1.0之间
    /// 高采样率：更准确，但计算慢
    /// 低采样率：较快，但可能遗漏细节
    /// </summary>
    public double DensityAnalysisSampleRate { get; set; } = 0.5;

    /// <summary>
    /// 切片压缩级别 - 输出数据的压缩级别（0-9）
    /// 算法影响：平衡文件大小和处理时间，0表示无压缩，9表示最大压缩
    /// 0：无压缩，最快处理
    /// 6：中等压缩，平衡性能（推荐）
    /// 9：最大压缩，最小文件
    /// </summary>
    public int CompressionLevel { get; set; } = 6;

    /// <summary>
    /// 并行处理数量 - 切片处理的并行度
    /// 算法影响：控制同时处理的切片数量，影响内存使用和处理速度
    /// 建议值：根据CPU核心数和内存大小调整，通常为CPU核心数的1-2倍
    /// </summary>
    public int ParallelProcessingCount { get; set; } = 4;

    /// <summary>
    /// 切片文件存储位置类型
    /// </summary>
    public StorageLocationType StorageLocation { get; set; } = StorageLocationType.MinIO;
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
/// 切片进度信息 - 实时进度跟踪数据结构
/// 提供切片任务执行过程中的详细进度信息
/// 支持前端实时显示和监控告警
/// </summary>
public class SlicingProgress
{
    /// <summary>
    /// 任务ID - 关联的切片任务唯一标识符
    /// 用于进度查询和任务关联
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    /// 当前进度（0-100）- 任务完成的百分比
    /// 实时更新，用于进度条显示和完成度判断
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// 当前处理阶段 - 描述当前正在执行的具体处理步骤
    /// 如：模型加载、空间剖分、切片生成、文件压缩、索引构建等
    /// </summary>
    public string CurrentStage { get; set; } = string.Empty;

    /// <summary>
    /// 已处理的切片数量 - 已经完成处理的切片总数
    /// 用于性能监控和吞吐量统计
    /// </summary>
    public long ProcessedTiles { get; set; }

    /// <summary>
    /// 总切片数量 - 预计需要处理的切片总数
    /// 在处理开始时估算，可能在过程中动态调整
    /// </summary>
    public long TotalTiles { get; set; }

    /// <summary>
    /// 预计剩余时间（秒）- 基于当前处理速度估算的剩余时间
    /// 使用线性外推算法，根据已用时间和当前进度计算
    /// </summary>
    public long EstimatedTimeRemaining { get; set; }
}