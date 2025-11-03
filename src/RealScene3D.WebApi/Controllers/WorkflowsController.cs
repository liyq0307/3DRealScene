using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;

namespace RealScene3D.WebApi.Controllers;

/// <summary>
/// 工作流管理API控制器
/// 提供工作流定义和实例管理的RESTful API接口
/// 支持工作流的完整生命周期管理：创建、更新、删除、执行、监控
/// 采用ASP.NET Core MVC模式,遵循RESTful设计规范
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkflowsController : ControllerBase
{
    private readonly IWorkflowService _workflowService;
    private readonly ILogger<WorkflowsController> _logger;

    /// <summary>
    /// 构造函数 - 依赖注入工作流服务和日志记录器
    /// </summary>
    /// <param name="workflowService">工作流应用服务接口</param>
    /// <param name="logger">日志记录器，用于记录操作日志和错误信息</param>
    public WorkflowsController(IWorkflowService workflowService, ILogger<WorkflowsController> logger)
    {
        _workflowService = workflowService;
        _logger = logger;
    }

    /// <summary>
    /// 创建工作流定义
    /// </summary>
    [HttpPost]
    /// <summary>
    /// 创建工作流定义
    /// </summary>
    /// <param name="request">工作流创建请求，包含定义和配置信息</param>
    /// <param name="userId">创建者用户ID</param>
    /// <returns>创建成功的工作流定义信息</returns>
    public async Task<ActionResult<WorkflowDtos.WorkflowDto>> CreateWorkflow([FromBody] WorkflowDtos.CreateWorkflowRequest request, [FromQuery] Guid userId)
    {
        try
        {
            var workflow = await _workflowService.CreateWorkflowAsync(request, userId);
            return CreatedAtAction(nameof(GetWorkflow), new { id = workflow.Id }, workflow);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "创建工作流失败");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建工作流时发生错误");
            return StatusCode(500, new { message = "创建工作流时发生错误" });
        }
    }

    /// <summary>
    /// 更新工作流定义
    /// </summary>
    [HttpPut("{id}")]
    /// <summary>
    /// 更新工作流定义
    /// </summary>
    /// <param name="id">工作流定义ID</param>
    /// <param name="request">更新请求，包含要修改的内容</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>更新后的工作流定义，如果不存在则返回404</returns>
    public async Task<ActionResult<WorkflowDtos.WorkflowDto>> UpdateWorkflow(Guid id, [FromBody] WorkflowDtos.UpdateWorkflowRequest request, [FromQuery] Guid userId)
    {
        try
        {
            var workflow = await _workflowService.UpdateWorkflowAsync(id, request, userId);
            if (workflow == null)
            {
                return NotFound(new { message = "工作流未找到或无权限" });
            }
            return Ok(workflow);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "更新工作流失败：工作流ID {WorkflowId}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新工作流时发生错误：工作流ID {WorkflowId}", id);
            return StatusCode(500, new { message = "更新工作流时发生错误" });
        }
    }

    /// <summary>
    /// 根据ID获取工作流定义详情
    /// </summary>
    /// <param name="id">工作流定义ID</param>
    /// <returns>工作流定义详情，如果不存在则返回404</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowDtos.WorkflowDto>> GetWorkflow(Guid id)
    {
        try
        {
            var workflow = await _workflowService.GetWorkflowByIdAsync(id);
            if (workflow == null)
            {
                return NotFound(new { message = "工作流未找到" });
            }
            return Ok(workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取工作流时发生错误：工作流ID {WorkflowId}", id);
            return StatusCode(500, new { message = "获取工作流时发生错误" });
        }
    }

    /// <summary>
    /// 获取指定用户的工作流定义列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户创建的工作流定义列表，按创建时间倒序排列</returns>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<WorkflowDtos.WorkflowDto>>> GetUserWorkflows(Guid userId)
    {
        try
        {
            var workflows = await _workflowService.GetUserWorkflowsAsync(userId);
            return Ok(workflows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户工作流列表时发生错误：用户ID {UserId}", userId);
            return StatusCode(500, new { message = "获取工作流列表时发生错误" });
        }
    }

    /// <summary>
    /// 获取所有启用的工作流定义列表
    /// </summary>
    /// <returns>系统中所有启用的工作流定义</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkflowDtos.WorkflowDto>>> GetAllWorkflows()
    {
        try
        {
            var workflows = await _workflowService.GetAllWorkflowsAsync();
            return Ok(workflows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有工作流时发生错误");
            return StatusCode(500, new { message = "获取所有工作流时发生错误" });
        }
    }

    /// <summary>
    /// 删除工作流定义
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteWorkflow(Guid id, [FromQuery] Guid userId)
    {
        try
        {
            var result = await _workflowService.DeleteWorkflowAsync(id, userId);
            if (!result)
            {
                return NotFound(new { message = "工作流未找到或无权限" });
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除工作流时发生错误：工作流ID {WorkflowId}", id);
            return StatusCode(500, new { message = "删除工作流时发生错误" });
        }
    }

    /// <summary>
    /// 启动工作流实例
    /// </summary>
    [HttpPost("{workflowId}/instances")]
    /// <summary>
    /// 启动工作流实例
    /// </summary>
    /// <param name="workflowId">工作流定义ID</param>
    /// <param name="request">启动请求，包含实例名称和输入参数</param>
    /// <param name="userId">启动用户ID</param>
    /// <returns>启动成功的工作流实例信息</returns>
    public async Task<ActionResult<WorkflowDtos.WorkflowInstanceDto>> StartWorkflowInstance(Guid workflowId, [FromBody] WorkflowDtos.StartWorkflowInstanceRequest request, [FromQuery] Guid userId)
    {
        try
        {
            var instance = await _workflowService.StartWorkflowInstanceAsync(workflowId, request, userId);
            return CreatedAtAction(nameof(GetWorkflowInstance), new { id = instance.Id }, instance);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "启动工作流实例失败：工作流ID {WorkflowId}", workflowId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动工作流实例时发生错误：工作流ID {WorkflowId}", workflowId);
            return StatusCode(500, new { message = "启动工作流实例时发生错误" });
        }
    }

    /// <summary>
    /// 根据ID获取工作流实例详情
    /// </summary>
    /// <param name="id">工作流实例ID</param>
    /// <returns>工作流实例详情，如果不存在则返回404</returns>
    [HttpGet("instances/{id}")]
    public async Task<ActionResult<WorkflowDtos.WorkflowInstanceDto>> GetWorkflowInstance(Guid id)
    {
        try
        {
            var instance = await _workflowService.GetWorkflowInstanceAsync(id);
            if (instance == null)
            {
                return NotFound(new { message = "工作流实例未找到" });
            }
            return Ok(instance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取工作流实例时发生错误：实例ID {InstanceId}", id);
            return StatusCode(500, new { message = "获取工作流实例时发生错误" });
        }
    }

    /// <summary>
    /// 获取工作流实例列表（支持分页和过滤）
    /// </summary>
    /// <param name="workflowId">可选，过滤特定工作流定义的实例</param>
    /// <param name="userId">可选，过滤特定用户的实例</param>
    /// <param name="page">页码，从1开始</param>
    /// <param name="pageSize">每页大小，默认20</param>
    /// <returns>工作流实例列表，按创建时间倒序排列</returns>
    [HttpGet("instances")]
    public async Task<ActionResult<IEnumerable<WorkflowDtos.WorkflowInstanceDto>>> GetWorkflowInstances([FromQuery] Guid? workflowId, [FromQuery] Guid? userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var instances = await _workflowService.GetWorkflowInstancesAsync(workflowId, userId, page, pageSize);
            return Ok(instances);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取工作流实例列表时发生错误");
            return StatusCode(500, new { message = "获取工作流实例列表时发生错误" });
        }
    }

    /// <summary>
    /// 暂停工作流实例
    /// </summary>
    [HttpPost("instances/{id}/suspend")]
    public async Task<IActionResult> SuspendWorkflowInstance(Guid id, [FromQuery] Guid userId)
    {
        try
        {
            var result = await _workflowService.SuspendWorkflowInstanceAsync(id, userId);
            if (!result)
            {
                return NotFound(new { message = "工作流实例未找到或无权限" });
            }
            return Ok(new { message = "工作流实例已暂停" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "暂停工作流实例时发生错误：实例ID {InstanceId}", id);
            return StatusCode(500, new { message = "暂停工作流实例时发生错误" });
        }
    }

    /// <summary>
    /// 恢复工作流实例
    /// </summary>
    [HttpPost("instances/{id}/resume")]
    public async Task<IActionResult> ResumeWorkflowInstance(Guid id, [FromQuery] Guid userId)
    {
        try
        {
            var result = await _workflowService.ResumeWorkflowInstanceAsync(id, userId);
            if (!result)
            {
                return NotFound(new { message = "工作流实例未找到或无权限" });
            }
            return Ok(new { message = "工作流实例已恢复" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复工作流实例时发生错误：实例ID {InstanceId}", id);
            return StatusCode(500, new { message = "恢复工作流实例时发生错误" });
        }
    }

    /// <summary>
    /// 取消工作流实例
    /// </summary>
    [HttpPost("instances/{id}/cancel")]
    public async Task<IActionResult> CancelWorkflowInstance(Guid id, [FromQuery] Guid userId)
    {
        try
        {
            var result = await _workflowService.CancelWorkflowInstanceAsync(id, userId);
            if (!result)
            {
                return NotFound(new { message = "工作流实例未找到或无权限" });
            }
            return Ok(new { message = "工作流实例已取消" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消工作流实例时发生错误：实例ID {InstanceId}", id);
            return StatusCode(500, new { message = "取消工作流实例时发生错误" });
        }
    }

    /// <summary>
    /// 获取工作流执行历史
    /// </summary>
    [HttpGet("instances/{id}/history")]
    /// <summary>
    /// 获取工作流实例的执行历史记录
    /// </summary>
    /// <param name="id">工作流实例ID</param>
    /// <returns>该实例的所有执行历史记录，按执行时间正序排列</returns>
    public async Task<ActionResult<IEnumerable<WorkflowDtos.WorkflowExecutionHistoryDto>>> GetWorkflowExecutionHistory(Guid id)
    {
        try
        {
            var history = await _workflowService.GetWorkflowExecutionHistoryAsync(id);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取工作流执行历史时发生错误：实例ID {InstanceId}", id);
            return StatusCode(500, new { message = "获取执行历史时发生错误" });
        }
    }
}