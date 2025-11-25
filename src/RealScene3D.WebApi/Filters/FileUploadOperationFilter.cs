using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace RealScene3D.WebApi.Filters;

/// <summary>
/// Swagger操作过滤器，用于处理文件上传
/// 解决 [FromForm] IFormFile 参数的Swagger文档生成问题
/// </summary>
public class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // 查找所有 IFormFile 类型的参数
        var formFileParameters = context.ApiDescription.ParameterDescriptions
            .Where(p =>
                p.Type == typeof(IFormFile) ||
                p.Type == typeof(IFormFileCollection) ||
                (p.ModelMetadata?.ModelType == typeof(IFormFile)) ||
                (p.ModelMetadata?.ModelType == typeof(IFormFileCollection)))
            .ToList();

        if (!formFileParameters.Any())
            return;

        // 检查方法是否有 [Consumes("multipart/form-data")] 属性
        var hasMultipartConsumes = context.MethodInfo
            .GetCustomAttributes<Microsoft.AspNetCore.Mvc.ConsumesAttribute>()
            .Any(x => x.ContentTypes.Contains("multipart/form-data"));

        if (!hasMultipartConsumes)
            return;

        // 构建 multipart/form-data 请求体
        var properties = new Dictionary<string, OpenApiSchema>();
        var requiredParams = new HashSet<string>();

        foreach (var param in formFileParameters)
        {
            properties[param.Name] = new OpenApiSchema
            {
                Type = "string",
                Format = "binary",
                Description = param.ModelMetadata?.Description
            };

            // 如果参数不可为空，则标记为必需
            if (!param.IsRequired && param.ModelMetadata?.IsRequired == true)
            {
                requiredParams.Add(param.Name);
            }
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = properties,
                        Required = requiredParams
                    }
                }
            }
        };

        // 移除原始的参数定义，避免重复
        foreach (var param in formFileParameters)
        {
            var paramToRemove = operation.Parameters
                .FirstOrDefault(p => p.Name == param.Name);
            if (paramToRemove != null)
            {
                operation.Parameters.Remove(paramToRemove);
            }
        }
    }
}
