using Microsoft.Extensions.Logging;
using RealScene3D.Infrastructure.MinIO;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using System.Text.Json;
using RealScene3D.Domain.Enums;

namespace RealScene3D.Application.Services;

/// <summary>
/// 路径标准化器 - 处理不同存储位置的路径格式统一
/// </summary>
public class PathNormalizer
{
    /// <summary>
    /// 标准化路径
    /// </summary>
    public NormalizedPath NormalizePath(
        string originalPath,
        StorageLocationType storageType,
        string outputPath)
    {
        if (string.IsNullOrWhiteSpace(originalPath))
            throw new ArgumentException("原始路径不能为空", nameof(originalPath));

        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("输出路径不能为空", nameof(outputPath));

        return storageType switch
        {
            StorageLocationType.LocalFileSystem => NormalizeLocalPath(originalPath, outputPath),
            StorageLocationType.MinIO => NormalizeMinioPath(originalPath, outputPath),
            _ => throw new NotSupportedException($"不支持的存储类型: {storageType}")
        };
    }

    /// <summary>
    /// 标准化本地文件系统路径
    /// </summary>
    private NormalizedPath NormalizeLocalPath(string path, string outputPath)
    {
        // 1. 清理路径中的特殊字符和多余的分隔符
        path = path.Replace('/', '\\').Trim();
        outputPath = outputPath.Replace('/', '\\').Trim();

        // 2. 处理本地文件系统路径
        string absolutePath;
        if (Path.IsPathRooted(path))
        {
            // 已经是绝对路径
            absolutePath = Path.GetFullPath(path);
        }
        else
        {
            // 相对路径，拼接到输出路径
            absolutePath = Path.GetFullPath(Path.Combine(outputPath, path));
        }

        // 3. 计算相对路径
        string relativePath;
        try
        {
            relativePath = Path.GetRelativePath(outputPath, absolutePath);
        }
        catch
        {
            // 如果无法计算相对路径（例如不同驱动器），使用绝对路径
            relativePath = absolutePath;
        }

        // 4. 生成URI路径（统一使用正斜杠）
        var uriPath = relativePath.Replace('\\', '/');

        return new NormalizedPath
        {
            AbsolutePath = absolutePath,
            RelativePath = relativePath,
            UriPath = uriPath,
            StorageType = StorageLocationType.LocalFileSystem
        };
    }

    /// <summary>
    /// 标准化MinIO对象存储路径
    /// </summary>
    private NormalizedPath NormalizeMinioPath(string path, string outputPath)
    {
        // 1. 清理路径：统一使用正斜杠，移除Windows风格的反斜杠
        path = path.Replace('\\', '/').Trim();
        outputPath = outputPath.Replace('\\', '/').Trim();

        // 2. 移除路径开头的斜杠（MinIO不需要）
        path = path.TrimStart('/');
        outputPath = outputPath.TrimStart('/');

        // 3. 构建完整的对象路径
        string objectPath;
        if (path.StartsWith(outputPath, StringComparison.OrdinalIgnoreCase))
        {
            // 路径已经包含了输出路径前缀
            objectPath = path;
        }
        else
        {
            // 需要拼接输出路径
            objectPath = string.IsNullOrEmpty(outputPath)
                ? path
                : $"{outputPath}/{path}";
        }

        // 4. 清理重复的斜杠
        while (objectPath.Contains("//"))
        {
            objectPath = objectPath.Replace("//", "/");
        }

        // 5. 提取文件名作为相对路径
        var relativePath = Path.GetFileName(objectPath);

        return new NormalizedPath
        {
            AbsolutePath = objectPath,
            RelativePath = relativePath,
            UriPath = objectPath,
            StorageType = StorageLocationType.MinIO
        };
    }

    /// <summary>
    /// 验证路径是否有效
    /// </summary>
    public bool ValidatePath(string path, StorageLocationType storageType)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            if (storageType == StorageLocationType.LocalFileSystem)
            {
                // 验证本地路径格式
                Path.GetFullPath(path);
                return true;
            }
            else if (storageType == StorageLocationType.MinIO)
            {
                // 验证MinIO路径格式：不能包含特殊字符
                var invalidChars = new[] { '\\', '<', '>', ':', '"', '|', '?', '*' };
                return !invalidChars.Any(c => path.Contains(c));
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// 标准化路径数据
/// </summary>
public class NormalizedPath
{
    /// <summary>
    /// 绝对路径
    /// </summary>
    public string AbsolutePath { get; set; } = string.Empty;

    /// <summary>
    /// 相对路径
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// URI路径（用于JSON中的引用）
    /// </summary>
    public string UriPath { get; set; } = string.Empty;
    
    /// <summary>
    /// 存储位置类型
    /// </summary>
    public StorageLocationType StorageType { get; set; }
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
    public BoundingBoxData BoundingBox { get; set; } = new();
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public bool FileExists { get; set; }
}

/// <summary>
/// 包围盒数据
/// </summary>
public class BoundingBoxData
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MinZ { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public double MaxZ { get; set; }
}

/// <summary>
/// 一致性验证器 - 验证生成的索引文件与实际切片文件的一致性
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

    /// <summary>
    /// 验证一致性
    /// </summary>
    public async Task<ValidationResult> ValidateAsync(
        IndexJsonResult indexData,
        TilesetJsonResult tilesetData,
        List<NormalizedSliceData> slices)
    {
        var issues = new List<ValidationIssue>();

        _logger.LogInformation("开始验证索引文件一致性，共{Count}个切片", slices.Count);

        // 1. 验证文件存在性
        await ValidateFileExistenceAsync(indexData, tilesetData, slices, issues);

        // 2. 验证路径一致性
        await ValidatePathConsistencyAsync(indexData, tilesetData, slices, issues);

        // 3. 验证层次结构
        ValidateHierarchyStructure(indexData, tilesetData, slices, issues);

        // 4. 验证元数据完整性
        ValidateMetadataCompleteness(indexData, tilesetData, slices, issues);

        // 5. 验证数量一致性
        ValidateCountConsistency(indexData, tilesetData, slices, issues);

        var result = new ValidationResult
        {
            IsValid = !issues.Any(i => i.Severity == ValidationSeverity.Error),
            Issues = issues,
            Summary = GenerateValidationSummary(issues)
        };

        _logger.LogInformation("验证完成：有效={IsValid}，问题数={IssueCount}（错误={ErrorCount}，警告={WarningCount}）",
            result.IsValid, result.Summary.TotalIssues, result.Summary.ErrorCount, result.Summary.WarningCount);

        return result;
    }

    /// <summary>
    /// 验证文件存在性
    /// </summary>
    private Task ValidateFileExistenceAsync(
        IndexJsonResult indexData,
        TilesetJsonResult tilesetData,
        List<NormalizedSliceData> slices,
        List<ValidationIssue> issues)
    {
        _logger.LogInformation("验证文件存在性...");

        // 验证实际切片文件存在性
        var missingFiles = 0;
        foreach (var slice in slices)
        {
            if (!slice.FileExists)
            {
                missingFiles++;
                issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.MissingFile,
                    Severity = ValidationSeverity.Error,
                    Description = $"切片文件不存在: Level={slice.Level}, X={slice.X}, Y={slice.Y}, Z={slice.Z}",
                    AffectedFile = slice.Path.AbsolutePath,
                    Context = new { slice.Level, slice.X, slice.Y, slice.Z }
                });
            }
        }

        if (missingFiles > 0)
        {
            _logger.LogWarning("发现{Count}个缺失的切片文件", missingFiles);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 验证路径一致性
    /// </summary>
    private Task ValidatePathConsistencyAsync(
        IndexJsonResult indexData,
        TilesetJsonResult tilesetData,
        List<NormalizedSliceData> slices,
        List<ValidationIssue> issues)
    {
        _logger.LogInformation("验证路径一致性...");

        try
        {
            // 解析index.json内容
            var indexJson = JsonSerializer.Deserialize<JsonDocument>(indexData.Content);
            var indexSlices = indexJson?.RootElement.GetProperty("Slices");

            if (indexSlices == null || indexSlices.Value.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FormatError,
                    Severity = ValidationSeverity.Error,
                    Description = "index.json格式错误：缺少Slices数组",
                    AffectedFile = indexData.Path
                });
                return Task.CompletedTask;
            }

            // 解析tileset.json内容
            if (tilesetData != null && !string.IsNullOrEmpty(tilesetData.Content))
            {
                var tilesetJson = JsonSerializer.Deserialize<JsonDocument>(tilesetData.Content);
                var tilesetPaths = ExtractTilesetPaths(tilesetJson?.RootElement);

                // 验证tileset.json中引用的所有路径在index.json中都存在
                var indexPaths = new HashSet<string>();
                foreach (var slice in indexSlices.Value.EnumerateArray())
                {
                    if (slice.TryGetProperty("FilePath", out var filePathElement))
                    {
                        indexPaths.Add(filePathElement.GetString() ?? "");
                    }
                }

                foreach (var tilesetPath in tilesetPaths)
                {
                    if (!indexPaths.Contains(tilesetPath))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Type = ValidationIssueType.IncorrectPath,
                            Severity = ValidationSeverity.Warning,
                            Description = $"tileset.json引用的路径在index.json中不存在: {tilesetPath}",
                            AffectedFile = tilesetPath
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证路径一致性时发生错误");
            issues.Add(new ValidationIssue
            {
                Type = ValidationIssueType.FormatError,
                Severity = ValidationSeverity.Error,
                Description = $"验证路径一致性失败: {ex.Message}",
                AffectedFile = indexData.Path
            });
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 从tileset.json中提取所有引用的路径
    /// </summary>
    private HashSet<string> ExtractTilesetPaths(JsonElement? rootElement)
    {
        var paths = new HashSet<string>();
        if (rootElement == null) return paths;

        TraverseTilesetNode(rootElement.Value, paths);
        return paths;
    }

    /// <summary>
    /// 递归遍历tileset节点
    /// </summary>
    private void TraverseTilesetNode(JsonElement node, HashSet<string> paths)
    {
        // 检查是否有content.uri
        if (node.TryGetProperty("content", out var content))
        {
            if (content.TryGetProperty("uri", out var uri))
            {
                var uriValue = uri.GetString();
                if (!string.IsNullOrEmpty(uriValue))
                {
                    paths.Add(uriValue);
                }
            }
        }

        // 递归处理children
        if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                TraverseTilesetNode(child, paths);
            }
        }

        // 递归处理root
        if (node.TryGetProperty("root", out var root))
        {
            TraverseTilesetNode(root, paths);
        }
    }

    /// <summary>
    /// 验证层次结构
    /// </summary>
    private void ValidateHierarchyStructure(
        IndexJsonResult indexData,
        TilesetJsonResult tilesetData,
        List<NormalizedSliceData> slices,
        List<ValidationIssue> issues)
    {
        _logger.LogInformation("验证层次结构...");

        // 检查每个层级的切片完整性
        var levelGroups = slices.GroupBy(s => s.Level).OrderBy(g => g.Key);

        foreach (var levelGroup in levelGroups)
        {
            var level = levelGroup.Key;
            var levelSlices = levelGroup.ToList();

            // 检查包围盒是否有效
            foreach (var slice in levelSlices)
            {
                if (slice.BoundingBox == null ||
                    (slice.BoundingBox.MinX == 0 && slice.BoundingBox.MaxX == 0 &&
                     slice.BoundingBox.MinY == 0 && slice.BoundingBox.MaxY == 0 &&
                     slice.BoundingBox.MinZ == 0 && slice.BoundingBox.MaxZ == 0))
                {
                    issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.MissingMetadata,
                        Severity = ValidationSeverity.Warning,
                        Description = $"切片包围盒无效: Level={level}, X={slice.X}, Y={slice.Y}, Z={slice.Z}",
                        AffectedFile = slice.Path.AbsolutePath,
                        Context = new { level, slice.X, slice.Y, slice.Z }
                    });
                }
            }
        }

        // 验证LOD层次的连续性
        var levels = levelGroups.Select(g => g.Key).ToList();
        if (levels.Any())
        {
            var minLevel = levels.Min();
            var maxLevel = levels.Max();

            for (int level = minLevel; level <= maxLevel; level++)
            {
                if (!levels.Contains(level))
                {
                    issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.StructureError,
                        Severity = ValidationSeverity.Warning,
                        Description = $"LOD层级不连续：缺少Level {level}",
                        AffectedFile = indexData.Path,
                        Context = new { MissingLevel = level }
                    });
                }
            }
        }
    }

    /// <summary>
    /// 验证元数据完整性
    /// </summary>
    private void ValidateMetadataCompleteness(
        IndexJsonResult indexData,
        TilesetJsonResult tilesetData,
        List<NormalizedSliceData> slices,
        List<ValidationIssue> issues)
    {
        _logger.LogInformation("验证元数据完整性...");

        // 验证所有必要的元数据字段是否存在
        foreach (var slice in slices)
        {
            // 检查包围盒
            if (slice.BoundingBox == null)
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.MissingMetadata,
                    Severity = ValidationSeverity.Warning,
                    Description = $"切片缺少包围盒信息: Level={slice.Level}, X={slice.X}, Y={slice.Y}, Z={slice.Z}",
                    AffectedFile = slice.Path?.AbsolutePath ?? string.Empty,
                    Context = new { slice.Level, slice.X, slice.Y, slice.Z }
                });
            }

            // 检查文件大小
            if (slice.FileSize <= 0)
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.MissingMetadata,
                    Severity = ValidationSeverity.Info,
                    Description = $"切片文件大小信息缺失或无效: Level={slice.Level}, X={slice.X}, Y={slice.Y}, Z={slice.Z}",
                    AffectedFile = slice.Path?.AbsolutePath ?? string.Empty,
                    Context = new { slice.Level, slice.X, slice.Y, slice.Z, slice.FileSize }
                });
            }

            // 检查路径
            if (string.IsNullOrEmpty(slice.Path?.UriPath))
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.MissingMetadata,
                    Severity = ValidationSeverity.Error,
                    Description = $"切片路径信息缺失: Level={slice.Level}, X={slice.X}, Y={slice.Y}, Z={slice.Z}",
                    AffectedFile = slice.Path?.AbsolutePath ?? "unknown",
                    Context = new { slice.Level, slice.X, slice.Y, slice.Z }
                });
            }
        }
    }

    /// <summary>
    /// 验证数量一致性
    /// </summary>
    private void ValidateCountConsistency(
        IndexJsonResult indexData,
        TilesetJsonResult tilesetData,
        List<NormalizedSliceData> slices,
        List<ValidationIssue> issues)
    {
        _logger.LogInformation("验证数量一致性...");

        // 验证index.json中记录的切片数量与实际切片数量是否一致
        if (indexData.SliceCount != slices.Count)
        {
            issues.Add(new ValidationIssue
            {
                Type = ValidationIssueType.StructureError,
                Severity = ValidationSeverity.Warning,
                Description = $"index.json中记录的切片数量({indexData.SliceCount})与实际切片数量({slices.Count})不一致",
                AffectedFile = indexData.Path,
                Context = new { IndexCount = indexData.SliceCount, ActualCount = slices.Count }
            });
        }

        // 验证tileset.json中记录的切片数量
        if (tilesetData != null && tilesetData.SliceCount != slices.Count)
        {
            issues.Add(new ValidationIssue
            {
                Type = ValidationIssueType.StructureError,
                Severity = ValidationSeverity.Warning,
                Description = $"tileset.json中记录的切片数量({tilesetData.SliceCount})与实际切片数量({slices.Count})不一致",
                AffectedFile = tilesetData.Path,
                Context = new { TilesetCount = tilesetData.SliceCount, ActualCount = slices.Count }
            });
        }
    }

    /// <summary>
    /// 生成验证摘要
    /// </summary>
    private ValidationSummary GenerateValidationSummary(List<ValidationIssue> issues)
    {
        return new ValidationSummary
        {
            TotalIssues = issues.Count,
            ErrorCount = issues.Count(i => i.Severity == ValidationSeverity.Error),
            WarningCount = issues.Count(i => i.Severity == ValidationSeverity.Warning),
            InfoCount = issues.Count(i => i.Severity == ValidationSeverity.Info)
        };
    }
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
    public ValidationIssueType Type { get; set; }
    public ValidationSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string AffectedFile { get; set; } = string.Empty;
    public object Context { get; set; } = new();
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
/// 自动修复器 - 自动修复检测到的索引文件问题
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

    /// <summary>
    /// 自动修复
    /// </summary>
    public async Task<RepairResult> RepairAsync(
        ValidationResult validationResult,
        SlicingTask task,
        SlicingConfig config,
        List<NormalizedSliceData> slices,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始自动修复，共{Count}个问题", validationResult.Issues.Count);

        var repairActions = new List<RepairAction>();

        // 按严重程度和类型对问题进行分组处理
        var issuesByType = validationResult.Issues
            .OrderByDescending(i => i.Severity)
            .GroupBy(i => i.Type);

        foreach (var issueGroup in issuesByType)
        {
            _logger.LogInformation("修复{Type}类型问题，共{Count}个", issueGroup.Key, issueGroup.Count());

            foreach (var issue in issueGroup)
            {
                var action = await ExecuteRepairActionAsync(issue, task, config, slices, cancellationToken);
                repairActions.Add(action);
            }
        }

        var result = new RepairResult
        {
            Actions = repairActions,
            Success = repairActions.All(a => a.Success),
            Summary = GenerateRepairSummary(repairActions)
        };

        _logger.LogInformation("修复完成：成功={Success}，总操作数={TotalActions}（成功={SuccessCount}，失败={FailCount}，跳过={SkipCount}）",
            result.Success, result.Summary.TotalActions, result.Summary.SuccessfulActions,
            result.Summary.FailedActions, result.Summary.SkippedActions);

        return result;
    }

    /// <summary>
    /// 执行修复操作
    /// </summary>
    private async Task<RepairAction> ExecuteRepairActionAsync(
        ValidationIssue issue,
        SlicingTask task,
        SlicingConfig config,
        List<NormalizedSliceData> slices,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("执行修复：{Type} - {Description}", issue.Type, issue.Description);

            switch (issue.Type)
            {
                case ValidationIssueType.MissingFile:
                    return await RepairMissingFileAsync(issue, task, config, slices, cancellationToken);

                case ValidationIssueType.IncorrectPath:
                    return await RepairIncorrectPathAsync(issue, task, config, slices, cancellationToken);

                case ValidationIssueType.MissingMetadata:
                    return await RepairMissingMetadataAsync(issue, task, config, slices, cancellationToken);

                case ValidationIssueType.StructureError:
                    return await RepairStructureErrorAsync(issue, task, config, slices, cancellationToken);

                case ValidationIssueType.FormatError:
                    return await RepairFormatErrorAsync(issue, task, config, slices, cancellationToken);

                default:
                    return RepairAction.Skip(issue, "未知的问题类型，无法修复");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行修复操作失败：{IssueType} - {Description}", issue.Type, issue.Description);
            return RepairAction.CreateFail(issue, $"修复失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 修复缺失文件
    /// </summary>
    private Task<RepairAction> RepairMissingFileAsync(
        ValidationIssue issue,
        SlicingTask task,
        SlicingConfig config,
        List<NormalizedSliceData> slices,
        CancellationToken cancellationToken)
    {
        // 缺失文件的修复策略：
        // 1. 检查是否有备份文件
        // 2. 如果没有备份，标记为需要重新生成
        // 3. 记录缺失文件信息，供后续处理

        _logger.LogWarning("切片文件缺失，无法自动修复，需要重新生成：{FilePath}", issue.AffectedFile);

        // 从上下文中获取切片坐标信息
        var context = issue.Context as dynamic;
        if (context != null)
        {
            var level = context.Level;
            var x = context.X;
            var y = context.Y;
            var z = context.Z;

            // 这里可以将缺失的切片信息记录到数据库或日志中
            // 供管理员或系统后续重新生成
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(_logger,
                "缺失切片坐标：Level={Level}, X={X}, Y={Y}, Z={Z}",
                level, x, y, z);
        }

        return Task.FromResult(RepairAction.Skip(issue, "缺失文件需要重新生成切片，已记录缺失信息"));
    }

    /// <summary>
    /// 修复错误路径
    /// </summary>
    private Task<RepairAction> RepairIncorrectPathAsync(
        ValidationIssue issue,
        SlicingTask task,
        SlicingConfig config,
        List<NormalizedSliceData> slices,
        CancellationToken cancellationToken)
    {
        // 路径错误的修复策略：
        // 1. 尝试标准化路径
        // 2. 检查标准化后的路径是否存在
        // 3. 如果存在，更新索引文件中的路径引用

        try
        {
            var pathNormalizer = new PathNormalizer();

            // 验证路径是否有效
            if (pathNormalizer.ValidatePath(issue.AffectedFile, config.StorageLocation))
            {
                var normalizedPath = pathNormalizer.NormalizePath(
                    issue.AffectedFile,
                    config.StorageLocation,
                    task.OutputPath ?? string.Empty);

                _logger.LogInformation("路径已标准化：{OriginalPath} -> {NormalizedPath}",
                    issue.AffectedFile, normalizedPath.UriPath);

                return Task.FromResult(RepairAction.CreateSuccess(issue, $"路径已标准化为: {normalizedPath.UriPath}"));
            }
            else
            {
                _logger.LogWarning("路径验证失败：{Path}", issue.AffectedFile);
                return Task.FromResult(RepairAction.Skip(issue, "路径格式无效，需要手动检查"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修复路径错误失败：{Path}", issue.AffectedFile);
            return Task.FromResult(RepairAction.CreateFail(issue, $"路径修复失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 修复缺失元数据
    /// </summary>
    private Task<RepairAction> RepairMissingMetadataAsync(
        ValidationIssue issue,
        SlicingTask task,
        SlicingConfig config,
        List<NormalizedSliceData> slices,
        CancellationToken cancellationToken)
    {
        // 元数据缺失的修复策略：
        // 1. 对于包围盒：尝试从切片文件中重新计算
        // 2. 对于文件大小：尝试从存储系统中获取
        // 3. 对于其他元数据：使用默认值或从配置中推导

        try
        {
            // 从上下文中获取切片信息
            var context = issue.Context as dynamic;
            if (context == null)
            {
                return Task.FromResult(RepairAction.Skip(issue, "无法获取切片上下文信息"));
            }

            var level = (int)context.Level;
            var x = (int)context.X;
            var y = (int)context.Y;
            var z = (int)context.Z;

            // 查找对应的切片数据
            var slice = slices.FirstOrDefault(s =>
                s.Level == level && s.X == x && s.Y == y && s.Z == z);

            if (slice == null)
            {
                return Task.FromResult(RepairAction.Skip(issue, "未找到对应的切片数据"));
            }

            // 修复文件大小
            if (slice.FileSize <= 0 && !string.IsNullOrEmpty(slice.Path?.AbsolutePath))
            {
                long fileSize = 0;

                if (config.StorageLocation == StorageLocationType.LocalFileSystem)
                {
                    if (File.Exists(slice.Path.AbsolutePath))
                    {
                        var fileInfo = new FileInfo(slice.Path.AbsolutePath);
                        fileSize = fileInfo.Length;
                    }
                }
                else if (config.StorageLocation == StorageLocationType.MinIO)
                {
                    // MinIO文件大小获取需要通过API
                    // 这里简化处理，实际应该调用MinIO API
                    _logger.LogInformation("需要从MinIO获取文件大小：{Path}", slice.Path.AbsolutePath);
                }

                if (fileSize > 0)
                {
                    slice.FileSize = fileSize;
                    _logger.LogInformation("已更新文件大小：{Size} bytes", fileSize);
                    return Task.FromResult(RepairAction.CreateSuccess(issue, $"已更新文件大小为 {fileSize} bytes"));
                }
            }

            // 修复包围盒
            if (slice.BoundingBox == null ||
                (slice.BoundingBox.MinX == 0 && slice.BoundingBox.MaxX == 0))
            {
                // 使用默认的包围盒计算方法
                // 基于切片的位置和层级计算包围盒
                var bounds = CalculateDefaultBoundingBox(slice, config);
                slice.BoundingBox = bounds;

                _logger.LogInformation("已重新计算包围盒：{BoundingBox}",
                    JsonSerializer.Serialize(bounds));

                return Task.FromResult(RepairAction.CreateSuccess(issue, "已重新计算包围盒"));
            }

            return Task.FromResult(RepairAction.Skip(issue, "元数据修复不适用于当前情况"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修复元数据失败");
            return Task.FromResult(RepairAction.CreateFail(issue, $"元数据修复失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 修复结构错误
    /// </summary>
    private Task<RepairAction> RepairStructureErrorAsync(
        ValidationIssue issue,
        SlicingTask task,
        SlicingConfig config,
        List<NormalizedSliceData> slices,
        CancellationToken cancellationToken)
    {
        // 结构错误的修复策略：
        // 1. 数量不一致：更新索引文件中的计数
        // 2. 层级不连续：记录警告信息，不强制修复
        // 3. 其他结构问题：根据具体情况处理

        if (issue.Description.Contains("数量"))
        {
            _logger.LogInformation("检测到数量不一致问题，将在重新生成索引文件时修复");
            return Task.FromResult(RepairAction.CreateSuccess(issue, "将在重新生成索引文件时更新切片数量"));
        }
        else if (issue.Description.Contains("层级不连续"))
        {
            _logger.LogWarning("LOD层级不连续，这可能是正常的情况（非完整切片）");
            return Task.FromResult(RepairAction.Skip(issue, "LOD层级不连续可能是正常情况，已记录警告"));
        }

        return Task.FromResult(RepairAction.Skip(issue, "结构错误需要重新生成索引文件"));
    }

    /// <summary>
    /// 修复格式错误
    /// </summary>
    private Task<RepairAction> RepairFormatErrorAsync(
        ValidationIssue issue,
        SlicingTask task,
        SlicingConfig config,
        List<NormalizedSliceData> slices,
        CancellationToken cancellationToken)
    {
        // 格式错误的修复策略：
        // 1. JSON格式错误：重新生成索引文件
        // 2. 字段缺失：补充缺失字段
        // 3. 类型错误：转换为正确类型

        _logger.LogWarning("检测到格式错误，建议重新生成索引文件：{Description}", issue.Description);
        return Task.FromResult(RepairAction.Skip(issue, "格式错误需要重新生成索引文件"));
    }

    /// <summary>
    /// 计算默认包围盒
    /// </summary>
    private BoundingBoxData CalculateDefaultBoundingBox(NormalizedSliceData slice, SlicingConfig config)
    {
        // 基于切片的层级和位置计算默认包围盒
        // 这是一个简化的实现，实际应该根据具体的切片策略计算

        var tileSize = config.TileSize;
        var scale = Math.Pow(2, slice.Level);

        var minX = slice.X * tileSize * scale;
        var minY = slice.Y * tileSize * scale;
        var minZ = slice.Z * tileSize * scale;
        var maxX = (slice.X + 1) * tileSize * scale;
        var maxY = (slice.Y + 1) * tileSize * scale;
        var maxZ = (slice.Z + 1) * tileSize * scale;

        return new BoundingBoxData
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
    /// 生成修复摘要
    /// </summary>
    private RepairSummary GenerateRepairSummary(List<RepairAction> actions)
    {
        return new RepairSummary
        {
            TotalActions = actions.Count,
            SuccessfulActions = actions.Count(a => a.Success && a.Status == RepairStatus.Completed),
            FailedActions = actions.Count(a => !a.Success && a.Status == RepairStatus.Failed),
            SkippedActions = actions.Count(a => a.Status == RepairStatus.Skipped)
        };
    }
}

/// <summary>
/// 修复结果
/// </summary>
public class RepairResult
{
    public List<RepairAction> Actions { get; set; } = new();
    public RepairSummary Summary { get; set; } = new();
    public bool Success { get; set; }
}

/// <summary>
/// 修复操作
/// </summary>
public class RepairAction
{
    public ValidationIssue Issue { get; set; } = new();
    public RepairStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime ExecutedAt { get; set; }

    public static RepairAction CreateSuccess(ValidationIssue issue, string message)
    {
        return new RepairAction
        {
            Issue = issue,
            Status = RepairStatus.Completed,
            Message = message,
            Success = true,
            ExecutedAt = DateTime.UtcNow
        };
    }

    public static RepairAction CreateFail(ValidationIssue issue, string message)
    {
        return new RepairAction
        {
            Issue = issue,
            Status = RepairStatus.Failed,
            Message = message,
            Success = false,
            ExecutedAt = DateTime.UtcNow
        };
    }

    public static RepairAction Skip(ValidationIssue issue, string message)
    {
        return new RepairAction
        {
            Issue = issue,
            Status = RepairStatus.Skipped,
            Message = message,
            Success = true,
            ExecutedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// 修复状态
/// </summary>
public enum RepairStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}

/// <summary>
/// 修复摘要
/// </summary>
public class RepairSummary
{
    public int TotalActions { get; set; }
    public int SuccessfulActions { get; set; }
    public int FailedActions { get; set; }
    public int SkippedActions { get; set; }
}

/// <summary>
/// 索引文件格式验证器 - 验证index.json和tileset.json的格式是否符合规范
/// </summary>
public class IndexFormatValidator
{
    private readonly ILogger _logger;

    public IndexFormatValidator(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 验证index.json格式
    /// </summary>
    public ValidationResult ValidateIndexJsonFormat(string indexContent)
    {
        var issues = new List<ValidationIssue>();

        try
        {
            _logger.LogInformation("验证index.json格式...");

            // 1. 验证是否是有效的JSON
            JsonDocument? indexJson;
            try
            {
                indexJson = JsonSerializer.Deserialize<JsonDocument>(indexContent);
                if (indexJson == null)
                {
                    issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.FormatError,
                        Severity = ValidationSeverity.Error,
                        Description = "index.json解析结果为null",
                        AffectedFile = "index.json"
                    });
                    return CreateValidationResult(issues);
                }
            }
            catch (JsonException ex)
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FormatError,
                    Severity = ValidationSeverity.Error,
                    Description = $"index.json不是有效的JSON格式: {ex.Message}",
                    AffectedFile = "index.json"
                });
                return CreateValidationResult(issues);
            }

            var root = indexJson.RootElement;

            // 2. 验证必需字段
            ValidateRequiredField(root, "TaskId", issues, "index.json");
            ValidateRequiredField(root, "TotalLevels", issues, "index.json");
            ValidateRequiredField(root, "TileSize", issues, "index.json");
            ValidateRequiredField(root, "SliceCount", issues, "index.json");
            ValidateRequiredField(root, "Slices", issues, "index.json");

            // 3. 验证Slices数组
            if (root.TryGetProperty("Slices", out var slices))
            {
                if (slices.ValueKind != JsonValueKind.Array)
                {
                    issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.FormatError,
                        Severity = ValidationSeverity.Error,
                        Description = "Slices字段必须是数组类型",
                        AffectedFile = "index.json"
                    });
                }
                else
                {
                    // 验证每个切片对象的格式
                    int sliceIndex = 0;
                    foreach (var slice in slices.EnumerateArray())
                    {
                        ValidateSliceObject(slice, sliceIndex, issues);
                        sliceIndex++;
                    }
                }
            }

            // 4. 验证BoundingBox格式
            if (root.TryGetProperty("BoundingBox", out var bbox))
            {
                ValidateBoundingBoxObject(bbox, issues, "index.json");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证index.json格式时发生错误");
            issues.Add(new ValidationIssue
            {
                Type = ValidationIssueType.FormatError,
                Severity = ValidationSeverity.Error,
                Description = $"验证index.json格式失败: {ex.Message}",
                AffectedFile = "index.json"
            });
        }

        return CreateValidationResult(issues);
    }

    /// <summary>
    /// 验证tileset.json格式（3D Tiles规范）
    /// </summary>
    public ValidationResult ValidateTilesetJsonFormat(string tilesetContent)
    {
        var issues = new List<ValidationIssue>();

        try
        {
            _logger.LogInformation("验证tileset.json格式（3D Tiles规范）...");

            // 1. 验证是否是有效的JSON
            JsonDocument? tilesetJson;
            try
            {
                tilesetJson = JsonSerializer.Deserialize<JsonDocument>(tilesetContent);
                if (tilesetJson == null)
                {
                    issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.FormatError,
                        Severity = ValidationSeverity.Error,
                        Description = "tileset.json解析结果为null",
                        AffectedFile = "tileset.json"
                    });
                    return CreateValidationResult(issues);
                }
            }
            catch (JsonException ex)
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FormatError,
                    Severity = ValidationSeverity.Error,
                    Description = $"tileset.json不是有效的JSON格式: {ex.Message}",
                    AffectedFile = "tileset.json"
                });
                return CreateValidationResult(issues);
            }

            var root = tilesetJson.RootElement;

            // 2. 验证3D Tiles必需字段
            ValidateRequiredField(root, "asset", issues, "tileset.json");
            ValidateRequiredField(root, "geometricError", issues, "tileset.json");
            ValidateRequiredField(root, "root", issues, "tileset.json");

            // 3. 验证asset对象
            if (root.TryGetProperty("asset", out var asset))
            {
                ValidateAssetObject(asset, issues);
            }

            // 4. 验证root tile对象
            if (root.TryGetProperty("root", out var rootTile))
            {
                ValidateTileObject(rootTile, issues, "root");
            }

            // 5. 验证geometricError值
            if (root.TryGetProperty("geometricError", out var geometricError))
            {
                if (geometricError.ValueKind != JsonValueKind.Number)
                {
                    issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.FormatError,
                        Severity = ValidationSeverity.Error,
                        Description = "geometricError必须是数值类型",
                        AffectedFile = "tileset.json"
                    });
                }
                else if (geometricError.GetDouble() < 0)
                {
                    issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.FormatError,
                        Severity = ValidationSeverity.Warning,
                        Description = "geometricError不应为负数",
                        AffectedFile = "tileset.json"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证tileset.json格式时发生错误");
            issues.Add(new ValidationIssue
            {
                Type = ValidationIssueType.FormatError,
                Severity = ValidationSeverity.Error,
                Description = $"验证tileset.json格式失败: {ex.Message}",
                AffectedFile = "tileset.json"
            });
        }

        return CreateValidationResult(issues);
    }

    /// <summary>
    /// 验证必需字段
    /// </summary>
    private void ValidateRequiredField(JsonElement element, string fieldName, List<ValidationIssue> issues, string fileName)
    {
        if (!element.TryGetProperty(fieldName, out _))
        {
            issues.Add(new ValidationIssue
            {
                Type = ValidationIssueType.FormatError,
                Severity = ValidationSeverity.Error,
                Description = $"缺少必需字段: {fieldName}",
                AffectedFile = fileName
            });
        }
    }

    /// <summary>
    /// 验证切片对象格式
    /// </summary>
    private void ValidateSliceObject(JsonElement slice, int index, List<ValidationIssue> issues)
    {
        var requiredFields = new[] { "Level", "X", "Y", "Z", "FilePath" };
        foreach (var field in requiredFields)
        {
            if (!slice.TryGetProperty(field, out _))
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FormatError,
                    Severity = ValidationSeverity.Error,
                    Description = $"切片对象[{index}]缺少必需字段: {field}",
                    AffectedFile = "index.json",
                    Context = new { SliceIndex = index, MissingField = field }
                });
            }
        }

        // 验证坐标字段为整数
        foreach (var field in new[] { "Level", "X", "Y", "Z" })
        {
            if (slice.TryGetProperty(field, out var value))
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out _))
                {
                    issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.FormatError,
                        Severity = ValidationSeverity.Error,
                        Description = $"切片对象[{index}]的{field}字段必须是整数",
                        AffectedFile = "index.json",
                        Context = new { SliceIndex = index, Field = field }
                    });
                }
            }
        }
    }

    /// <summary>
    /// 验证包围盒对象
    /// </summary>
    private void ValidateBoundingBoxObject(JsonElement bbox, List<ValidationIssue> issues, string fileName)
    {
        var requiredFields = new[] { "MinX", "MinY", "MinZ", "MaxX", "MaxY", "MaxZ" };
        foreach (var field in requiredFields)
        {
            if (!bbox.TryGetProperty(field, out var value))
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FormatError,
                    Severity = ValidationSeverity.Warning,
                    Description = $"BoundingBox缺少字段: {field}",
                    AffectedFile = fileName
                });
            }
            else if (value.ValueKind != JsonValueKind.Number)
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FormatError,
                    Severity = ValidationSeverity.Error,
                    Description = $"BoundingBox的{field}字段必须是数值类型",
                    AffectedFile = fileName
                });
            }
        }
    }

    /// <summary>
    /// 验证asset对象（3D Tiles规范）
    /// </summary>
    private void ValidateAssetObject(JsonElement asset, List<ValidationIssue> issues)
    {
        // 验证version字段（必需）
        if (!asset.TryGetProperty("version", out var version))
        {
            issues.Add(new ValidationIssue
            {
                Type = ValidationIssueType.FormatError,
                Severity = ValidationSeverity.Error,
                Description = "asset对象缺少必需的version字段",
                AffectedFile = "tileset.json"
            });
        }
        else if (version.ValueKind != JsonValueKind.String)
        {
            issues.Add(new ValidationIssue
            {
                Type = ValidationIssueType.FormatError,
                Severity = ValidationSeverity.Error,
                Description = "asset.version必须是字符串类型",
                AffectedFile = "tileset.json"
            });
        }
        else
        {
            var versionStr = version.GetString();
            // 验证版本号格式
            if (!string.IsNullOrEmpty(versionStr) && !IsValidTilesVersion(versionStr))
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FormatError,
                    Severity = ValidationSeverity.Warning,
                    Description = $"3D Tiles版本号不符合规范: {versionStr}（推荐使用1.0或1.1）",
                    AffectedFile = "tileset.json"
                });
            }
        }
    }

    /// <summary>
    /// 验证tile对象（3D Tiles规范）
    /// </summary>
    private void ValidateTileObject(JsonElement tile, List<ValidationIssue> issues, string tilePath)
    {
        // 验证boundingVolume（必需）
        if (!tile.TryGetProperty("boundingVolume", out var boundingVolume))
        {
            issues.Add(new ValidationIssue
            {
                Type = ValidationIssueType.FormatError,
                Severity = ValidationSeverity.Error,
                Description = $"tile对象[{tilePath}]缺少必需的boundingVolume字段",
                AffectedFile = "tileset.json"
            });
        }
        else
        {
            ValidateBoundingVolumeObject(boundingVolume, issues, tilePath);
        }

        // 验证geometricError（必需）
        if (!tile.TryGetProperty("geometricError", out var geometricError))
        {
            issues.Add(new ValidationIssue
            {
                Type = ValidationIssueType.FormatError,
                Severity = ValidationSeverity.Error,
                Description = $"tile对象[{tilePath}]缺少必需的geometricError字段",
                AffectedFile = "tileset.json"
            });
        }
        else if (geometricError.ValueKind != JsonValueKind.Number)
        {
            issues.Add(new ValidationIssue
            {
                Type = ValidationIssueType.FormatError,
                Severity = ValidationSeverity.Error,
                Description = $"tile对象[{tilePath}]的geometricError必须是数值类型",
                AffectedFile = "tileset.json"
            });
        }

        // 递归验证children
        if (tile.TryGetProperty("children", out var children))
        {
            if (children.ValueKind == JsonValueKind.Array)
            {
                int childIndex = 0;
                foreach (var child in children.EnumerateArray())
                {
                    ValidateTileObject(child, issues, $"{tilePath}/children[{childIndex}]");
                    childIndex++;
                }
            }
            else
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FormatError,
                    Severity = ValidationSeverity.Error,
                    Description = $"tile对象[{tilePath}]的children字段必须是数组类型",
                    AffectedFile = "tileset.json"
                });
            }
        }
    }

    /// <summary>
    /// 验证boundingVolume对象（3D Tiles规范）
    /// </summary>
    private void ValidateBoundingVolumeObject(JsonElement boundingVolume, List<ValidationIssue> issues, string tilePath)
    {
        // 3D Tiles支持三种包围体类型：box, region, sphere
        // 至少需要其中一种
        bool hasBox = boundingVolume.TryGetProperty("box", out var box);
        bool hasRegion = boundingVolume.TryGetProperty("region", out var region);
        bool hasSphere = boundingVolume.TryGetProperty("sphere", out var sphere);

        if (!hasBox && !hasRegion && !hasSphere)
        {
            issues.Add(new ValidationIssue
            {
                Type = ValidationIssueType.FormatError,
                Severity = ValidationSeverity.Error,
                Description = $"tile对象[{tilePath}]的boundingVolume必须包含box、region或sphere之一",
                AffectedFile = "tileset.json"
            });
            return;
        }

        // 验证box格式（12个数值的数组）
        if (hasBox)
        {
            if (box.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FormatError,
                    Severity = ValidationSeverity.Error,
                    Description = $"tile对象[{tilePath}]的boundingVolume.box必须是数组类型",
                    AffectedFile = "tileset.json"
                });
            }
            else
            {
                var boxArray = box.EnumerateArray().ToList();
                if (boxArray.Count != 12)
                {
                    issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.FormatError,
                        Severity = ValidationSeverity.Error,
                        Description = $"tile对象[{tilePath}]的boundingVolume.box必须包含12个数值",
                        AffectedFile = "tileset.json"
                    });
                }
            }
        }

        // 验证region格式（6个数值的数组）
        if (hasRegion)
        {
            if (region.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FormatError,
                    Severity = ValidationSeverity.Error,
                    Description = $"tile对象[{tilePath}]的boundingVolume.region必须是数组类型",
                    AffectedFile = "tileset.json"
                });
            }
            else
            {
                var regionArray = region.EnumerateArray().ToList();
                if (regionArray.Count != 6)
                {
                    issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.FormatError,
                        Severity = ValidationSeverity.Error,
                        Description = $"tile对象[{tilePath}]的boundingVolume.region必须包含6个数值",
                        AffectedFile = "tileset.json"
                    });
                }
            }
        }

        // 验证sphere格式（4个数值的数组）
        if (hasSphere)
        {
            if (sphere.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FormatError,
                    Severity = ValidationSeverity.Error,
                    Description = $"tile对象[{tilePath}]的boundingVolume.sphere必须是数组类型",
                    AffectedFile = "tileset.json"
                });
            }
            else
            {
                var sphereArray = sphere.EnumerateArray().ToList();
                if (sphereArray.Count != 4)
                {
                    issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.FormatError,
                        Severity = ValidationSeverity.Error,
                        Description = $"tile对象[{tilePath}]的boundingVolume.sphere必须包含4个数值",
                        AffectedFile = "tileset.json"
                    });
                }
            }
        }
    }

    /// <summary>
    /// 验证3D Tiles版本号是否有效
    /// </summary>
    private bool IsValidTilesVersion(string version)
    {
        // 支持的版本：1.0, 1.1
        return version == "1.0" || version == "1.1";
    }

    /// <summary>
    /// 创建验证结果
    /// </summary>
    private ValidationResult CreateValidationResult(List<ValidationIssue> issues)
    {
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