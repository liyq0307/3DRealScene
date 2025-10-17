using RealScene3D.Application.DTOs;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Application.Interfaces;

/// <summary>
/// 三维切片应用服务接口 - 应用层服务契约
/// 定义应用层面的切片处理服务接口，桥接领域服务和Web API
/// 提供DTO转换、业务流程编排、数据聚合等应用层功能
/// 支持切片任务的全生命周期管理和服务组合
/// </summary>
public interface ISlicingAppService
{
    /// <summary>
    /// 创建切片任务 - 应用层任务创建入口
    /// 将前端请求转换为领域对象，执行业务规则验证，启动切片处理流程
    /// </summary>
    /// <param name="request">切片任务创建请求，包含所有必要参数和配置</param>
    /// <param name="userId">创建用户ID，用于权限验证和审计追踪</param>
    /// <returns>创建成功的切片任务DTO，包含任务基本信息和初始状态</returns>
    /// <exception cref="ArgumentException">当请求参数无效时抛出</exception>
    /// <exception cref="InvalidOperationException">当业务规则验证失败时抛出</exception>
    Task<SlicingDtos.SlicingTaskDto> CreateSlicingTaskAsync(SlicingDtos.CreateSlicingTaskRequest request, Guid userId);

    /// <summary>
    /// 获取切片任务详情 - 查询并转换任务信息
    /// 从数据层获取任务数据，转换为DTO格式返回给前端
    /// </summary>
    /// <param name="taskId">切片任务唯一标识符</param>
    /// <returns>切片任务详情DTO，如果不存在则返回null</returns>
    Task<SlicingDtos.SlicingTaskDto?> GetSlicingTaskAsync(Guid taskId);

    /// <summary>
    /// 获取用户的切片任务列表 - 分页查询用户任务
    /// 支持分页和排序，提供高效的任务列表查询功能
    /// </summary>
    /// <param name="userId">用户ID，查询该用户创建的所有切片任务</param>
    /// <param name="page">页码，从1开始，默认为1</param>
    /// <param name="pageSize">每页大小，默认20条，最大不超过100条</param>
    /// <returns>用户的切片任务DTO列表，按创建时间倒序排列</returns>
    Task<IEnumerable<SlicingDtos.SlicingTaskDto>> GetUserSlicingTasksAsync(Guid userId, int page = 1, int pageSize = 20);

    /// <summary>
    /// 获取切片任务进度 - 实时进度查询接口
    /// 提供详细的进度信息，包括当前阶段、处理速度、预计剩余时间等
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <returns>切片任务进度DTO，如果不存在则返回null</returns>
    Task<SlicingDtos.SlicingProgressDto?> GetSlicingProgressAsync(Guid taskId);

    /// <summary>
    /// 取消切片任务 - 安全取消正在执行的任务
    /// 检查任务状态和用户权限，执行取消操作并清理资源
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>取消是否成功</returns>
    Task<bool> CancelSlicingTaskAsync(Guid taskId, Guid userId);

    /// <summary>
    /// 删除切片任务 - 清理任务和相关文件
    /// 删除数据库记录和存储文件，释放系统资源
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>删除是否成功</returns>
    Task<bool> DeleteSlicingTaskAsync(Guid taskId, Guid userId);

    /// <summary>
    /// 获取切片数据 - 根据坐标获取切片DTO
    /// 从数据层获取切片数据，转换为DTO格式
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="level">LOD层级，0表示最高细节级别</param>
    /// <param name="x">切片X坐标</param>
    /// <param name="y">切片Y坐标</param>
    /// <param name="z">切片Z坐标</param>
    /// <returns>切片DTO，如果不存在则返回null</returns>
    Task<SlicingDtos.SliceDto?> GetSliceAsync(Guid taskId, int level, int x, int y, int z);

    /// <summary>
    /// 获取指定级别的切片元数据 - 批量获取切片信息
    /// 只返回元数据，不包含实际的几何数据，适合大批量查询
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="level">LOD层级</param>
    /// <returns>该层级所有切片的元数据集合</returns>
    Task<IEnumerable<SlicingDtos.SliceMetadataDto>> GetSliceMetadataAsync(Guid taskId, int level);

    /// <summary>
    /// 下载切片文件 - 获取切片文件的二进制数据
    /// 从对象存储下载切片文件，返回给客户端下载
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="level">LOD层级</param>
    /// <param name="x">切片X坐标</param>
    /// <param name="y">切片Y坐标</param>
    /// <param name="z">切片Z坐标</param>
    /// <returns>切片文件的字节数组</returns>
    /// <exception cref="FileNotFoundException">当切片文件不存在时抛出</exception>
    Task<byte[]> DownloadSliceAsync(Guid taskId, int level, int x, int y, int z);

    /// <summary>
    /// 批量获取切片 - 高效批量数据获取接口
    /// 支持多个切片的一次性获取，减少网络往返次数
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="level">LOD层级</param>
    /// <param name="coordinates">切片坐标集合，最大支持1000个坐标</param>
    /// <returns>切片DTO集合，只返回存在的切片</returns>
    Task<IEnumerable<SlicingDtos.SliceDto>> GetSlicesBatchAsync(Guid taskId, int level, IEnumerable<(int x, int y, int z)> coordinates);

    /// <summary>
    /// 执行视锥剔除 - 渲染优化算法应用层接口
    /// 算法：基于视口参数剔除不可见的切片，减少渲染负载
    /// 调用领域层视锥剔除算法，返回优化的切片集合
    /// </summary>
    /// <param name="viewport">视口参数，包含相机位置、视角等信息</param>
    /// <param name="allSlices">所有待测试的切片元数据集合</param>
    /// <returns>可见切片元数据集合</returns>
    Task<IEnumerable<SlicingDtos.SliceMetadataDto>> PerformFrustumCullingAsync(ViewportInfo viewport, IEnumerable<SlicingDtos.SliceMetadataDto> allSlices);

    /// <summary>
    /// 预测加载算法 - 预加载优化算法应用层接口
    /// 算法：基于用户视点移动趋势预测需要加载的切片
    /// 提供智能预加载建议，提升用户体验
    /// </summary>
    /// <param name="currentViewport">当前视口信息</param>
    /// <param name="movementVector">用户移动向量</param>
    /// <param name="allSlices">所有可用切片元数据</param>
    /// <returns>预测加载的切片元数据集合</returns>
    Task<IEnumerable<SlicingDtos.SliceMetadataDto>> PredictLoadingAsync(ViewportInfo currentViewport, Vector3D movementVector, IEnumerable<SlicingDtos.SliceMetadataDto> allSlices);

    /// <summary>
    /// 获取增量更新索引 - 获取切片任务的增量更新索引信息
    /// 从MinIO存储中读取增量更新索引文件
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <returns>增量更新索引信息，如果不存在则返回null</returns>
    Task<SlicingDtos.IncrementalUpdateIndexDto?> GetIncrementalUpdateIndexAsync(Guid taskId);
}

/// <summary>
/// 切片处理器接口 - 后台任务处理接口
/// 定义异步切片处理的核心接口，支持队列管理和任务调度
/// 提供切片处理流程控制和进度更新机制
/// </summary>
public interface ISlicingProcessor
{
    /// <summary>
    /// 处理切片任务队列 - 持续监控和处理任务队列
    /// 后台运行，轮询检查新任务并启动处理
    /// </summary>
    /// <param name="cancellationToken">取消令牌，支持优雅关闭</param>
    /// <returns>异步任务</returns>
    Task ProcessSlicingQueueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 处理单个切片任务 - 执行具体的切片处理逻辑
    /// 实现完整的切片处理流程，包括剖分、生成、压缩、索引等
    /// </summary>
    /// <param name="taskId">待处理的切片任务ID</param>
    /// <param name="cancellationToken">取消令牌，支持中途取消</param>
    /// <returns>异步任务</returns>
    Task ProcessSlicingTaskAsync(Guid taskId, CancellationToken cancellationToken);

    /// <summary>
    /// 更新任务进度 - 实时进度更新接口
    /// 支持精细化的进度控制和阶段跟踪
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="progress">进度信息</param>
    /// <returns>异步任务</returns>
    Task UpdateProgressAsync(Guid taskId, SlicingProgress progress);
}
