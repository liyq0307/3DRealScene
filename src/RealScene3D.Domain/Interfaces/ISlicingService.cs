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
public class SlicingConfigDomain : SlicingConfig
{
    /// <summary>
    /// 是否压缩输出 - 数据压缩优化
    /// 算法影响：启用压缩可减少存储空间，但会增加处理时间
    /// 建议：生产环境启用，开发环境可选择性启用
    /// </summary>
    public bool CompressOutput { get; set; } = true;

    /// <summary>
    /// 纹理质量（0-1）- 纹理压缩质量参数
    /// 算法影响：平衡纹理质量和文件大小，影响视觉效果和传输性能
    /// 1.0：最高质量，最大文件
    /// 0.5：中等质量，平衡大小
    /// 0.1：最低质量，最小文件
    /// </summary>
    public double TextureQuality { get; set; } = 0.8;

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
}

/// <summary>
/// 切片进度信息 - 用于监控和报告切片任务的处理状态
/// 提供详细的进度指标，支持实时更新和历史查询
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