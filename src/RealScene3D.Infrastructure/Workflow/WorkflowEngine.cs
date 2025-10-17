using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using System.Text.Json;

namespace RealScene3D.Infrastructure.Workflow;

/// <summary>
/// 工作流引擎实现
/// </summary>
public class WorkflowEngine : IWorkflowEngine
{
    private readonly IWorkflowNodeExecutor[] _nodeExecutors;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        IEnumerable<IWorkflowNodeExecutor> nodeExecutors,
        ILogger<WorkflowEngine> logger)
    {
        _nodeExecutors = nodeExecutors.ToArray();
        _logger = logger;
    }

    public async Task<Guid> StartWorkflowAsync(Guid workflowId, string inputParameters, Guid userId)
    {
        _logger.LogInformation("启动工作流：{WorkflowId}, 用户：{UserId}", workflowId, userId);

        // 这里应该从仓储获取工作流定义并创建实例
        // 目前返回一个示例ID
        await Task.Yield(); // 确保异步执行
        return Guid.NewGuid();
    }

    public Task<bool> SuspendWorkflowAsync(Guid instanceId, Guid userId)
    {
        _logger.LogInformation("暂停工作流实例：{InstanceId}, 用户：{UserId}", instanceId, userId);

        // 这里应该实现暂停逻辑
        return Task.FromResult(true);
    }

    public Task<bool> ResumeWorkflowAsync(Guid instanceId, Guid userId)
    {
        _logger.LogInformation("恢复工作流实例：{InstanceId}, 用户：{UserId}", instanceId, userId);

        // 这里应该实现恢复逻辑
        return Task.FromResult(true);
    }

    public Task<bool> CancelWorkflowAsync(Guid instanceId, Guid userId)
    {
        _logger.LogInformation("取消工作流实例：{InstanceId}, 用户：{UserId}", instanceId, userId);

        // 这里应该实现取消逻辑
        return Task.FromResult(true);
    }

    public Task<WorkflowInstance?> GetWorkflowInstanceAsync(Guid instanceId)
    {
        // 这里应该从仓储获取工作流实例
        return Task.FromResult<WorkflowInstance?>(null);
    }

    public Task<IEnumerable<WorkflowExecutionHistory>> GetExecutionHistoryAsync(Guid instanceId)
    {
        // 这里应该从仓储获取执行历史
        return Task.FromResult(Enumerable.Empty<WorkflowExecutionHistory>());
    }

    public Task<IEnumerable<WorkflowInstance>> GetUserWorkflowInstancesAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        // 这里应该从仓储获取用户的工作流实例列表
        return Task.FromResult(Enumerable.Empty<WorkflowInstance>());
    }
}

/// <summary>
/// 基础工作流节点执行器
/// </summary>
public abstract class BaseWorkflowNodeExecutor : IWorkflowNodeExecutor
{
    private readonly ILogger _logger;

    protected BaseWorkflowNodeExecutor(ILogger logger)
    {
        _logger = logger;
    }

    public abstract Task<WorkflowNodeResult> ExecuteNodeAsync(WorkflowNodeContext context);

    public abstract IEnumerable<string> GetSupportedNodeTypes();

    protected virtual bool CanExecute(string nodeType)
    {
        return GetSupportedNodeTypes().Contains(nodeType);
    }

    protected void LogInformation(string message, params object[] args)
    {
        _logger.LogInformation(message, args);
    }

    protected void LogError(Exception ex, string message, params object[] args)
    {
        _logger.LogError(ex, message, args);
    }
}

/// <summary>
/// 延迟节点执行器（示例）
/// </summary>
public class DelayNodeExecutor : BaseWorkflowNodeExecutor
{
    public DelayNodeExecutor(ILogger<DelayNodeExecutor> logger) : base(logger)
    {
    }

    public override async Task<WorkflowNodeResult> ExecuteNodeAsync(WorkflowNodeContext context)
    {
        try
        {
            LogInformation("执行延迟节点：{NodeId}", context.NodeId);

            // 从输入数据中获取延迟时间（毫秒）
            var inputData = JsonDocument.Parse(context.InputData ?? "{}");
            var delayMsValue = inputData.RootElement.GetProperty("delayMs").GetInt32();
            var delayMs = delayMsValue;

            await Task.Delay(delayMs);

            return new WorkflowNodeResult
            {
                Status = WorkflowNodeStatus.Completed,
                OutputData = JsonSerializer.Serialize(new { delayedMs = delayMs }),
                UpdatedVariables = new Dictionary<string, object>
                {
                    ["lastDelayMs"] = delayMs,
                    ["delayCompletedAt"] = DateTime.UtcNow
                }
            };
        }
        catch (Exception ex)
        {
            LogError(ex, "延迟节点执行失败：{NodeId}", context.NodeId);

            return new WorkflowNodeResult
            {
                Status = WorkflowNodeStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public override IEnumerable<string> GetSupportedNodeTypes()
    {
        return new[] { "delay", "wait" };
    }
}

/// <summary>
/// 条件判断节点执行器（示例）
/// </summary>
public class ConditionNodeExecutor : BaseWorkflowNodeExecutor
{
    public ConditionNodeExecutor(ILogger<ConditionNodeExecutor> logger) : base(logger)
    {
    }

    public override Task<WorkflowNodeResult> ExecuteNodeAsync(WorkflowNodeContext context)
    {
        try
        {
            LogInformation("执行条件判断节点：{NodeId}", context.NodeId);

            // 从输入数据中获取条件表达式和变量
            var inputData = JsonDocument.Parse(context.InputData ?? "{}");
            var conditionValue = inputData.RootElement.GetProperty("condition").GetString();
            var condition = conditionValue ?? "";
            var variableNameValue = inputData.RootElement.GetProperty("variable").GetString();
            var variableName = variableNameValue ?? "";

            // 简单条件判断（这里应该实现更复杂的表达式引擎）
            var variableValue = context.Variables?.GetValueOrDefault(variableName) ?? "";
            var conditionMet = EvaluateCondition(variableValue, condition ?? "");

            var nextNodes = new List<string>();
            if (conditionMet)
            {
                var trueNextValue = inputData.RootElement.GetProperty("trueNext").GetString();
                nextNodes.Add(trueNextValue ?? "");
            }
            else
            {
                var falseNextValue = inputData.RootElement.GetProperty("falseNext").GetString();
                nextNodes.Add(falseNextValue ?? "");
            }

            return Task.FromResult(new WorkflowNodeResult
            {
                Status = WorkflowNodeStatus.Completed,
                OutputData = JsonSerializer.Serialize(new
                {
                    conditionMet,
                    variableValue,
                    condition
                }),
                UpdatedVariables = new Dictionary<string, object>
                {
                    ["conditionResult"] = conditionMet,
                    ["lastCondition"] = condition ?? string.Empty
                },
                NextNodes = nextNodes
            });
        }
        catch (Exception ex)
        {
            LogError(ex, "条件判断节点执行失败：{NodeId}", context.NodeId);

            return Task.FromResult(new WorkflowNodeResult
            {
                Status = WorkflowNodeStatus.Failed,
                ErrorMessage = ex.Message
            });
        }
    }

    public override IEnumerable<string> GetSupportedNodeTypes()
    {
        return new[] { "condition", "if" };
    }

    private bool EvaluateCondition(object value, string condition)
    {
        // 简单的条件判断实现
        if (value == null) return false;

        return condition switch
        {
            "isNull" => value == null,
            "isNotNull" => value != null,
            "equals" => value.ToString() == condition,
            _ => false
        };
    }
}