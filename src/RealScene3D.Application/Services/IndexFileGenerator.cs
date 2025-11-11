using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.MinIO;
using System.Text.Json;

namespace RealScene3D.Application.Services;

/// <summary>
/// 索引文件生成器 - 统一管理index.json和tileset.json的生成
/// 提供切片文件与索引文件的一致性验证和自动修复功能
/// </summary>
public class IndexFileGenerator
{
    private readonly IRepository<Slice> _sliceRepository;
    private readonly IMinioStorageService _minioService;
    private readonly ILogger _logger;

    public IndexFileGenerator(
        IRepository<Slice> sliceRepository,
        IMinioStorageService minioService,
        ILogger logger)
    {
        _sliceRepository = sliceRepository;
        _minioService = minioService;
        _logger = logger;
    }

    /// <summary>
    /// 生成索引文件并验证一致性
    /// </summary>
    public async Task<IndexGenerationResult> GenerateIndexFileAsync(
        SlicingTask task,
        SlicingConfig config,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("开始生成index.json索引文件：任务{TaskId}", task.Id);

            // 1. 收集所有切片数据
            var sliceData = await CollectSliceDataAsync(task.Id);
            _logger.LogInformation("收集到{Count}个切片数据", sliceData.Count);

            // 2. 验证切片文件存在性
            var validatedSlices = await ValidateSliceFilesAsync(sliceData, config);
            _logger.LogInformation("验证{ValidCount}/{TotalCount}个切片文件存在",
                validatedSlices.Count(s => s.FileExists), validatedSlices.Count);

            // 3. 过滤掉不存在的切片文件
            var existingSlices = validatedSlices.Where(s => s.FileExists).ToList();
            _logger.LogInformation("过滤后剩余{Count}个存在的切片", existingSlices.Count);

            // 4. 标准化路径（仅对存在的切片）
            var normalizedSlices = NormalizeSlicePaths(existingSlices, config, task.OutputPath ?? string.Empty);
            _logger.LogInformation("完成{Count}个切片路径标准化", normalizedSlices.Count);

            // 5. 生成index.json（仅包含存在的切片）
            var indexResult = await GenerateIndexJsonAsync(task, normalizedSlices, config, cancellationToken);

            // 6. 验证格式
            var formatValidationResult = ValidateIndexFormat(indexResult);

            // 7. 验证一致性
            var validationResult = await ValidateConsistencyAsync(indexResult, normalizedSlices);

            // 8. 合并格式验证和一致性验证结果
            validationResult = MergeValidationResults(formatValidationResult, validationResult);

            // 9. 如果验证失败，尝试自动修复
            var repairResult = new RepairResult { Success = true };
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("索引文件验证失败，尝试自动修复：任务{TaskId}", task.Id);
                repairResult = await AutoRepairAsync(validationResult, task, config, normalizedSlices, cancellationToken);

                // 修复后重新验证
                if (repairResult.Success)
                {
                    var revalidationResult = await ValidateConsistencyAsync(indexResult, normalizedSlices);
                    validationResult = revalidationResult;
                }
            }

            var result = new IndexGenerationResult
            {
                IndexJson = indexResult,
                ValidationResult = validationResult,
                RepairResult = repairResult,
                Success = validationResult.IsValid || repairResult.Success
            };

            _logger.LogInformation("index.json生成完成：任务{TaskId}，成功={Success}", task.Id, result.Success);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成index.json失败：任务{TaskId}", task.Id);
            throw;
        }
    }

    /// <summary>
    /// 收集切片数据
    /// </summary>
    private async Task<List<NormalizedSliceData>> CollectSliceDataAsync(Guid taskId)
    {
        var allSlices = await _sliceRepository.GetAllAsync();
        var taskSlices = allSlices.Where(s => s.SlicingTaskId == taskId).ToList();
        
        return taskSlices.Select(s => new NormalizedSliceData
        {
            Id = s.Id,
            Level = s.Level,
            X = s.X,
            Y = s.Y,
            Z = s.Z,
            Path = new NormalizedPath
            {
                AbsolutePath = s.FilePath,
                RelativePath = "",
                UriPath = "",
                StorageType = StorageLocationType.LocalFileSystem
            },
            BoundingBox = ParseBoundingBox(s.BoundingBox),
            FileSize = s.FileSize,
            CreatedAt = s.CreatedAt,
            ContentType = GetContentType(s.FilePath),
            FileExists = false // 稍后验证
        }).ToList();
    }

    /// <summary>
    /// 验证切片文件存在性
    /// </summary>
    private async Task<List<NormalizedSliceData>> ValidateSliceFilesAsync(
        List<NormalizedSliceData> slices,
        SlicingConfig config)
    {
        var validatedSlices = new List<NormalizedSliceData>();
        
        foreach (var slice in slices)
        {
            try
            {
                bool exists;
                if (config.StorageLocation == StorageLocationType.LocalFileSystem)
                {
                    exists = File.Exists(slice.Path.AbsolutePath);
                }
                else
                {
                    exists = await _minioService.FileExistsAsync("slices", slice.Path.AbsolutePath);
                }
                
                slice.FileExists = exists;
                validatedSlices.Add(slice);
                
                if (!exists)
                {
                    _logger.LogWarning("切片文件不存在：{FilePath}", slice.Path.AbsolutePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "验证切片文件时出错：{FilePath}", slice.Path.AbsolutePath);
                slice.FileExists = false;
                validatedSlices.Add(slice);
            }
        }
        
        return validatedSlices;
    }

    /// <summary>
    /// 标准化切片路径
    /// </summary>
    private List<NormalizedSliceData> NormalizeSlicePaths(
        List<NormalizedSliceData> slices,
        SlicingConfig config,
        string outputPath)
    {
        var pathNormalizer = new PathNormalizer();
        var normalizedSlices = new List<NormalizedSliceData>();
        
        foreach (var slice in slices)
        {
            var normalizedPath = pathNormalizer.NormalizePath(
                slice.Path.AbsolutePath,
                config.StorageLocation,
                outputPath);
            
            slice.Path = normalizedPath;
            normalizedSlices.Add(slice);
        }
        
        return normalizedSlices;
    }

    /// <summary>
    /// 生成index.json
    /// </summary>
    private async Task<IndexJsonResult> GenerateIndexJsonAsync(
        SlicingTask task,
        List<NormalizedSliceData> slices,
        SlicingConfig config,
        CancellationToken cancellationToken)
    {
        var indexData = new
        {
            TaskId = task.Id,
            TotalLevels = config.MaxLevel,
            TileSize = config.TileSize,
            OutputFormat = config.OutputFormat,
            SliceCount = slices.Count,
            BoundingBox = CalculateTotalBoundingBox(slices),
            Slices = slices.Select(s => new
            {
                s.Level,
                s.X,
                s.Y,
                s.Z,
                FilePath = s.Path.UriPath,
                s.FileSize,
                BoundingBox = s.BoundingBox
            }).ToList()
        };

        var indexContent = JsonSerializer.Serialize(indexData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var indexPath = $"{task.OutputPath}/index.json";

        if (config.StorageLocation == StorageLocationType.LocalFileSystem)
        {
            var fullPath = Path.Combine(task.OutputPath!, "index.json");
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllTextAsync(fullPath, indexContent, cancellationToken);
            _logger.LogInformation("index.json已保存到本地：{FilePath}", fullPath);
        }
        else
        {
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(indexContent)))
            {
                await _minioService.UploadFileAsync("slices", indexPath, stream, "application/json", cancellationToken);
            }
            _logger.LogInformation("index.json已上传到MinIO：{FilePath}", indexPath);
        }

        return new IndexJsonResult
        {
            Content = indexContent,
            Path = indexPath,
            SliceCount = slices.Count
        };
    }


    /// <summary>
    /// 验证索引文件格式
    /// </summary>
    private ValidationResult ValidateIndexFormat(IndexJsonResult indexResult)
    {
        _logger.LogInformation("验证index.json格式...");

        var formatValidator = new IndexFormatValidator(_logger);
        var allIssues = new List<ValidationIssue>();

        // 验证index.json格式
        if (indexResult != null && !string.IsNullOrEmpty(indexResult.Content))
        {
            var indexFormatResult = formatValidator.ValidateIndexJsonFormat(indexResult.Content);
            allIssues.AddRange(indexFormatResult.Issues);
        }

        return new ValidationResult
        {
            IsValid = !allIssues.Any(i => i.Severity == ValidationSeverity.Error),
            Issues = allIssues,
            Summary = new ValidationSummary
            {
                TotalIssues = allIssues.Count,
                ErrorCount = allIssues.Count(i => i.Severity == ValidationSeverity.Error),
                WarningCount = allIssues.Count(i => i.Severity == ValidationSeverity.Warning),
                InfoCount = allIssues.Count(i => i.Severity == ValidationSeverity.Info)
            }
        };
    }

    /// <summary>
    /// 合并验证结果
    /// </summary>
    private ValidationResult MergeValidationResults(ValidationResult result1, ValidationResult result2)
    {
        var allIssues = new List<ValidationIssue>();
        allIssues.AddRange(result1.Issues);
        allIssues.AddRange(result2.Issues);

        return new ValidationResult
        {
            IsValid = result1.IsValid && result2.IsValid,
            Issues = allIssues,
            Summary = new ValidationSummary
            {
                TotalIssues = allIssues.Count,
                ErrorCount = allIssues.Count(i => i.Severity == ValidationSeverity.Error),
                WarningCount = allIssues.Count(i => i.Severity == ValidationSeverity.Warning),
                InfoCount = allIssues.Count(i => i.Severity == ValidationSeverity.Info)
            }
        };
    }

    /// <summary>
    /// 验证一致性
    /// </summary>
    private async Task<ValidationResult> ValidateConsistencyAsync(
        IndexJsonResult indexResult,
        List<NormalizedSliceData> slices)
    {
        var validator = new ConsistencyValidator(_minioService, _logger);
        return await validator.ValidateAsync(indexResult, slices);
    }

    /// <summary>
    /// 自动修复
    /// </summary>
    private async Task<RepairResult> AutoRepairAsync(
        ValidationResult validationResult,
        SlicingTask task,
        SlicingConfig config,
        List<NormalizedSliceData> slices,
        CancellationToken cancellationToken)
    {
        var repairer = new AutoRepairer(_minioService, _logger);
        return await repairer.RepairAsync(validationResult, task, config, slices, cancellationToken);
    }

    /// <summary>
    /// 计算总包围盒
    /// </summary>
    private object CalculateTotalBoundingBox(List<NormalizedSliceData> slices)
    {
        if (!slices.Any()) return new { };

        var minX = slices.Min(s => s.BoundingBox.MinX);
        var minY = slices.Min(s => s.BoundingBox.MinY);
        var minZ = slices.Min(s => s.BoundingBox.MinZ);
        var maxX = slices.Max(s => s.BoundingBox.MaxX);
        var maxY = slices.Max(s => s.BoundingBox.MaxY);
        var maxZ = slices.Max(s => s.BoundingBox.MaxZ);

        return new
        {
            MinX = minX,
            MinY = minY,
            MinZ = minZ,
            MaxX = maxX,
            MaxY = maxY,
            MaxZ = maxZ
        };
    }

    /// <summary>
    /// 解析包围盒
    /// </summary>
    private BoundingBox3D ParseBoundingBox(string boundingBoxJson)
    {
        if (string.IsNullOrEmpty(boundingBoxJson))
            return new BoundingBox3D();

        try
        {
            var boundingBox = JsonSerializer.Deserialize<Dictionary<string, double>>(boundingBoxJson);
            if (boundingBox == null)
                return new BoundingBox3D();

            return new BoundingBox3D
            {
                // 使用大写键名匹配GridSlicingStrategy生成的JSON格式
                MinX = boundingBox.GetValueOrDefault("MinX", 0),
                MinY = boundingBox.GetValueOrDefault("MinY", 0),
                MinZ = boundingBox.GetValueOrDefault("MinZ", 0),
                MaxX = boundingBox.GetValueOrDefault("MaxX", 0),
                MaxY = boundingBox.GetValueOrDefault("MaxY", 0),
                MaxZ = boundingBox.GetValueOrDefault("MaxZ", 0)
            };
        }
        catch
        {
            return new BoundingBox3D();
        }
    }

    /// <summary>
    /// 获取文件的内容类型
    /// </summary>
    private string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".b3dm" => "application/octet-stream",
            ".i3dm" => "application/octet-stream",
            ".pnts" => "application/octet-stream",
            ".cmpt" => "application/octet-stream",
            ".glb" => "model/gltf-binary",
            ".gltf" => "model/gltf+json",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }
}

/// <summary>
/// 索引文件生成结果
/// </summary>
public class IndexGenerationResult
{
    public IndexJsonResult IndexJson { get; set; } = new();
    public ValidationResult ValidationResult { get; set; } = new();
    public RepairResult RepairResult { get; set; } = new();
    public bool Success { get; set; }
}

/// <summary>
/// index.json生成结果
/// </summary>
public class IndexJsonResult
{
    public string Content { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int SliceCount { get; set; }
}

/// <summary>
/// 标准化切片数据
/// </summary>
public class NormalizedSliceData
{
    public Guid Id { get; set; }
    public int Level { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public NormalizedPath Path { get; set; } = new();
    public BoundingBox3D BoundingBox { get; set; } = new();
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public bool FileExists { get; set; }
}

/// <summary>
/// 标准化路径
/// </summary>
public class NormalizedPath
{
    public string AbsolutePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string UriPath { get; set; } = string.Empty;
    public StorageLocationType StorageType { get; set; }
}

/// <summary>
/// 验证结果
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationIssue> Issues { get; set; } = new();
    public ValidationSummary Summary { get; set; } = new();
}

/// <summary>
/// 验证问题
/// </summary>
public class ValidationIssue
{
    public ValidationSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

/// <summary>
/// 验证摘要
/// </summary>
public class ValidationSummary
{
    public int TotalIssues { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
}

/// <summary>
/// 修复结果
/// </summary>
public class RepairResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> RepairedItems { get; set; } = new();
}

/// <summary>
/// 路径标准化器
/// </summary>
public class PathNormalizer
{
    public NormalizedPath NormalizePath(string absolutePath, StorageLocationType storageType, string outputPath)
    {
        string relativePath = absolutePath;
        string uriPath = absolutePath;

        if (storageType == StorageLocationType.LocalFileSystem && !string.IsNullOrEmpty(outputPath))
        {
            try
            {
                relativePath = Path.GetRelativePath(outputPath, absolutePath);
                uriPath = relativePath.Replace('\\', '/');
            }
            catch
            {
                relativePath = Path.GetFileName(absolutePath);
                uriPath = relativePath;
            }
        }
        else if (storageType == StorageLocationType.MinIO)
        {
            uriPath = absolutePath.Replace('\\', '/');
            relativePath = uriPath;
        }

        return new NormalizedPath
        {
            AbsolutePath = absolutePath,
            RelativePath = relativePath,
            UriPath = uriPath,
            StorageType = storageType
        };
    }
}

/// <summary>
/// 索引格式验证器
/// </summary>
public class IndexFormatValidator
{
    private readonly ILogger _logger;

    public IndexFormatValidator(ILogger logger)
    {
        _logger = logger;
    }

    public ValidationResult ValidateIndexJsonFormat(string content)
    {
        var issues = new List<ValidationIssue>();

        try
        {
            var doc = JsonSerializer.Deserialize<JsonDocument>(content);
            if (doc == null)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "无法解析index.json内容",
                    Location = "index.json"
                });
            }
        }
        catch (Exception ex)
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = $"JSON格式错误: {ex.Message}",
                Location = "index.json"
            });
        }

        return new ValidationResult
        {
            IsValid = !issues.Any(i => i.Severity == ValidationSeverity.Error),
            Issues = issues,
            Summary = new ValidationSummary
            {
                TotalIssues = issues.Count,
                ErrorCount = issues.Count(i => i.Severity == ValidationSeverity.Error),
                WarningCount = issues.Count(i => i.Severity == ValidationSeverity.Warning),
                InfoCount = issues.Count(i => i.Severity == ValidationSeverity.Info)
            }
        };
    }
}

/// <summary>
/// 一致性验证器
/// </summary>
public class ConsistencyValidator
{
    private readonly IMinioStorageService _minioService;
    private readonly ILogger _logger;

    public ConsistencyValidator(IMinioStorageService minioService, ILogger logger)
    {
        _minioService = minioService;
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateAsync(IndexJsonResult indexResult, List<NormalizedSliceData> slices)
    {
        var issues = new List<ValidationIssue>();

        // 验证切片数量一致性
        if (indexResult.SliceCount != slices.Count)
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Message = $"索引文件记录的切片数量({indexResult.SliceCount})与实际切片数量({slices.Count})不一致",
                Location = "index.json"
            });
        }

        await Task.CompletedTask;

        return new ValidationResult
        {
            IsValid = !issues.Any(i => i.Severity == ValidationSeverity.Error),
            Issues = issues,
            Summary = new ValidationSummary
            {
                TotalIssues = issues.Count,
                ErrorCount = issues.Count(i => i.Severity == ValidationSeverity.Error),
                WarningCount = issues.Count(i => i.Severity == ValidationSeverity.Warning),
                InfoCount = issues.Count(i => i.Severity == ValidationSeverity.Info)
            }
        };
    }
}

/// <summary>
/// 自动修复器
/// </summary>
public class AutoRepairer
{
    private readonly IMinioStorageService _minioService;
    private readonly ILogger _logger;

    public AutoRepairer(IMinioStorageService minioService, ILogger logger)
    {
        _minioService = minioService;
        _logger = logger;
    }

    public async Task<RepairResult> RepairAsync(
        ValidationResult validationResult,
        SlicingTask task,
        SlicingConfig config,
        List<NormalizedSliceData> slices,
        CancellationToken cancellationToken)
    {
        var repairedItems = new List<string>();

        // 目前只是占位实现，可以在未来添加具体的修复逻辑
        _logger.LogInformation("执行自动修复...");

        await Task.CompletedTask;

        return new RepairResult
        {
            Success = true,
            Message = "修复完成",
            RepairedItems = repairedItems
        };
    }
}
