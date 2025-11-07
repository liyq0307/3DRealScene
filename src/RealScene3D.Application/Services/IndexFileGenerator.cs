using Microsoft.Extensions.Logging;
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
    public async Task<IndexGenerationResult> GenerateIndexFilesAsync(
        SlicingTask task,
        SlicingConfig config,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("开始生成索引文件：任务{TaskId}", task.Id);
            
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

            // 6. 生成tileset.json（仅包含存在的切片）
            var tilesetResult = await GenerateTilesetJsonAsync(task, normalizedSlices, config, cancellationToken);

            // 6. 验证格式
            var formatValidationResult = ValidateIndexFormats(indexResult, tilesetResult!);

            // 7. 验证一致性
            var validationResult = await ValidateConsistencyAsync(indexResult, tilesetResult!, normalizedSlices);

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
                    var revalidationResult = await ValidateConsistencyAsync(indexResult, tilesetResult!, normalizedSlices);
                    validationResult = revalidationResult;
                }
            }
            
            var result = new IndexGenerationResult
            {
                IndexJson = indexResult,
                TilesetJson = tilesetResult,
                ValidationResult = validationResult,
                RepairResult = repairResult,
                Success = validationResult.IsValid || repairResult.Success
            };
            
            _logger.LogInformation("索引文件生成完成：任务{TaskId}，成功={Success}", task.Id, result.Success);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成索引文件失败：任务{TaskId}", task.Id);
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
    /// 生成tileset.json
    /// </summary>
    private async Task<TilesetJsonResult> GenerateTilesetJsonAsync(
        SlicingTask task,
        List<NormalizedSliceData> slices,
        SlicingConfig config,
        CancellationToken cancellationToken)
    {
        if (!slices.Any())
        {
            _logger.LogWarning("没有切片数据，生成空的tileset.json：任务{TaskId}", task.Id);
            return new TilesetJsonResult
            {
                Content = "{}",
                Path = $"{task.OutputPath}/tileset.json",
                SliceCount = 0
            };
        }

        // 计算根节点的包围体积
        var rootBoundingVolume = CalculateRootBoundingVolume(slices);

        // 构建LOD层次结构
        var rootTile = new
        {
            boundingVolume = rootBoundingVolume,
            geometricError = CalculateGeometricError(0, config),
            refine = "REPLACE",
            children = BuildLodHierarchy(slices, config, 0)
        };

        // 生成完整的tileset
        var tileset = new
        {
            asset = new
            {
                version = "1.1",
                generator = "RealScene3D Slicer v1.0",
                tilesetVersion = "1.0"
            },
            geometricError = config.GeometricErrorThreshold * 4,
            root = rootTile
        };

        var tilesetContent = JsonSerializer.Serialize(tileset, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        var tilesetPath = $"{task.OutputPath}/tileset.json";

        if (config.StorageLocation == StorageLocationType.LocalFileSystem)
        {
            var fullPath = Path.Combine(task.OutputPath!, "tileset.json");
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllTextAsync(fullPath, tilesetContent, cancellationToken);
            _logger.LogInformation("tileset.json已保存到本地：{FilePath}", fullPath);
        }
        else
        {
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(tilesetContent)))
            {
                await _minioService.UploadFileAsync("slices", tilesetPath, stream, "application/json", cancellationToken);
            }
            _logger.LogInformation("tileset.json已上传到MinIO：{FilePath}", tilesetPath);
        }

        return new TilesetJsonResult
        {
            Content = tilesetContent,
            Path = tilesetPath,
            SliceCount = slices.Count
        };
    }

    /// <summary>
    /// 验证索引文件格式
    /// </summary>
    private ValidationResult ValidateIndexFormats(IndexJsonResult indexResult, TilesetJsonResult tilesetResult)
    {
        _logger.LogInformation("验证索引文件格式...");

        var formatValidator = new IndexFormatValidator(_logger);
        var allIssues = new List<ValidationIssue>();

        // 验证index.json格式
        if (indexResult != null && !string.IsNullOrEmpty(indexResult.Content))
        {
            var indexFormatResult = formatValidator.ValidateIndexJsonFormat(indexResult.Content);
            allIssues.AddRange(indexFormatResult.Issues);
        }

        // 验证tileset.json格式
        if (tilesetResult != null && !string.IsNullOrEmpty(tilesetResult.Content))
        {
            var tilesetFormatResult = formatValidator.ValidateTilesetJsonFormat(tilesetResult.Content);
            allIssues.AddRange(tilesetFormatResult.Issues);
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
        TilesetJsonResult tilesetResult,
        List<NormalizedSliceData> slices)
    {
        var validator = new ConsistencyValidator(_minioService, _logger);
        return await validator.ValidateAsync(indexResult, tilesetResult, slices);
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
    /// 计算根节点包围体积
    /// </summary>
    private object CalculateRootBoundingVolume(List<NormalizedSliceData> slices)
    {
        if (!slices.Any()) return new { box = new[] { 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0 } };

        var minX = slices.Min(s => s.BoundingBox.MinX);
        var minY = slices.Min(s => s.BoundingBox.MinY);
        var minZ = slices.Min(s => s.BoundingBox.MinZ);
        var maxX = slices.Max(s => s.BoundingBox.MaxX);
        var maxY = slices.Max(s => s.BoundingBox.MaxY);
        var maxZ = slices.Max(s => s.BoundingBox.MaxZ);

        var centerX = (minX + maxX) / 2.0;
        var centerY = (minY + maxY) / 2.0;
        var centerZ = (minZ + maxZ) / 2.0;
        var halfWidth = (maxX - minX) / 2.0;
        var halfHeight = (maxY - minY) / 2.0;
        var halfDepth = (maxZ - minZ) / 2.0;

        return new
        {
            box = new[]
            {
                centerX, centerY, centerZ,
                halfWidth, 0.0, 0.0,
                0.0, halfHeight, 0.0,
                0.0, 0.0, halfDepth
            }
        };
    }

    /// <summary>
    /// 构建LOD层次结构
    /// </summary>
    private List<object>? BuildLodHierarchy(List<NormalizedSliceData> allSlices, SlicingConfig config, int currentLevel)
    {
        var levelSlices = allSlices.Where(s => s.Level == currentLevel).ToList();
        if (!levelSlices.Any()) return null;

        var children = new List<object>();

        if (currentLevel >= config.MaxLevel)
        {
            // 叶子级别
            foreach (var slice in levelSlices)
            {
                children.Add(new
                {
                    boundingVolume = ParseBoundingVolume(slice.BoundingBox),
                    geometricError = 0.0,
                    content = new { uri = slice.Path.UriPath }
                });
            }
        }
        else
        {
            // 非叶子级别
            foreach (var slice in levelSlices)
            {
                var childSlices = allSlices.Where(s =>
                    s.Level == currentLevel + 1 &&
                    s.X >= slice.X * 2 && s.X < slice.X * 2 + 2 &&
                    s.Y >= slice.Y * 2 && s.Y < slice.Y * 2 + 2 &&
                    s.Z >= slice.Z * 2 && s.Z < slice.Z * 2 + 2).ToList();

                var tileData = new Dictionary<string, object>
                {
                    ["boundingVolume"] = ParseBoundingVolume(slice.BoundingBox),
                    ["geometricError"] = CalculateGeometricError(currentLevel + 1, config),
                    ["refine"] = "REPLACE"
                };

                var childHierarchy = BuildLodHierarchy(allSlices, config, currentLevel + 1);
                if (childHierarchy != null && childHierarchy.Any())
                {
                    tileData["children"] = childHierarchy;
                }

                if (currentLevel > 0)
                {
                    tileData["content"] = new { uri = slice.Path.UriPath };
                }

                children.Add(tileData);
            }
        }

        return children.Any() ? children : null;
    }

    /// <summary>
    /// 计算几何误差
    /// </summary>
    private double CalculateGeometricError(int level, SlicingConfig config)
    {
        var baseError = config.GeometricErrorThreshold;
        var errorFactor = Math.Pow(2.0, config.MaxLevel - level);
        return baseError * errorFactor;
    }

    /// <summary>
    /// 解析包围盒
    /// </summary>
    private BoundingBoxData ParseBoundingBox(string boundingBoxJson)
    {
        if (string.IsNullOrEmpty(boundingBoxJson))
            return new BoundingBoxData();

        try
        {
            var boundingBox = JsonSerializer.Deserialize<Dictionary<string, double>>(boundingBoxJson);
            if (boundingBox == null)
                return new BoundingBoxData();

            return new BoundingBoxData
            {
                // 修复：使用大写键名匹配GridSlicingStrategy生成的JSON格式
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
            return new BoundingBoxData();
        }
    }

    /// <summary>
    /// 解析包围体积
    /// </summary>
    private object ParseBoundingVolume(BoundingBoxData boundingBox)
    {
        var centerX = (boundingBox.MinX + boundingBox.MaxX) / 2.0;
        var centerY = (boundingBox.MinY + boundingBox.MaxY) / 2.0;
        var centerZ = (boundingBox.MinZ + boundingBox.MaxZ) / 2.0;
        var halfWidth = (boundingBox.MaxX - boundingBox.MinX) / 2.0;
        var halfHeight = (boundingBox.MaxY - boundingBox.MinY) / 2.0;
        var halfDepth = (boundingBox.MaxZ - boundingBox.MinZ) / 2.0;

        return new
        {
            box = new[]
            {
                centerX, centerY, centerZ,
                halfWidth, 0.0, 0.0,
                0.0, halfHeight, 0.0,
                0.0, 0.0, halfDepth
            }
        };
    }

    /// <summary>
    /// 获取内容类型
    /// </summary>
    private string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return extension switch
        {
            ".b3dm" => "application/octet-stream",
            ".gltf" => "application/json",
            ".glb" => "application/octet-stream",
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
    public TilesetJsonResult TilesetJson { get; set; } = new();
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
/// tileset.json生成结果
/// </summary>
public class TilesetJsonResult
{
    public string Content { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int SliceCount { get; set; }
}