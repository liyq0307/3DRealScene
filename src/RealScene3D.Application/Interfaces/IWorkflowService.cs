using RealScene3D.Application.DTOs;

namespace RealScene3D.Application.Interfaces;

/// <summary>
/// 工作流应用服务接口
/// 提供工作流定义管理、实例执行、状态控制等完整功能
/// 支持复杂业务流程的自动化执行和监控
/// </summary>
public interface IWorkflowService
{
    /// <summary>
    /// 创建工作流定义
    /// </summary>
    /// <param name="request">工作流创建请求，包含定义和配置信息</param>
    /// <param name="userId">创建者用户ID</param>
    /// <returns>创建成功的工作流定义信息</returns>
    Task<WorkflowDtos.WorkflowDto> CreateWorkflowAsync(WorkflowDtos.CreateWorkflowRequest request, Guid userId);

    /// <summary>
    /// 更新工作流定义
    /// </summary>
    /// <param name="workflowId">工作流定义ID</param>
    /// <param name="request">更新请求，包含要修改的内容</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>更新后的工作流定义，如果不存在则返回null</returns>
    Task<WorkflowDtos.WorkflowDto?> UpdateWorkflowAsync(Guid workflowId, WorkflowDtos.UpdateWorkflowRequest request, Guid userId);

    /// <summary>
    /// 删除工作流定义
    /// </summary>
    /// <param name="workflowId">工作流定义ID</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>删除是否成功</returns>
    Task<bool> DeleteWorkflowAsync(Guid workflowId, Guid userId);

    /// <summary>
    /// 获取工作流定义详情
    /// </summary>
    /// <param name="workflowId">工作流定义ID</param>
    /// <returns>工作流定义详情，如果不存在则返回null</returns>
    Task<WorkflowDtos.WorkflowDto?> GetWorkflowByIdAsync(Guid workflowId);

    /// <summary>
    /// 获取指定用户的工作流定义列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户创建的工作流定义列表</returns>
    Task<IEnumerable<WorkflowDtos.WorkflowDto>> GetUserWorkflowsAsync(Guid userId);

    /// <summary>
    /// 获取所有启用的工作流定义列表
    /// </summary>
    /// <returns>系统中所有启用的工作流定义</returns>
    Task<IEnumerable<WorkflowDtos.WorkflowDto>> GetAllWorkflowsAsync();

    /// <summary>
    /// 启动工作流实例
    /// </summary>
    /// <param name="workflowId">工作流定义ID</param>
    /// <param name="request">启动请求，包含实例名称和输入参数</param>
    /// <param name="userId">启动用户ID</param>
    /// <returns>启动成功的工作流实例信息</returns>
    Task<WorkflowDtos.WorkflowInstanceDto> StartWorkflowInstanceAsync(Guid workflowId, WorkflowDtos.StartWorkflowInstanceRequest request, Guid userId);

    /// <summary>
    /// 获取工作流实例详情
    /// </summary>
    /// <param name="instanceId">工作流实例ID</param>
    /// <returns>工作流实例详情，如果不存在则返回null</returns>
    Task<WorkflowDtos.WorkflowInstanceDto?> GetWorkflowInstanceAsync(Guid instanceId);

    /// <summary>
    /// 获取工作流实例列表
    /// </summary>
    /// <param name="workflowId">可选，过滤特定工作流定义的实例</param>
    /// <param name="userId">可选，过滤特定用户的实例</param>
    /// <param name="page">页码，从1开始</param>
    /// <param name="pageSize">每页大小，默认20</param>
    /// <returns>工作流实例列表</returns>
    Task<IEnumerable<WorkflowDtos.WorkflowInstanceDto>> GetWorkflowInstancesAsync(Guid? workflowId = null, Guid? userId = null, int page = 1, int pageSize = 20);

    /// <summary>
    /// 暂停工作流实例
    /// </summary>
    /// <param name="instanceId">工作流实例ID</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>暂停是否成功</returns>
    Task<bool> SuspendWorkflowInstanceAsync(Guid instanceId, Guid userId);

    /// <summary>
    /// 恢复工作流实例
    /// </summary>
    /// <param name="instanceId">工作流实例ID</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>恢复是否成功</returns>
    Task<bool> ResumeWorkflowInstanceAsync(Guid instanceId, Guid userId);

    /// <summary>
    /// 取消工作流实例
    /// </summary>
    /// <param name="instanceId">工作流实例ID</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>取消是否成功</returns>
    Task<bool> CancelWorkflowInstanceAsync(Guid instanceId, Guid userId);

    /// <summary>
    /// 获取工作流执行历史记录
    /// </summary>
    /// <param name="instanceId">工作流实例ID</param>
    /// <returns>该实例的所有执行历史记录</returns>
    Task<IEnumerable<WorkflowDtos.WorkflowExecutionHistoryDto>> GetWorkflowExecutionHistoryAsync(Guid instanceId);
}

/// <summary>
/// 工作流执行器服务接口
/// 负责工作流实例的实际执行和节点调度
/// 支持异步执行和事件驱动的节点处理
/// </summary>
public interface IWorkflowExecutorService
{
    /// <summary>
    /// 执行工作流实例
    /// </summary>
    /// <param name="instanceId">工作流实例ID</param>
    /// <returns>执行任务</returns>
    Task ExecuteWorkflowAsync(Guid instanceId);

    /// <summary>
    /// 处理节点执行完成事件
    /// </summary>
    /// <param name="instanceId">工作流实例ID</param>
    /// <param name="nodeId">完成执行的节点ID</param>
    /// <param name="result">节点执行结果</param>
    /// <returns>处理任务</returns>
    Task HandleNodeCompletedAsync(Guid instanceId, string nodeId, Domain.Interfaces.WorkflowNodeResult result);
}