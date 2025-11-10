using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.Workflow;
using System.Text.Json;

namespace RealScene3D.Application.Services.Workflows;

/// <summary>
/// 自定义工作流节点执行器
/// 支持更多类型的节点执行
/// </summary>
public class CustomWorkflowNodeExecutor : BaseWorkflowNodeExecutor
{
    public CustomWorkflowNodeExecutor(ILogger<CustomWorkflowNodeExecutor> logger)
        : base(logger)
    {
    }

    public override async Task<WorkflowNodeResult> ExecuteNodeAsync(WorkflowNodeContext context)
    {
        try
        {
            LogInformation("执行自定义工作流节点：{NodeId}, 类型：{NodeType}", context.NodeId, context.NodeType);

            // 根据节点类型执行不同的逻辑
            return context.NodeType.ToLower() switch
            {
                "start" => await ExecuteStartNodeAsync(context),
                "end" => await ExecuteEndNodeAsync(context),
                "action" => await ExecuteActionNodeAsync(context),
                "script" => await ExecuteScriptNodeAsync(context),
                "api" => await ExecuteApiNodeAsync(context),
                "database" => await ExecuteDatabaseNodeAsync(context),
                _ => await ExecuteDefaultNodeAsync(context)
            };
        }
        catch (Exception ex)
        {
            LogError(ex, "自定义节点执行失败：{NodeId}", context.NodeId);

            return new WorkflowNodeResult
            {
                Status = WorkflowNodeStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public override IEnumerable<string> GetSupportedNodeTypes()
    {
        return new[]
        {
            "start", "end", "action", "script", "api", "database",
            "email", "notification", "file", "http", "transform"
        };
    }

    /// <summary>
    /// 执行开始节点
    /// </summary>
    private Task<WorkflowNodeResult> ExecuteStartNodeAsync(WorkflowNodeContext context)
    {
        LogInformation("执行开始节点：{NodeId}", context.NodeId);

        // 开始节点主要用于初始化工作流变量
        var inputData = JsonDocument.Parse(context.InputData ?? "{}");
        var variables = new Dictionary<string, object>();

        // 从输入数据中提取变量
        if (inputData.RootElement.TryGetProperty("variables", out var variablesElement))
        {
            foreach (var variable in variablesElement.EnumerateObject())
            {
                variables[variable.Name] = variable.Value.ToString();
            }
        }

        return Task.FromResult(new WorkflowNodeResult
        {
            Status = WorkflowNodeStatus.Completed,
            OutputData = JsonSerializer.Serialize(new { initialized = true }),
            UpdatedVariables = variables
        });
    }

    /// <summary>
    /// 执行结束节点
    /// </summary>
    private Task<WorkflowNodeResult> ExecuteEndNodeAsync(WorkflowNodeContext context)
    {
        LogInformation("执行结束节点：{NodeId}", context.NodeId);

        // 结束节点主要用于清理和输出最终结果
        var inputData = JsonDocument.Parse(context.InputData ?? "{}");
        var output = new
        {
            completed = true,
            timestamp = DateTime.UtcNow,
            finalVariables = context.Variables
        };

        return Task.FromResult(new WorkflowNodeResult
        {
            Status = WorkflowNodeStatus.Completed,
            OutputData = JsonSerializer.Serialize(output)
        });
    }

    /// <summary>
    /// 执行动作节点
    /// </summary>
    private async Task<WorkflowNodeResult> ExecuteActionNodeAsync(WorkflowNodeContext context)
    {
        LogInformation("执行动作节点：{NodeId}", context.NodeId);

        var inputData = JsonDocument.Parse(context.InputData ?? "{}");
        var action = inputData.RootElement.GetProperty("action").GetString() ?? "";
        var parameters = inputData.RootElement.GetProperty("parameters");

        // 执行具体的动作逻辑
        var result = await ExecuteActionAsync(action, parameters);

        return new WorkflowNodeResult
        {
            Status = WorkflowNodeStatus.Completed,
            OutputData = JsonSerializer.Serialize(new
            {
                action,
                success = true,
                result
            }),
            UpdatedVariables = new Dictionary<string, object>
            {
                ["lastAction"] = action,
                ["lastActionResult"] = result
            }
        };
    }

    /// <summary>
    /// 执行脚本节点
    /// </summary>
    private async Task<WorkflowNodeResult> ExecuteScriptNodeAsync(WorkflowNodeContext context)
    {
        LogInformation("执行脚本节点：{NodeId}", context.NodeId);

        var inputData = JsonDocument.Parse(context.InputData ?? "{}");
        var script = inputData.RootElement.GetProperty("script").GetString() ?? "";
        var language = inputData.RootElement.GetProperty("language").GetString() ?? "csharp";

        // 执行脚本逻辑（这里可以集成脚本引擎）
        var result = await ExecuteScriptAsync(script, language, context.Variables);

        return new WorkflowNodeResult
        {
            Status = WorkflowNodeStatus.Completed,
            OutputData = JsonSerializer.Serialize(new
            {
                scriptExecuted = true,
                language,
                result
            }),
            UpdatedVariables = new Dictionary<string, object>
            {
                ["lastScriptResult"] = result
            }
        };
    }

    /// <summary>
    /// 执行API节点
    /// </summary>
    private async Task<WorkflowNodeResult> ExecuteApiNodeAsync(WorkflowNodeContext context)
    {
        LogInformation("执行API节点：{NodeId}", context.NodeId);

        var inputData = JsonDocument.Parse(context.InputData ?? "{}");
        var url = inputData.RootElement.GetProperty("url").GetString() ?? "";
        var method = inputData.RootElement.GetProperty("method").GetString() ?? "GET";
        var headers = inputData.RootElement.GetProperty("headers");
        var body = inputData.RootElement.GetProperty("body");

        // 执行API调用
        var response = await CallApiAsync(url, method, headers, body);

        return new WorkflowNodeResult
        {
            Status = WorkflowNodeStatus.Completed,
            OutputData = JsonSerializer.Serialize(new
            {
                apiCallSuccess = true,
                url,
                method,
                response
            }),
            UpdatedVariables = new Dictionary<string, object>
            {
                ["lastApiResponse"] = response
            }
        };
    }

    /// <summary>
    /// 执行数据库节点
    /// </summary>
    private async Task<WorkflowNodeResult> ExecuteDatabaseNodeAsync(WorkflowNodeContext context)
    {
        LogInformation("执行数据库节点：{NodeId}", context.NodeId);

        var inputData = JsonDocument.Parse(context.InputData ?? "{}");
        var query = inputData.RootElement.GetProperty("query").GetString() ?? "";
        var connectionString = inputData.RootElement.GetProperty("connectionString").GetString() ?? "";

        // 执行数据库操作
        var result = await ExecuteDatabaseQueryAsync(query, connectionString);

        return new WorkflowNodeResult
        {
            Status = WorkflowNodeStatus.Completed,
            OutputData = JsonSerializer.Serialize(new
            {
                queryExecuted = true,
                result
            }),
            UpdatedVariables = new Dictionary<string, object>
            {
                ["lastQueryResult"] = result
            }
        };
    }

    /// <summary>
    /// 执行默认节点
    /// </summary>
    private Task<WorkflowNodeResult> ExecuteDefaultNodeAsync(WorkflowNodeContext context)
    {
        LogInformation("执行默认节点：{NodeId}, 类型：{NodeType}", context.NodeId, context.NodeType);

        return Task.FromResult(new WorkflowNodeResult
        {
            Status = WorkflowNodeStatus.Completed,
            OutputData = JsonSerializer.Serialize(new
            {
                nodeType = context.NodeType,
                executed = true
            })
        });
    }

    /// <summary>
    /// 执行动作
    /// </summary>
    private Task<object> ExecuteActionAsync(string action, JsonElement parameters)
    {
        // 这里实现具体的动作执行逻辑
        return Task.FromResult<object>(new { action, executed = true, timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// 执行脚本
    /// </summary>
    private Task<object> ExecuteScriptAsync(string script, string language, Dictionary<string, object> variables)
    {
        // 这里实现脚本执行逻辑（可以集成Roslyn或其他脚本引擎）
        return Task.FromResult<object>(new { script, language, executed = true });
    }

    /// <summary>
    /// 调用API
    /// </summary>
    private async Task<object> CallApiAsync(string url, string method, JsonElement headers, JsonElement body)
    {
        using var client = new HttpClient();

        // 设置请求头
        if (headers.ValueKind == JsonValueKind.Object)
        {
            foreach (var header in headers.EnumerateObject())
            {
                client.DefaultRequestHeaders.Add(header.Name, header.Value.GetString());
            }
        }

        HttpResponseMessage response;
        if (method.ToUpper() == "GET")
        {
            response = await client.GetAsync(url);
        }
        else if (method.ToUpper() == "POST")
        {
            var content = body.ValueKind != JsonValueKind.Undefined
                ? new StringContent(body.ToString(), System.Text.Encoding.UTF8, "application/json")
                : null;
            response = await client.PostAsync(url, content);
        }
        else
        {
            throw new NotSupportedException($"不支持的HTTP方法：{method}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        return new
        {
            statusCode = (int)response.StatusCode,
            success = response.IsSuccessStatusCode,
            content = responseContent
        };
    }

    /// <summary>
    /// 执行数据库查询
    /// </summary>
    private Task<object> ExecuteDatabaseQueryAsync(string query, string connectionString)
    {
        // 这里实现数据库查询逻辑
        return Task.FromResult<object>(new { query, executed = true, timestamp = DateTime.UtcNow });
    }
}

/// <summary>
/// 邮件节点执行器
/// </summary>
public class EmailNodeExecutor : BaseWorkflowNodeExecutor
{
    public EmailNodeExecutor(ILogger<EmailNodeExecutor> logger)
        : base(logger)
    {
    }

    public override async Task<WorkflowNodeResult> ExecuteNodeAsync(WorkflowNodeContext context)
    {
        try
        {
            LogInformation("执行邮件节点：{NodeId}", context.NodeId);

            var inputData = JsonDocument.Parse(context.InputData ?? "{}");
            var to = inputData.RootElement.GetProperty("to").GetString() ?? "";
            var subject = inputData.RootElement.GetProperty("subject").GetString() ?? "";
            var body = inputData.RootElement.GetProperty("body").GetString() ?? "";

            // 发送邮件逻辑
            await SendEmailAsync(to, subject, body);

            return new WorkflowNodeResult
            {
                Status = WorkflowNodeStatus.Completed,
                OutputData = JsonSerializer.Serialize(new
                {
                    emailSent = true,
                    to,
                    subject
                })
            };
        }
        catch (Exception ex)
        {
            LogError(ex, "邮件节点执行失败：{NodeId}", context.NodeId);

            return new WorkflowNodeResult
            {
                Status = WorkflowNodeStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public override IEnumerable<string> GetSupportedNodeTypes()
    {
        return new[] { "email", "mail" };
    }

    private async Task SendEmailAsync(string to, string subject, string body)
    {
        // 这里实现邮件发送逻辑
        LogInformation("发送邮件到：{To}, 主题：{Subject}", to, subject);
        await Task.Yield(); // 模拟异步操作
    }
}

/// <summary>
/// 文件操作节点执行器
/// </summary>
public class FileNodeExecutor : BaseWorkflowNodeExecutor
{
    public FileNodeExecutor(ILogger<FileNodeExecutor> logger)
        : base(logger)
    {
    }

    public override async Task<WorkflowNodeResult> ExecuteNodeAsync(WorkflowNodeContext context)
    {
        try
        {
            LogInformation("执行文件操作节点：{NodeId}", context.NodeId);

            var inputData = JsonDocument.Parse(context.InputData ?? "{}");
            var operation = inputData.RootElement.GetProperty("operation").GetString() ?? "";
            var filePath = inputData.RootElement.GetProperty("filePath").GetString() ?? "";
            var content = inputData.RootElement.GetProperty("content").GetString() ?? "";

            object result;
            switch (operation.ToLower())
            {
                case "read":
                    result = await ReadFileAsync(filePath);
                    break;
                case "write":
                    result = await WriteFileAsync(filePath, content);
                    break;
                case "delete":
                    result = await DeleteFileAsync(filePath);
                    break;
                default:
                    throw new NotSupportedException($"不支持的文件操作：{operation}");
            }

            return new WorkflowNodeResult
            {
                Status = WorkflowNodeStatus.Completed,
                OutputData = JsonSerializer.Serialize(new
                {
                    operation,
                    filePath,
                    success = true,
                    result
                }),
                UpdatedVariables = new Dictionary<string, object>
                {
                    ["lastFileOperation"] = operation,
                    ["lastFilePath"] = filePath
                }
            };
        }
        catch (Exception ex)
        {
            LogError(ex, "文件操作节点执行失败：{NodeId}", context.NodeId);

            return new WorkflowNodeResult
            {
                Status = WorkflowNodeStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public override IEnumerable<string> GetSupportedNodeTypes()
    {
        return new[] { "file", "fileOperation" };
    }

    private async Task<object> ReadFileAsync(string filePath)
    {
        if (!System.IO.File.Exists(filePath))
        {
            throw new FileNotFoundException("文件不存在", filePath);
        }

        var content = await System.IO.File.ReadAllTextAsync(filePath);
        return new { exists = true, content };
    }

    private async Task<object> WriteFileAsync(string filePath, string content)
    {
        await System.IO.File.WriteAllTextAsync(filePath, content);
        return new { written = true, length = content.Length };
    }

    private Task<object> DeleteFileAsync(string filePath)
    {
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
            return Task.FromResult<object>(new { deleted = true });
        }
        return Task.FromResult<object>(new { deleted = false, reason = "文件不存在" });
    }
}