using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.WebApi.Controllers;

/// <summary>
/// 系统监控管理API控制器
/// 提供系统监控、指标收集、告警管理、仪表板等完整监控功能
/// 支持系统指标和业务指标的收集、存储、查询和可视化展示
/// 集成告警规则引擎和通知机制，确保系统稳定运行
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MonitoringController : ControllerBase
{
    private readonly IMonitoringService _monitoringService;
    private readonly ILogger<MonitoringController> _logger;

    /// <summary>
    /// 构造函数 - 依赖注入监控服务和日志记录器
    /// </summary>
    /// <param name="monitoringService">监控应用服务接口，提供监控业务逻辑处理</param>
    /// <param name="logger">日志记录器，用于记录监控操作和告警事件</param>
    public MonitoringController(IMonitoringService monitoringService, ILogger<MonitoringController> logger)
    {
        _monitoringService = monitoringService;
        _logger = logger;
    }

    /// <summary>
    /// 记录系统指标
    /// </summary>
    [HttpPost("metrics/system")]
    public async Task<IActionResult> RecordSystemMetric([FromBody] RecordSystemMetricRequest request)
    {
        try
        {
            await _monitoringService.RecordSystemMetricAsync(
                request.MetricName,
                request.Value,
                request.Category,
                request.Unit,
                request.Tags,
                request.Host);

            return Ok(new { message = "指标已记录" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording system metric {MetricName}", request.MetricName);
            return StatusCode(500, new { message = "记录系统指标时发生错误" });
        }
    }

    /// <summary>
    /// 记录业务指标
    /// </summary>
    [HttpPost("metrics/business")]
    public async Task<IActionResult> RecordBusinessMetric([FromBody] RecordBusinessMetricRequest request)
    {
        try
        {
            await _monitoringService.RecordBusinessMetricAsync(
                request.MetricName,
                request.Value,
                request.Unit,
                request.Dimensions);

            return Ok(new { message = "业务指标已记录" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording business metric {MetricName}", request.MetricName);
            return StatusCode(500, new { message = "记录业务指标时发生错误" });
        }
    }

    /// <summary>
    /// 获取系统指标历史数据
    /// </summary>
    [HttpGet("metrics/system/{metricName}")]
    public async Task<ActionResult<IEnumerable<SystemMetric>>> GetSystemMetrics(
        string metricName,
        [FromQuery] DateTime startTime,
        [FromQuery] DateTime endTime,
        [FromQuery] string category = "")
    {
        try
        {
            var metrics = await _monitoringService.GetSystemMetricsAsync(metricName, startTime, endTime, category);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system metrics for {MetricName}", metricName);
            return StatusCode(500, new { message = "获取系统指标时发生错误" });
        }
    }

    /// <summary>
    /// 获取业务指标历史数据
    /// </summary>
    [HttpGet("metrics/business/{metricName}")]
    public async Task<ActionResult<IEnumerable<BusinessMetric>>> GetBusinessMetrics(
        string metricName,
        [FromQuery] DateTime startTime,
        [FromQuery] DateTime endTime)
    {
        try
        {
            var metrics = await _monitoringService.GetBusinessMetricsAsync(metricName, startTime, endTime);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting business metrics for {MetricName}", metricName);
            return StatusCode(500, new { message = "获取业务指标时发生错误" });
        }
    }

    /// <summary>
    /// 获取最新系统指标快照
    /// </summary>
    [HttpGet("metrics/system/snapshot")]
    public async Task<ActionResult<Dictionary<string, SystemMetric>>> GetLatestSystemMetrics([FromQuery] string category = "")
    {
        try
        {
            var metrics = await _monitoringService.GetLatestSystemMetricsAsync(category);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest system metrics");
            return StatusCode(500, new { message = "获取系统指标快照时发生错误" });
        }
    }

    /// <summary>
    /// 创建告警规则
    /// </summary>
    [HttpPost("alert-rules")]
    public async Task<ActionResult<AlertRule>> CreateAlertRule([FromBody] CreateAlertRuleRequest request, [FromQuery] Guid userId)
    {
        try
        {
            var condition = new AlertCondition
            {
                Operator = request.Condition.Operator,
                Threshold = request.Condition.Threshold,
                DurationSeconds = request.Condition.DurationSeconds,
                Aggregation = request.Condition.Aggregation,
                TimeWindowSeconds = request.Condition.TimeWindowSeconds
            };

            var rule = await _monitoringService.CreateAlertRuleAsync(
                request.Name,
                request.Description,
                request.MetricName,
                condition,
                request.Level,
                userId);

            return CreatedAtAction(nameof(GetAlertRule), new { id = rule.Id }, rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alert rule");
            return StatusCode(500, new { message = "创建告警规则时发生错误" });
        }
    }

    /// <summary>
    /// 获取告警规则列表
    /// </summary>
    [HttpGet("alert-rules")]
    public async Task<ActionResult<IEnumerable<AlertRule>>> GetAlertRules([FromQuery] Guid? userId = null)
    {
        try
        {
            var rules = await _monitoringService.GetAlertRulesAsync(userId);
            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alert rules");
            return StatusCode(500, new { message = "获取告警规则列表时发生错误" });
        }
    }

    /// <summary>
    /// 获取告警规则详情
    /// </summary>
    [HttpGet("alert-rules/{id}")]
    public async Task<ActionResult<AlertRule>> GetAlertRule(Guid id)
    {
        try
        {
            var rules = await _monitoringService.GetAlertRulesAsync();
            var rule = rules.FirstOrDefault(r => r.Id == id);

            if (rule == null)
            {
                return NotFound(new { message = "告警规则未找到" });
            }

            return Ok(rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alert rule {RuleId}", id);
            return StatusCode(500, new { message = "获取告警规则时发生错误" });
        }
    }

    /// <summary>
    /// 更新告警规则
    /// </summary>
    [HttpPut("alert-rules/{id}")]
    public async Task<ActionResult<AlertRule>> UpdateAlertRule(Guid id, [FromBody] UpdateAlertRuleRequest request, [FromQuery] Guid userId)
    {
        try
        {
            var condition = new AlertCondition
            {
                Operator = request.Condition.Operator,
                Threshold = request.Condition.Threshold,
                DurationSeconds = request.Condition.DurationSeconds,
                Aggregation = request.Condition.Aggregation,
                TimeWindowSeconds = request.Condition.TimeWindowSeconds
            };

            var success = await _monitoringService.UpdateAlertRuleAsync(
                id,
                request.Name,
                request.Description,
                condition,
                request.Level,
                userId);

            if (!success)
            {
                return NotFound(new { message = "告警规则未找到或无权限" });
            }

            var rules = await _monitoringService.GetAlertRulesAsync();
            var updatedRule = rules.FirstOrDefault(r => r.Id == id);

            return Ok(updatedRule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating alert rule {RuleId}", id);
            return StatusCode(500, new { message = "更新告警规则时发生错误" });
        }
    }

    /// <summary>
    /// 删除告警规则
    /// </summary>
    [HttpDelete("alert-rules/{id}")]
    public async Task<IActionResult> DeleteAlertRule(Guid id, [FromQuery] Guid userId)
    {
        try
        {
            var success = await _monitoringService.DeleteAlertRuleAsync(id, userId);
            if (!success)
            {
                return NotFound(new { message = "告警规则未找到或无权限" });
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting alert rule {RuleId}", id);
            return StatusCode(500, new { message = "删除告警规则时发生错误" });
        }
    }

    /// <summary>
    /// 获取活跃告警事件
    /// </summary>
    [HttpGet("alerts/active")]
    public async Task<ActionResult<IEnumerable<AlertEvent>>> GetActiveAlerts()
    {
        try
        {
            var alerts = await _monitoringService.GetActiveAlertsAsync();
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active alerts");
            return StatusCode(500, new { message = "获取活跃告警时发生错误" });
        }
    }

    /// <summary>
    /// 获取告警事件历史
    /// </summary>
    [HttpGet("alerts/history")]
    public async Task<ActionResult<IEnumerable<AlertEvent>>> GetAlertHistory(
        [FromQuery] DateTime startTime,
        [FromQuery] DateTime endTime,
        [FromQuery] AlertLevel? level = null)
    {
        try
        {
            var alerts = await _monitoringService.GetAlertHistoryAsync(startTime, endTime, level);
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alert history");
            return StatusCode(500, new { message = "获取告警历史时发生错误" });
        }
    }

    /// <summary>
    /// 确认告警事件
    /// </summary>
    [HttpPost("alerts/{id}/acknowledge")]
    public async Task<IActionResult> AcknowledgeAlert(Guid id, [FromQuery] Guid userId)
    {
        try
        {
            var success = await _monitoringService.AcknowledgeAlertAsync(id, userId);
            if (!success)
            {
                return NotFound(new { message = "告警事件未找到或无权限" });
            }
            return Ok(new { message = "告警已确认" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging alert {AlertId}", id);
            return StatusCode(500, new { message = "确认告警时发生错误" });
        }
    }

    /// <summary>
    /// 解决告警事件
    /// </summary>
    [HttpPost("alerts/{id}/resolve")]
    public async Task<IActionResult> ResolveAlert(Guid id, [FromQuery] Guid userId)
    {
        try
        {
            var success = await _monitoringService.ResolveAlertAsync(id, userId);
            if (!success)
            {
                return NotFound(new { message = "告警事件未找到或无权限" });
            }
            return Ok(new { message = "告警已解决" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving alert {AlertId}", id);
            return StatusCode(500, new { message = "解决告警时发生错误" });
        }
    }

    /// <summary>
    /// 创建监控仪表板
    /// </summary>
    [HttpPost("dashboards")]
    public async Task<ActionResult<Dashboard>> CreateDashboard([FromBody] CreateDashboardRequest request, [FromQuery] Guid userId)
    {
        try
        {
            var config = new DashboardConfig
            {
                Charts = request.Charts,
                Layout = request.Layout,
                RefreshInterval = request.RefreshInterval,
                TimeRange = request.TimeRange
            };

            var dashboard = await _monitoringService.CreateDashboardAsync(
                request.Name,
                request.Description,
                config,
                userId);

            return CreatedAtAction(nameof(GetDashboard), new { id = dashboard.Id }, dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating dashboard");
            return StatusCode(500, new { message = "创建仪表板时发生错误" });
        }
    }

    /// <summary>
    /// 获取监控仪表板列表
    /// </summary>
    [HttpGet("dashboards")]
    public async Task<ActionResult<IEnumerable<Dashboard>>> GetDashboards([FromQuery] Guid? userId = null)
    {
        try
        {
            var dashboards = await _monitoringService.GetDashboardsAsync(userId);
            return Ok(dashboards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboards");
            return StatusCode(500, new { message = "获取仪表板列表时发生错误" });
        }
    }

    /// <summary>
    /// 获取仪表板详情
    /// </summary>
    [HttpGet("dashboards/{id}")]
    public async Task<ActionResult<Dashboard>> GetDashboard(Guid id)
    {
        try
        {
            var dashboard = await _monitoringService.GetDashboardAsync(id);
            if (dashboard == null)
            {
                return NotFound(new { message = "仪表板未找到" });
            }
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard {DashboardId}", id);
            return StatusCode(500, new { message = "获取仪表板时发生错误" });
        }
    }

    /// <summary>
    /// 更新监控仪表板
    /// </summary>
    [HttpPut("dashboards/{id}")]
    public async Task<ActionResult<Dashboard>> UpdateDashboard(Guid id, [FromBody] UpdateDashboardRequest request, [FromQuery] Guid userId)
    {
        try
        {
            var config = new DashboardConfig
            {
                Charts = request.Charts,
                Layout = request.Layout,
                RefreshInterval = request.RefreshInterval,
                TimeRange = request.TimeRange
            };

            var success = await _monitoringService.UpdateDashboardAsync(
                id,
                request.Name,
                request.Description,
                config,
                userId);

            if (!success)
            {
                return NotFound(new { message = "仪表板未找到或无权限" });
            }

            var dashboard = await _monitoringService.GetDashboardAsync(id);
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating dashboard {DashboardId}", id);
            return StatusCode(500, new { message = "更新仪表板时发生错误" });
        }
    }

    /// <summary>
    /// 删除监控仪表板
    /// </summary>
    [HttpDelete("dashboards/{id}")]
    public async Task<IActionResult> DeleteDashboard(Guid id, [FromQuery] Guid userId)
    {
        try
        {
            var success = await _monitoringService.DeleteDashboardAsync(id, userId);
            if (!success)
            {
                return NotFound(new { message = "仪表板未找到或无权限" });
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting dashboard {DashboardId}", id);
            return StatusCode(500, new { message = "删除仪表板时发生错误" });
        }
    }
}

/// <summary>
/// 记录系统指标请求DTO
/// </summary>
public class RecordSystemMetricRequest
{
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public Dictionary<string, string>? Tags { get; set; }
    public string Host { get; set; } = string.Empty;
}

/// <summary>
/// 记录业务指标请求DTO
/// </summary>
public class RecordBusinessMetricRequest
{
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public Dictionary<string, string>? Dimensions { get; set; }
}

/// <summary>
/// 创建告警规则请求DTO
/// </summary>
public class CreateAlertRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public AlertConditionDto Condition { get; set; } = new();
    public AlertLevel Level { get; set; }
    public string NotificationChannels { get; set; } = "[]";
}

/// <summary>
/// 更新告警规则请求DTO
/// </summary>
public class UpdateAlertRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AlertConditionDto Condition { get; set; } = new();
    public AlertLevel Level { get; set; }
}

/// <summary>
/// 告警条件DTO
/// </summary>
public class AlertConditionDto
{
    public ComparisonOperator Operator { get; set; }
    public double Threshold { get; set; }
    public int DurationSeconds { get; set; } = 300;
    public AggregationFunction Aggregation { get; set; } = AggregationFunction.Avg;
    public int TimeWindowSeconds { get; set; } = 300;
}

/// <summary>
/// 创建仪表板请求DTO
/// </summary>
public class CreateDashboardRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ChartConfig> Charts { get; set; } = new();
    public LayoutConfig Layout { get; set; } = new();
    public int RefreshInterval { get; set; } = 60;
    public int TimeRange { get; set; } = 60;
}

/// <summary>
/// 更新仪表板请求DTO
/// </summary>
public class UpdateDashboardRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ChartConfig> Charts { get; set; } = new();
    public LayoutConfig Layout { get; set; } = new();
    public int RefreshInterval { get; set; } = 60;
    public int TimeRange { get; set; } = 60;
}