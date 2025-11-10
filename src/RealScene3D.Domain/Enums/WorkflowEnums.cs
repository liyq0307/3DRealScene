namespace RealScene3D.Domain.Enums;

/// <summary>
/// 工作流实例状态枚举
/// </summary>
public enum WorkflowInstanceStatus
{
    Created = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
    Suspended = 5
}

/// <summary>
/// 工作流节点状态枚举
/// </summary>
public enum WorkflowNodeStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Skipped = 4
}
