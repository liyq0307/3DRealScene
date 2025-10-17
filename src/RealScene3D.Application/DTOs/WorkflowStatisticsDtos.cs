using System.ComponentModel.DataAnnotations;

namespace RealScene3D.Application.DTOs;

/// <summary>
/// 工作流统计相关数据传输对象
/// </summary>
public class WorkflowStatisticsDtos
{
    /// <summary>
    /// 工作流概览统计DTO
    /// </summary>
    public class WorkflowOverviewStatsDto
    {
        /// <summary>
        /// 总工作流定义数量
        /// </summary>
        public int TotalWorkflowDefinitions { get; set; }

        /// <summary>
        /// 总实例数量
        /// </summary>
        public int TotalInstances { get; set; }

        /// <summary>
        /// 运行中实例数量
        /// </summary>
        public int RunningInstances { get; set; }

        /// <summary>
        /// 已完成实例数量
        /// </summary>
        public int CompletedInstances { get; set; }

        /// <summary>
        /// 失败实例数量
        /// </summary>
        public int FailedInstances { get; set; }

        /// <summary>
        /// 成功率（百分比）
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// 平均执行时间（毫秒）
        /// </summary>
        public long AverageExecutionTimeMs { get; set; }
    }

    /// <summary>
    /// 工作流性能统计DTO
    /// </summary>
    public class WorkflowPerformanceStatsDto
    {
        /// <summary>
        /// 工作流ID
        /// </summary>
        public Guid WorkflowId { get; set; }

        /// <summary>
        /// 工作流名称
        /// </summary>
        public string WorkflowName { get; set; } = string.Empty;

        /// <summary>
        /// 总实例数量
        /// </summary>
        public int TotalInstances { get; set; }

        /// <summary>
        /// 已完成实例数量
        /// </summary>
        public int CompletedInstances { get; set; }

        /// <summary>
        /// 失败实例数量
        /// </summary>
        public int FailedInstances { get; set; }

        /// <summary>
        /// 成功率（百分比）
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// 平均执行时间（毫秒）
        /// </summary>
        public long AverageExecutionTimeMs { get; set; }
    }

    /// <summary>
    /// 节点执行统计DTO
    /// </summary>
    public class NodeExecutionStatsDto
    {
        /// <summary>
        /// 节点ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>
        /// 节点类型
        /// </summary>
        public string NodeType { get; set; } = string.Empty;

        /// <summary>
        /// 总执行次数
        /// </summary>
        public int TotalExecutions { get; set; }

        /// <summary>
        /// 成功执行次数
        /// </summary>
        public int SuccessfulExecutions { get; set; }

        /// <summary>
        /// 失败执行次数
        /// </summary>
        public int FailedExecutions { get; set; }

        /// <summary>
        /// 成功率（百分比）
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// 平均执行时间（毫秒）
        /// </summary>
        public long AverageExecutionTimeMs { get; set; }
    }

    /// <summary>
    /// 工作流执行趋势DTO
    /// </summary>
    public class WorkflowExecutionTrendDto
    {
        /// <summary>
        /// 时间段
        /// </summary>
        public string Period { get; set; } = string.Empty;

        /// <summary>
        /// 总实例数量
        /// </summary>
        public int TotalInstances { get; set; }

        /// <summary>
        /// 已完成实例数量
        /// </summary>
        public int CompletedInstances { get; set; }

        /// <summary>
        /// 失败实例数量
        /// </summary>
        public int FailedInstances { get; set; }

        /// <summary>
        /// 成功率（百分比）
        /// </summary>
        public double SuccessRate { get; set; }
    }

    /// <summary>
    /// 工作流健康状态DTO
    /// </summary>
    public class WorkflowHealthStatusDto
    {
        /// <summary>
        /// 健康分数（0-100）
        /// </summary>
        public double HealthScore { get; set; }

        /// <summary>
        /// 健康状态
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// 最近统计
        /// </summary>
        public WorkflowHealthStatsDto? RecentStats { get; set; }

        /// <summary>
        /// 每日统计
        /// </summary>
        public WorkflowHealthStatsDto? DailyStats { get; set; }
    }

    /// <summary>
    /// 工作流健康统计DTO
    /// </summary>
    public class WorkflowHealthStatsDto
    {
        /// <summary>
        /// 时间范围
        /// </summary>
        public string TimeRange { get; set; } = string.Empty;

        /// <summary>
        /// 总实例数量
        /// </summary>
        public int TotalInstances { get; set; }

        /// <summary>
        /// 已完成实例数量
        /// </summary>
        public int CompletedInstances { get; set; }

        /// <summary>
        /// 失败实例数量
        /// </summary>
        public int FailedInstances { get; set; }

        /// <summary>
        /// 成功率（百分比）
        /// </summary>
        public double SuccessRate { get; set; }
    }

    /// <summary>
    /// 工作流瓶颈DTO
    /// </summary>
    public class WorkflowBottleneckDto
    {
        /// <summary>
        /// 节点ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>
        /// 节点类型
        /// </summary>
        public string NodeType { get; set; } = string.Empty;

        /// <summary>
        /// 失败次数
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// 慢执行次数（超过30秒）
        /// </summary>
        public int SlowExecutionCount { get; set; }

        /// <summary>
        /// 平均执行时间（毫秒）
        /// </summary>
        public long AverageExecutionTimeMs { get; set; }

        /// <summary>
        /// 最大执行时间（毫秒）
        /// </summary>
        public long MaxExecutionTimeMs { get; set; }

        /// <summary>
        /// 风险等级
        /// </summary>
        public string RiskLevel { get; set; } = string.Empty;
    }

    /// <summary>
    /// 工作流监控指标DTO
    /// </summary>
    public class WorkflowMetricsDto
    {
        /// <summary>
        /// 指标名称
        /// </summary>
        public string MetricName { get; set; } = string.Empty;

        /// <summary>
        /// 指标值
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// 单位
        /// </summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 标签
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    /// <summary>
    /// 工作流告警DTO
    /// </summary>
    public class WorkflowAlertDto
    {
        /// <summary>
        /// 告警ID
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 告警级别
        /// </summary>
        public string Level { get; set; } = string.Empty;

        /// <summary>
        /// 告警标题
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 告警消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 工作流ID
        /// </summary>
        public Guid? WorkflowId { get; set; }

        /// <summary>
        /// 实例ID
        /// </summary>
        public Guid? InstanceId { get; set; }

        /// <summary>
        /// 节点ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>
        /// 告警时间
        /// </summary>
        public DateTime AlertTime { get; set; }

        /// <summary>
        /// 是否已确认
        /// </summary>
        public bool IsAcknowledged { get; set; }

        /// <summary>
        /// 确认时间
        /// </summary>
        public DateTime? AcknowledgedAt { get; set; }
    }
}