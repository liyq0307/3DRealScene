using System.ComponentModel.DataAnnotations;
using RealScene3D.Domain.Enums;

namespace RealScene3D.Domain.Entities;

/// <summary>
/// 工作流定义实体
/// </summary>
public class Workflow
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// 工作流名称
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 工作流描述
    /// </summary>
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// 工作流定义JSON（节点和连接信息）
    /// </summary>
    [Required]
    public string Definition { get; set; } = string.Empty;
    
    /// <summary>
    /// 工作流版本
    /// </summary>
    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// 创建者ID
    /// </summary>
    public Guid CreatedBy { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 工作流实例实体
/// </summary>
public class WorkflowInstance
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// 关联的工作流定义ID
    /// </summary>
    public Guid WorkflowId { get; set; }
    
    /// <summary>
    /// 实例名称
    /// </summary>
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 当前状态
    /// </summary>
    public WorkflowInstanceStatus Status { get; set; } = WorkflowInstanceStatus.Created;
    
    /// <summary>
    /// 输入参数
    /// </summary>
    public string InputParameters { get; set; } = "{}";
    
    /// <summary>
    /// 当前执行上下文
    /// </summary>
    public string Context { get; set; } = "{}";
    
    /// <summary>
    /// 执行结果
    /// </summary>
    public string? Result { get; set; }
    
    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 创建者ID
    /// </summary>
    public Guid CreatedBy { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 开始执行时间
    /// </summary>
    public DateTime? StartedAt { get; set; }
    
    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// 关联的工作流定义
    /// </summary>
    public virtual Workflow Workflow { get; set; } = null!;
}

/// <summary>
/// 工作流执行历史实体
/// </summary>
public class WorkflowExecutionHistory
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// 工作流实例ID
    /// </summary>
    public Guid WorkflowInstanceId { get; set; }
    
    /// <summary>
    /// 执行的节点ID
    /// </summary>
    [MaxLength(100)]
    public string NodeId { get; set; } = string.Empty;
    
    /// <summary>
    /// 节点类型
    /// </summary>
    [MaxLength(50)]
    public string NodeType { get; set; } = string.Empty;
    
    /// <summary>
    /// 执行状态
    /// </summary>
    public WorkflowNodeStatus Status { get; set; }
    
    /// <summary>
    /// 输入数据
    /// </summary>
    public string InputData { get; set; } = "{}";
    
    /// <summary>
    /// 输出数据
    /// </summary>
    public string OutputData { get; set; } = "{}";
    
    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }
    
    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 执行时间
    /// </summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 关联的工作流实例
    /// </summary>
    public virtual WorkflowInstance WorkflowInstance { get; set; } = null!;
}

