using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Enums;

namespace RealScene3D.Domain.Interfaces;

/// <summary>
/// 监控服务接口
/// </summary>
public interface IMonitoringService
{
    /// <summary>
    /// 记录系统指标
    /// </summary>
    Task RecordSystemMetricAsync(string metricName, double value, string category, string unit = "", Dictionary<string, string>? tags = null, string host = "");

    /// <summary>
    /// 记录业务指标
    /// </summary>
    Task RecordBusinessMetricAsync(string metricName, double value, string unit = "", Dictionary<string, string>? dimensions = null);

    /// <summary>
    /// 获取系统指标历史数据
    /// </summary>
    Task<IEnumerable<SystemMetric>> GetSystemMetricsAsync(string metricName, DateTime startTime, DateTime endTime, string category = "");

    /// <summary>
    /// 获取业务指标历史数据
    /// </summary>
    Task<IEnumerable<BusinessMetric>> GetBusinessMetricsAsync(string metricName, DateTime startTime, DateTime endTime);

    /// <summary>
    /// 获取最新系统指标快照
    /// </summary>
    Task<Dictionary<string, SystemMetric>> GetLatestSystemMetricsAsync(string category = "");

    /// <summary>
    /// 创建告警规则
    /// </summary>
    Task<AlertRule> CreateAlertRuleAsync(string name, string description, string metricName, AlertCondition condition, AlertLevel level, Guid userId);

    /// <summary>
    /// 更新告警规则
    /// </summary>
    Task<bool> UpdateAlertRuleAsync(Guid ruleId, string name, string description, AlertCondition condition, AlertLevel level, Guid userId);

    /// <summary>
    /// 删除告警规则
    /// </summary>
    Task<bool> DeleteAlertRuleAsync(Guid ruleId, Guid userId);

    /// <summary>
    /// 获取告警规则列表
    /// </summary>
    Task<IEnumerable<AlertRule>> GetAlertRulesAsync(Guid? userId = null);

    /// <summary>
    /// 获取活跃告警事件
    /// </summary>
    Task<IEnumerable<AlertEvent>> GetActiveAlertsAsync();

    /// <summary>
    /// 获取告警事件历史
    /// </summary>
    Task<IEnumerable<AlertEvent>> GetAlertHistoryAsync(DateTime startTime, DateTime endTime, AlertLevel? level = null);

    /// <summary>
    /// 确认告警事件
    /// </summary>
    Task<bool> AcknowledgeAlertAsync(Guid alertId, Guid userId);

    /// <summary>
    /// 解决告警事件
    /// </summary>
    Task<bool> ResolveAlertAsync(Guid alertId, Guid userId);

    /// <summary>
    /// 创建监控仪表板
    /// </summary>
    Task<Dashboard> CreateDashboardAsync(string name, string description, DashboardConfig config, Guid userId);

    /// <summary>
    /// 更新监控仪表板
    /// </summary>
    Task<bool> UpdateDashboardAsync(Guid dashboardId, string name, string description, DashboardConfig config, Guid userId);

    /// <summary>
    /// 删除监控仪表板
    /// </summary>
    Task<bool> DeleteDashboardAsync(Guid dashboardId, Guid userId);

    /// <summary>
    /// 获取监控仪表板列表
    /// </summary>
    Task<IEnumerable<Dashboard>> GetDashboardsAsync(Guid? userId = null);

    /// <summary>
    /// 获取仪表板详情
    /// </summary>
    Task<Dashboard?> GetDashboardAsync(Guid dashboardId);
}

/// <summary>
/// 告警条件
/// </summary>
public class AlertCondition
{
    /// <summary>
    /// 比较操作符
    /// </summary>
    public ComparisonOperator Operator { get; set; }

    /// <summary>
    /// 阈值
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// 持续时间（秒）- 在此时间内连续满足条件才触发告警
    /// </summary>
    public int DurationSeconds { get; set; } = 300;

    /// <summary>
    /// 聚合函数（avg, max, min, sum, count）
    /// </summary>
    public AggregationFunction Aggregation { get; set; } = AggregationFunction.Avg;

    /// <summary>
    /// 时间窗口（秒）
    /// </summary>
    public int TimeWindowSeconds { get; set; } = 300;
}

/// <summary>
/// 仪表板配置
/// </summary>
public class DashboardConfig
{
    /// <summary>
    /// 图表配置列表
    /// </summary>
    public List<ChartConfig> Charts { get; set; } = new();

    /// <summary>
    /// 布局配置
    /// </summary>
    public LayoutConfig Layout { get; set; } = new();

    /// <summary>
    /// 刷新间隔（秒）
    /// </summary>
    public int RefreshInterval { get; set; } = 60;

    /// <summary>
    /// 时间范围（分钟）
    /// </summary>
    public int TimeRange { get; set; } = 60;
}

/// <summary>
/// 图表配置
/// </summary>
public class ChartConfig
{
    /// <summary>
    /// 图表ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 图表类型（line, bar, pie, gauge等）
    /// </summary>
    public string Type { get; set; } = "line";

    /// <summary>
    /// 图表标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 指标名称
    /// </summary>
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// 位置和大小
    /// </summary>
    public PositionConfig Position { get; set; } = new();

    /// <summary>
    /// 数据配置
    /// </summary>
    public DataConfig Data { get; set; } = new();
}

/// <summary>
/// 布局配置
/// </summary>
public class LayoutConfig
{
    /// <summary>
    /// 列数
    /// </summary>
    public int Columns { get; set; } = 12;

    /// <summary>
    /// 行高（像素）
    /// </summary>
    public int RowHeight { get; set; } = 150;

    /// <summary>
    /// 边距（像素）
    /// </summary>
    public int Margin { get; set; } = 10;
}

/// <summary>
/// 位置配置
/// </summary>
public class PositionConfig
{
    /// <summary>
    /// X坐标（列）
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Y坐标（行）
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// 宽度（列）
    /// </summary>
    public int Width { get; set; } = 6;

    /// <summary>
    /// 高度（行）
    /// </summary>
    public int Height { get; set; } = 4;
}

/// <summary>
/// 数据配置
/// </summary>
public class DataConfig
{
    /// <summary>
    /// 聚合函数
    /// </summary>
    public AggregationFunction Aggregation { get; set; } = AggregationFunction.Avg;

    /// <summary>
    /// 分组字段
    /// </summary>
    public List<string> GroupBy { get; set; } = new();

    /// <summary>
    /// 过滤条件
    /// </summary>
    public Dictionary<string, string> Filters { get; set; } = new();

    /// <summary>
    /// 排序字段
    /// </summary>
    public string SortBy { get; set; } = string.Empty;

    /// <summary>
    /// 排序方向
    /// </summary>
    public SortDirection SortDirection { get; set; } = SortDirection.Desc;
}

