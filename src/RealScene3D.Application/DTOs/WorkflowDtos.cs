using RealScene3D.Domain.Enums;

namespace RealScene3D.Application.DTOs;

/// <summary>
/// 工作流管理数据传输对象集合
/// 包含工作流定义、实例执行、历史记录等相关DTO
/// 用于处理工作流业务的数据传输和工作流引擎交互
/// </summary>
public class WorkflowDtos
{
    /// <summary>
    /// 工作流创建请求DTO
    /// 用于接收客户端创建新工作流定义的请求数据
    /// </summary>
    public class CreateWorkflowRequest
    {
        /// <summary>
        /// 工作流名称，必填项
        /// 用于标识和区分不同工作流，支持中文和英文
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 工作流描述，可选项
        /// 用于详细说明工作流的功能和用途
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 工作流定义JSON，必填项
        /// 包含节点、连接、参数等完整的工作流结构定义
        /// </summary>
        public string Definition { get; set; } = string.Empty;

        /// <summary>
        /// 工作流版本号，默认1.0.0
        /// 用于版本管理和兼容性控制，建议遵循语义化版本格式
        /// </summary>
        public string Version { get; set; } = "1.0.0";
    }

    /// <summary>
    /// 工作流更新请求DTO
    /// 用于接收客户端更新现有工作流定义的请求数据
    /// </summary>
    public class UpdateWorkflowRequest
    {
        /// <summary>
        /// 工作流名称，可选项
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 工作流描述，可选项
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 工作流定义JSON，可选项
        /// 更新后的工作流结构定义
        /// </summary>
        public string Definition { get; set; } = string.Empty;

        /// <summary>
        /// 工作流版本号，可选项
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// 工作流是否启用，默认true
        /// 禁用的工作流无法启动新实例，但已运行实例不受影响
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// 工作流响应DTO
    /// 用于向前端返回工作流定义的完整信息
    /// </summary>
    public class WorkflowDto
    {
        /// <summary>
        /// 工作流唯一标识符
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 工作流名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 工作流描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 工作流定义JSON
        /// </summary>
        public string Definition { get; set; } = string.Empty;

        /// <summary>
        /// 工作流版本号
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// 工作流是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 工作流创建者用户ID
        /// </summary>
        public Guid CreatedBy { get; set; }

        /// <summary>
        /// 工作流创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 工作流最后更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// 启动工作流实例请求DTO
    /// 用于接收客户端启动工作流实例的请求数据
    /// </summary>
    public class StartWorkflowInstanceRequest
    {
        /// <summary>
        /// 工作流实例名称，可选项
        /// 用于标识具体的工作流执行实例
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 输入参数JSON，默认空对象
        /// 传递给工作流实例的初始参数和工作数据
        /// </summary>
        public string InputParameters { get; set; } = "{}";
    }

    /// <summary>
    /// 工作流实例响应DTO
    /// 用于向前端返回工作流实例的执行状态和结果
    /// </summary>
    public class WorkflowInstanceDto
    {
        /// <summary>
        /// 工作流实例唯一标识符
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 关联的工作流定义ID
        /// </summary>
        public Guid WorkflowId { get; set; }

        /// <summary>
        /// 工作流实例名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 工作流实例当前状态
        /// 如：等待启动、运行中、已完成、失败、已取消等
        /// </summary>
        public WorkflowInstanceStatus Status { get; set; }

        /// <summary>
        /// 输入参数JSON
        /// </summary>
        public string InputParameters { get; set; } = "{}";

        /// <summary>
        /// 执行上下文JSON
        /// 包含工作流实例运行时的状态和中间数据
        /// </summary>
        public string Context { get; set; } = "{}";

        /// <summary>
        /// 执行结果，可选项
        /// 工作流成功完成后的输出结果
        /// </summary>
        public string? Result { get; set; }

        /// <summary>
        /// 错误信息，可选项
        /// 工作流执行失败时的错误详情
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 实例创建者用户ID
        /// </summary>
        public Guid CreatedBy { get; set; }

        /// <summary>
        /// 实例创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 实例开始执行时间，可选项
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// 实例完成时间，可选项
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// 关联的工作流定义，可选项
        /// </summary>
        public WorkflowDto? Workflow { get; set; }
    }

    /// <summary>
    /// 工作流执行历史响应DTO
    /// 用于记录和返回工作流节点的执行历史
    /// </summary>
    public class WorkflowExecutionHistoryDto
    {
        /// <summary>
        /// 执行历史记录唯一标识符
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 关联的工作流实例ID
        /// </summary>
        public Guid WorkflowInstanceId { get; set; }

        /// <summary>
        /// 执行节点ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>
        /// 节点类型，如：开始节点、处理节点、条件节点、结束节点等
        /// </summary>
        public string NodeType { get; set; } = string.Empty;

        /// <summary>
        /// 节点执行状态
        /// </summary>
        public WorkflowNodeStatus Status { get; set; }

        /// <summary>
        /// 节点输入数据JSON
        /// </summary>
        public string InputData { get; set; } = "{}";

        /// <summary>
        /// 节点输出数据JSON
        /// </summary>
        public string OutputData { get; set; } = "{}";

        /// <summary>
        /// 节点执行耗时（毫秒）
        /// </summary>
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// 错误信息，可选项
        /// 节点执行失败时的错误详情
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 节点执行时间
        /// </summary>
        public DateTime ExecutedAt { get; set; }
    }
}