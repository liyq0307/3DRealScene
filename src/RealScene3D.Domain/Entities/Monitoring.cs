using System.ComponentModel.DataAnnotations;
using RealScene3D.Domain.Enums;

namespace RealScene3D.Domain.Entities;

/// <summary>
/// 系统指标实体
/// </summary>
public class SystemMetric
{
    public Guid Id { get; set; }

    /// <summary>
    /// 指标名称
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// 指标分类（cpu, memory, disk, network, database等）
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 指标值
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// 指标单位
    /// </summary>
    [MaxLength(20)]
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// 指标标签（JSON格式）
    /// </summary>
    public string Tags { get; set; } = "{}";

    /// <summary>
    /// 采集时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 主机名或服务标识
    /// </summary>
    [MaxLength(100)]
    public string Host { get; set; } = string.Empty;
}

/// <summary>
/// 业务指标实体
/// </summary>
public class BusinessMetric
{
    public Guid Id { get; set; }

    /// <summary>
    /// 指标名称
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// 指标值
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// 指标单位
    /// </summary>
    [MaxLength(20)]
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// 维度标签（用户、场景、操作类型等）
    /// </summary>
    public string Dimensions { get; set; } = "{}";

    /// <summary>
    /// 采集时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 告警规则实体
/// </summary>
public class AlertRule
{
    public Guid Id { get; set; }

    /// <summary>
    /// 规则名称
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 规则描述
    /// </summary>
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 监控的指标名称
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// 告警条件（JSON格式）
    /// </summary>
    [Required]
    public string Condition { get; set; } = string.Empty;

    /// <summary>
    /// 告警级别
    /// </summary>
    public AlertLevel Level { get; set; } = AlertLevel.Warning;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 通知渠道（JSON格式）
    /// </summary>
    public string NotificationChannels { get; set; } = "[]";

    /// <summary>
    /// 静默期（分钟）
    /// </summary>
    public int SilentPeriodMinutes { get; set; } = 30;

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
/// 告警事件实体
/// </summary>
public class AlertEvent
{
    public Guid Id { get; set; }

    /// <summary>
    /// 关联的告警规则ID
    /// </summary>
    public Guid AlertRuleId { get; set; }

    /// <summary>
    /// 告警标题
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 告警消息
    /// </summary>
    [Required]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 告警级别
    /// </summary>
    public AlertLevel Level { get; set; } = AlertLevel.Warning;

    /// <summary>
    /// 告警状态
    /// </summary>
    public AlertStatus Status { get; set; } = AlertStatus.Firing;

    /// <summary>
    /// 指标值
    /// </summary>
    public double MetricValue { get; set; }

    /// <summary>
    /// 阈值
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// 触发时间
    /// </summary>
    public DateTime FiredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 解决时间
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// 通知发送状态
    /// </summary>
    public string NotificationStatus { get; set; } = "{}";

    /// <summary>
    /// 关联的告警规则
    /// </summary>
    public virtual AlertRule AlertRule { get; set; } = null!;
}

/// <summary>
/// 监控仪表板实体
/// </summary>
public class Dashboard
{
    public Guid Id { get; set; }

    /// <summary>
    /// 仪表板名称
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 仪表板描述
    /// </summary>
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 仪表板配置（图表布局等）
    /// </summary>
    [Required]
    public string Configuration { get; set; } = "{}";

    /// <summary>
    /// 是否公开可见
    /// </summary>
    public bool IsPublic { get; set; } = false;

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

