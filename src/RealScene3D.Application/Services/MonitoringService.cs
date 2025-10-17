using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.Data;

namespace RealScene3D.Application.Services;

/// <summary>
/// 监控服务实现
/// </summary>
public class MonitoringAppService : IMonitoringService
{
    private readonly IRepository<SystemMetric> _systemMetricRepository;
    private readonly IRepository<BusinessMetric> _businessMetricRepository;
    private readonly IRepository<AlertRule> _alertRuleRepository;
    private readonly IRepository<AlertEvent> _alertEventRepository;
    private readonly IRepository<Dashboard> _dashboardRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MonitoringAppService> _logger;

    public MonitoringAppService(
        IRepository<SystemMetric> systemMetricRepository,
        IRepository<BusinessMetric> businessMetricRepository,
        IRepository<AlertRule> alertRuleRepository,
        IRepository<AlertEvent> alertEventRepository,
        IRepository<Dashboard> dashboardRepository,
        IUnitOfWork unitOfWork,
        ApplicationDbContext context,
        ILogger<MonitoringAppService> logger)
    {
        _systemMetricRepository = systemMetricRepository;
        _businessMetricRepository = businessMetricRepository;
        _alertRuleRepository = alertRuleRepository;
        _alertEventRepository = alertEventRepository;
        _dashboardRepository = dashboardRepository;
        _unitOfWork = unitOfWork;
        _context = context;
        _logger = logger;
    }

    #region 系统指标管理

    public async Task RecordSystemMetricAsync(string metricName, double value, string category, string unit = "", Dictionary<string, string>? tags = null, string host = "")
    {
        try
        {
            var metric = new SystemMetric
            {
                MetricName = metricName,
                Category = category,
                Value = value,
                Unit = unit,
                Tags = tags != null ? System.Text.Json.JsonSerializer.Serialize(tags) : "{}",
                Host = host
            };

            await _systemMetricRepository.AddAsync(metric);
            await _unitOfWork.SaveChangesAsync();

            // 检查告警规则
            await CheckAlertRulesAsync(metricName, value);

            _logger.LogDebug("系统指标已记录：{MetricName} = {Value} {Unit}", metricName, value, unit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录系统指标失败：{MetricName}", metricName);
            throw;
        }
    }

    public async Task RecordBusinessMetricAsync(string metricName, double value, string unit = "", Dictionary<string, string>? dimensions = null)
    {
        try
        {
            var metric = new BusinessMetric
            {
                MetricName = metricName,
                Value = value,
                Unit = unit,
                Dimensions = dimensions != null ? System.Text.Json.JsonSerializer.Serialize(dimensions) : "{}"
            };

            await _businessMetricRepository.AddAsync(metric);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogDebug("业务指标已记录：{MetricName} = {Value} {Unit}", metricName, value, unit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录业务指标失败：{MetricName}", metricName);
            throw;
        }
    }

    public async Task<IEnumerable<SystemMetric>> GetSystemMetricsAsync(string metricName, DateTime startTime, DateTime endTime, string category = "")
    {
        var query = _context.SystemMetrics
            .Where(m => m.MetricName == metricName && m.Timestamp >= startTime && m.Timestamp <= endTime);

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(m => m.Category == category);
        }

        return await query.OrderBy(m => m.Timestamp).ToListAsync();
    }

    public async Task<IEnumerable<BusinessMetric>> GetBusinessMetricsAsync(string metricName, DateTime startTime, DateTime endTime)
    {
        return await _context.BusinessMetrics
            .Where(m => m.MetricName == metricName && m.Timestamp >= startTime && m.Timestamp <= endTime)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }

    public async Task<Dictionary<string, SystemMetric>> GetLatestSystemMetricsAsync(string category = "")
    {
        var query = _context.SystemMetrics.AsQueryable();

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(m => m.Category == category);
        }

        var latestMetrics = await query
            .GroupBy(m => new { m.MetricName, m.Host })
            .Select(g => g.OrderByDescending(m => m.Timestamp).First())
            .ToListAsync();

        return latestMetrics.ToDictionary(m => $"{m.MetricName}@{m.Host}", m => m);
    }

    #endregion

    #region 告警规则管理

    public async Task<AlertRule> CreateAlertRuleAsync(string name, string description, string metricName, AlertCondition condition, AlertLevel level, Guid userId)
    {
        try
        {
            var rule = new AlertRule
            {
                Name = name,
                Description = description,
                MetricName = metricName,
                Condition = System.Text.Json.JsonSerializer.Serialize(condition),
                Level = level,
                CreatedBy = userId
            };

            await _alertRuleRepository.AddAsync(rule);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("告警规则已创建：{RuleName}, 用户：{UserId}", name, userId);

            return rule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建告警规则失败：{RuleName}", name);
            throw;
        }
    }

    public async Task<bool> UpdateAlertRuleAsync(Guid ruleId, string name, string description, AlertCondition condition, AlertLevel level, Guid userId)
    {
        try
        {
            var rule = await _alertRuleRepository.GetByIdAsync(ruleId);
            if (rule == null || rule.CreatedBy != userId)
            {
                return false;
            }

            rule.Name = name;
            rule.Description = description;
            rule.Condition = System.Text.Json.JsonSerializer.Serialize(condition);
            rule.Level = level;
            rule.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("告警规则已更新：{RuleId}, 用户：{UserId}", ruleId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新告警规则失败：{RuleId}", ruleId);
            return false;
        }
    }

    public async Task<bool> DeleteAlertRuleAsync(Guid ruleId, Guid userId)
    {
        try
        {
            var rule = await _alertRuleRepository.GetByIdAsync(ruleId);
            if (rule == null || rule.CreatedBy != userId)
            {
                return false;
            }

            await _alertRuleRepository.DeleteAsync(rule);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("告警规则已删除：{RuleId}, 用户：{UserId}", ruleId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除告警规则失败：{RuleId}", ruleId);
            return false;
        }
    }

    public async Task<IEnumerable<AlertRule>> GetAlertRulesAsync(Guid? userId = null)
    {
        var query = _context.AlertRules.Where(r => r.IsEnabled);

        if (userId.HasValue)
        {
            query = query.Where(r => r.CreatedBy == userId.Value);
        }

        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    #endregion

    #region 告警事件管理

    public async Task<IEnumerable<AlertEvent>> GetActiveAlertsAsync()
    {
        return await _context.AlertEvents
            .Include(e => e.AlertRule)
            .Where(e => e.Status == AlertStatus.Firing)
            .OrderByDescending(e => e.FiredAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<AlertEvent>> GetAlertHistoryAsync(DateTime startTime, DateTime endTime, AlertLevel? level = null)
    {
        var query = _context.AlertEvents
            .Include(e => e.AlertRule)
            .Where(e => e.FiredAt >= startTime && e.FiredAt <= endTime);

        if (level.HasValue)
        {
            query = query.Where(e => e.Level == level.Value);
        }

        return await query.OrderByDescending(e => e.FiredAt).ToListAsync();
    }

    public async Task<bool> AcknowledgeAlertAsync(Guid alertId, Guid userId)
    {
        try
        {
            var alert = await _alertEventRepository.GetByIdAsync(alertId);
            if (alert == null)
            {
                return false;
            }

            alert.Status = AlertStatus.Acknowledged;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("告警已确认：{AlertId}, 用户：{UserId}", alertId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "确认告警失败：{AlertId}", alertId);
            return false;
        }
    }

    public async Task<bool> ResolveAlertAsync(Guid alertId, Guid userId)
    {
        try
        {
            var alert = await _alertEventRepository.GetByIdAsync(alertId);
            if (alert == null)
            {
                return false;
            }

            alert.Status = AlertStatus.Resolved;
            alert.ResolvedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("告警已解决：{AlertId}, 用户：{UserId}", alertId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解决告警失败：{AlertId}", alertId);
            return false;
        }
    }

    #endregion

    #region 仪表板管理

    public async Task<Dashboard> CreateDashboardAsync(string name, string description, DashboardConfig config, Guid userId)
    {
        try
        {
            var dashboard = new Dashboard
            {
                Name = name,
                Description = description,
                Configuration = System.Text.Json.JsonSerializer.Serialize(config),
                CreatedBy = userId
            };

            await _dashboardRepository.AddAsync(dashboard);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("仪表板已创建：{DashboardName}, 用户：{UserId}", name, userId);

            return dashboard;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建仪表板失败：{DashboardName}", name);
            throw;
        }
    }

    public async Task<bool> UpdateDashboardAsync(Guid dashboardId, string name, string description, DashboardConfig config, Guid userId)
    {
        try
        {
            var dashboard = await _dashboardRepository.GetByIdAsync(dashboardId);
            if (dashboard == null || dashboard.CreatedBy != userId)
            {
                return false;
            }

            dashboard.Name = name;
            dashboard.Description = description;
            dashboard.Configuration = System.Text.Json.JsonSerializer.Serialize(config);
            dashboard.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("仪表板已更新：{DashboardId}, 用户：{UserId}", dashboardId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新仪表板失败：{DashboardId}", dashboardId);
            return false;
        }
    }

    public async Task<bool> DeleteDashboardAsync(Guid dashboardId, Guid userId)
    {
        try
        {
            var dashboard = await _dashboardRepository.GetByIdAsync(dashboardId);
            if (dashboard == null || dashboard.CreatedBy != userId)
            {
                return false;
            }

            await _dashboardRepository.DeleteAsync(dashboard);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("仪表板已删除：{DashboardId}, 用户：{UserId}", dashboardId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除仪表板失败：{DashboardId}", dashboardId);
            return false;
        }
    }

    public async Task<IEnumerable<Dashboard>> GetDashboardsAsync(Guid? userId = null)
    {
        var query = _context.Dashboards.AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(d => d.CreatedBy == userId.Value || d.IsPublic);
        }
        else
        {
            query = query.Where(d => d.IsPublic);
        }

        return await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
    }

    public async Task<Dashboard?> GetDashboardAsync(Guid dashboardId)
    {
        return await _context.Dashboards
            .FirstOrDefaultAsync(d => d.Id == dashboardId && (d.IsPublic || d.CreatedAt != default));
    }

    #endregion

    #region 私有方法

    private async Task CheckAlertRulesAsync(string metricName, double value)
    {
        try
        {
            var rules = await _context.AlertRules
                .Where(r => r.IsEnabled && r.MetricName == metricName)
                .ToListAsync();

            foreach (var rule in rules)
            {
                try
                {
                    var condition = System.Text.Json.JsonSerializer.Deserialize<AlertCondition>(rule.Condition);
                    if (condition != null && IsConditionMet(value, condition))
                    {
                        await CreateAlertEventAsync(rule, value);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "检查告警规则失败：{RuleId}", rule.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查告警规则时发生错误");
        }
    }

    private bool IsConditionMet(double value, AlertCondition condition)
    {
        return condition.Operator switch
        {
            ComparisonOperator.GreaterThan => value > condition.Threshold,
            ComparisonOperator.GreaterThanOrEqual => value >= condition.Threshold,
            ComparisonOperator.LessThan => value < condition.Threshold,
            ComparisonOperator.LessThanOrEqual => value <= condition.Threshold,
            ComparisonOperator.Equal => Math.Abs(value - condition.Threshold) < 0.001,
            ComparisonOperator.NotEqual => Math.Abs(value - condition.Threshold) >= 0.001,
            _ => false
        };
    }

    private async Task CreateAlertEventAsync(AlertRule rule, double metricValue)
    {
        try
        {
            // 检查是否在静默期内
            var lastAlert = await _context.AlertEvents
                .Where(e => e.AlertRuleId == rule.Id && e.Status == AlertStatus.Firing)
                .OrderByDescending(e => e.FiredAt)
                .FirstOrDefaultAsync();

            if (lastAlert != null)
            {
                var timeSinceLastAlert = DateTime.UtcNow - lastAlert.FiredAt;
                if (timeSinceLastAlert.TotalMinutes < rule.SilentPeriodMinutes)
                {
                    return; // 在静默期内，不创建新告警
                }
            }

            var alertEvent = new AlertEvent
            {
                AlertRuleId = rule.Id,
                Title = $"{rule.Name} 触发告警",
                Message = $"{rule.MetricName} 当前值 {metricValue} 超过阈值 {GetThresholdFromCondition(rule.Condition)}",
                Level = rule.Level,
                MetricValue = metricValue,
                Threshold = GetThresholdFromCondition(rule.Condition)
            };

            await _alertEventRepository.AddAsync(alertEvent);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogWarning("告警事件已创建：{AlertTitle}", alertEvent.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建告警事件失败：规则{RuleId}", rule.Id);
        }
    }

    private double GetThresholdFromCondition(string conditionJson)
    {
        try
        {
            var condition = System.Text.Json.JsonSerializer.Deserialize<AlertCondition>(conditionJson);
            return condition?.Threshold ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    #endregion
}

/// <summary>
/// 系统指标收集器
/// </summary>
public class SystemMetricsCollector
{
    private readonly IMonitoringService _monitoringService;
    private readonly ILogger<SystemMetricsCollector> _logger;
    private Timer? _timer;

    public SystemMetricsCollector(
        IMonitoringService monitoringService,
        ILogger<SystemMetricsCollector> logger)
    {
        _monitoringService = monitoringService;
        _logger = logger;
    }

    public void StartCollection(int intervalSeconds = 60)
    {
        _timer = new Timer(CollectMetricsAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(intervalSeconds));
        _logger.LogInformation("系统指标收集器已启动，收集间隔：{IntervalSeconds}秒", intervalSeconds);
    }

    public void StopCollection()
    {
        _timer?.Dispose();
        _logger.LogInformation("系统指标收集器已停止");
    }

    private async void CollectMetricsAsync(object? state)
    {
        try
        {
            await CollectSystemMetricsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "收集系统指标时发生错误");
        }
    }

    private async Task CollectSystemMetricsAsync()
    {
        var timestamp = DateTime.UtcNow;
        var hostName = Environment.MachineName;

        // CPU使用率
        var cpuUsage = await GetCpuUsageAsync();
        await _monitoringService.RecordSystemMetricAsync("cpu_usage", cpuUsage, "system", "%", new Dictionary<string, string> { ["host"] = hostName });

        // 内存使用率
        var memoryUsage = GetMemoryUsage();
        await _monitoringService.RecordSystemMetricAsync("memory_usage", memoryUsage, "system", "%", new Dictionary<string, string> { ["host"] = hostName });

        // 磁盘使用率
        var diskUsage = GetDiskUsage();
        await _monitoringService.RecordSystemMetricAsync("disk_usage", diskUsage, "system", "%", new Dictionary<string, string> { ["host"] = hostName, ["path"] = "C:" });

        _logger.LogDebug("系统指标收集完成：{Timestamp}", timestamp);
    }

    /// <summary>
    /// 获取CPU使用率 - 异步方法用于未来扩展
    /// </summary>
    /// <returns>CPU使用率百分比</returns>
    private async Task<double> GetCpuUsageAsync()
    {
        // 这里应该实现实际的CPU使用率收集逻辑
        // 目前返回示例数据
        // 使用Task.Run将同步操作包装为异步，避免阻塞
        return await Task.Run(() => new Random().NextDouble() * 100);
    }

    private double GetMemoryUsage()
    {
        // 这里应该实现实际的内存使用率收集逻辑
        // 目前返回示例数据
        return new Random().NextDouble() * 100;
    }

    private double GetDiskUsage()
    {
        // 这里应该实现实际的磁盘使用率收集逻辑
        // 目前返回示例数据
        return new Random().NextDouble() * 100;
    }
}