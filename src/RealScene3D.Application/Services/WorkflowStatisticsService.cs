using Microsoft.EntityFrameworkCore;
using RealScene3D.Application.DTOs;
using RealScene3D.Domain.Entities;
using RealScene3D.Infrastructure.Data;
using System.Text.Json;
using WorkflowStats = RealScene3D.Application.DTOs.WorkflowStatisticsDtos;

namespace RealScene3D.Application.Services;

/// <summary>
/// 工作流统计和监控服务
/// 提供工作流执行统计、性能监控、成功率分析等功能
/// </summary>
public class WorkflowStatisticsService
{
    private readonly ApplicationDbContext _context;

    public WorkflowStatisticsService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取工作流概览统计
    /// </summary>
    public async Task<WorkflowStats.WorkflowOverviewStatsDto> GetWorkflowOverviewStatsAsync()
    {
        var totalWorkflows = await _context.Workflows.CountAsync(w => w.IsEnabled);
        var totalInstances = await _context.WorkflowInstances.CountAsync();
        var runningInstances = await _context.WorkflowInstances.CountAsync(i => i.Status == WorkflowInstanceStatus.Running);
        var completedInstances = await _context.WorkflowInstances.CountAsync(i => i.Status == WorkflowInstanceStatus.Completed);
        var failedInstances = await _context.WorkflowInstances.CountAsync(i => i.Status == WorkflowInstanceStatus.Failed);

        // 计算成功率
        var totalFinishedInstances = completedInstances + failedInstances;
        var successRate = totalFinishedInstances > 0 ? (double)completedInstances / totalFinishedInstances * 100 : 0;

        // 计算平均执行时间
        double? avgExecutionTime = await _context.WorkflowInstances
            .Where(i => i.Status == WorkflowInstanceStatus.Completed && i.StartedAt.HasValue && i.CompletedAt.HasValue)
            .Select(i => EF.Functions.DateDiffMillisecond(i.StartedAt!.Value, i.CompletedAt!.Value))
            .AverageAsync();

        return new WorkflowStats.WorkflowOverviewStatsDto
        {
            TotalWorkflowDefinitions = totalWorkflows,
            TotalInstances = totalInstances,
            RunningInstances = runningInstances,
            CompletedInstances = completedInstances,
            FailedInstances = failedInstances,
            SuccessRate = Math.Round(successRate, 2),
            AverageExecutionTimeMs = avgExecutionTime.HasValue ? Convert.ToInt64(avgExecutionTime.Value) : 0
        };
    }

    /// <summary>
    /// 获取工作流性能统计
    /// </summary>
    public async Task<List<WorkflowStats.WorkflowPerformanceStatsDto>> GetWorkflowPerformanceStatsAsync(DateTime startDate, DateTime endDate)
    {
        var stats = await _context.WorkflowInstances
            .Where(i => i.CreatedAt >= startDate && i.CreatedAt <= endDate)
            .GroupBy(i => i.WorkflowId)
            .Select(g => new
            {
                WorkflowId = g.Key,
                TotalInstances = g.Count(),
                CompletedInstances = g.Count(i => i.Status == WorkflowInstanceStatus.Completed),
                FailedInstances = g.Count(i => i.Status == WorkflowInstanceStatus.Failed),
                AverageExecutionTime = (decimal?)g.Where(i => i.Status == WorkflowInstanceStatus.Completed && i.StartedAt.HasValue && i.CompletedAt.HasValue)
                    .Select(i => EF.Functions.DateDiffMillisecond(i.StartedAt!.Value, i.CompletedAt!.Value))
                    .Average() ?? 0
            })
            .ToListAsync();

        var workflowIds = stats.Select(s => s.WorkflowId).ToList();
        var workflows = await _context.Workflows
            .Where(w => workflowIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name);

        return stats.Select(s => new WorkflowStats.WorkflowPerformanceStatsDto
        {
            WorkflowId = s.WorkflowId,
            WorkflowName = workflows.GetValueOrDefault(s.WorkflowId, "Unknown"),
            TotalInstances = s.TotalInstances,
            CompletedInstances = s.CompletedInstances,
            FailedInstances = s.FailedInstances,
            SuccessRate = s.TotalInstances > 0 ? Math.Round((double)s.CompletedInstances / s.TotalInstances * 100, 2) : 0,
            AverageExecutionTimeMs = (long)s.AverageExecutionTime
        }).ToList();
    }

    /// <summary>
    /// 获取节点执行统计
    /// </summary>
    public async Task<List<WorkflowStats.NodeExecutionStatsDto>> GetNodeExecutionStatsAsync(DateTime startDate, DateTime endDate)
    {
        var stats = await _context.WorkflowExecutionHistories
            .Where(h => h.ExecutedAt >= startDate && h.ExecutedAt <= endDate)
            .GroupBy(h => new { h.NodeId, h.NodeType })
            .Select(g => new
            {
                NodeId = g.Key.NodeId,
                NodeType = g.Key.NodeType,
                TotalExecutions = g.Count(),
                SuccessfulExecutions = g.Count(h => h.Status == WorkflowNodeStatus.Completed),
                FailedExecutions = g.Count(h => h.Status == WorkflowNodeStatus.Failed),
                AverageExecutionTime = (decimal)g.Where(h => h.ExecutionTimeMs > 0).Average(h => (decimal)h.ExecutionTimeMs)
            })
            .ToListAsync();

        return stats.Select(s => new WorkflowStats.NodeExecutionStatsDto
        {
            NodeId = s.NodeId,
            NodeType = s.NodeType,
            TotalExecutions = s.TotalExecutions,
            SuccessfulExecutions = s.SuccessfulExecutions,
            FailedExecutions = s.FailedExecutions,
            SuccessRate = s.TotalExecutions > 0 ? Math.Round((double)s.SuccessfulExecutions / s.TotalExecutions * 100, 2) : 0,
            AverageExecutionTimeMs = (long)s.AverageExecutionTime
        }).ToList();
    }

    /// <summary>
    /// 获取工作流执行趋势
    /// </summary>
    public async Task<List<WorkflowStats.WorkflowExecutionTrendDto>> GetWorkflowExecutionTrendAsync(DateTime startDate, DateTime endDate, string period = "day")
    {
        IQueryable<IGrouping<DateTime, WorkflowInstance>> query;

        if (period == "hour")
        {
            query = _context.WorkflowInstances
                .Where(i => i.CreatedAt >= startDate && i.CreatedAt <= endDate)
                .GroupBy(i => new DateTime(i.CreatedAt.Year, i.CreatedAt.Month, i.CreatedAt.Day, i.CreatedAt.Hour, 0, 0));
        }
        else if (period == "week")
        {
            query = _context.WorkflowInstances
                .Where(i => i.CreatedAt >= startDate && i.CreatedAt <= endDate)
                .GroupBy(i => i.CreatedAt.AddDays(-(int)i.CreatedAt.DayOfWeek));
        }
        else // day
        {
            query = _context.WorkflowInstances
                .Where(i => i.CreatedAt >= startDate && i.CreatedAt <= endDate)
                .GroupBy(i => i.CreatedAt.Date);
        }

        var trends = await query
            .Select(g => new
            {
                Period = g.Key,
                TotalInstances = g.Count(),
                CompletedInstances = g.Count(i => i.Status == WorkflowInstanceStatus.Completed),
                FailedInstances = g.Count(i => i.Status == WorkflowInstanceStatus.Failed)
            })
            .OrderBy(t => t.Period)
            .ToListAsync();

        return trends.Select(t => new WorkflowStats.WorkflowExecutionTrendDto
        {
            Period = t.Period.ToString("yyyy-MM-dd" + (period == "hour" ? " HH" : "")),
            TotalInstances = t.TotalInstances,
            CompletedInstances = t.CompletedInstances,
            FailedInstances = t.FailedInstances,
            SuccessRate = t.TotalInstances > 0 ? Math.Round((double)t.CompletedInstances / t.TotalInstances * 100, 2) : 0
        }).ToList();
    }

    /// <summary>
    /// 获取工作流健康状态
    /// </summary>
    public async Task<WorkflowStats.WorkflowHealthStatusDto> GetWorkflowHealthStatusAsync()
    {
        var now = DateTime.UtcNow;
        var oneHourAgo = now.AddHours(-1);
        var oneDayAgo = now.AddDays(-1);

        // 最近1小时的统计
        var recentInstances = await _context.WorkflowInstances
            .Where(i => i.CreatedAt >= oneHourAgo)
            .ToListAsync();

        var recentCompleted = recentInstances.Count(i => i.Status == WorkflowInstanceStatus.Completed);
        var recentFailed = recentInstances.Count(i => i.Status == WorkflowInstanceStatus.Failed);

        // 最近24小时的统计
        var dailyInstances = await _context.WorkflowInstances
            .Where(i => i.CreatedAt >= oneDayAgo)
            .ToListAsync();

        var dailyCompleted = dailyInstances.Count(i => i.Status == WorkflowInstanceStatus.Completed);
        var dailyFailed = dailyInstances.Count(i => i.Status == WorkflowInstanceStatus.Failed);

        // 计算健康分数 (0-100)
        var totalRecent = recentInstances.Count;
        var totalDaily = dailyInstances.Count;

        var recentSuccessRate = totalRecent > 0 ? (double)recentCompleted / totalRecent : 1.0;
        var dailySuccessRate = totalDaily > 0 ? (double)dailyCompleted / totalDaily : 1.0;

        // 综合评分：成功率80% + 执行量稳定性20%
        var healthScore = (recentSuccessRate * 0.8 + dailySuccessRate * 0.8) * 100;

        // 确定健康状态
        var status = healthScore >= 95 ? "Excellent" :
                    healthScore >= 85 ? "Good" :
                    healthScore >= 70 ? "Fair" : "Poor";

        return new WorkflowStats.WorkflowHealthStatusDto
        {
            HealthScore = Math.Round(healthScore, 1),
            Status = status,
            RecentStats = new WorkflowStats.WorkflowHealthStatsDto
            {
                TimeRange = "Last Hour",
                TotalInstances = totalRecent,
                CompletedInstances = recentCompleted,
                FailedInstances = recentFailed,
                SuccessRate = Math.Round(recentSuccessRate * 100, 2)
            },
            DailyStats = new WorkflowStats.WorkflowHealthStatsDto
            {
                TimeRange = "Last 24 Hours",
                TotalInstances = totalDaily,
                CompletedInstances = dailyCompleted,
                FailedInstances = dailyFailed,
                SuccessRate = Math.Round(dailySuccessRate * 100, 2)
            }
        };
    }

    /// <summary>
    /// 获取工作流瓶颈分析
    /// </summary>
    public async Task<List<WorkflowStats.WorkflowBottleneckDto>> GetWorkflowBottlenecksAsync(int topN = 10)
    {
        var bottlenecks = await _context.WorkflowExecutionHistories
            .Where(h => h.Status == WorkflowNodeStatus.Failed ||
                       h.ExecutionTimeMs > 30000) // 执行时间超过30秒
            .GroupBy(h => new { h.NodeId, h.NodeType })
            .Select(g => new
            {
                NodeId = g.Key.NodeId,
                NodeType = g.Key.NodeType,
                FailureCount = g.Count(h => h.Status == WorkflowNodeStatus.Failed),
                SlowExecutionCount = g.Count(h => h.ExecutionTimeMs > 30000),
                AverageExecutionTime = (decimal)g.Average(h => (decimal)h.ExecutionTimeMs),
                MaxExecutionTime = (long)g.Max(h => h.ExecutionTimeMs)
            })
            .OrderByDescending(b => b.FailureCount + b.SlowExecutionCount)
            .Take(topN)
            .ToListAsync();

        return bottlenecks.Select(b => new WorkflowStats.WorkflowBottleneckDto
        {
            NodeId = b.NodeId,
            NodeType = b.NodeType,
            FailureCount = b.FailureCount,
            SlowExecutionCount = b.SlowExecutionCount,
            AverageExecutionTimeMs = (long)b.AverageExecutionTime,
            MaxExecutionTimeMs = (long)b.MaxExecutionTime,
            RiskLevel = b.FailureCount > 10 || b.SlowExecutionCount > 20 ? "High" :
                       b.FailureCount > 5 || b.SlowExecutionCount > 10 ? "Medium" : "Low"
        }).ToList();
    }
}