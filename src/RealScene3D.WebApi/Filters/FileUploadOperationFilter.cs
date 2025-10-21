using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RealScene3D.WebApi.Filters;

/// <summary>
/// Swagger操作过滤器，用于处理文件上传
/// 解决 [FromForm] IFormFile 参数的Swagger文档生成问题
/// </summary>
public class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var formFileParameters = context.ApiDescription.ParameterDescriptions
            .Where(p => p.ModelMetadata?.ModelType == typeof(IFormFile))
            .ToList();

        if (!formFileParameters.Any())
            return;

        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = formFileParameters.ToDictionary(
                            p => p.Name,
                            p => new OpenApiSchema
                            {
                                Type = "string",
                                Format = "binary"
                            }
                        ),
                        Required = formFileParameters
                            .Select(p => p.Name)
                            .ToHashSet()
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
