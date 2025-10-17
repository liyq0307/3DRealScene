using RealScene3D.Domain.Entities;

namespace RealScene3D.Domain.Interfaces;

/// <summary>
/// 工作流引擎接口
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// 启动工作流实例
    /// </summary>
    Task<Guid> StartWorkflowAsync(Guid workflowId, string inputParameters, Guid userId);
    
    /// <summary>
    /// 暂停工作流实例
    /// </summary>
    Task<bool> SuspendWorkflowAsync(Guid instanceId, Guid userId);
    
    /// <summary>
    /// 恢复工作流实例
    /// </summary>
    Task<bool> ResumeWorkflowAsync(Guid instanceId, Guid userId);
    
    /// <summary>
    /// 取消工作流实例
    /// </summary>
    Task<bool> CancelWorkflowAsync(Guid instanceId, Guid userId);
    
    /// <summary>
    /// 获取工作流实例状态
    /// </summary>
    Task<WorkflowInstance?> GetWorkflowInstanceAsync(Guid instanceId);
    
    /// <summary>
    /// 获取工作流执行历史
    /// </summary>
    Task<IEnumerable<WorkflowExecutionHistory>> GetExecutionHistoryAsync(Guid instanceId);
    
    /// <summary>
    /// 获取用户的工作流实例列表
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetUserWorkflowInstancesAsync(Guid userId, int page = 1, int pageSize = 20);
}

/// <summary>
/// 工作流节点执行器接口
/// </summary>
public interface IWorkflowNodeExecutor
{
    /// <summary>
    /// 执行工作流节点
    /// </summary>
    Task<WorkflowNodeResult> ExecuteNodeAsync(WorkflowNodeContext context);
    
    /// <summary>
    /// 获取支持的节点类型
    /// </summary>
    IEnumerable<string> GetSupportedNodeTypes();
}

/// <summary>
/// 工作流节点上下文
/// </summary>
public class WorkflowNodeContext
{
    public Guid InstanceId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string InputData { get; set; } = "{}";
    public Dictionary<string, object> Variables { get; set; } = new();
}

/// <summary>
/// 工作流节点执行结果
/// </summary>
public class WorkflowNodeResult
{
    public WorkflowNodeStatus Status { get; set; }
    public string OutputData { get; set; } = "{}";
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> UpdatedVariables { get; set; } = new();
    public List<string> NextNodes { get; set; } = new();
}