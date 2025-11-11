using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Interfaces;
using System.Collections.Concurrent;

namespace RealScene3D.Application.Services.Workflows;

/// <summary>
/// 工作流性能优化器
/// 提供工作流执行性能优化功能，包括缓存、并发控制、资源管理等
/// </summary>
public class WorkflowPerformanceOptimizer
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<WorkflowPerformanceOptimizer> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _nodeSemaphores;
    private readonly ConcurrentDictionary<string, WorkflowPerformanceMetrics> _performanceMetrics;

    public WorkflowPerformanceOptimizer(
        IMemoryCache cache,
        ILogger<WorkflowPerformanceOptimizer> logger)
    {
        _cache = cache;
        _logger = logger;
        _nodeSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
        _performanceMetrics = new ConcurrentDictionary<string, WorkflowPerformanceMetrics>();
    }

    /// <summary>
    /// 获取工作流执行缓存
    /// </summary>
    public Task<WorkflowNodeResult?> GetCachedResultAsync(string cacheKey, string nodeType)
    {
        if (_cache.TryGetValue(cacheKey, out WorkflowNodeResult? cachedResult))
        {
            _logger.LogInformation("使用缓存结果：{CacheKey}, 节点类型：{NodeType}", cacheKey, nodeType);
            return Task.FromResult(cachedResult);
        }
        return Task.FromResult<WorkflowNodeResult?>(null);
    }

    /// <summary>
    /// 设置工作流执行缓存
    /// </summary>
    public Task SetCachedResultAsync(string cacheKey, WorkflowNodeResult result, TimeSpan expiration)
    {
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(expiration);

        _cache.Set(cacheKey, result, cacheEntryOptions);
        _logger.LogInformation("缓存工作流结果：{CacheKey}", cacheKey);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 生成工作流节点缓存键
    /// </summary>
    public string GenerateNodeCacheKey(WorkflowNodeContext context)
    {
        var inputHash = GetStableHash(context.InputData ?? "{}");
        return $"workflow_node_{context.NodeType}_{context.NodeId}_{inputHash}";
    }

    /// <summary>
    /// 执行节点时控制并发
    /// </summary>
    public async Task<T> ExecuteWithConcurrencyControlAsync<T>(
        string nodeId,
        Func<Task<T>> executionFunc,
        int maxConcurrency = 3)
    {
        var semaphore = _nodeSemaphores.GetOrAdd(nodeId, _ => new SemaphoreSlim(maxConcurrency));

        await semaphore.WaitAsync();
        try
        {
            return await executionFunc();
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// 记录节点执行性能指标
    /// </summary>
    public void RecordNodeExecutionMetrics(string nodeId, string nodeType, long executionTimeMs, bool success)
    {
        var metrics = _performanceMetrics.GetOrAdd($"{nodeId}_{nodeType}",
            _ => new WorkflowPerformanceMetrics());

        metrics.TotalExecutions++;
        metrics.TotalExecutionTimeMs += executionTimeMs;

        if (success)
        {
            metrics.SuccessfulExecutions++;
        }
        else
        {
            metrics.FailedExecutions++;
        }

        if (executionTimeMs > metrics.MaxExecutionTimeMs)
        {
            metrics.MaxExecutionTimeMs = executionTimeMs;
        }

        if (executionTimeMs < metrics.MinExecutionTimeMs || metrics.MinExecutionTimeMs == 0)
        {
            metrics.MinExecutionTimeMs = executionTimeMs;
        }

        metrics.LastExecutionTime = DateTime.UtcNow;
    }

    /// <summary>
    /// 获取节点性能指标
    /// </summary>
    public WorkflowPerformanceMetrics? GetNodePerformanceMetrics(string nodeId, string nodeType)
    {
        _performanceMetrics.TryGetValue($"{nodeId}_{nodeType}", out var metrics);
        return metrics;
    }

    /// <summary>
    /// 获取性能最差的节点
    /// </summary>
    public List<KeyValuePair<string, WorkflowPerformanceMetrics>> GetSlowestNodes(int topN = 10)
    {
        return _performanceMetrics
            .Where(m => m.Value.TotalExecutions > 0)
            .OrderByDescending(m => m.Value.AverageExecutionTimeMs)
            .Take(topN)
            .ToList();
    }

    /// <summary>
    /// 清理过期缓存
    /// </summary>
    public void CleanupExpiredCache()
    {
        // MemoryCache 会自动清理过期项，这里可以添加额外的清理逻辑
        var expiredKeys = _performanceMetrics
            .Where(m => DateTime.UtcNow - m.Value.LastExecutionTime > TimeSpan.FromDays(7))
            .Select(m => m.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _performanceMetrics.TryRemove(key, out _);
        }

        _logger.LogInformation("清理了 {Count} 个过期的性能指标记录", expiredKeys.Count);
    }

    /// <summary>
    /// 优化工作流执行顺序
    /// </summary>
    public List<string> OptimizeExecutionOrder(
        Dictionary<string, ExecutionNode> executionGraph,
        Dictionary<string, WorkflowPerformanceMetrics> nodeMetrics)
    {
        // 基于性能指标重新排序执行顺序
        // 优先执行快速且可靠的节点
        var nodes = executionGraph.Keys.ToList();

        return nodes.OrderBy(nodeId =>
        {
            var metrics = nodeMetrics.GetValueOrDefault(nodeId, new WorkflowPerformanceMetrics());
            if (metrics.TotalExecutions == 0)
                return 0; // 未执行过的节点优先级为0

            // 计算综合评分：成功率高、执行时间短的节点优先
            var successRate = metrics.TotalExecutions > 0 ?
                (double)metrics.SuccessfulExecutions / metrics.TotalExecutions : 0;
            var avgTime = metrics.AverageExecutionTimeMs;

            // 评分公式：成功率 * 100 - 平均执行时间（毫秒）/ 1000
            return successRate * 100 - avgTime / 1000;
        }).ToList();
    }

    /// <summary>
    /// 预热常用节点
    /// </summary>
    public Task WarmupNodesAsync(IEnumerable<string> nodeTypes)
    {
        foreach (var nodeType in nodeTypes)
        {
            // 这里可以添加节点预热逻辑，如预加载依赖、建立连接等
            _logger.LogInformation("预热节点类型：{NodeType}", nodeType);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 批量执行优化
    /// </summary>
    public async Task<List<WorkflowNodeResult>> ExecuteBatchAsync(
        List<WorkflowNodeContext> contexts,
        Func<WorkflowNodeContext, Task<WorkflowNodeResult>> executor,
        int batchSize = 10)
    {
        var results = new List<WorkflowNodeResult>();
        var batches = contexts.Select((context, index) => new { context, index })
                             .GroupBy(x => x.index / batchSize)
                             .Select(g => g.Select(x => x.context).ToList())
                             .ToList();

        foreach (var batch in batches)
        {
            var batchTasks = batch.Select(context => executor(context)).ToList();
            var batchResults = await Task.WhenAll(batchTasks);
            results.AddRange(batchResults);

            // 小延迟以避免资源竞争
            await Task.Delay(10);
        }

        return results;
    }

    /// <summary>
    /// 生成稳定的哈希值
    /// </summary>
    private string GetStableHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash).Substring(0, 16); // 取前16个字符
    }

    /// <summary>
    /// 工作流性能指标
    /// </summary>
    public class WorkflowPerformanceMetrics
    {
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
        /// 总执行时间（毫秒）
        /// </summary>
        public long TotalExecutionTimeMs { get; set; }

        /// <summary>
        /// 最大执行时间（毫秒）
        /// </summary>
        public long MaxExecutionTimeMs { get; set; }

        /// <summary>
        /// 最小执行时间（毫秒）
        /// </summary>
        public long MinExecutionTimeMs { get; set; }

        /// <summary>
        /// 最后执行时间
        /// </summary>
        public DateTime LastExecutionTime { get; set; }

        /// <summary>
        /// 平均执行时间（毫秒）
        /// </summary>
        public double AverageExecutionTimeMs =>
            TotalExecutions > 0 ? (double)TotalExecutionTimeMs / TotalExecutions : 0;

        /// <summary>
        /// 成功率（百分比）
        /// </summary>
        public double SuccessRate =>
            TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions * 100 : 0;
    }

    /// <summary>
    /// 执行节点（内部类，简化版）
    /// </summary>
    public class ExecutionNode
    {
        public WorkflowNodeModel Node { get; set; } = new();
        public List<string> PreviousNodes { get; set; } = new();
        public List<string> NextNodes { get; set; } = new();
    }

    /// <summary>
    /// 工作流节点模型（内部类，简化版）
    /// </summary>
    public class WorkflowNodeModel
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}