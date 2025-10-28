using System.ComponentModel.DataAnnotations;

namespace RealScene3D.Domain.Entities;

/// <summary>
/// 3D模型切片任务领域实体类
/// 表示三维模型切片处理的核心业务概念，管理切片任务的完整生命周期
/// 包含任务配置、执行状态、进度跟踪等关键信息
/// </summary>
public class SlicingTask
{
    /// <summary>
    /// 切片任务唯一标识符，主键
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 切片任务名称，必填项，最大长度200字符
    /// 用于标识和区分不同的切片任务，支持中英文字符
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 源3D模型文件路径，必填项，最大长度500字符
    /// 指向MinIO对象存储中的源模型文件路径
    /// 支持多种3D模型格式：OBJ、FBX、GLTF、BIM等
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string SourceModelPath { get; set; } = string.Empty;

    /// <summary>
    /// 关联的场景对象ID，可选项
    /// 外键关联到SceneObject实体，建立切片任务与场景对象的从属关系
    /// </summary>
    public Guid? SceneObjectId { get; set; }

    /// <summary>
    /// 关联的场景对象导航属性
    /// 与SceneObject实体建立多对一关联关系
    /// </summary>
    public SceneObject? SceneObject { get; set; }

    /// <summary>
    /// 模型类型，必填项，最大长度50字符
    /// 指定切片算法和处理策略，如：BIM、倾斜摄影、激光点云等
    /// 不同类型采用不同的切片算法和参数配置
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ModelType { get; set; } = string.Empty;

    /// <summary>
    /// 切片配置JSON，必填项
    /// 包含切片粒度、输出格式、LOD层级等详细配置参数
    /// JSON格式，支持扩展配置项
    /// </summary>
    [Required]
    public string SlicingConfig { get; set; } = string.Empty;

    /// <summary>
    /// 切片任务当前状态，默认Created
    /// 使用枚举类型定义状态流转：Created -> Queued -> Processing -> Completed/Failed
    /// 支持取消操作：Processing -> Cancelled
    /// </summary>
    public SlicingTaskStatus Status { get; set; } = SlicingTaskStatus.Created;

    /// <summary>
    /// 切片处理进度，范围0-100，默认0
    /// 表示切片任务的完成百分比，用于前端进度显示和监控
    /// 实时更新，反映当前处理进度
    /// </summary>
    public int Progress { get; set; } = 0;

    /// <summary>
    /// 切片输出目录路径，可选项，最大长度500字符
    /// 指定切片结果文件的存储目录路径
    /// 支持嵌套目录结构，按层级组织切片文件
    /// </summary>
    [MaxLength(500)]
    public string? OutputPath { get; set; }

    /// <summary>
    /// 错误信息，可选项
    /// 当切片任务执行失败时，记录详细的错误信息
    /// 用于问题诊断和故障排查
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 切片任务创建者用户ID，必填项
    /// 外键关联到User实体，建立任务与用户的所属关系
    /// 用于权限验证和任务管理
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// 切片任务创建时间，必填项，默认当前时间
    /// 记录任务的创建时间戳，支持审计和排序
    /// 使用UTC时间，确保跨时区的准确性
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 切片任务开始处理时间，可选项
    /// 记录任务开始实际处理的时间点
    /// 用于计算处理耗时和性能分析
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 切片任务完成时间，可选项
    /// 记录任务完成的时间点，支持成功和失败两种状态
    /// 用于统计和历史记录
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// 三维切片实体
/// </summary>
public class Slice
{
    public Guid Id { get; set; }

    /// <summary>
    /// 关联的切片任务ID
    /// </summary>
    public Guid SlicingTaskId { get; set; }

    /// <summary>
    /// 切片级别（LOD级别）
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// 切片X坐标
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// 切片Y坐标
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// 切片Z坐标
    /// </summary>
    public int Z { get; set; }

    /// <summary>
    /// 切片文件路径
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 切片包围盒（JSON格式）
    /// </summary>
    public string BoundingBox { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 关联的切片任务
    /// </summary>
    public virtual SlicingTask SlicingTask { get; set; } = null!;
}

/// <summary>
/// 切片任务状态枚举
/// </summary>
public enum SlicingTaskStatus
{
    Created = 0,
    Queued = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}