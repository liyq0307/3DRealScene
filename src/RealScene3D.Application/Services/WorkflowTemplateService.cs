using RealScene3D.Application.DTOs;
using RealScene3D.Domain.Entities;
using RealScene3D.Infrastructure.Data;
using System.Text.Json;

namespace RealScene3D.Application.Services;

/// <summary>
/// 工作流模板服务
/// 提供预定义的工作流模板管理功能
/// </summary>
public class WorkflowTemplateService
{
    private readonly ApplicationDbContext _context;

    public WorkflowTemplateService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取所有可用的工作流模板
    /// </summary>
    public async Task<List<WorkflowTemplateDto>> GetAvailableTemplatesAsync()
    {
        return await Task.FromResult(GetBuiltInTemplates());
    }

    /// <summary>
    /// 根据ID获取工作流模板详情
    /// </summary>
    public Task<WorkflowTemplateDto?> GetTemplateByIdAsync(string templateId)
    {
        var templates = GetBuiltInTemplates();
        return Task.FromResult(templates.FirstOrDefault(t => t.Id == templateId));
    }

    /// <summary>
    /// 根据分类获取工作流模板
    /// </summary>
    public Task<List<WorkflowTemplateDto>> GetTemplatesByCategoryAsync(string category)
    {
        var templates = GetBuiltInTemplates();
        return Task.FromResult(templates.Where(t => t.Category == category).ToList());
    }

    /// <summary>
    /// 从模板创建工作流定义
    /// </summary>
    public async Task<WorkflowDtos.WorkflowDto> CreateWorkflowFromTemplateAsync(
        string templateId,
        WorkflowDtos.CreateWorkflowRequest request,
        Guid userId)
    {
        var template = await GetTemplateByIdAsync(templateId);
        if (template == null)
        {
            throw new ArgumentException($"模板不存在：{templateId}");
        }

        // 使用模板的定义作为基础
        var workflowDefinition = template.Definition;

        // 可以在这里添加模板参数替换逻辑
        if (request.Definition != "{}" && !string.IsNullOrEmpty(request.Definition))
        {
            // 如果提供了自定义配置，合并到模板中
            var customConfig = JsonDocument.Parse(request.Definition);
            workflowDefinition = MergeTemplateWithCustomConfig(workflowDefinition, customConfig);
        }

        var workflow = new Workflow
        {
            Name = request.Name,
            Description = request.Description ?? template.Description,
            Definition = workflowDefinition,
            Version = request.Version,
            CreatedBy = userId
        };

        _context.Workflows.Add(workflow);
        await _context.SaveChangesAsync();

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
    /// 获取模板分类列表
    /// </summary>
    public Task<List<string>> GetTemplateCategoriesAsync()
    {
        var templates = GetBuiltInTemplates();
        return Task.FromResult(templates.Select(t => t.Category).Distinct().OrderBy(c => c).ToList());
    }

    /// <summary>
    /// 搜索模板
    /// </summary>
    public Task<List<WorkflowTemplateDto>> SearchTemplatesAsync(string keyword)
    {
        var templates = GetBuiltInTemplates();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Task.FromResult(templates);
        }

        var lowerKeyword = keyword.ToLower();
        return Task.FromResult(templates.Where(t =>
            t.Name.ToLower().Contains(lowerKeyword) ||
            t.Description.ToLower().Contains(lowerKeyword) ||
            t.Category.ToLower().Contains(lowerKeyword) ||
            t.Tags.Any(tag => tag.ToLower().Contains(lowerKeyword))
        ).ToList());
    }

    /// <summary>
    /// 获取内置工作流模板
    /// </summary>
    private List<WorkflowTemplateDto> GetBuiltInTemplates()
    {
        return new List<WorkflowTemplateDto>
        {
            // 数据处理类模板
            new WorkflowTemplateDto
            {
                Id = "data-import-template",
                Name = "数据导入流程",
                Description = "通用的数据导入和处理工作流模板",
                Category = "数据处理",
                Tags = new[] { "导入", "数据", "处理" },
                Definition = CreateDataImportWorkflowDefinition()
            },

            new WorkflowTemplateDto
            {
                Id = "data-export-template",
                Name = "数据导出流程",
                Description = "数据导出和文件生成工作流模板",
                Category = "数据处理",
                Tags = new[] { "导出", "数据", "文件" },
                Definition = CreateDataExportWorkflowDefinition()
            },

            new WorkflowTemplateDto
            {
                Id = "data-validation-template",
                Name = "数据验证流程",
                Description = "数据质量验证和清理工作流模板",
                Category = "数据处理",
                Tags = new[] { "验证", "质量", "清理" },
                Definition = CreateDataValidationWorkflowDefinition()
            },

            // 业务流程类模板
            new WorkflowTemplateDto
            {
                Id = "approval-workflow-template",
                Name = "审批工作流",
                Description = "通用审批流程模板，支持多级审批",
                Category = "业务流程",
                Tags = new[] { "审批", "流程", "审核" },
                Definition = CreateApprovalWorkflowDefinition()
            },

            new WorkflowTemplateDto
            {
                Id = "notification-workflow-template",
                Name = "通知工作流",
                Description = "消息通知和提醒工作流模板",
                Category = "业务流程",
                Tags = new[] { "通知", "消息", "提醒" },
                Definition = CreateNotificationWorkflowDefinition()
            },

            // 集成类模板
            new WorkflowTemplateDto
            {
                Id = "api-integration-template",
                Name = "API集成流程",
                Description = "外部API调用和数据同步工作流模板",
                Category = "系统集成",
                Tags = new[] { "API", "集成", "同步" },
                Definition = CreateApiIntegrationWorkflowDefinition()
            },

            new WorkflowTemplateDto
            {
                Id = "file-processing-template",
                Name = "文件处理流程",
                Description = "文件上传、处理和存储工作流模板",
                Category = "文件处理",
                Tags = new[] { "文件", "处理", "存储" },
                Definition = CreateFileProcessingWorkflowDefinition()
            },

            // 监控类模板
            new WorkflowTemplateDto
            {
                Id = "monitoring-alert-template",
                Name = "监控告警流程",
                Description = "系统监控和告警处理工作流模板",
                Category = "监控告警",
                Tags = new[] { "监控", "告警", "系统" },
                Definition = CreateMonitoringAlertWorkflowDefinition()
            },

            // 3D场景专用模板
            new WorkflowTemplateDto
            {
                Id = "scene-processing-template",
                Name = "3D场景处理流程",
                Description = "3D场景数据处理和转换工作流模板",
                Category = "3D处理",
                Tags = new[] { "3D", "场景", "处理" },
                Definition = CreateSceneProcessingWorkflowDefinition()
            },

            new WorkflowTemplateDto
            {
                Id = "slicing-workflow-template",
                Name = "切片处理工作流",
                Description = "3D模型切片处理和优化工作流模板",
                Category = "3D处理",
                Tags = new[] { "切片", "3D", "模型" },
                Definition = CreateSlicingWorkflowDefinition()
            }
        };
    }

    /// <summary>
    /// 创建数据导入工作流定义
    /// </summary>
    private string CreateDataImportWorkflowDefinition()
    {
        var definition = new
        {
            nodes = new[]
            {
                new { id = "start", type = "start", name = "开始", position = new { x = 100, y = 100 } },
                new { id = "validate-file", type = "action", name = "验证文件", position = new { x = 300, y = 100 } },
                new { id = "parse-data", type = "action", name = "解析数据", position = new { x = 500, y = 100 } },
                new { id = "validate-data", type = "condition", name = "数据验证", position = new { x = 700, y = 100 } },
                new { id = "save-data", type = "database", name = "保存数据", position = new { x = 900, y = 50 } },
                new { id = "error-handling", type = "action", name = "错误处理", position = new { x = 900, y = 150 } },
                new { id = "end", type = "end", name = "结束", position = new { x = 1100, y = 100 } }
            },
            connections = new object[]
            {
                new { id = "c1", source = "start", target = "validate-file" },
                new { id = "c2", source = "validate-file", target = "parse-data" },
                new { id = "c3", source = "parse-data", target = "validate-data" },
                new { id = "c4", source = "validate-data", target = "save-data", sourceHandle = "true" },
                new { id = "c5", source = "validate-data", target = "error-handling", sourceHandle = "false" },
                new { id = "c6", source = "save-data", target = "end" },
                new { id = "c7", source = "error-handling", target = "end" }
            }
        };

        return JsonSerializer.Serialize(definition);
    }

    /// <summary>
    /// 创建数据导出工作流定义
    /// </summary>
    private string CreateDataExportWorkflowDefinition()
    {
        var definition = new
        {
            nodes = new[]
            {
                new { id = "start", type = "start", name = "开始", position = new { x = 100, y = 100 } },
                new { id = "query-data", type = "database", name = "查询数据", position = new { x = 300, y = 100 } },
                new { id = "format-data", type = "action", name = "格式化数据", position = new { x = 500, y = 100 } },
                new { id = "generate-file", type = "file", name = "生成文件", position = new { x = 700, y = 100 } },
                new { id = "send-notification", type = "email", name = "发送通知", position = new { x = 900, y = 100 } },
                new { id = "end", type = "end", name = "结束", position = new { x = 1100, y = 100 } }
            },
            connections = new[]
            {
                new { id = "c1", source = "start", target = "query-data" },
                new { id = "c2", source = "query-data", target = "format-data" },
                new { id = "c3", source = "format-data", target = "generate-file" },
                new { id = "c4", source = "generate-file", target = "send-notification" },
                new { id = "c5", source = "send-notification", target = "end" }
            }
        };

        return JsonSerializer.Serialize(definition);
    }

    /// <summary>
    /// 创建数据验证工作流定义
    /// </summary>
    private string CreateDataValidationWorkflowDefinition()
    {
        var definition = new
        {
            nodes = new[]
            {
                new { id = "start", type = "start", name = "开始", position = new { x = 100, y = 100 } },
                new { id = "load-data", type = "database", name = "加载数据", position = new { x = 300, y = 100 } },
                new { id = "check-completeness", type = "condition", name = "完整性检查", position = new { x = 500, y = 100 } },
                new { id = "check-accuracy", type = "condition", name = "准确性检查", position = new { x = 700, y = 50 } },
                new { id = "clean-data", type = "action", name = "数据清理", position = new { x = 700, y = 150 } },
                new { id = "update-status", type = "database", name = "更新状态", position = new { x = 900, y = 100 } },
                new { id = "end", type = "end", name = "结束", position = new { x = 1100, y = 100 } }
            },
            connections = new object[]
            {
                new { id = "c1", source = "start", target = "load-data" },
                new { id = "c2", source = "load-data", target = "check-completeness" },
                new { id = "c3", source = "check-completeness", target = "check-accuracy", sourceHandle = "true" },
                new { id = "c4", source = "check-completeness", target = "clean-data", sourceHandle = "false" },
                new { id = "c5", source = "check-accuracy", target = "update-status", sourceHandle = "true" },
                new { id = "c6", source = "check-accuracy", target = "clean-data", sourceHandle = "false" },
                new { id = "c7", source = "clean-data", target = "update-status" },
                new { id = "c8", source = "update-status", target = "end" }
            }
        };

        return JsonSerializer.Serialize(definition);
    }

    /// <summary>
    /// 创建审批工作流定义
    /// </summary>
    private string CreateApprovalWorkflowDefinition()
    {
        var definition = new
        {
            nodes = new[]
            {
                new { id = "start", type = "start", name = "提交申请", position = new { x = 100, y = 100 } },
                new { id = "level1-approval", type = "condition", name = "一级审批", position = new { x = 300, y = 100 } },
                new { id = "level2-approval", type = "condition", name = "二级审批", position = new { x = 500, y = 50 } },
                new { id = "approved", type = "action", name = "审批通过", position = new { x = 700, y = 25 } },
                new { id = "rejected", type = "action", name = "审批驳回", position = new { x = 700, y = 75 } },
                new { id = "notification", type = "email", name = "发送通知", position = new { x = 900, y = 50 } },
                new { id = "end", type = "end", name = "流程结束", position = new { x = 1100, y = 50 } }
            },
            connections = new object[]
            {
                new { id = "c1", source = "start", target = "level1-approval" },
                new { id = "c2", source = "level1-approval", target = "level2-approval", sourceHandle = "true" },
                new { id = "c3", source = "level1-approval", target = "rejected", sourceHandle = "false" },
                new { id = "c4", source = "level2-approval", target = "approved", sourceHandle = "true" },
                new { id = "c5", source = "level2-approval", target = "rejected", sourceHandle = "false" },
                new { id = "c6", source = "approved", target = "notification" },
                new { id = "c7", source = "rejected", target = "notification" },
                new { id = "c8", source = "notification", target = "end" }
            }
        };

        return JsonSerializer.Serialize(definition);
    }

    /// <summary>
    /// 创建通知工作流定义
    /// </summary>
    private string CreateNotificationWorkflowDefinition()
    {
        var definition = new
        {
            nodes = new[]
            {
                new { id = "trigger", type = "start", name = "触发事件", position = new { x = 100, y = 100 } },
                new { id = "check-condition", type = "condition", name = "条件检查", position = new { x = 300, y = 100 } },
                new { id = "email-notification", type = "email", name = "邮件通知", position = new { x = 500, y = 50 } },
                new { id = "sms-notification", type = "action", name = "短信通知", position = new { x = 500, y = 150 } },
                new { id = "log-notification", type = "action", name = "日志记录", position = new { x = 700, y = 100 } },
                new { id = "end", type = "end", name = "结束", position = new { x = 900, y = 100 } }
            },
            connections = new object[]
            {
                new { id = "c1", source = "trigger", target = "check-condition" },
                new { id = "c2", source = "check-condition", target = "email-notification", sourceHandle = "true" },
                new { id = "c3", source = "check-condition", target = "end", sourceHandle = "false" },
                new { id = "c4", source = "email-notification", target = "sms-notification" },
                new { id = "c5", source = "sms-notification", target = "log-notification" },
                new { id = "c6", source = "log-notification", target = "end" }
            }
        };

        return JsonSerializer.Serialize(definition);
    }

    /// <summary>
    /// 创建API集成工作流定义
    /// </summary>
    private string CreateApiIntegrationWorkflowDefinition()
    {
        var definition = new
        {
            nodes = new[]
            {
                new { id = "start", type = "start", name = "开始", position = new { x = 100, y = 100 } },
                new { id = "prepare-request", type = "action", name = "准备请求", position = new { x = 300, y = 100 } },
                new { id = "call-api", type = "api", name = "调用API", position = new { x = 500, y = 100 } },
                new { id = "check-response", type = "condition", name = "检查响应", position = new { x = 700, y = 100 } },
                new { id = "process-success", type = "action", name = "处理成功", position = new { x = 900, y = 50 } },
                new { id = "handle-error", type = "action", name = "处理错误", position = new { x = 900, y = 150 } },
                new { id = "end", type = "end", name = "结束", position = new { x = 1100, y = 100 } }
            },
            connections = new object[]
            {
                new { id = "c1", source = "start", target = "prepare-request" },
                new { id = "c2", source = "prepare-request", target = "call-api" },
                new { id = "c3", source = "call-api", target = "check-response" },
                new { id = "c4", source = "check-response", target = "process-success", sourceHandle = "true" },
                new { id = "c5", source = "check-response", target = "handle-error", sourceHandle = "false" },
                new { id = "c6", source = "process-success", target = "end" },
                new { id = "c7", source = "handle-error", target = "end" }
            }
        };

        return JsonSerializer.Serialize(definition);
    }

    /// <summary>
    /// 创建文件处理工作流定义
    /// </summary>
    private string CreateFileProcessingWorkflowDefinition()
    {
        var definition = new
        {
            nodes = new[]
            {
                new { id = "start", type = "start", name = "文件上传", position = new { x = 100, y = 100 } },
                new { id = "validate-file", type = "condition", name = "验证文件", position = new { x = 300, y = 100 } },
                new { id = "process-file", type = "action", name = "处理文件", position = new { x = 500, y = 50 } },
                new { id = "save-file", type = "file", name = "保存文件", position = new { x = 500, y = 150 } },
                new { id = "send-confirmation", type = "email", name = "发送确认", position = new { x = 700, y = 100 } },
                new { id = "end", type = "end", name = "完成", position = new { x = 900, y = 100 } }
            },
            connections = new object[]
            {
                new { id = "c1", source = "start", target = "validate-file" },
                new { id = "c2", source = "validate-file", target = "process-file", sourceHandle = "true" },
                new { id = "c3", source = "validate-file", target = "save-file", sourceHandle = "false" },
                new { id = "c4", source = "process-file", target = "send-confirmation" },
                new { id = "c5", source = "save-file", target = "send-confirmation" },
                new { id = "c6", source = "send-confirmation", target = "end" }
            }
        };

        return JsonSerializer.Serialize(definition);
    }

    /// <summary>
    /// 创建监控告警工作流定义
    /// </summary>
    private string CreateMonitoringAlertWorkflowDefinition()
    {
        var definition = new
        {
            nodes = new[]
            {
                new { id = "alert-trigger", type = "start", name = "告警触发", position = new { x = 100, y = 100 } },
                new { id = "check-severity", type = "condition", name = "检查严重程度", position = new { x = 300, y = 100 } },
                new { id = "high-priority", type = "action", name = "高优先级处理", position = new { x = 500, y = 50 } },
                new { id = "normal-priority", type = "action", name = "普通优先级处理", position = new { x = 500, y = 150 } },
                new { id = "send-alert", type = "email", name = "发送告警", position = new { x = 700, y = 100 } },
                new { id = "auto-resolve", type = "condition", name = "自动解决检查", position = new { x = 900, y = 100 } },
                new { id = "manual-intervention", type = "action", name = "人工干预", position = new { x = 1100, y = 150 } },
                new { id = "end", type = "end", name = "告警处理完成", position = new { x = 1100, y = 50 } }
            },
            connections = new object[]
            {
                new { id = "c1", source = "alert-trigger", target = "check-severity" },
                new { id = "c2", source = "check-severity", target = "high-priority", sourceHandle = "high" },
                new { id = "c3", source = "check-severity", target = "normal-priority", sourceHandle = "normal" },
                new { id = "c4", source = "high-priority", target = "send-alert" },
                new { id = "c5", source = "normal-priority", target = "send-alert" },
                new { id = "c6", source = "send-alert", target = "auto-resolve" },
                new { id = "c7", source = "auto-resolve", target = "end", sourceHandle = "true" },
                new { id = "c8", source = "auto-resolve", target = "manual-intervention", sourceHandle = "false" },
                new { id = "c9", source = "manual-intervention", target = "end" }
            }
        };

        return JsonSerializer.Serialize(definition);
    }

    /// <summary>
    /// 创建3D场景处理工作流定义
    /// </summary>
    private string CreateSceneProcessingWorkflowDefinition()
    {
        var definition = new
        {
            nodes = new[]
            {
                new { id = "start", type = "start", name = "场景数据接收", position = new { x = 100, y = 100 } },
                new { id = "validate-scene", type = "condition", name = "验证场景数据", position = new { x = 300, y = 100 } },
                new { id = "optimize-geometry", type = "action", name = "几何优化", position = new { x = 500, y = 50 } },
                new { id = "convert-format", type = "action", name = "格式转换", position = new { x = 500, y = 150 } },
                new { id = "generate-lods", type = "action", name = "生成LOD", position = new { x = 700, y = 100 } },
                new { id = "save-scene", type = "database", name = "保存场景", position = new { x = 900, y = 100 } },
                new { id = "end", type = "end", name = "处理完成", position = new { x = 1100, y = 100 } }
            },
            connections = new object[]
            {
                new { id = "c1", source = "start", target = "validate-scene" },
                new { id = "c2", source = "validate-scene", target = "optimize-geometry", sourceHandle = "true" },
                new { id = "c3", source = "validate-scene", target = "convert-format", sourceHandle = "false" },
                new { id = "c4", source = "optimize-geometry", target = "generate-lods" },
                new { id = "c5", source = "convert-format", target = "generate-lods" },
                new { id = "c6", source = "generate-lods", target = "save-scene" },
                new { id = "c7", source = "save-scene", target = "end" }
            }
        };

        return JsonSerializer.Serialize(definition);
    }

    /// <summary>
    /// 创建切片处理工作流定义
    /// </summary>
    private string CreateSlicingWorkflowDefinition()
    {
        var definition = new
        {
            nodes = new[]
            {
                new { id = "start", type = "start", name = "切片任务启动", position = new { x = 100, y = 100 } },
                new { id = "load-model", type = "action", name = "加载模型", position = new { x = 300, y = 100 } },
                new { id = "analyze-density", type = "action", name = "密度分析", position = new { x = 500, y = 100 } },
                new { id = "generate-slices", type = "action", name = "生成切片", position = new { x = 700, y = 100 } },
                new { id = "optimize-slices", type = "action", name = "切片优化", position = new { x = 900, y = 100 } },
                new { id = "save-results", type = "database", name = "保存结果", position = new { x = 1100, y = 100 } },
                new { id = "send-notification", type = "email", name = "发送完成通知", position = new { x = 1300, y = 100 } },
                new { id = "end", type = "end", name = "切片完成", position = new { x = 1500, y = 100 } }
            },
            connections = new object[]
            {
                new { id = "c1", source = "start", target = "load-model" },
                new { id = "c2", source = "load-model", target = "analyze-density" },
                new { id = "c3", source = "analyze-density", target = "generate-slices" },
                new { id = "c4", source = "generate-slices", target = "optimize-slices" },
                new { id = "c5", source = "optimize-slices", target = "save-results" },
                new { id = "c6", source = "save-results", target = "send-notification" },
                new { id = "c7", source = "send-notification", target = "end" }
            }
        };

        return JsonSerializer.Serialize(definition);
    }

    /// <summary>
    /// 合并模板配置和自定义配置
    /// </summary>
    private string MergeTemplateWithCustomConfig(string templateDefinition, JsonDocument customConfig)
    {
        // 这里可以实现更复杂的配置合并逻辑
        // 目前简单返回模板定义
        return templateDefinition;
    }
}

/// <summary>
/// 工作流模板DTO
/// </summary>
public class WorkflowTemplateDto
{
    /// <summary>
    /// 模板ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 模板名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 模板描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 模板分类
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 模板标签
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 模板图标
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// 工作流定义JSON
    /// </summary>
    public string Definition { get; set; } = string.Empty;

    /// <summary>
    /// 模板版本
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// 作者
    /// </summary>
    public string Author { get; set; } = "System";

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}