using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.Data;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RealScene3D.Application.Services.Workflows;

/// <summary>
/// 工作流服务实现
/// </summary>
public class WorkflowService : IWorkflowService, IWorkflowExecutorService
{
    private readonly IRepository<Workflow> _workflowRepository;
    private readonly IRepository<WorkflowInstance> _workflowInstanceRepository;
    private readonly IRepository<WorkflowExecutionHistory> _executionHistoryRepository;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IEnumerable<IWorkflowNodeExecutor> _nodeExecutors;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(
        IRepository<Workflow> workflowRepository,
        IRepository<WorkflowInstance> workflowInstanceRepository,
        IRepository<WorkflowExecutionHistory> executionHistoryRepository,
        IWorkflowEngine workflowEngine,
        IEnumerable<IWorkflowNodeExecutor> nodeExecutors,
        IUnitOfWork unitOfWork,
        ApplicationDbContext context,
        ILogger<WorkflowService> logger)
    {
        _workflowRepository = workflowRepository;
        _workflowInstanceRepository = workflowInstanceRepository;
        _executionHistoryRepository = executionHistoryRepository;
        _workflowEngine = workflowEngine;
        _nodeExecutors = nodeExecutors;
        _unitOfWork = unitOfWork;
        _context = context;
        _logger = logger;
    }

    #region 工作流定义管理

    /// <summary>
    /// 创建工作流定义
    /// </summary>
    /// <param name="request">工作流创建请求，包含定义和配置信息</param>
    /// <param name="userId">创建者用户ID</param>
    /// <returns>创建成功的工作流定义信息</returns>
    public async Task<WorkflowDtos.WorkflowDto> CreateWorkflowAsync(WorkflowDtos.CreateWorkflowRequest request, Guid userId)
    {
        try
        {
            // 验证工作流定义JSON
            ValidateWorkflowDefinition(request.Definition);

            var workflow = new Workflow
            {
                Name = request.Name,
                Description = request.Description,
                Definition = request.Definition,
                Version = request.Version,
                CreatedBy = userId
            };

            await _workflowRepository.AddAsync(workflow);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("工作流定义已创建：{WorkflowId}, 用户：{UserId}", workflow.Id, userId);

            return MapToDto(workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建工作流定义失败：{WorkflowName}, 用户：{UserId}", request.Name, userId);
            throw;
        }
    }

    /// <summary>
    /// 更新工作流定义
    /// </summary>
    /// <param name="workflowId">工作流定义ID</param>
    /// <param name="request">更新请求，包含要修改的内容</param>
    /// <param name="userId">操作用户ID，需要验证权限</param>
    /// <returns>更新后的工作流定义，如果不存在则返回null</returns>
    public async Task<WorkflowDtos.WorkflowDto?> UpdateWorkflowAsync(Guid workflowId, WorkflowDtos.UpdateWorkflowRequest request, Guid userId)
    {
        try
        {
            var workflow = await _workflowRepository.GetByIdAsync(workflowId);
            if (workflow == null || workflow.CreatedBy != userId)
            {
                return null;
            }

            // 验证工作流定义JSON
            if (!string.IsNullOrEmpty(request.Definition))
            {
                ValidateWorkflowDefinition(request.Definition);
            }

            workflow.Name = request.Name ?? workflow.Name;
            workflow.Description = request.Description ?? workflow.Description;
            workflow.Definition = request.Definition ?? workflow.Definition;
            workflow.Version = request.Version ?? workflow.Version;
            workflow.IsEnabled = request.IsEnabled;
            workflow.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("工作流定义已更新：{WorkflowId}, 用户：{UserId}", workflowId, userId);

            return MapToDto(workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新工作流定义失败：{WorkflowId}, 用户：{UserId}", workflowId, userId);
            throw;
        }
    }

    public async Task<bool> DeleteWorkflowAsync(Guid workflowId, Guid userId)
    {
        try
        {
            var workflow = await _workflowRepository.GetByIdAsync(workflowId);
            if (workflow == null || workflow.CreatedBy != userId)
            {
                return false;
            }

            workflow.IsEnabled = false;
            workflow.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("工作流定义已禁用：{WorkflowId}, 用户：{UserId}", workflowId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除工作流定义失败：{WorkflowId}, 用户：{UserId}", workflowId, userId);
            return false;
        }
    }

    /// <summary>
    /// 根据ID获取工作流定义详情
    /// </summary>
    /// <param name="workflowId">工作流定义ID</param>
    /// <returns>工作流定义详情，如果不存在则返回null</returns>
    public async Task<WorkflowDtos.WorkflowDto?> GetWorkflowByIdAsync(Guid workflowId)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        return workflow != null ? MapToDto(workflow) : null;
    }

    /// <summary>
    /// 获取指定用户的工作流定义列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户创建的工作流定义列表，按创建时间倒序排列</returns>
    public async Task<IEnumerable<WorkflowDtos.WorkflowDto>> GetUserWorkflowsAsync(Guid userId)
    {
        var workflows = await _context.Workflows
            .Where(w => w.CreatedBy == userId && w.IsEnabled)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

        return workflows.Select(MapToDto);
    }

    /// <summary>
    /// 获取所有启用的工作流定义列表
    /// </summary>
    /// <returns>系统中所有启用的工作流定义</returns>
    public async Task<IEnumerable<WorkflowDtos.WorkflowDto>> GetAllWorkflowsAsync()
    {
        var workflows = await _workflowRepository.GetAllAsync();
        return workflows.Where(w => w.IsEnabled).Select(MapToDto);
    }

    #endregion

    #region 工作流实例管理

    /// <summary>
    /// 启动工作流实例
    /// </summary>
    /// <param name="workflowId">工作流定义ID</param>
    /// <param name="request">启动请求，包含实例名称和输入参数</param>
    /// <param name="userId">启动用户ID</param>
    /// <returns>启动成功的工作流实例信息</returns>
    public async Task<WorkflowDtos.WorkflowInstanceDto> StartWorkflowInstanceAsync(Guid workflowId, WorkflowDtos.StartWorkflowInstanceRequest request, Guid userId)
    {
        try
        {
            var workflow = await _workflowRepository.GetByIdAsync(workflowId);
            if (workflow == null || !workflow.IsEnabled)
            {
                throw new InvalidOperationException("工作流定义不存在或已禁用");
            }

            var instance = new WorkflowInstance
            {
                WorkflowId = workflowId,
                Name = request.Name,
                InputParameters = request.InputParameters,
                CreatedBy = userId,
                Status = WorkflowInstanceStatus.Created
            };

            await _workflowInstanceRepository.AddAsync(instance);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("工作流实例已创建：{InstanceId}, 工作流：{WorkflowId}, 用户：{UserId}", instance.Id, workflowId, userId);

            // 异步启动工作流执行
            _ = Task.Run(() => ExecuteWorkflowAsync(instance.Id));

            return await GetWorkflowInstanceDtoAsync(instance.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动工作流实例失败：工作流：{WorkflowId}, 用户：{UserId}", workflowId, userId);
            throw;
        }
    }

    /// <summary>
    /// 根据ID获取工作流实例详情
    /// </summary>
    /// <param name="instanceId">工作流实例ID</param>
    /// <returns>工作流实例详情，如果不存在则返回null</returns>
    public async Task<WorkflowDtos.WorkflowInstanceDto?> GetWorkflowInstanceAsync(Guid instanceId)
    {
        return await GetWorkflowInstanceDtoAsync(instanceId);
    }

    /// <summary>
    /// 获取工作流实例列表（支持分页和过滤）
    /// </summary>
    /// <param name="workflowId">可选，过滤特定工作流定义的实例</param>
    /// <param name="userId">可选，过滤特定用户的实例</param>
    /// <param name="page">页码，从1开始</param>
    /// <param name="pageSize">每页大小，默认20</param>
    /// <returns>工作流实例列表，按创建时间倒序排列</returns>
    public async Task<IEnumerable<WorkflowDtos.WorkflowInstanceDto>> GetWorkflowInstancesAsync(Guid? workflowId = null, Guid? userId = null, int page = 1, int pageSize = 20)
    {
        var query = _context.WorkflowInstances.AsQueryable();

        if (workflowId.HasValue)
        {
            query = query.Where(i => i.WorkflowId == workflowId.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(i => i.CreatedBy == userId.Value);
        }

        var instances = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = new List<WorkflowDtos.WorkflowInstanceDto>();
        foreach (var instance in instances)
        {
            dtos.Add(await GetWorkflowInstanceDtoAsync(instance.Id));
        }

        return dtos;
    }

    public async Task<bool> SuspendWorkflowInstanceAsync(Guid instanceId, Guid userId)
    {
        try
        {
            var instance = await _workflowInstanceRepository.GetByIdAsync(instanceId);
            if (instance == null || instance.CreatedBy != userId)
            {
                return false;
            }

            if (instance.Status != WorkflowInstanceStatus.Running)
            {
                return false;
            }

            instance.Status = WorkflowInstanceStatus.Suspended;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("工作流实例已暂停：{InstanceId}, 用户：{UserId}", instanceId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "暂停工作流实例失败：{InstanceId}, 用户：{UserId}", instanceId, userId);
            return false;
        }
    }

    public async Task<bool> ResumeWorkflowInstanceAsync(Guid instanceId, Guid userId)
    {
        try
        {
            var instance = await _workflowInstanceRepository.GetByIdAsync(instanceId);
            if (instance == null || instance.CreatedBy != userId)
            {
                return false;
            }

            if (instance.Status != WorkflowInstanceStatus.Suspended)
            {
                return false;
            }

            instance.Status = WorkflowInstanceStatus.Running;
            await _unitOfWork.SaveChangesAsync();

            // 继续执行工作流
            _ = Task.Run(() => ExecuteWorkflowAsync(instanceId));

            _logger.LogInformation("工作流实例已恢复：{InstanceId}, 用户：{UserId}", instanceId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复工作流实例失败：{InstanceId}, 用户：{UserId}", instanceId, userId);
            return false;
        }
    }

    public async Task<bool> CancelWorkflowInstanceAsync(Guid instanceId, Guid userId)
    {
        try
        {
            var instance = await _workflowInstanceRepository.GetByIdAsync(instanceId);
            if (instance == null || instance.CreatedBy != userId)
            {
                return false;
            }

            if (instance.Status == WorkflowInstanceStatus.Completed ||
                instance.Status == WorkflowInstanceStatus.Failed ||
                instance.Status == WorkflowInstanceStatus.Cancelled)
            {
                return false;
            }

            instance.Status = WorkflowInstanceStatus.Cancelled;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("工作流实例已取消：{InstanceId}, 用户：{UserId}", instanceId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消工作流实例失败：{InstanceId}, 用户：{UserId}", instanceId, userId);
            return false;
        }
    }

    /// <summary>
    /// 获取工作流实例的执行历史记录
    /// </summary>
    /// <param name="instanceId">工作流实例ID</param>
    /// <returns>该实例的所有执行历史记录，按执行时间正序排列</returns>
    public async Task<IEnumerable<WorkflowDtos.WorkflowExecutionHistoryDto>> GetWorkflowExecutionHistoryAsync(Guid instanceId)
    {
        var history = await _context.WorkflowExecutionHistories
            .Where(h => h.WorkflowInstanceId == instanceId)
            .OrderBy(h => h.ExecutedAt)
            .ToListAsync();

        return history.Select(MapHistoryToDto);
    }

    #endregion

    #region 工作流执行

    public async Task ExecuteWorkflowAsync(Guid instanceId)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var completedNodes = new Dictionary<string, WorkflowNodeStatus>();
        var runningNodes = new Dictionary<string, WorkflowNodeStatus>();
        var variables = new Dictionary<string, object>();

        try
        {
            var instance = await _workflowInstanceRepository.GetByIdAsync(instanceId);
            if (instance == null)
            {
                _logger.LogWarning("工作流实例不存在：{InstanceId}", instanceId);
                return;
            }

            // 检查实例状态
            if (instance.Status == WorkflowInstanceStatus.Completed ||
                instance.Status == WorkflowInstanceStatus.Failed ||
                instance.Status == WorkflowInstanceStatus.Cancelled)
            {
                _logger.LogWarning("工作流实例已结束，无法重新执行：{InstanceId}, 状态：{Status}", instanceId, instance.Status);
                return;
            }

            instance.Status = WorkflowInstanceStatus.Running;
            instance.StartedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("开始执行工作流实例：{InstanceId}", instanceId);

            // 获取工作流定义
            var workflow = await _workflowRepository.GetByIdAsync(instance.WorkflowId);
            if (workflow == null)
            {
                throw new InvalidOperationException($"工作流定义不存在：{instance.WorkflowId}");
            }

            // 解析工作流定义
            var definitionModel = ParseWorkflowDefinition(workflow.Definition);
            var executionGraph = BuildExecutionGraph(definitionModel);

            // 初始化变量
            if (!string.IsNullOrEmpty(instance.InputParameters))
            {
                try
                {
                    var inputParams = JsonDocument.Parse(instance.InputParameters);
                    var root = inputParams.RootElement;
                    foreach (var property in root.EnumerateObject())
                    {
                        variables[property.Name] = property.Value.ToString();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "解析输入参数失败：{InstanceId}", instanceId);
                }
            }

            // 执行工作流节点
            var startNodes = GetExecutableNodes(executionGraph, completedNodes, runningNodes);
            if (!startNodes.Any())
            {
                // 如果没有起始节点，尝试查找开始节点
                startNodes = definitionModel.Nodes
                    .Where(n => n.Type.ToLower() == "start" || n.Type.ToLower() == "trigger")
                    .Select(n => n.Id)
                    .ToList();
            }

            // 并发执行起始节点
            var executionTasks = new List<Task>();
            foreach (var nodeId in startNodes)
            {
                if (executionGraph.TryGetValue(nodeId, out var nodeInfo))
                {
                    executionTasks.Add(ExecuteNodeAsync(instance, nodeInfo, executionGraph, completedNodes, runningNodes, variables));
                }
            }

            // 等待所有起始节点完成
            await Task.WhenAll(executionTasks);

            // 检查是否有节点执行失败
            var failedNodes = completedNodes.Where(n => n.Value == WorkflowNodeStatus.Failed).ToList();
            if (failedNodes.Any())
            {
                throw new InvalidOperationException($"节点执行失败：{string.Join(", ", failedNodes.Select(n => n.Key))}");
            }

            // 检查是否所有节点都已完成
            var allNodesCompleted = executionGraph.Keys.All(nodeId => completedNodes.ContainsKey(nodeId));
            if (!allNodesCompleted)
            {
                throw new InvalidOperationException("工作流执行不完整，仍有节点未执行");
            }

            stopwatch.Stop();
            instance.Status = WorkflowInstanceStatus.Completed;
            instance.CompletedAt = DateTime.UtcNow;
            instance.Result = JsonSerializer.Serialize(new
            {
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                CompletedNodes = completedNodes.Count,
                Variables = variables
            });

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("工作流实例执行完成：{InstanceId}, 执行时间：{ExecutionTimeMs}ms",
                instanceId, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "工作流实例执行失败：{InstanceId}, 执行时间：{ExecutionTimeMs}ms",
                instanceId, stopwatch.ElapsedMilliseconds);

            var instance = await _workflowInstanceRepository.GetByIdAsync(instanceId);
            if (instance != null)
            {
                instance.Status = WorkflowInstanceStatus.Failed;
                instance.ErrorMessage = ex.Message;
                instance.CompletedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// 执行单个工作流节点
    /// </summary>
    private async Task ExecuteNodeAsync(
        WorkflowInstance instance,
        ExecutionNode nodeInfo,
        Dictionary<string, ExecutionNode> executionGraph,
        Dictionary<string, WorkflowNodeStatus> completedNodes,
        Dictionary<string, WorkflowNodeStatus> runningNodes,
        Dictionary<string, object> variables)
    {
        var nodeStopwatch = System.Diagnostics.Stopwatch.StartNew();
        runningNodes[nodeInfo.Node.Id] = WorkflowNodeStatus.Running;

        try
        {
            _logger.LogInformation("开始执行节点：{NodeId} ({NodeType})", nodeInfo.Node.Id, nodeInfo.Node.Type);

            // 创建节点上下文
            var nodeContext = new WorkflowNodeContext
            {
                InstanceId = instance.Id,
                NodeId = nodeInfo.Node.Id,
                NodeType = nodeInfo.Node.Type,
                InputData = JsonSerializer.Serialize(nodeInfo.Node.Data),
                Variables = variables
            };

            // 查找合适的节点执行器
            var executor = _nodeExecutors.FirstOrDefault(e => e.GetSupportedNodeTypes().Contains(nodeInfo.Node.Type));
            if (executor == null)
            {
                throw new InvalidOperationException($"不支持的节点类型：{nodeInfo.Node.Type}");
            }

            // 执行节点
            var startTime = DateTime.UtcNow;
            var result = await executor.ExecuteNodeAsync(nodeContext);
            var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            nodeStopwatch.Stop();

            if (result.Status == WorkflowNodeStatus.Completed)
            {
                // 更新变量
                foreach (var variable in result.UpdatedVariables)
                {
                    variables[variable.Key] = variable.Value;
                }

                // 更新工作流实例上下文
                await UpdateWorkflowInstanceContextAsync(instance.Id, variables, completedNodes);

                // 更新执行历史
                await HandleNodeCompletedAsync(instance.Id, nodeInfo.Node.Id, new Domain.Interfaces.WorkflowNodeResult
                {
                    Status = result.Status,
                    OutputData = result.OutputData,
                    UpdatedVariables = result.UpdatedVariables,
                    ErrorMessage = result.ErrorMessage
                });

                completedNodes[nodeInfo.Node.Id] = WorkflowNodeStatus.Completed;

                _logger.LogInformation("节点执行完成：{NodeId}, 执行时间：{ExecutionTimeMs}ms",
                    nodeInfo.Node.Id, nodeStopwatch.ElapsedMilliseconds);

                // 递归执行下一个节点
                var nextExecutionTasks = new List<Task>();
                foreach (var nextNodeId in nodeInfo.NextNodes)
                {
                    if (executionGraph.TryGetValue(nextNodeId, out var nextNodeInfo))
                    {
                        // 检查前置节点是否都已完成
                        var canExecuteNext = nextNodeInfo.PreviousNodes.All(prevId => completedNodes.ContainsKey(prevId));
                        if (canExecuteNext && !runningNodes.ContainsKey(nextNodeId) && !completedNodes.ContainsKey(nextNodeId))
                        {
                            nextExecutionTasks.Add(ExecuteNodeAsync(instance, nextNodeInfo, executionGraph, completedNodes, runningNodes, variables));
                        }
                    }
                }

                // 等待所有后续节点完成
                if (nextExecutionTasks.Any())
                {
                    await Task.WhenAll(nextExecutionTasks);
                }
            }
            else
            {
                // 节点执行失败
                completedNodes[nodeInfo.Node.Id] = WorkflowNodeStatus.Failed;

                await HandleNodeCompletedAsync(instance.Id, nodeInfo.Node.Id, new WorkflowNodeResult
                {
                    Status = result.Status,
                    OutputData = result.OutputData,
                    ErrorMessage = result.ErrorMessage
                });

                throw new InvalidOperationException($"节点执行失败：{nodeInfo.Node.Id}, 错误：{result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            nodeStopwatch.Stop();
            completedNodes[nodeInfo.Node.Id] = WorkflowNodeStatus.Failed;
            runningNodes.Remove(nodeInfo.Node.Id);

            _logger.LogError(ex, "节点执行异常：{NodeId}, 执行时间：{ExecutionTimeMs}ms",
                nodeInfo.Node.Id, nodeStopwatch.ElapsedMilliseconds);

            await HandleNodeCompletedAsync(instance.Id, nodeInfo.Node.Id, new WorkflowNodeResult
            {
                Status = WorkflowNodeStatus.Failed,
                ErrorMessage = ex.Message
            });

            throw;
        }
        finally
        {
            runningNodes.Remove(nodeInfo.Node.Id);
        }
    }

    public async Task HandleNodeCompletedAsync(Guid instanceId, string nodeId, Domain.Interfaces.WorkflowNodeResult result)
    {
        try
        {
            // 获取或创建执行历史记录
            var existingHistory = await _context.WorkflowExecutionHistories
                .FirstOrDefaultAsync(h => h.WorkflowInstanceId == instanceId && h.NodeId == nodeId);

            if (existingHistory != null)
            {
                // 更新现有记录
                existingHistory.Status = result.Status;
                existingHistory.OutputData = result.OutputData ?? existingHistory.OutputData;
                existingHistory.ErrorMessage = result.ErrorMessage ?? existingHistory.ErrorMessage;
                existingHistory.ExecutedAt = DateTime.UtcNow;

                await _unitOfWork.SaveChangesAsync();
            }
            else
            {
                // 创建新记录
                var history = new WorkflowExecutionHistory
                {
                    WorkflowInstanceId = instanceId,
                    NodeId = nodeId,
                    Status = result.Status,
                    InputData = JsonSerializer.Serialize(new { nodeId, timestamp = DateTime.UtcNow }),
                    OutputData = result.OutputData ?? "{}",
                    ExecutionTimeMs = 0, // 这里可以后续添加执行时间计算
                    ErrorMessage = result.ErrorMessage,
                    ExecutedAt = DateTime.UtcNow,
                    NodeType = "workflow" // 这里可以后续添加节点类型获取
                };

                await _executionHistoryRepository.AddAsync(history);
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("工作流节点执行完成：实例{InstanceId}, 节点{NodeId}, 状态{Status}",
                instanceId, nodeId, result.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理节点执行完成事件失败：实例{InstanceId}, 节点{NodeId}", instanceId, nodeId);
        }
    }

    #endregion

    #region 工作流执行模型

    /// <summary>
    /// 工作流定义模型
    /// </summary>
    private class WorkflowDefinitionModel
    {
        public List<WorkflowNodeModel> Nodes { get; set; } = new();
        public List<WorkflowConnectionModel> Connections { get; set; } = new();
    }

    /// <summary>
    /// 工作流节点模型
    /// </summary>
    private class WorkflowNodeModel
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = new();
        public JsonObject Data { get; set; } = new JsonObject();
    }

    /// <summary>
    /// 工作流连接模型
    /// </summary>
    private class WorkflowConnectionModel
    {
        public string Id { get; set; } = string.Empty;
        public string SourceNodeId { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string SourceHandle { get; set; } = string.Empty;
        public string TargetHandle { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    /// <summary>
    /// 执行节点信息
    /// </summary>
    private class ExecutionNode
    {
        public WorkflowNodeModel Node { get; set; } = new();
        public List<string> PreviousNodes { get; set; } = new();
        public List<string> NextNodes { get; set; } = new();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 解析工作流定义JSON
    /// </summary>
    private WorkflowDefinitionModel ParseWorkflowDefinition(string definitionJson)
    {
        try
        {
            var definition = JsonNode.Parse(definitionJson);
            if (definition == null)
                throw new InvalidOperationException("无效的工作流定义JSON");

            var model = new WorkflowDefinitionModel();

            // 解析节点
            var nodesNode = definition["nodes"];
            if (nodesNode is JsonArray nodesArray)
            {
                foreach (var nodeItem in nodesArray)
                {
                    if (nodeItem is JsonObject nodeObj)
                    {
                        var node = new WorkflowNodeModel
                        {
                            Id = nodeObj["id"]?.ToString() ?? string.Empty,
                            Type = nodeObj["type"]?.ToString() ?? string.Empty,
                            Name = nodeObj["name"]?.ToString() ?? string.Empty,
                            Data = nodeObj
                        };

                        // 解析属性
                        if (nodeObj["properties"] is JsonObject propertiesObj)
                        {
                            foreach (var property in propertiesObj)
                            {
                                node.Properties[property.Key] = property.Value?.ToString() ?? string.Empty;
                            }
                        }

                        model.Nodes.Add(node);
                    }
                }
            }

            // 解析连接
            var connectionsNode = definition["connections"];
            if (connectionsNode is JsonArray connectionsArray)
            {
                foreach (var connItem in connectionsArray)
                {
                    if (connItem is JsonObject connObj)
                    {
                        var connection = new WorkflowConnectionModel
                        {
                            Id = connObj["id"]?.ToString() ?? string.Empty,
                            SourceNodeId = connObj["source"]?.ToString() ?? string.Empty,
                            TargetNodeId = connObj["target"]?.ToString() ?? string.Empty,
                            SourceHandle = connObj["sourceHandle"]?.ToString() ?? string.Empty,
                            TargetHandle = connObj["targetHandle"]?.ToString() ?? string.Empty
                        };

                        // 解析连接属性
                        if (connObj["properties"] is JsonObject propertiesObj)
                        {
                            foreach (var property in propertiesObj)
                            {
                                connection.Properties[property.Key] = property.Value?.ToString() ?? string.Empty;
                            }
                        }

                        model.Connections.Add(connection);
                    }
                }
            }

            return model;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"解析工作流定义失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 构建执行节点图
    /// </summary>
    private Dictionary<string, ExecutionNode> BuildExecutionGraph(WorkflowDefinitionModel definition)
    {
        var graph = new Dictionary<string, ExecutionNode>();

        // 初始化所有节点
        foreach (var node in definition.Nodes)
        {
            graph[node.Id] = new ExecutionNode
            {
                Node = node,
                PreviousNodes = new List<string>(),
                NextNodes = new List<string>()
            };
        }

        // 构建节点关系
        foreach (var connection in definition.Connections)
        {
            if (graph.ContainsKey(connection.SourceNodeId) && graph.ContainsKey(connection.TargetNodeId))
            {
                graph[connection.SourceNodeId].NextNodes.Add(connection.TargetNodeId);
                graph[connection.TargetNodeId].PreviousNodes.Add(connection.SourceNodeId);
            }
        }

        return graph;
    }

    /// <summary>
    /// 获取可执行的节点（前置节点都已完成）
    /// </summary>
    private List<string> GetExecutableNodes(
        Dictionary<string, ExecutionNode> graph,
        Dictionary<string, WorkflowNodeStatus> completedNodes,
        Dictionary<string, WorkflowNodeStatus> runningNodes)
    {
        var executable = new List<string>();

        foreach (var (nodeId, nodeInfo) in graph)
        {
            // 跳过正在运行或已完成的节点
            if (runningNodes.ContainsKey(nodeId) || completedNodes.ContainsKey(nodeId))
                continue;

            // 检查所有前置节点是否已完成
            bool canExecute = true;
            foreach (var prevNodeId in nodeInfo.PreviousNodes)
            {
                if (!completedNodes.ContainsKey(prevNodeId))
                {
                    canExecute = false;
                    break;
                }
            }

            if (canExecute)
            {
                executable.Add(nodeId);
            }
        }

        return executable;
    }

    /// <summary>
    /// 更新工作流实例上下文
    /// </summary>
    private async Task UpdateWorkflowInstanceContextAsync(
        Guid instanceId,
        Dictionary<string, object> variables,
        Dictionary<string, WorkflowNodeStatus> completedNodes)
    {
        try
        {
            var instance = await _workflowInstanceRepository.GetByIdAsync(instanceId);
            if (instance == null) return;

            // 更新上下文信息
            var context = new
            {
                Variables = variables,
                CompletedNodes = completedNodes.ToDictionary(k => k.Key, v => v.Value.ToString()),
                NodeCount = completedNodes.Count,
                LastUpdated = DateTime.UtcNow
            };

            instance.Context = JsonSerializer.Serialize(context);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新工作流实例上下文失败：{InstanceId}", instanceId);
        }
    }

    private void ValidateWorkflowDefinition(string definition)
    {
        try
        {
            JsonDocument.Parse(definition);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"无效的工作流定义JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// 将工作流领域对象映射为DTO对象
    /// </summary>
    /// <param name="workflow">工作流领域实体</param>
    /// <returns>工作流DTO，包含前端所需的所有信息</returns>
    private static WorkflowDtos.WorkflowDto MapToDto(Workflow workflow)
    {
        return new WorkflowDtos.WorkflowDto
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            Definition = workflow.Definition,
            Version = workflow.Version,
            IsEnabled = workflow.IsEnabled,
            CreatedBy = workflow.CreatedBy,
            CreatedAt = workflow.CreatedAt,
            UpdatedAt = workflow.UpdatedAt
        };
    }

    /// <summary>
    /// 获取工作流实例DTO的私有方法
    /// </summary>
    /// <param name="instanceId">工作流实例ID</param>
    /// <returns>工作流实例DTO，如果不存在则返回null</returns>
    private async Task<WorkflowDtos.WorkflowInstanceDto> GetWorkflowInstanceDtoAsync(Guid instanceId)
    {
        var instance = await _context.WorkflowInstances
            .Include(i => i.Workflow)
            .FirstOrDefaultAsync(i => i.Id == instanceId);

        if (instance == null)
        {
            return null!; // 明确表示此处可能返回null
        }

        var dto = new WorkflowDtos.WorkflowInstanceDto
        {
            Id = instance.Id,
            WorkflowId = instance.WorkflowId,
            Name = instance.Name,
            Status = instance.Status,
            InputParameters = instance.InputParameters,
            Context = instance.Context,
            Result = instance.Result,
            ErrorMessage = instance.ErrorMessage,
            CreatedBy = instance.CreatedBy,
            CreatedAt = instance.CreatedAt,
            StartedAt = instance.StartedAt,
            CompletedAt = instance.CompletedAt,
            Workflow = instance.Workflow != null ? MapToDto(instance.Workflow) : null
        };

        return dto;
    }

    /// <summary>
    /// 将执行历史领域对象映射为DTO对象
    /// </summary>
    /// <param name="history">执行历史领域实体</param>
    /// <returns>执行历史DTO，包含节点执行的详细信息</returns>
    private static WorkflowDtos.WorkflowExecutionHistoryDto MapHistoryToDto(WorkflowExecutionHistory history)
    {
        return new WorkflowDtos.WorkflowExecutionHistoryDto
        {
            Id = history.Id,
            WorkflowInstanceId = history.WorkflowInstanceId,
            NodeId = history.NodeId,
            NodeType = history.NodeType,
            Status = history.Status,
            InputData = history.InputData,
            OutputData = history.OutputData,
            ExecutionTimeMs = history.ExecutionTimeMs,
            ErrorMessage = history.ErrorMessage,
            ExecutedAt = history.ExecutedAt
        };
    }

    #endregion
}