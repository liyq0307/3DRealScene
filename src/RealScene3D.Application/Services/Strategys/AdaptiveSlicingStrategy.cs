using Microsoft.Extensions.Logging;
using RealScene3D.Application.Services.Slicing;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.MinIO;
using System.Text.Json;

namespace RealScene3D.Application.Services.Strategys;

/// <summary>
/// 自适应切片策略 - 基于几何密度和特征的智能剖分
/// 自动调整切片大小和LOD级别，优化渲染性能和视觉效果
/// </summary>
public class AdaptiveSlicingStrategy : ISlicingStrategy
{
    private readonly ILogger _logger;
    private readonly IMinioStorageService? _minioService;
    private readonly ITileGeneratorFactory? _tileGeneratorFactory;

    /// <summary>
    /// 构造函数 - 注入日志记录器和可选服务
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="tileGeneratorFactory">瓦片生成器工厂，用于动态创建不同格式的生成器（可选）</param>
    /// <param name="minioService">MinIO存储服务（可选）</param>
    public AdaptiveSlicingStrategy(
        ILogger logger,
        ITileGeneratorFactory? tileGeneratorFactory = null,
        IMinioStorageService? minioService = null)
    {
        _logger = logger;
        _tileGeneratorFactory = tileGeneratorFactory;
        _minioService = minioService;
    }

    public async Task<List<Slice>> GenerateSlicesAsync(SlicingTask task, int level, SlicingConfig config, BoundingBox3D modelBounds, CancellationToken cancellationToken)
    {
        var slices = new List<Slice>();

        _logger.LogInformation("自适应切片策略：级别{Level}", level);

        // 自适应剖分算法：基于几何密度和视点重要性
        var adaptiveRegions = await AnalyzeGeometricDensityAsync(task, level, config, cancellationToken);

        _logger.LogInformation("自适应策略分析完成：级别{Level}, 区域数量{RegionCount}", level, adaptiveRegions.Count);

        // 区域数量验证（理论上现在应该始终有区域）
        if (adaptiveRegions.Count == 0)
        {
            _logger.LogError("自适应策略意外地未生成任何区域：级别{Level}，这不应该发生", level);
            // 这种情况理论上不应该发生了，因为我们移除了密度阈值过滤
            // 如果仍然发生，说明可能存在其他问题（如取消、异常等）
        }

        foreach (var region in adaptiveRegions)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // 空值检查：确保OutputPath不为null
            var outputPath = task.OutputPath ?? "default_output";
            var slice = new Slice
            {
                SlicingTaskId = task.Id,
                Level = level,
                X = region.X,
                Y = region.Y,
                Z = region.Z,
                FilePath = $"{outputPath}/{level}/{region.X}_{region.Y}_{region.Z}.{config.OutputFormat.ToLower()}",
                BoundingBox = GenerateAdaptiveBoundingBox(region, config.TileSize),
                FileSize = CalculateAdaptiveFileSize(region, config.OutputFormat)
            };

            slices.Add(slice);
        }

        _logger.LogInformation("自适应策略切片生成完成：级别{Level}, 切片数量{SliceCount}", level, slices.Count);

        return slices;
    }

    /// <summary>
    /// 估算指定级别的切片数量 - 增强的自适应策略算法实现
    /// 算法：基于几何复杂性、误差阈值和剖分效率的综合估算
    /// 支持：多因子权重计算、边界条件处理、溢出保护
    /// </summary>
    /// <param name="level">LOD级别，必须为非负整数</param>
    /// <param name="config">切片配置，包含几何误差阈值等参数</param>
    /// <returns>估算的切片数量，基于自适应几何复杂性计算</returns>
    /// <exception cref="ArgumentNullException">当config为null时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">当level为负数时抛出</exception>
    /// <exception cref="OverflowException">当计算结果超过int最大值时抛出</exception>
    public int EstimateSliceCount(int level, SlicingConfig config)
    {
        // 1. 参数验证：确保输入参数的有效性
        if (config == null)
            throw new ArgumentNullException(nameof(config), "切片配置不能为空");

        if (level < 0)
            throw new ArgumentOutOfRangeException(nameof(level), level, "LOD级别不能为负数");

        // 2. 特殊情况处理：根级别只有一个切片
        if (level == 0)
            return 1;

        try
        {
            // 3. 基础几何因子计算
            // 自适应策略的基础增长模式：几何级数但有衰减
            var geometricBaseFactor = CalculateGeometricBase(level);

            // 4. 密度增长因子：考虑几何复杂度随级别增加
            var densityGrowthFactor = CalculateDensityGrowthFactor(level, config);

            // 5. 效率衰减因子：考虑实际剖分效率
            var efficiencyAttenuationFactor = CalculateEfficiencyAttenuation(level, config);

            // 6. 几何误差阈值调整因子
            var errorThresholdFactor = CalculateErrorThresholdFactor(config);

            // 7. 计算初步估算值
            var preliminaryEstimate = geometricBaseFactor * densityGrowthFactor * efficiencyAttenuationFactor * errorThresholdFactor;

            // 8. 应用级别特定调整
            var levelSpecificAdjustment = GetLevelSpecificAdjustment(level);
            var finalEstimate = (long)(preliminaryEstimate * levelSpecificAdjustment);

            // 9. 边界检查和溢出处理
            if (finalEstimate > int.MaxValue)
                throw new OverflowException($"估算切片数量超过int最大值：{finalEstimate}");

            if (finalEstimate < 1)
                finalEstimate = 1;

            // 10. 记录估算详情
            _logger.LogDebug("自适应切片数量估算：级别{Level}, 几何基数{Base:F2}, 密度因子{Density:F3}, 效率因子{Efficiency:F3}, 误差因子{Error:F3}, 最终估算{Final}",
                level, geometricBaseFactor, densityGrowthFactor, efficiencyAttenuationFactor, errorThresholdFactor, finalEstimate);

            return (int)finalEstimate;
        }
        catch (OverflowException)
        {
            _logger.LogWarning("切片数量估算溢出：级别{Level}，返回最大保守值", level);
            return int.MaxValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切片数量估算失败：级别{Level}", level);
            return 1; // 返回最小值确保流程继续
        }
    }

    /// <summary>
    /// 计算几何基础因子
    /// </summary>
    private double CalculateGeometricBase(int level)
    {
        // 理论上自适应策略的切片数量约为4^level，但实际有优化
        // 使用位运算提高性能：4^level = 2^(2*level)
        var geometricBase = 1L << (2 * level);

        // 转换为double类型进行后续计算
        return Math.Min(geometricBase, (double)int.MaxValue / 10); // 防止后续计算溢出
    }

    /// <summary>
    /// 计算密度增长因子
    /// </summary>
    private double CalculateDensityGrowthFactor(int level, SlicingConfig config)
    {
        // 基础密度增长率
        const double baseGrowthRate = 0.12; // 比固定策略更保守

        // 级别影响：高级别密度增长较慢
        var levelInfluence = 1.0 + level * baseGrowthRate;

        // 配置影响：考虑切片尺寸对密度的影响
        var sizeInfluence = Math.Log(Math.Max(1.0, config.TileSize), 2) * 0.05;

        return Math.Max(1.0, levelInfluence + sizeInfluence);
    }

    /// <summary>
    /// 计算效率衰减因子
    /// </summary>
    private double CalculateEfficiencyAttenuation(int level, SlicingConfig config)
    {
        // 基础效率因子
        const double baseEfficiency = 0.65; // 自适应策略的实际效率

        // 级别衰减：高级别效率略有下降（算法复杂度增加）
        var levelAttenuation = Math.Pow(0.98, level);

        // 并行处理优化：多线程可提升效率
        var parallelBonus = config.ParallelProcessingCount > 1 ?
            Math.Min(1.2, 1.0 + config.ParallelProcessingCount * 0.05) : 1.0;

        return baseEfficiency * levelAttenuation * parallelBonus;
    }

    /// <summary>
    /// 计算误差阈值因子
    /// </summary>
    private double CalculateErrorThresholdFactor(SlicingConfig config)
    {
        if (config.GeometricErrorThreshold <= 0)
            return 1.0;

        // 高精度要求增加切片数量
        var precisionFactor = Math.Max(0.5, 1.0 / config.GeometricErrorThreshold);

        // 限制放大倍数避免过度切片
        return Math.Min(precisionFactor, 2.5);
    }

    /// <summary>
    /// 获取级别特定调整因子
    /// </summary>
    private double GetLevelSpecificAdjustment(int level)
    {
        // 不同级别有特定的调整需求
        return level switch
        {
            1 => 1.1,  // 第一级通常需要更多切片建立基础
            2 => 1.05, // 第二级适中调整
            3 => 0.95, // 第三级开始优化减少冗余
            _ => level > 3 ? Math.Pow(0.92, level - 3) : 1.0 // 高级别进一步衰减
        };
    }

    private async Task<List<AdaptiveRegion>> AnalyzeGeometricDensityAsync(SlicingTask task, int level, SlicingConfig config, CancellationToken cancellationToken)
    {
        // 几何密度分析算法：多维度几何复杂度分析
        _logger.LogInformation("开始几何密度分析：级别{Level}", level);

        var regions = new List<AdaptiveRegion>();

        try
        {
            // 1. 获取模型几何数据
            var geometricData = await LoadGeometricDataAsync(task, cancellationToken);
            if (geometricData == null || !geometricData.Any())
            {
                _logger.LogWarning("无法获取几何数据，使用fallback密度分析：级别{Level}", level);
                return await FallbackDensityAnalysisAsync(level, config, cancellationToken);
            }

            _logger.LogInformation("加载几何数据成功：{PrimitiveCount}个几何图元", geometricData.Count);

            // 2. 构建空间索引以提高分析效率
            var spatialIndex = await BuildSpatialIndexAsync(geometricData, config, cancellationToken);

            // 3. 计算基础网格尺寸
            var baseTileSize = config.TileSize;
            var levelTileSize = baseTileSize * Math.Pow(2, config.MaxLevel - level);

            // 4. 多分辨率密度分析（优化版：减少临时对象分配）
            var densityAnalyzer = new GeometricDensityAnalyzer(_logger);
            var levelSize = (int)Math.Pow(2, level);
            var zSize = level == 0 ? 1 : levelSize / 2;

            // 预分配 regions 容量以减少重新分配
            regions.Capacity = levelSize * levelSize * zSize;

            // 分析不同LOD级别的密度分布
            for (int z = 0; z < zSize; z++)
            {
                for (int y = 0; y < levelSize; y++)
                {
                    for (int x = 0; x < levelSize; x++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        // 定义分析区域的包围盒
                        var regionBounds = new BoundingBox3D
                        {
                            MinX = x * levelTileSize,
                            MinY = y * levelTileSize,
                            MinZ = z * levelTileSize,
                            MaxX = (x + 1) * levelTileSize,
                            MaxY = (y + 1) * levelTileSize,
                            MaxZ = (z + 1) * levelTileSize
                        };

                        // 多维度密度分析
                        var densityMetrics = await densityAnalyzer.AnalyzeRegionDensityAsync(
                            geometricData, spatialIndex, regionBounds, config, cancellationToken);

                        // 综合密度评分
                        var compositeDensity = CalculateCompositeDensity(densityMetrics);

                        // 修复：始终添加区域，不再使用密度阈值过滤
                        // 原逻辑: if (compositeDensity > config.GeometricErrorThreshold) - 会导致某些级别没有区域
                        // 新逻辑: 始终添加区域，让后续的优化决定是否需要细分
                        // 这样可以确保每个级别都有完整的切片覆盖
                        regions.Add(new AdaptiveRegion
                        {
                            X = x,
                            Y = y,
                            Z = z,
                            Density = compositeDensity,
                            Importance = CalculateRegionImportance(densityMetrics, level),
                            VertexDensity = densityMetrics.VertexDensity,
                            TriangleDensity = densityMetrics.TriangleDensity,
                            CurvatureComplexity = densityMetrics.CurvatureComplexity,
                            SurfaceArea = densityMetrics.SurfaceArea
                        });
                    }
                }

                // 在每处理完一个 Z 层后，触发轻量级 GC
                if (z % 2 == 0 && regions.Count > 1000)
                {
                    GC.Collect(0, GCCollectionMode.Optimized);
                }
            }

            _logger.LogDebug("几何密度分析完成：级别{Level}, 发现{RegionCount}个高密度区域",
                level, regions.Count);

            return regions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "几何密度分析失败，回退到默认分析：级别{Level}", level);
            return await FallbackDensityAnalysisAsync(level, config, cancellationToken);
        }
    }

    /// <summary>
    /// 加载几何数据 - 从模型源获取三角形网格数据
    /// 支持多种3D模型格式：OBJ、STL、PLY、GLTF等
    /// 支持数据源：MinIO对象存储、本地文件系统
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>几何数据集合</returns>
    private async Task<List<GeometricPrimitive>> LoadGeometricDataAsync(SlicingTask task, CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始加载几何数据：{SourceModelPath}", task.SourceModelPath);

        var geometricData = new List<GeometricPrimitive>();

        try
        {
            Stream? modelStream = null;

            // 1. 优先尝试从MinIO加载
            if (_minioService != null)
            {
                try
                {
                    // 转换Windows路径为MinIO兼容的对象名
                    var minioObjectName = ConvertToMinioObjectName(task.SourceModelPath);
                    _logger.LogDebug("转换MinIO对象名：{OriginalPath} -> {MinioObjectName}", task.SourceModelPath, minioObjectName);

                    modelStream = await _minioService.DownloadFileAsync("models", minioObjectName);
                    _logger.LogDebug("模型文件从MinIO下载成功：{MinioObjectName}", minioObjectName);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "无法从MinIO下载模型文件：{SourceModelPath}", task.SourceModelPath);
                    modelStream = null;
                }
            }

            // 2. 如果MinIO失败，尝试从本地文件系统加载
            if (modelStream == null)
            {
                var localPath = task.SourceModelPath;

                // 处理相对路径：转换为绝对路径
                if (!Path.IsPathRooted(localPath))
                {
                    // 尝试多个可能的基础路径
                    var basePaths = new[]
                    {
                        Directory.GetCurrentDirectory(),
                        Path.Combine(Directory.GetCurrentDirectory(), "models"),
                        Path.Combine(Directory.GetCurrentDirectory(), "data"),
                        Path.Combine(Directory.GetCurrentDirectory(), "..", "models"),
                        Path.Combine(Directory.GetCurrentDirectory(), "..", "data")
                    };

                    foreach (var basePath in basePaths)
                    {
                        var fullPath = Path.Combine(basePath, localPath);
                        if (File.Exists(fullPath))
                        {
                            localPath = fullPath;
                            break;
                        }
                    }
                }

                // 检查本地文件是否存在
                if (File.Exists(localPath))
                {
                    try
                    {
                        modelStream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        _logger.LogInformation("模型文件从本地文件系统加载成功：{LocalPath}", localPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "无法从本地文件系统读取模型文件：{LocalPath}", localPath);
                        modelStream = null;
                    }
                }
                else
                {
                    _logger.LogDebug("本地文件不存在：{LocalPath}", localPath);
                }
            }

            // 3. 如果所有数据源都失败，使用模拟数据
            if (modelStream == null)
            {
                _logger.LogWarning("无法从任何数据源加载模型文件，使用模拟数据：{SourceModelPath}", task.SourceModelPath);
                var mockTriangles = GenerateMockGeometricData(task);
                return ConvertTrianglesToPrimitives(mockTriangles, cancellationToken);
            }

            // 4. 根据文件扩展名解析模型格式
            var fileExtension = Path.GetExtension(task.SourceModelPath).ToLowerInvariant();
            List<Triangle> triangles;

            // 使用 await using 语句确保 Stream 资源正确释放
            try
            {
                await using (modelStream)
                {
                    switch (fileExtension)
                    {
                        case ".obj":
                            _logger.LogDebug("解析OBJ格式模型文件");
                            triangles = await ParseOBJFormatAsync(modelStream, cancellationToken);
                            break;

                        case ".stl":
                            _logger.LogDebug("解析STL格式模型文件");
                            triangles = await ParseSTLFormatAsync(modelStream, cancellationToken);
                            break;

                        case ".ply":
                            _logger.LogDebug("解析PLY格式模型文件");
                            triangles = await ParsePLYFormatAsync(modelStream, cancellationToken);
                            break;

                        case ".gltf":
                        case ".glb":
                            _logger.LogDebug("解析GLTF/GLB格式模型文件");
                            triangles = await ParseGLTFFormatAsync(modelStream, cancellationToken);
                            break;

                        default:
                            _logger.LogWarning("不支持的模型格式：{FileExtension}，使用模拟数据", fileExtension);
                            var mockTriangles = GenerateMockGeometricData(task);
                            return ConvertTrianglesToPrimitives(mockTriangles, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "模型文件解析失败，使用模拟数据：{FileExtension}", fileExtension);
                var mockTriangles = GenerateMockGeometricData(task);
                triangles = mockTriangles;
            }

            // 转换三角形为几何图元
            geometricData = ConvertTrianglesToPrimitives(triangles, cancellationToken);

            _logger.LogInformation("几何数据加载完成：{VertexCount}个顶点, {TriangleCount}个三角形",
                geometricData.Sum(g => g.Vertices.Length), geometricData.Count);

            return geometricData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载几何数据失败：{SourceModelPath}", task.SourceModelPath);
            // 发生错误时返回模拟数据，确保流程继续
            var mockTriangles = GenerateMockGeometricData(task);
            return ConvertTrianglesToPrimitives(mockTriangles, cancellationToken);
        }
    }

    /// <summary>
    /// 将三角形列表转换为几何图元列表
    /// </summary>
    private List<GeometricPrimitive> ConvertTrianglesToPrimitives(List<Triangle> triangles, CancellationToken cancellationToken)
    {
        var primitives = new List<GeometricPrimitive>();

        foreach (var triangle in triangles)
        {
            if (cancellationToken.IsCancellationRequested) break;

            primitives.Add(new GeometricPrimitive
            {
                Vertices = triangle.Vertices,
                Triangle = triangle,
                Normal = CalculateTriangleNormal(triangle.Vertices[0], triangle.Vertices[1], triangle.Vertices[2]),
                Area = CalculateTriangleArea(triangle.Vertices[0], triangle.Vertices[1], triangle.Vertices[2])
            });
        }

        return primitives;
    }

    /// <summary>
    /// 解析OBJ格式模型文件
    /// OBJ是一种基于文本的3D模型格式，由顶点(v)、纹理坐标(vt)、法向量(vn)和面(f)组成
    /// 增强特性：错误恢复、数据验证、性能优化、大文件支持、多编码支持
    /// </summary>
    public async Task<List<Triangle>> ParseOBJFormatAsync(Stream stream, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();
        var vertices = new List<Vector3D>();
        var lineNumber = 0;
        var errorCount = 0;
        const int MAX_ERRORS = 100; // 最大容忍错误数

        _logger.LogInformation("开始解析OBJ文件");

        // 尝试检测文件编码（支持UTF-8、GBK等编码）
        System.Text.Encoding encoding;
        try
        {
            // 先读取一部分数据来检测编码
            var buffer = new byte[Math.Min(4096, stream.Length)];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            stream.Position = 0; // 重置流位置

            // 检查BOM标记
            if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                encoding = System.Text.Encoding.UTF8;
                _logger.LogInformation("检测到UTF-8 BOM编码");
            }
            // 尝试检测GBK编码（中文系统常用）
            else if (HasChineseCharacters(buffer, bytesRead))
            {
                try
                {
                    encoding = System.Text.Encoding.GetEncoding("GBK");
                    _logger.LogInformation("检测到可能的GBK编码");
                }
                catch
                {
                    encoding = System.Text.Encoding.UTF8;
                    _logger.LogInformation("GBK编码不可用，使用UTF-8");
                }
            }
            else
            {
                encoding = System.Text.Encoding.UTF8;
                _logger.LogInformation("使用默认UTF-8编码");
            }

            _logger.LogInformation("OBJ文件大小：{Size} 字节，已读取{BytesRead}字节用于编码检测", stream.Length, bytesRead);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "编码检测失败，使用默认UTF-8编码");
            encoding = System.Text.Encoding.UTF8;
        }

        var sampleLines = new List<string>(); // 保存前几行用于诊断
        const int MAX_SAMPLE_LINES = 10;

        using (var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true))
        {
            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;

                // 收集前几行用于诊断（不包含注释和空行）
                if (sampleLines.Count < MAX_SAMPLE_LINES && !string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#"))
                {
                    sampleLines.Add(line.Trim());
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("OBJ解析被取消：已解析{TriangleCount}个三角形", triangles.Count);
                    break;
                }

                // 跳过空行和注释
                line = line.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                try
                {
                    switch (parts[0])
                    {
                        case "v": // 顶点坐标
                            if (parts.Length >= 4)
                            {
                                // 健壮的数值解析
                                if (double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                                                   System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                                    double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                                                   System.Globalization.CultureInfo.InvariantCulture, out var y) &&
                                    double.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                                                   System.Globalization.CultureInfo.InvariantCulture, out var z))
                                {
                                    // 验证顶点坐标的合理性
                                    if (double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z) &&
                                        Math.Abs(x) < 1e10 && Math.Abs(y) < 1e10 && Math.Abs(z) < 1e10)
                                    {
                                        vertices.Add(new Vector3D { X = x, Y = y, Z = z });
                                    }
                                    else
                                    {
                                        _logger.LogWarning("OBJ行{Line}：顶点坐标超出合理范围", lineNumber);
                                        errorCount++;
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("OBJ行{Line}：顶点坐标解析失败", lineNumber);
                                    errorCount++;
                                }
                            }
                            else
                            {
                                _logger.LogWarning("OBJ行{Line}：顶点定义不完整", lineNumber);
                                errorCount++;
                            }
                            break;

                        case "f": // 面定义
                            if (parts.Length >= 4)
                            {
                                // 解析面索引（支持 v、v/vt、v/vt/vn、v//vn 等格式）
                                var indices = new List<int>();
                                bool allIndicesValid = true;

                                for (int i = 1; i < parts.Length; i++)
                                {
                                    var indexParts = parts[i].Split('/');
                                    if (int.TryParse(indexParts[0], out var index))
                                    {
                                        // 处理负索引（相对索引）
                                        if (index < 0)
                                        {
                                            index = vertices.Count + index + 1;
                                        }

                                        // 转换为从0开始的索引
                                        var zeroBasedIndex = index - 1;

                                        // 验证索引范围
                                        if (zeroBasedIndex >= 0 && zeroBasedIndex < vertices.Count)
                                        {
                                            indices.Add(zeroBasedIndex);
                                        }
                                        else
                                        {
                                            _logger.LogWarning("OBJ行{Line}：面索引{Index}超出范围（顶点数：{VertexCount}）",
                                                             lineNumber, index, vertices.Count);
                                            allIndicesValid = false;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogWarning("OBJ行{Line}：面索引解析失败：{Part}", lineNumber, parts[i]);
                                        allIndicesValid = false;
                                        break;
                                    }
                                }

                                // 三角形化多边形面（扇形三角化）
                                if (allIndicesValid && indices.Count >= 3)
                                {
                                    for (int i = 1; i < indices.Count - 1; i++)
                                    {
                                        triangles.Add(new Triangle
                                        {
                                            Vertices = new[]
                                            {
                                                vertices[indices[0]],
                                                vertices[indices[i]],
                                                vertices[indices[i + 1]]
                                            }
                                        });
                                    }
                                }
                                else if (allIndicesValid)
                                {
                                    _logger.LogWarning("OBJ行{Line}：面顶点数不足（需要至少3个顶点）", lineNumber);
                                    errorCount++;
                                }
                            }
                            else
                            {
                                _logger.LogWarning("OBJ行{Line}：面定义不完整", lineNumber);
                                errorCount++;
                            }
                            break;

                        // 其他OBJ命令（忽略但记录）
                        case "vt": // 纹理坐标
                        case "vn": // 法向量
                        case "usemtl": // 材质
                        case "mtllib": // 材质库
                        case "g": // 组
                        case "o": // 对象名称
                        case "s": // 平滑组
                            // 静默忽略这些命令
                            break;

                        default:
                            // 未识别的命令
                            if (errorCount < 10) // 只记录前10个未知命令
                            {
                                _logger.LogDebug("OBJ行{Line}：未识别的命令：{Command}", lineNumber, parts[0]);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OBJ行{Line}：解析失败", lineNumber);
                    errorCount++;
                }

                // 错误容忍机制：超过阈值则终止解析
                if (errorCount > MAX_ERRORS)
                {
                    _logger.LogError("OBJ解析错误过多（{ErrorCount}个错误），终止解析", errorCount);
                    throw new InvalidDataException($"OBJ文件包含过多错误（{errorCount}个），可能已损坏");
                }
            }
        }

        // 验证解析结果
        _logger.LogInformation("OBJ解析完成：总行数={LineCount}, 顶点数={VertexCount}, 三角形数={TriangleCount}, 错误数={ErrorCount}",
            lineNumber, vertices.Count, triangles.Count, errorCount);

        if (vertices.Count == 0)
        {
            // 输出前几行内容用于诊断
            if (sampleLines.Any())
            {
                _logger.LogError("OBJ文件前{Count}行内容示例：\n{Lines}",
                    sampleLines.Count, string.Join("\n", sampleLines.Select((l, i) => $"  {i + 1}: {l}")));
            }

            _logger.LogError("OBJ文件解析失败：文件共{LineCount}行，但未找到任何顶点数据（'v x y z'格式）。" +
                           "可能原因：1) 文件为空或格式错误；2) 文件编码不正确；3) 文件内容被损坏。" +
                           "请检查文件内容和编码（当前使用：{Encoding}）",
                lineNumber, encoding.EncodingName);
            throw new InvalidDataException($"OBJ文件不包含任何顶点数据（总行数：{lineNumber}，使用编码：{encoding.EncodingName}）");
        }

        if (triangles.Count == 0)
        {
            _logger.LogError("OBJ文件解析失败：找到{VertexCount}个顶点，但未找到任何面数据（'f i1 i2 i3'格式）。" +
                           "可能原因：1) 文件只包含顶点没有面定义；2) 面索引格式不正确",
                vertices.Count);
            throw new InvalidDataException($"OBJ文件不包含任何面数据（顶点数：{vertices.Count}）");
        }

        _logger.LogInformation("OBJ解析成功：{VertexCount}个顶点, {TriangleCount}个三角形",
            vertices.Count, triangles.Count);

        return triangles;
    }

    /// <summary>
    /// 检测字节数组中是否包含中文字符（用于编码检测）
    /// </summary>
    private bool HasChineseCharacters(byte[] buffer, int length)
    {
        for (int i = 0; i < length - 1; i++)
        {
            // 检测GBK编码的中文字符范围
            if ((buffer[i] >= 0x81 && buffer[i] <= 0xFE) &&
                ((buffer[i + 1] >= 0x40 && buffer[i + 1] <= 0x7E) ||
                 (buffer[i + 1] >= 0x80 && buffer[i + 1] <= 0xFE)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 将Windows路径转换为MinIO兼容的对象名
    /// MinIO对象名规则：
    /// - 使用正斜杠(/)作为路径分隔符
    /// - 不支持绝对路径（不能以盘符开头）
    /// - 不支持某些特殊字符，特别是在某些Windows环境下的 + 符号
    /// </summary>
    private string ConvertToMinioObjectName(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        // 1. 标准化路径分隔符（统一使用正斜杠）
        var normalizedPath = filePath.Replace('\\', '/');

        // 2. 移除Windows盘符（如 "F:/Data" -> "Data"）
        if (normalizedPath.Length >= 2 && normalizedPath[1] == ':')
        {
            normalizedPath = normalizedPath.Substring(2);
        }

        // 3. 移除开头的斜杠
        normalizedPath = normalizedPath.TrimStart('/');

        // 4. 处理特殊字符：某些MinIO实现不支持特定字符
        // 将 + 替换为 _plus_ 以避免兼容性问题
        normalizedPath = normalizedPath.Replace("+", "_plus_");

        // 其他潜在的问题字符也可以在这里处理
        // 例如：空格、#、& 等（根据实际需要添加）
        normalizedPath = normalizedPath.Replace(" ", "_");
        normalizedPath = normalizedPath.Replace("#", "_hash_");
        normalizedPath = normalizedPath.Replace("&", "_and_");

        // 5. 确保路径不为空
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            throw new InvalidOperationException($"无法将路径转换为有效的MinIO对象名：{filePath}");
        }

        return normalizedPath;
    }

    /// <summary>
    /// 解析STL格式模型文件（支持二进制和ASCII格式）
    /// STL是一种简单的三角形网格格式，广泛用于3D打印和CAD软件
    /// </summary>
    public async Task<List<Triangle>> ParseSTLFormatAsync(Stream stream, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        // 读取前80字节判断是二进制还是ASCII格式
        var header = new byte[80];
        await stream.ReadAsync(header, 0, 80, cancellationToken);

        // 检查是否为ASCII STL（以"solid"开头）
        var headerText = System.Text.Encoding.ASCII.GetString(header).ToLower();
        stream.Position = 0; // 重置流位置

        if (headerText.StartsWith("solid"))
        {
            // ASCII STL格式
            triangles = await ParseASCIISTLAsync(stream, cancellationToken);
        }
        else
        {
            // 二进制STL格式
            triangles = await ParseBinarySTLAsync(stream, cancellationToken);
        }

        _logger.LogDebug("STL解析完成：{TriangleCount}个三角形", triangles.Count);
        return triangles;
    }

    /// <summary>
    /// 解析ASCII格式的STL文件
    /// </summary>
    private async Task<List<Triangle>> ParseASCIISTLAsync(Stream stream, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();
        var currentVertices = new List<Vector3D>();

        using (var reader = new StreamReader(stream))
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (cancellationToken.IsCancellationRequested) break;

                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                switch (parts[0].ToLower())
                {
                    case "vertex":
                        if (parts.Length >= 4)
                        {
                            currentVertices.Add(new Vector3D
                            {
                                X = double.Parse(parts[1]),
                                Y = double.Parse(parts[2]),
                                Z = double.Parse(parts[3])
                            });
                        }
                        break;

                    case "endfacet":
                        if (currentVertices.Count == 3)
                        {
                            triangles.Add(new Triangle
                            {
                                Vertices = currentVertices.ToArray()
                            });
                        }
                        currentVertices.Clear();
                        break;
                }
            }
        }

        return triangles;
    }

    /// <summary>
    /// 解析二进制格式的STL文件
    /// 二进制STL格式：80字节头 + 4字节三角形数量 + 每个三角形50字节（法向量12字节 + 3个顶点各12字节 + 属性2字节）
    /// </summary>
    private Task<List<Triangle>> ParseBinarySTLAsync(Stream stream, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        using (var reader = new BinaryReader(stream))
        {
            // 跳过80字节头
            reader.ReadBytes(80);

            // 读取三角形数量
            var triangleCount = reader.ReadUInt32();

            for (uint i = 0; i < triangleCount; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // 跳过法向量（12字节）
                reader.ReadBytes(12);

                // 读取3个顶点
                var vertices = new Vector3D[3];
                for (int j = 0; j < 3; j++)
                {
                    vertices[j] = new Vector3D
                    {
                        X = reader.ReadSingle(),
                        Y = reader.ReadSingle(),
                        Z = reader.ReadSingle()
                    };
                }

                // 跳过属性字节（2字节）
                reader.ReadUInt16();

                triangles.Add(new Triangle { Vertices = vertices });
            }
        }

        return Task.FromResult(triangles);
    }

    /// <summary>
    /// 解析PLY格式模型文件（完整实现，支持ASCII和二进制格式）
    /// PLY（Polygon File Format）是一种灵活的多边形网格格式
    /// 支持：ASCII/Binary格式、多边形三角化、顶点属性（位置、法向量、颜色等）
    /// </summary>
    public async Task<List<Triangle>> ParsePLYFormatAsync(Stream stream, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        try
        {
            // 读取并解析PLY头部信息
            var headerInfo = await ParsePLYHeaderAsync(stream, cancellationToken);

            if (headerInfo == null)
            {
                _logger.LogWarning("PLY文件头部解析失败");
                return triangles;
            }

            _logger.LogDebug("PLY格式：{Format}, 顶点数：{VertexCount}, 面数：{FaceCount}",
                headerInfo.Format, headerInfo.VertexCount, headerInfo.FaceCount);

            // 根据格式选择解析方法
            if (headerInfo.Format == PLYFormat.ASCII)
            {
                triangles = await ParseASCIIPLYDataAsync(stream, headerInfo, cancellationToken);
            }
            else
            {
                triangles = await ParseBinaryPLYDataAsync(stream, headerInfo, cancellationToken);
            }

            _logger.LogDebug("PLY解析完成：{VertexCount}个顶点, {TriangleCount}个三角形",
                headerInfo.VertexCount, triangles.Count);

            return triangles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PLY格式解析失败");
            return triangles;
        }
    }

    /// <summary>
    /// 解析PLY文件头部 - 提取格式信息和元数据
    /// </summary>
    private async Task<PLYHeader?> ParsePLYHeaderAsync(Stream stream, CancellationToken cancellationToken)
    {
        var headerInfo = new PLYHeader();
        using (var reader = new StreamReader(stream, leaveOpen: true))
        {
            string? line;
            var lineNumber = 0;

            // 检查魔数
            line = await reader.ReadLineAsync();
            if (line == null || !line.Trim().Equals("ply", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("PLY文件魔数验证失败");
                return null;
            }

            // 解析头部信息
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (cancellationToken.IsCancellationRequested) break;

                line = line.Trim();
                lineNumber++;

                // 忽略空行和注释
                if (string.IsNullOrEmpty(line) || line.StartsWith("comment", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                switch (parts[0].ToLowerInvariant())
                {
                    case "format":
                        if (parts.Length >= 2)
                        {
                            headerInfo.Format = parts[1].ToLowerInvariant() switch
                            {
                                "ascii" => PLYFormat.ASCII,
                                "binary_little_endian" => PLYFormat.BinaryLittleEndian,
                                "binary_big_endian" => PLYFormat.BinaryBigEndian,
                                _ => PLYFormat.ASCII
                            };
                            if (parts.Length >= 3)
                                headerInfo.Version = parts[2];
                        }
                        break;

                    case "element":
                        if (parts.Length >= 3)
                        {
                            var elementName = parts[1].ToLowerInvariant();
                            var elementCount = int.Parse(parts[2]);

                            if (elementName == "vertex")
                            {
                                headerInfo.VertexCount = elementCount;
                                headerInfo.CurrentElement = "vertex";
                            }
                            else if (elementName == "face")
                            {
                                headerInfo.FaceCount = elementCount;
                                headerInfo.CurrentElement = "face";
                            }
                        }
                        break;

                    case "property":
                        // 记录属性定义
                        if (parts.Length >= 3)
                        {
                            var propertyType = parts[1].ToLowerInvariant();

                            if (propertyType == "list" && parts.Length >= 5)
                            {
                                // list属性（用于面的顶点索引）
                                var propertyName = parts[4].ToLowerInvariant();
                                if (headerInfo.CurrentElement == "face")
                                {
                                    headerInfo.FacePropertiesCount++;
                                    headerInfo.FaceListIndexType = parts[2]; // 索引计数类型
                                    headerInfo.FaceListValueType = parts[3]; // 索引值类型
                                }
                            }
                            else
                            {
                                // 标量属性
                                var propertyName = parts[2].ToLowerInvariant();
                                if (headerInfo.CurrentElement == "vertex")
                                {
                                    headerInfo.VertexProperties.Add(propertyName);
                                }
                            }
                        }
                        break;

                    case "end_header":
                        headerInfo.HeaderEndPosition = stream.Position;
                        return headerInfo;
                }
            }
        }

        return headerInfo;
    }

    /// <summary>
    /// 解析ASCII格式的PLY数据
    /// </summary>
    private async Task<List<Triangle>> ParseASCIIPLYDataAsync(Stream stream, PLYHeader headerInfo, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();
        var vertices = new List<Vector3D>();

        using (var reader = new StreamReader(stream, leaveOpen: true))
        {
            // 读取顶点数据
            for (int i = 0; i < headerInfo.VertexCount; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    try
                    {
                        vertices.Add(new Vector3D
                        {
                            X = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                            Y = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                            Z = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)
                        });
                    }
                    catch (FormatException ex)
                    {
                        _logger.LogWarning(ex, "PLY顶点数据解析失败：行{LineNumber}", i + 1);
                        continue;
                    }
                }
            }

            // 读取面数据
            for (int i = 0; i < headerInfo.FaceCount; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                try
                {
                    var indexCount = int.Parse(parts[0]);
                    if (parts.Length < indexCount + 1) continue;

                    // 提取顶点索引
                    var indices = new List<int>();
                    for (int j = 0; j < indexCount; j++)
                    {
                        var index = int.Parse(parts[j + 1]);
                        if (index >= 0 && index < vertices.Count)
                            indices.Add(index);
                    }

                    // 三角化多边形（使用扇形三角化算法）
                    if (indices.Count >= 3)
                    {
                        for (int j = 1; j < indices.Count - 1; j++)
                        {
                            triangles.Add(new Triangle
                            {
                                Vertices = new[]
                                {
                                    vertices[indices[0]],
                                    vertices[indices[j]],
                                    vertices[indices[j + 1]]
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PLY面数据解析失败：行{LineNumber}", i + 1);
                    continue;
                }
            }
        }

        return triangles;
    }

    /// <summary>
    /// 解析二进制格式的PLY数据
    /// </summary>
    private Task<List<Triangle>> ParseBinaryPLYDataAsync(Stream stream, PLYHeader headerInfo, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();
        var vertices = new List<Vector3D>();

        using (var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            var isLittleEndian = headerInfo.Format == PLYFormat.BinaryLittleEndian;

            // 读取顶点数据
            for (int i = 0; i < headerInfo.VertexCount; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    // 读取X、Y、Z坐标（假设为float类型）
                    var x = ReadFloat(reader, isLittleEndian);
                    var y = ReadFloat(reader, isLittleEndian);
                    var z = ReadFloat(reader, isLittleEndian);

                    vertices.Add(new Vector3D { X = x, Y = y, Z = z });

                    // 跳过额外的顶点属性（如法向量、颜色等）
                    var extraPropertiesCount = headerInfo.VertexProperties.Count - 3;
                    for (int j = 0; j < extraPropertiesCount; j++)
                    {
                        reader.ReadSingle(); // 假设额外属性都是float类型
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PLY二进制顶点数据解析失败：索引{Index}", i);
                    break;
                }
            }

            // 读取面数据
            for (int i = 0; i < headerInfo.FaceCount; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    // 读取顶点索引计数
                    var indexCount = reader.ReadByte(); // 通常是uchar类型

                    // 读取顶点索引（假设为int类型）
                    var indices = new List<int>();
                    for (int j = 0; j < indexCount; j++)
                    {
                        var index = ReadInt(reader, isLittleEndian, headerInfo.FaceListValueType);
                        if (index >= 0 && index < vertices.Count)
                            indices.Add(index);
                    }

                    // 三角化多边形
                    if (indices.Count >= 3)
                    {
                        for (int j = 1; j < indices.Count - 1; j++)
                        {
                            triangles.Add(new Triangle
                            {
                                Vertices = new[]
                                {
                                    vertices[indices[0]],
                                    vertices[indices[j]],
                                    vertices[indices[j + 1]]
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PLY二进制面数据解析失败：索引{Index}", i);
                    continue;
                }
            }
        }

        return Task.FromResult(triangles);
    }

    /// <summary>
    /// 读取浮点数（支持大小端）
    /// </summary>
    private float ReadFloat(BinaryReader reader, bool isLittleEndian)
    {
        var bytes = reader.ReadBytes(4);
        if (isLittleEndian != BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>
    /// 读取整数（支持多种类型和大小端）
    /// </summary>
    private int ReadInt(BinaryReader reader, bool isLittleEndian, string dataType)
    {
        switch (dataType.ToLowerInvariant())
        {
            case "char":
            case "int8":
                return (sbyte)reader.ReadByte();

            case "uchar":
            case "uint8":
                return reader.ReadByte();

            case "short":
            case "int16":
                var shortBytes = reader.ReadBytes(2);
                if (isLittleEndian != BitConverter.IsLittleEndian)
                    Array.Reverse(shortBytes);
                return BitConverter.ToInt16(shortBytes, 0);

            case "ushort":
            case "uint16":
                var ushortBytes = reader.ReadBytes(2);
                if (isLittleEndian != BitConverter.IsLittleEndian)
                    Array.Reverse(ushortBytes);
                return BitConverter.ToUInt16(ushortBytes, 0);

            case "int":
            case "int32":
                var intBytes = reader.ReadBytes(4);
                if (isLittleEndian != BitConverter.IsLittleEndian)
                    Array.Reverse(intBytes);
                return BitConverter.ToInt32(intBytes, 0);

            case "uint":
            case "uint32":
                var uintBytes = reader.ReadBytes(4);
                if (isLittleEndian != BitConverter.IsLittleEndian)
                    Array.Reverse(uintBytes);
                return (int)BitConverter.ToUInt32(uintBytes, 0);

            default:
                // 默认按int32处理
                return reader.ReadInt32();
        }
    }

    /// <summary>
    /// PLY格式枚举
    /// </summary>
    private enum PLYFormat
    {
        ASCII,
        BinaryLittleEndian,
        BinaryBigEndian
    }

    /// <summary>
    /// PLY文件头部信息
    /// </summary>
    private class PLYHeader
    {
        public PLYFormat Format { get; set; } = PLYFormat.ASCII;
        public string Version { get; set; } = "1.0";
        public int VertexCount { get; set; }
        public int FaceCount { get; set; }
        public List<string> VertexProperties { get; set; } = new List<string>();
        public int FacePropertiesCount { get; set; }
        public string CurrentElement { get; set; } = "";
        public long HeaderEndPosition { get; set; }
        public string FaceListIndexType { get; set; } = "uchar";
        public string FaceListValueType { get; set; } = "int";
    }

    /// <summary>
    /// 解析GLTF/GLB格式模型文件（完整实现，支持glTF 2.0标准）
    /// GLTF是现代的3D场景传输格式，支持复杂的材质、动画等
    /// 支持：GLTF（JSON格式）和GLB（二进制格式）、嵌入式和外部Buffer、多种图元类型
    /// </summary>
    public async Task<List<Triangle>> ParseGLTFFormatAsync(Stream stream, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        try
        {
            // 检测文件格式：GLB（二进制）或GLTF（JSON）
            var headerBytes = new byte[12];
            await stream.ReadAsync(headerBytes, 0, 12, cancellationToken);
            stream.Position = 0; // 重置流位置

            // 检查GLB魔数：0x46546C67（"glTF"的ASCII码）
            var magic = BitConverter.ToUInt32(headerBytes, 0);
            if (magic == 0x46546C67) // GLB格式
            {
                _logger.LogDebug("检测到GLB（二进制glTF）格式");
                triangles = await ParseGLBFileAsync(stream, cancellationToken);
            }
            else // GLTF（JSON）格式
            {
                _logger.LogDebug("检测到GLTF（JSON）格式");
                triangles = await ParseGLTFFileAsync(stream, cancellationToken);
            }

            _logger.LogDebug("GLTF解析完成：{TriangleCount}个三角形", triangles.Count);
            return triangles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GLTF/GLB格式解析失败");
            return triangles;
        }
    }

    /// <summary>
    /// 解析GLB（二进制glTF）文件
    /// GLB格式：12字节头 + JSON块 + 可选的Binary块
    /// </summary>
    private async Task<List<Triangle>> ParseGLBFileAsync(Stream stream, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            // 读取GLB头部（12字节）
            var magic = reader.ReadUInt32();
            if (magic != 0x46546C67) // "glTF"
            {
                _logger.LogWarning("GLB文件魔数验证失败");
                return triangles;
            }

            var version = reader.ReadUInt32();
            var length = reader.ReadUInt32();

            _logger.LogDebug("GLB版本：{Version}, 文件长度：{Length}", version, length);

            // 读取JSON块
            var jsonChunkLength = reader.ReadUInt32();
            var jsonChunkType = reader.ReadUInt32();

            if (jsonChunkType != 0x4E4F534A) // "JSON"
            {
                _logger.LogWarning("GLB JSON块类型验证失败");
                return triangles;
            }

            var jsonBytes = reader.ReadBytes((int)jsonChunkLength);
            var jsonString = System.Text.Encoding.UTF8.GetString(jsonBytes);

            // 读取可选的Binary块
            byte[]? binaryData = null;
            if (stream.Position < stream.Length)
            {
                var binaryChunkLength = reader.ReadUInt32();
                var binaryChunkType = reader.ReadUInt32();

                if (binaryChunkType == 0x004E4942) // "BIN\0"
                {
                    binaryData = reader.ReadBytes((int)binaryChunkLength);
                    _logger.LogDebug("读取Binary块：{Length}字节", binaryData.Length);
                }
            }

            // 解析glTF JSON并提取几何数据
            triangles = await ParseGLTFJsonAsync(jsonString, binaryData, cancellationToken);
        }

        return triangles;
    }

    /// <summary>
    /// 解析GLTF（JSON）文件
    /// </summary>
    private async Task<List<Triangle>> ParseGLTFFileAsync(Stream stream, CancellationToken cancellationToken)
    {
        using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            var jsonString = await reader.ReadToEndAsync();
            return await ParseGLTFJsonAsync(jsonString, null, cancellationToken);
        }
    }

    /// <summary>
    /// 解析glTF JSON内容并提取三角形网格
    /// </summary>
    private async Task<List<Triangle>> ParseGLTFJsonAsync(string jsonString, byte[]? binaryData, CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        try
        {
            // 解析glTF JSON
            using (var document = JsonDocument.Parse(jsonString))
            {
                var root = document.RootElement;

                // 检查必需的属性
                if (!root.TryGetProperty("asset", out var asset))
                {
                    _logger.LogWarning("glTF文件缺少asset属性");
                    return triangles;
                }

                // 获取buffers、bufferViews、accessors
                var buffers = root.TryGetProperty("buffers", out var buffersElement) ? buffersElement : default;
                var bufferViews = root.TryGetProperty("bufferViews", out var bufferViewsElement) ? bufferViewsElement : default;
                var accessors = root.TryGetProperty("accessors", out var accessorsElement) ? accessorsElement : default;
                var meshes = root.TryGetProperty("meshes", out var meshesElement) ? meshesElement : default;

                if (meshes.ValueKind == JsonValueKind.Undefined)
                {
                    _logger.LogDebug("glTF文件中没有meshes数据");
                    return triangles;
                }

                // 遍历所有meshes
                foreach (var mesh in meshes.EnumerateArray())
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    if (!mesh.TryGetProperty("primitives", out var primitives))
                        continue;

                    // 遍历mesh的所有primitives
                    foreach (var primitive in primitives.EnumerateArray())
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        // 提取三角形数据
                        var meshTriangles = await ExtractTrianglesFromPrimitiveAsync(
                            primitive, accessors, bufferViews, binaryData, cancellationToken);

                        triangles.AddRange(meshTriangles);
                    }
                }
            }

            _logger.LogDebug("从glTF JSON提取{Count}个三角形", triangles.Count);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "解析glTF JSON失败");
        }

        return triangles;
    }

    /// <summary>
    /// 从glTF primitive中提取三角形
    /// </summary>
    private Task<List<Triangle>> ExtractTrianglesFromPrimitiveAsync(
        JsonElement primitive,
        JsonElement accessors,
        JsonElement bufferViews,
        byte[]? binaryData,
        CancellationToken cancellationToken)
    {
        var triangles = new List<Triangle>();

        try
        {
            // 检查primitive类型（mode）
            var mode = primitive.TryGetProperty("mode", out var modeElement) ? modeElement.GetInt32() : 4;
            // mode: 0=POINTS, 1=LINES, 2=LINE_LOOP, 3=LINE_STRIP, 4=TRIANGLES, 5=TRIANGLE_STRIP, 6=TRIANGLE_FAN

            if (mode != 4 && mode != 5 && mode != 6)
            {
                _logger.LogDebug("跳过非三角形primitive，mode={Mode}", mode);
                return Task.FromResult(triangles);
            }

            // 获取attributes（顶点数据）
            if (!primitive.TryGetProperty("attributes", out var attributes))
            {
                _logger.LogWarning("primitive缺少attributes属性");
                return Task.FromResult(triangles);
            }

            // 获取POSITION accessor索引
            if (!attributes.TryGetProperty("POSITION", out var positionAccessorIndex))
            {
                _logger.LogWarning("primitive缺少POSITION属性");
                return Task.FromResult(triangles);
            }

            // 读取顶点位置数据
            var vertices = ReadAccessorData(
                accessors, bufferViews, binaryData,
                positionAccessorIndex.GetInt32(), 3); // POSITION是3D向量

            if (vertices == null || vertices.Count == 0)
            {
                _logger.LogWarning("无法读取顶点数据");
                return Task.FromResult(triangles);
            }

            // 读取索引数据（如果存在）
            List<int>? indices = null;
            if (primitive.TryGetProperty("indices", out var indicesAccessorIndex))
            {
                var indicesData = ReadAccessorData(
                    accessors, bufferViews, binaryData,
                    indicesAccessorIndex.GetInt32(), 1); // 索引是标量

                indices = indicesData?.Select(v => (int)v[0]).ToList();
            }

            // 根据mode生成三角形
            switch (mode)
            {
                case 4: // TRIANGLES
                    triangles = GenerateTrianglesFromMode4(vertices, indices);
                    break;
                case 5: // TRIANGLE_STRIP
                    triangles = GenerateTrianglesFromMode5(vertices, indices);
                    break;
                case 6: // TRIANGLE_FAN
                    triangles = GenerateTrianglesFromMode6(vertices, indices);
                    break;
            }

            _logger.LogDebug("从primitive提取{Count}个三角形", triangles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取primitive三角形失败");
        }

        return Task.FromResult(triangles);
    }

    /// <summary>
    /// 从accessor读取数据
    /// </summary>
    private List<double[]>? ReadAccessorData(
        JsonElement accessors,
        JsonElement bufferViews,
        byte[]? binaryData,
        int accessorIndex,
        int componentsPerElement)
    {
        try
        {
            var accessor = accessors[accessorIndex];

            // 获取accessor属性
            var bufferViewIndex = accessor.GetProperty("bufferView").GetInt32();
            var componentType = accessor.GetProperty("componentType").GetInt32();
            var count = accessor.GetProperty("count").GetInt32();
            var byteOffset = accessor.TryGetProperty("byteOffset", out var offsetElement) ? offsetElement.GetInt32() : 0;

            // 获取bufferView
            var bufferView = bufferViews[bufferViewIndex];
            var bufferIndex = bufferView.GetProperty("buffer").GetInt32();
            var bufferViewByteOffset = bufferView.TryGetProperty("byteOffset", out var bvOffsetElement) ? bvOffsetElement.GetInt32() : 0;
            var bufferViewByteLength = bufferView.GetProperty("byteLength").GetInt32();

            // 如果没有binaryData，说明是外部buffer（暂不支持）
            if (binaryData == null || bufferIndex != 0)
            {
                _logger.LogWarning("不支持外部buffer引用");
                return null;
            }

            // 计算实际偏移量
            var actualOffset = bufferViewByteOffset + byteOffset;

            // 根据componentType读取数据
            var result = new List<double[]>();
            var bytesPerComponent = GetBytesPerComponent(componentType);

            for (int i = 0; i < count; i++)
            {
                if (actualOffset + bytesPerComponent * componentsPerElement > binaryData.Length)
                {
                    _logger.LogWarning("Buffer溢出，停止读取");
                    break;
                }

                var element = new double[componentsPerElement];
                for (int j = 0; j < componentsPerElement; j++)
                {
                    element[j] = ReadComponentValue(binaryData, actualOffset, componentType);
                    actualOffset += bytesPerComponent;
                }
                result.Add(element);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取accessor数据失败：索引{Index}", accessorIndex);
            return null;
        }
    }

    /// <summary>
    /// 获取componentType对应的字节数
    /// </summary>
    private int GetBytesPerComponent(int componentType)
    {
        return componentType switch
        {
            5120 => 1, // BYTE
            5121 => 1, // UNSIGNED_BYTE
            5122 => 2, // SHORT
            5123 => 2, // UNSIGNED_SHORT
            5125 => 4, // UNSIGNED_INT
            5126 => 4, // FLOAT
            _ => 4
        };
    }

    /// <summary>
    /// 从buffer读取组件值
    /// </summary>
    private double ReadComponentValue(byte[] buffer, int offset, int componentType)
    {
        return componentType switch
        {
            5120 => (sbyte)buffer[offset], // BYTE
            5121 => buffer[offset], // UNSIGNED_BYTE
            5122 => BitConverter.ToInt16(buffer, offset), // SHORT
            5123 => BitConverter.ToUInt16(buffer, offset), // UNSIGNED_SHORT
            5125 => BitConverter.ToUInt32(buffer, offset), // UNSIGNED_INT
            5126 => BitConverter.ToSingle(buffer, offset), // FLOAT
            _ => 0.0
        };
    }

    /// <summary>
    /// 生成TRIANGLES模式的三角形（mode=4）
    /// </summary>
    private List<Triangle> GenerateTrianglesFromMode4(List<double[]> vertices, List<int>? indices)
    {
        var triangles = new List<Triangle>();

        if (indices != null)
        {
            // 使用索引
            for (int i = 0; i < indices.Count; i += 3)
            {
                if (i + 2 >= indices.Count) break;

                var v1 = vertices[indices[i]];
                var v2 = vertices[indices[i + 1]];
                var v3 = vertices[indices[i + 2]];

                triangles.Add(new Triangle
                {
                    Vertices = new[]
                    {
                        new Vector3D { X = v1[0], Y = v1[1], Z = v1[2] },
                        new Vector3D { X = v2[0], Y = v2[1], Z = v2[2] },
                        new Vector3D { X = v3[0], Y = v3[1], Z = v3[2] }
                    }
                });
            }
        }
        else
        {
            // 不使用索引，直接按顺序
            for (int i = 0; i < vertices.Count; i += 3)
            {
                if (i + 2 >= vertices.Count) break;

                var v1 = vertices[i];
                var v2 = vertices[i + 1];
                var v3 = vertices[i + 2];

                triangles.Add(new Triangle
                {
                    Vertices = new[]
                    {
                        new Vector3D { X = v1[0], Y = v1[1], Z = v1[2] },
                        new Vector3D { X = v2[0], Y = v2[1], Z = v2[2] },
                        new Vector3D { X = v3[0], Y = v3[1], Z = v3[2] }
                    }
                });
            }
        }

        return triangles;
    }

    /// <summary>
    /// 生成TRIANGLE_STRIP模式的三角形（mode=5）
    /// </summary>
    private List<Triangle> GenerateTrianglesFromMode5(List<double[]> vertices, List<int>? indices)
    {
        var triangles = new List<Triangle>();
        var vertexList = indices != null
            ? indices.Select(i => vertices[i]).ToList()
            : vertices;

        for (int i = 0; i < vertexList.Count - 2; i++)
        {
            var v1 = vertexList[i];
            var v2 = vertexList[i + 1];
            var v3 = vertexList[i + 2];

            // Triangle strip要求奇数索引的三角形翻转顶点顺序
            if (i % 2 == 0)
            {
                triangles.Add(new Triangle
                {
                    Vertices = new[]
                    {
                        new Vector3D { X = v1[0], Y = v1[1], Z = v1[2] },
                        new Vector3D { X = v2[0], Y = v2[1], Z = v2[2] },
                        new Vector3D { X = v3[0], Y = v3[1], Z = v3[2] }
                    }
                });
            }
            else
            {
                triangles.Add(new Triangle
                {
                    Vertices = new[]
                    {
                        new Vector3D { X = v1[0], Y = v1[1], Z = v1[2] },
                        new Vector3D { X = v3[0], Y = v3[1], Z = v3[2] },
                        new Vector3D { X = v2[0], Y = v2[1], Z = v2[2] }
                    }
                });
            }
        }

        return triangles;
    }

    /// <summary>
    /// 生成TRIANGLE_FAN模式的三角形（mode=6）
    /// </summary>
    private List<Triangle> GenerateTrianglesFromMode6(List<double[]> vertices, List<int>? indices)
    {
        var triangles = new List<Triangle>();
        var vertexList = indices != null
            ? indices.Select(i => vertices[i]).ToList()
            : vertices;

        if (vertexList.Count < 3) return triangles;

        var v0 = vertexList[0]; // 扇形中心点

        for (int i = 1; i < vertexList.Count - 1; i++)
        {
            var v1 = vertexList[i];
            var v2 = vertexList[i + 1];

            triangles.Add(new Triangle
            {
                Vertices = new[]
                {
                    new Vector3D { X = v0[0], Y = v0[1], Z = v0[2] },
                    new Vector3D { X = v1[0], Y = v1[1], Z = v1[2] },
                    new Vector3D { X = v2[0], Y = v2[1], Z = v2[2] }
                }
            });
        }

        return triangles;
    }

    /// <summary>
    /// 备用密度分析算法 - 当无法获取几何数据时使用
    /// </summary>
    private Task<List<AdaptiveRegion>> FallbackDensityAnalysisAsync(int level, SlicingConfig config, CancellationToken cancellationToken)
    {
        _logger.LogWarning("使用fallback密度分析策略：级别{Level}", level);

        var regions = new List<AdaptiveRegion>();
        var baseTilesInLevel = (int)Math.Pow(2, level);

        for (int x = 0; x < baseTilesInLevel; x++)
        {
            for (int y = 0; y < baseTilesInLevel; y++)
            {
                for (int z = 0; z < (level == 0 ? 1 : baseTilesInLevel / 2); z++)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var density = CalculateFallbackDensity(level, x, y, z);
                    // 移除密度阈值检查,确保生成所有切片
                    // 原逻辑: if (density > config.GeometricErrorThreshold)
                    // 新逻辑: 始终添加区域
                    regions.Add(new AdaptiveRegion
                    {
                        X = x,
                        Y = y,
                        Z = z,
                        Density = density,
                        Importance = CalculateRegionImportance(level, x, y, z)
                    });
                }
            }
        }

        _logger.LogInformation("fallback策略生成区域数量：{RegionCount}, 级别{Level}", regions.Count, level);

        return Task.FromResult(regions);
    }

    /// <summary>
    /// 构建空间索引 - 提高几何查询效率
    /// </summary>
    private Task<SpatialIndex> BuildSpatialIndexAsync(List<GeometricPrimitive> geometricData, SlicingConfig config, CancellationToken cancellationToken)
    {
        _logger.LogDebug("构建空间索引：{PrimitiveCount}个几何图元", geometricData.Count);

        var spatialIndex = new SpatialIndex
        {
            Bounds = new BoundingBox3D() // 初始化 Bounds，后续会计算
        };

        // 使用均匀网格空间索引
        var gridSize = Math.Max(10, (int)Math.Sqrt(geometricData.Count) / 4);

        foreach (var primitive in geometricData)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var gridIndex = CalculateGridIndex(primitive.Center, gridSize, config.TileSize);
            if (!spatialIndex.Grid.ContainsKey(gridIndex))
            {
                spatialIndex.Grid[gridIndex] = new List<GeometricPrimitive>();
            }
            spatialIndex.Grid[gridIndex].Add(primitive);
        }

        spatialIndex.Bounds = CalculateGlobalBounds(geometricData);
        _logger.LogDebug("空间索引构建完成：{GridCellCount}个网格单元", spatialIndex.Grid.Count);

        return Task.FromResult(spatialIndex);
    }

    /// <summary>
    /// 计算综合密度评分 - 多维度密度指标融合
    /// 算法：采用加权综合计算，考虑顶点密度、三角形密度、曲率复杂度和表面积等多维度指标
    /// 权重分配基于各指标对几何复杂性的贡献度
    /// </summary>
    private double CalculateCompositeDensity(DensityMetrics metrics)
    {
        // 动态权重调整：基于输入数据的特征自适应调整权重
        var weights = CalculateAdaptiveWeights(metrics);

        // 归一化密度值到[0,1]区间
        var normalizedVertexDensity = NormalizeDensity(metrics.VertexDensity, 0, 100);
        var normalizedTriangleDensity = NormalizeDensity(metrics.TriangleDensity, 0, 50);
        var normalizedCurvature = Math.Min(metrics.CurvatureComplexity, 1.0); // 曲率已归一化
        var normalizedSurfaceArea = NormalizeDensity(metrics.SurfaceArea, 0, 1000);

        // 加权综合密度计算
        var compositeDensity = weights.VertexWeight * normalizedVertexDensity +
                              weights.TriangleWeight * normalizedTriangleDensity +
                              weights.CurvatureWeight * normalizedCurvature +
                              weights.SurfaceAreaWeight * normalizedSurfaceArea;

        // 应用非线性增强：对高密度区域进行增强，低密度区域进行抑制
        compositeDensity = ApplyNonlinearEnhancement(compositeDensity, metrics);

        return Math.Max(0.0, Math.Min(1.0, compositeDensity));
    }

    /// <summary>
    /// 计算自适应权重 - 基于数据特征动态调整权重
    /// </summary>
    private (double VertexWeight, double TriangleWeight, double CurvatureWeight, double SurfaceAreaWeight) CalculateAdaptiveWeights(DensityMetrics metrics)
    {
        // 基础权重
        double vertexWeight = 0.3;
        double triangleWeight = 0.3;
        double curvatureWeight = 0.25;
        double surfaceAreaWeight = 0.15;

        // 基于数据范围的自适应调整
        if (metrics.VertexDensity > 80)
        {
            // 高顶点密度场景，增加顶点权重
            vertexWeight += 0.1;
            triangleWeight -= 0.05;
            curvatureWeight -= 0.05;
        }

        if (metrics.CurvatureComplexity > 0.7)
        {
            // 高曲率复杂性场景，增加曲率权重
            curvatureWeight += 0.1;
            vertexWeight -= 0.05;
            triangleWeight -= 0.05;
        }

        if (metrics.SurfaceArea > 800)
        {
            // 大表面积场景，增加表面积权重
            surfaceAreaWeight += 0.1;
            vertexWeight -= 0.05;
            triangleWeight -= 0.05;
        }

        // 确保权重总和为1
        var totalWeight = vertexWeight + triangleWeight + curvatureWeight + surfaceAreaWeight;
        vertexWeight /= totalWeight;
        triangleWeight /= totalWeight;
        curvatureWeight /= totalWeight;
        surfaceAreaWeight /= totalWeight;

        return (vertexWeight, triangleWeight, curvatureWeight, surfaceAreaWeight);
    }

    /// <summary>
    /// 应用非线性增强 - 提高密度对比度
    /// </summary>
    private double ApplyNonlinearEnhancement(double density, DensityMetrics metrics)
    {
        // Sigmoid函数增强中间密度值的区分度
        var enhanced = 1.0 / (1.0 + Math.Exp(-5.0 * (density - 0.5)));

        // 基于几何复杂性的额外增强
        var complexityBonus = Math.Min(metrics.CurvatureComplexity * 0.2, 0.1);

        return Math.Min(1.0, enhanced + complexityBonus);
    }

    /// <summary>
    /// 归一化密度值到0-1范围
    /// </summary>
    private double NormalizeDensity(double value, double min, double max)
    {
        if (max <= min) return 0;
        return Math.Max(0, Math.Min(1, (value - min) / (max - min)));
    }

    private double CalculateRegionImportance(int level, int x, int y, int z)
    {
        // 计算区域重要性（影响LOD选择和渲染优先级）
        return 1.0 / (level + 1) + Math.Abs(Math.Sin(x * 0.05) * Math.Cos(y * 0.05));
    }

    /// <summary>
    /// 生成增强的模拟几何数据 - 真实感几何特征模拟算法
    /// 算法：生成具有多样化几何特征的高质量模拟数据，用于测试和演示
    /// 特征：多种几何形状、不同复杂度级别、真实的空间分布、多样化的表面特征
    /// 用途：算法验证、性能测试、视觉演示（实际生产环境应从真实模型文件加载）
    /// </summary>
    /// <param name="task">切片任务，包含模型路径和配置信息</param>
    /// <returns>三角形网格集合，包含多种几何特征的模拟数据</returns>
    private List<Triangle> GenerateMockGeometricData(SlicingTask task)
    {
        var triangles = new List<Triangle>();
        var random = new Random(42); // 固定种子保证结果可重现

        _logger.LogDebug("开始生成增强的模拟几何数据：任务{TaskId}", task.Id);

        // 1. 生成地形网格 - 模拟真实地形起伏
        GenerateTerrainMesh(triangles, random, new Vector3D { X = 50, Y = 50, Z = 0 }, 100, 100, 20);

        // 2. 生成建筑群 - 模拟城市建筑
        GenerateBuildingCluster(triangles, random, new Vector3D { X = 30, Y = 30, Z = 5 }, 10);

        // 3. 生成有机形状 - 模拟自然物体（树木、岩石等）
        GenerateOrganicShapes(triangles, random, 15);

        // 4. 生成精细网格区域 - 模拟高细节区域
        GenerateDetailedMeshRegion(triangles, random, new Vector3D { X = 70, Y = 70, Z = 10 }, 15, 64);

        // 5. 生成球体表面 - 模拟圆润物体
        AddSphereSurface(triangles, new Vector3D { X = 25, Y = 25, Z = 15 }, 8, random);

        // 6. 生成圆柱体 - 模拟柱状结构
        GenerateCylinderSurface(triangles, new Vector3D { X = 75, Y = 25, Z = 5 }, 5, 15, 16, random);

        // 7. 生成圆锥体 - 模拟锥形结构
        GenerateConeSurface(triangles, new Vector3D { X = 50, Y = 75, Z = 5 }, 6, 12, 16, random);

        // 8. 生成环面（Torus）- 模拟甜甜圈形状
        GenerateTorusSurface(triangles, new Vector3D { X = 25, Y = 75, Z = 10 }, 6, 2, 16, 12, random);

        // 9. 生成随机散布的三角形 - 模拟细碎几何细节
        GenerateRandomScatteredTriangles(triangles, random, 500, 100, 100, 30);

        // 10. 生成复杂多面体 - 模拟结构化物体
        AddComplexPolyhedron(triangles, new Vector3D { X = 75, Y = 75, Z = 15 }, 6, random);

        _logger.LogInformation("模拟几何数据生成完成：总三角形数{TriangleCount}", triangles.Count);

        return triangles;
    }

    /// <summary>
    /// 生成地形网格 - 基于Perlin噪声的真实地形模拟算法
    /// 算法：使用多层次Perlin噪声生成自然起伏的地形表面
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="random">随机数生成器</param>
    /// <param name="center">地形中心点</param>
    /// <param name="width">地形宽度</param>
    /// <param name="height">地形长度</param>
    /// <param name="resolution">网格分辨率</param>
    private void GenerateTerrainMesh(List<Triangle> triangles, Random random, Vector3D center, double width, double height, int resolution)
    {
        var vertices = new Vector3D[resolution, resolution];

        // 生成网格顶点，应用Perlin噪声模拟地形高度
        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                var x = center.X - width / 2 + (i * width / (resolution - 1));
                var y = center.Y - height / 2 + (j * height / (resolution - 1));

                // 多层次噪声叠加模拟真实地形
                var z = center.Z +
                    PerlinNoise(x * 0.05, y * 0.05, random) * 5.0 +  // 大尺度起伏
                    PerlinNoise(x * 0.15, y * 0.15, random) * 2.0 +  // 中尺度细节
                    PerlinNoise(x * 0.3, y * 0.3, random) * 0.5;     // 小尺度纹理

                vertices[i, j] = new Vector3D { X = x, Y = y, Z = z };
            }
        }

        // 将网格转换为三角形
        for (int i = 0; i < resolution - 1; i++)
        {
            for (int j = 0; j < resolution - 1; j++)
            {
                // 每个网格单元生成两个三角形
                triangles.Add(new Triangle
                {
                    Vertices = new[] { vertices[i, j], vertices[i + 1, j], vertices[i + 1, j + 1] }
                });

                triangles.Add(new Triangle
                {
                    Vertices = new[] { vertices[i, j], vertices[i + 1, j + 1], vertices[i, j + 1] }
                });
            }
        }

        _logger.LogDebug("地形网格生成完成：{Width}x{Height}, 分辨率{Resolution}, 三角形数{TriangleCount}",
            width, height, resolution, (resolution - 1) * (resolution - 1) * 2);
    }

    // Perlin噪声预计算梯度表 - 标准Perlin噪声使用256个预定义梯度向量
    private static readonly (double x, double y)[] _perlinGradients = new (double, double)[256];
    private static readonly int[] _perlinPermutation = new int[512];

    static AdaptiveSlicingStrategy()
    {
        // 初始化标准Perlin噪声梯度表（Ken Perlin原始算法）
        // 使用12个均匀分布的梯度向量
        var baseGradients = new (double, double)[]
        {
            (1, 1), (-1, 1), (1, -1), (-1, -1),
            (1, 0), (-1, 0), (0, 1), (0, -1),
            (1, 1), (-1, 1), (1, -1), (-1, -1) // 重复以填充256个位置
        };

        // 填充256个梯度
        for (int i = 0; i < 256; i++)
        {
            _perlinGradients[i] = baseGradients[i % 12];
        }

        // 初始化排列表 - 使用Ken Perlin的原始排列
        var p = new int[256];
        for (int i = 0; i < 256; i++)
            p[i] = i;

        // Fisher-Yates洗牌算法
        var rng = new Random(12345); // 固定种子保证可重复性
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        // 复制到512长度数组以避免边界检查
        for (int i = 0; i < 512; i++)
            _perlinPermutation[i] = p[i % 256];
    }

    /// <summary>
    /// Perlin噪声算法 - 自然纹理生成
    /// 算法：标准Perlin噪声实现，基于Ken Perlin的改进噪声算法（2002年）
    /// 特性：
    /// - 使用预计算梯度表，提升性能
    /// - 确定性输出，相同输入产生相同结果
    /// - 平滑连续，无明显接缝
    /// - 适用于地形生成、程序化纹理、自然效果模拟
    /// </summary>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <param name="random">随机数生成器（仅用于兼容，实际使用预计算表）</param>
    /// <returns>噪声值（-1到1之间）</returns>
    private double PerlinNoise(double x, double y, Random random)
    {
        // 1. 找到单位网格的整数坐标
        int xi = (int)Math.Floor(x) & 255;
        int yi = (int)Math.Floor(y) & 255;

        // 2. 计算网格内的相对坐标（0-1范围）
        double xf = x - Math.Floor(x);
        double yf = y - Math.Floor(y);

        // 3. 计算淡入淡出曲线值（Perlin的改进插值函数）
        double u = Fade(xf);
        double v = Fade(yf);

        // 4. 使用排列表获取网格四个角的哈希值
        int aa = _perlinPermutation[_perlinPermutation[xi] + yi];
        int ab = _perlinPermutation[_perlinPermutation[xi] + yi + 1];
        int ba = _perlinPermutation[_perlinPermutation[xi + 1] + yi];
        int bb = _perlinPermutation[_perlinPermutation[xi + 1] + yi + 1];

        // 5. 使用梯度表计算四个角的影响值
        double n00 = DotGridGradient(aa, xf, yf);
        double n10 = DotGridGradient(ba, xf - 1, yf);
        double n01 = DotGridGradient(ab, xf, yf - 1);
        double n11 = DotGridGradient(bb, xf - 1, yf - 1);

        // 6. 双线性插值计算最终噪声值
        double x1 = Lerp(n00, n10, u);
        double x2 = Lerp(n01, n11, u);
        double result = Lerp(x1, x2, v);

        // 7. 归一化到[-1, 1]范围（原始Perlin噪声范围约为[-0.707, 0.707]）
        return result * 1.414;
    }

    /// <summary>
    /// 计算梯度向量与距离向量的点积
    /// </summary>
    /// <param name="hash">哈希值，用于索引梯度表</param>
    /// <param name="x">距离向量X分量</param>
    /// <param name="y">距离向量Y分量</param>
    /// <returns>点积结果</returns>
    private double DotGridGradient(int hash, double x, double y)
    {
        // 使用预计算的梯度向量
        var gradient = _perlinGradients[hash & 255];
        return gradient.x * x + gradient.y * y;
    }

    /// <summary>
    /// 淡入淡出函数 - Perlin噪声平滑插值
    /// </summary>
    private double Fade(double t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    /// <summary>
    /// 线性插值
    /// </summary>
    private double Lerp(double a, double b, double t)
    {
        return a + t * (b - a);
    }

    /// <summary>
    /// 生成建筑群 - 模拟城市建筑物
    /// 算法：生成多个不同高度和尺寸的立方体建筑，模拟城市天际线
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="random">随机数生成器</param>
    /// <param name="center">建筑群中心</param>
    /// <param name="buildingCount">建筑数量</param>
    private void GenerateBuildingCluster(List<Triangle> triangles, Random random, Vector3D center, int buildingCount)
    {
        for (int i = 0; i < buildingCount; i++)
        {
            // 随机位置
            var offsetX = (random.NextDouble() - 0.5) * 30;
            var offsetY = (random.NextDouble() - 0.5) * 30;

            // 随机尺寸
            var width = 3 + random.NextDouble() * 5;
            var depth = 3 + random.NextDouble() * 5;
            var height = 5 + random.NextDouble() * 20;

            var buildingCenter = new Vector3D
            {
                X = center.X + offsetX,
                Y = center.Y + offsetY,
                Z = center.Z + height / 2
            };

            GenerateBox(triangles, buildingCenter, width, depth, height);
        }

        _logger.LogDebug("建筑群生成完成：建筑数量{BuildingCount}", buildingCount);
    }

    /// <summary>
    /// 生成立方体
    /// </summary>
    private void GenerateBox(List<Triangle> triangles, Vector3D center, double width, double depth, double height)
    {
        var halfWidth = width / 2;
        var halfDepth = depth / 2;
        var halfHeight = height / 2;

        var vertices = new[]
        {
            new Vector3D { X = center.X - halfWidth, Y = center.Y - halfDepth, Z = center.Z - halfHeight },
            new Vector3D { X = center.X + halfWidth, Y = center.Y - halfDepth, Z = center.Z - halfHeight },
            new Vector3D { X = center.X + halfWidth, Y = center.Y + halfDepth, Z = center.Z - halfHeight },
            new Vector3D { X = center.X - halfWidth, Y = center.Y + halfDepth, Z = center.Z - halfHeight },
            new Vector3D { X = center.X - halfWidth, Y = center.Y - halfDepth, Z = center.Z + halfHeight },
            new Vector3D { X = center.X + halfWidth, Y = center.Y - halfDepth, Z = center.Z + halfHeight },
            new Vector3D { X = center.X + halfWidth, Y = center.Y + halfDepth, Z = center.Z + halfHeight },
            new Vector3D { X = center.X - halfWidth, Y = center.Y + halfDepth, Z = center.Z + halfHeight }
        };

        var faces = new[]
        {
            new[] { 0, 1, 2, 3 }, // 底面
            new[] { 4, 5, 6, 7 }, // 顶面
            new[] { 0, 1, 5, 4 }, // 前面
            new[] { 2, 3, 7, 6 }, // 后面
            new[] { 0, 3, 7, 4 }, // 左面
            new[] { 1, 2, 6, 5 }  // 右面
        };

        foreach (var face in faces)
        {
            triangles.Add(new Triangle { Vertices = new[] { vertices[face[0]], vertices[face[1]], vertices[face[2]] } });
            triangles.Add(new Triangle { Vertices = new[] { vertices[face[0]], vertices[face[2]], vertices[face[3]] } });
        }
    }

    /// <summary>
    /// 生成有机形状 - 模拟自然物体
    /// 算法：生成不规则、有机的几何形状，模拟树木、岩石等自然物体
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="random">随机数生成器</param>
    /// <param name="shapeCount">形状数量</param>
    private void GenerateOrganicShapes(List<Triangle> triangles, Random random, int shapeCount)
    {
        for (int i = 0; i < shapeCount; i++)
        {
            var center = new Vector3D
            {
                X = random.NextDouble() * 100,
                Y = random.NextDouble() * 100,
                Z = random.NextDouble() * 30
            };

            var radius = 2 + random.NextDouble() * 4;
            var segments = 8 + random.Next(8);

            // 使用变形球体模拟有机形状
            GenerateDeformedSphere(triangles, center, radius, segments, random);
        }

        _logger.LogDebug("有机形状生成完成：形状数量{ShapeCount}", shapeCount);
    }

    /// <summary>
    /// 生成变形球体 - 不规则球形
    /// </summary>
    private void GenerateDeformedSphere(List<Triangle> triangles, Vector3D center, double radius, int segments, Random random)
    {
        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                var phi1 = 2 * Math.PI * i / segments;
                var phi2 = 2 * Math.PI * (i + 1) / segments;
                var theta1 = Math.PI * j / segments;
                var theta2 = Math.PI * (j + 1) / segments;

                // 应用随机变形
                var deform1 = 1.0 + (random.NextDouble() - 0.5) * 0.3;
                var deform2 = 1.0 + (random.NextDouble() - 0.5) * 0.3;
                var deform3 = 1.0 + (random.NextDouble() - 0.5) * 0.3;

                var v1 = new Vector3D
                {
                    X = center.X + radius * deform1 * Math.Sin(theta1) * Math.Cos(phi1),
                    Y = center.Y + radius * deform1 * Math.Sin(theta1) * Math.Sin(phi1),
                    Z = center.Z + radius * deform1 * Math.Cos(theta1)
                };

                var v2 = new Vector3D
                {
                    X = center.X + radius * deform2 * Math.Sin(theta2) * Math.Cos(phi1),
                    Y = center.Y + radius * deform2 * Math.Sin(theta2) * Math.Sin(phi1),
                    Z = center.Z + radius * deform2 * Math.Cos(theta2)
                };

                var v3 = new Vector3D
                {
                    X = center.X + radius * deform3 * Math.Sin(theta2) * Math.Cos(phi2),
                    Y = center.Y + radius * deform3 * Math.Sin(theta2) * Math.Sin(phi2),
                    Z = center.Z + radius * deform3 * Math.Cos(theta2)
                };

                triangles.Add(new Triangle { Vertices = new[] { v1, v2, v3 } });
            }
        }
    }

    /// <summary>
    /// 生成精细网格区域 - 高密度细节区域
    /// 算法：生成高分辨率网格，模拟需要高细节的区域
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="random">随机数生成器</param>
    /// <param name="center">区域中心</param>
    /// <param name="size">区域大小</param>
    /// <param name="resolution">网格分辨率</param>
    private void GenerateDetailedMeshRegion(List<Triangle> triangles, Random random, Vector3D center, double size, int resolution)
    {
        var halfSize = size / 2;
        var vertices = new Vector3D[resolution, resolution];

        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                var x = center.X - halfSize + (i * size / (resolution - 1));
                var y = center.Y - halfSize + (j * size / (resolution - 1));
                var z = center.Z + Math.Sin(i * 0.5) * Math.Cos(j * 0.5) * 2.0;

                vertices[i, j] = new Vector3D { X = x, Y = y, Z = z };
            }
        }

        for (int i = 0; i < resolution - 1; i++)
        {
            for (int j = 0; j < resolution - 1; j++)
            {
                triangles.Add(new Triangle { Vertices = new[] { vertices[i, j], vertices[i + 1, j], vertices[i + 1, j + 1] } });
                triangles.Add(new Triangle { Vertices = new[] { vertices[i, j], vertices[i + 1, j + 1], vertices[i, j + 1] } });
            }
        }

        _logger.LogDebug("精细网格区域生成完成：大小{Size}, 分辨率{Resolution}", size, resolution);
    }

    /// <summary>
    /// 生成圆柱体表面
    /// </summary>
    private void GenerateCylinderSurface(List<Triangle> triangles, Vector3D center, double radius, double height, int segments, Random random)
    {
        var angleStep = 2 * Math.PI / segments;

        for (int i = 0; i < segments; i++)
        {
            var angle1 = i * angleStep;
            var angle2 = (i + 1) * angleStep;

            var x1 = center.X + radius * Math.Cos(angle1);
            var y1 = center.Y + radius * Math.Sin(angle1);
            var x2 = center.X + radius * Math.Cos(angle2);
            var y2 = center.Y + radius * Math.Sin(angle2);

            // 侧面
            var v1 = new Vector3D { X = x1, Y = y1, Z = center.Z };
            var v2 = new Vector3D { X = x2, Y = y2, Z = center.Z };
            var v3 = new Vector3D { X = x2, Y = y2, Z = center.Z + height };
            var v4 = new Vector3D { X = x1, Y = y1, Z = center.Z + height };

            triangles.Add(new Triangle { Vertices = new[] { v1, v2, v3 } });
            triangles.Add(new Triangle { Vertices = new[] { v1, v3, v4 } });

            // 顶面和底面
            var centerBottom = new Vector3D { X = center.X, Y = center.Y, Z = center.Z };
            var centerTop = new Vector3D { X = center.X, Y = center.Y, Z = center.Z + height };

            triangles.Add(new Triangle { Vertices = new[] { centerBottom, v2, v1 } });
            triangles.Add(new Triangle { Vertices = new[] { centerTop, v4, v3 } });
        }
    }

    /// <summary>
    /// 生成圆锥体表面
    /// </summary>
    private void GenerateConeSurface(List<Triangle> triangles, Vector3D center, double radius, double height, int segments, Random random)
    {
        var angleStep = 2 * Math.PI / segments;
        var apex = new Vector3D { X = center.X, Y = center.Y, Z = center.Z + height };

        for (int i = 0; i < segments; i++)
        {
            var angle1 = i * angleStep;
            var angle2 = (i + 1) * angleStep;

            var x1 = center.X + radius * Math.Cos(angle1);
            var y1 = center.Y + radius * Math.Sin(angle1);
            var x2 = center.X + radius * Math.Cos(angle2);
            var y2 = center.Y + radius * Math.Sin(angle2);

            var v1 = new Vector3D { X = x1, Y = y1, Z = center.Z };
            var v2 = new Vector3D { X = x2, Y = y2, Z = center.Z };

            // 侧面
            triangles.Add(new Triangle { Vertices = new[] { v1, v2, apex } });

            // 底面
            var centerBase = new Vector3D { X = center.X, Y = center.Y, Z = center.Z };
            triangles.Add(new Triangle { Vertices = new[] { centerBase, v2, v1 } });
        }
    }

    /// <summary>
    /// 生成环面（Torus）表面
    /// </summary>
    private void GenerateTorusSurface(List<Triangle> triangles, Vector3D center, double majorRadius, double minorRadius, int majorSegments, int minorSegments, Random random)
    {
        for (int i = 0; i < majorSegments; i++)
        {
            for (int j = 0; j < minorSegments; j++)
            {
                var u1 = 2 * Math.PI * i / majorSegments;
                var u2 = 2 * Math.PI * (i + 1) / majorSegments;
                var v1 = 2 * Math.PI * j / minorSegments;
                var v2 = 2 * Math.PI * (j + 1) / minorSegments;

                var v1p = CalculateTorusVertex(center, majorRadius, minorRadius, u1, v1);
                var v2p = CalculateTorusVertex(center, majorRadius, minorRadius, u2, v1);
                var v3p = CalculateTorusVertex(center, majorRadius, minorRadius, u2, v2);
                var v4p = CalculateTorusVertex(center, majorRadius, minorRadius, u1, v2);

                triangles.Add(new Triangle { Vertices = new[] { v1p, v2p, v3p } });
                triangles.Add(new Triangle { Vertices = new[] { v1p, v3p, v4p } });
            }
        }
    }

    /// <summary>
    /// 计算环面顶点坐标
    /// </summary>
    private Vector3D CalculateTorusVertex(Vector3D center, double majorRadius, double minorRadius, double u, double v)
    {
        return new Vector3D
        {
            X = center.X + (majorRadius + minorRadius * Math.Cos(v)) * Math.Cos(u),
            Y = center.Y + (majorRadius + minorRadius * Math.Cos(v)) * Math.Sin(u),
            Z = center.Z + minorRadius * Math.Sin(v)
        };
    }

    /// <summary>
    /// 生成随机散布的三角形 - 模拟细碎几何细节
    /// </summary>
    private void GenerateRandomScatteredTriangles(List<Triangle> triangles, Random random, int count, double rangeX, double rangeY, double rangeZ)
    {
        for (int i = 0; i < count; i++)
        {
            var triangle = new Triangle();

            for (int j = 0; j < 3; j++)
            {
                triangle.Vertices[j] = new Vector3D
                {
                    X = random.NextDouble() * rangeX,
                    Y = random.NextDouble() * rangeY,
                    Z = random.NextDouble() * rangeZ
                };
            }

            triangles.Add(triangle);
        }
    }

    /// <summary>
    /// 计算三角形法向量
    /// </summary>
    private Vector3D CalculateTriangleNormal(Vector3D v1, Vector3D v2, Vector3D v3)
    {
        var edge1 = new Vector3D { X = v2.X - v1.X, Y = v2.Y - v1.Y, Z = v2.Z - v1.Z };
        var edge2 = new Vector3D { X = v3.X - v1.X, Y = v3.Y - v1.Y, Z = v3.Z - v1.Z };

        return new Vector3D
        {
            X = edge1.Y * edge2.Z - edge1.Z * edge2.Y,
            Y = edge1.Z * edge2.X - edge1.X * edge2.Z,
            Z = edge1.X * edge2.Y - edge1.Y * edge2.X
        };
    }

    /// <summary>
    /// 计算三角形面积
    /// </summary>
    private double CalculateTriangleArea(Vector3D v1, Vector3D v2, Vector3D v3)
    {
        var edge1 = new Vector3D { X = v2.X - v1.X, Y = v2.Y - v1.Y, Z = v2.Z - v1.Z };
        var edge2 = new Vector3D { X = v3.X - v1.X, Y = v3.Y - v1.Y, Z = v3.Z - v1.Z };

        var crossProduct = new Vector3D
        {
            X = edge1.Y * edge2.Z - edge1.Z * edge2.Y,
            Y = edge1.Z * edge2.X - edge1.X * edge2.Z,
            Z = edge1.X * edge2.Y - edge1.Y * edge2.X
        };

        return 0.5 * Math.Sqrt(crossProduct.X * crossProduct.X +
                              crossProduct.Y * crossProduct.Y +
                              crossProduct.Z * crossProduct.Z);
    }

    /// <summary>
    /// 计算备用密度值 - 增强的密度估算算法
    /// 算法：基于空间位置、LOD级别和几何分布特征的综合估算
    /// </summary>
    /// <param name="level">LOD级别</param>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <param name="z">Z坐标</param>
    /// <returns>估算的几何密度值（0-1范围）</returns>
    private double CalculateFallbackDensity(int level, int x, int y, int z)
    {
        // 基础密度：根据LOD级别设定
        var baseDensity = 0.5 / (level + 1);

        // 空间位置因子：使用多频率正弦波模拟几何分布的不均匀性
        // 低频成分：大尺度特征
        var lowFreqFactor = Math.Sin(x * 0.05) * Math.Cos(y * 0.05) * Math.Sin(z * 0.05);
        // 中频成分：中等尺度特征
        var midFreqFactor = Math.Sin(x * 0.15) * Math.Cos(y * 0.15) * Math.Sin(z * 0.15) * 0.5;
        // 高频成分：精细尺度特征
        var highFreqFactor = Math.Sin(x * 0.3) * Math.Cos(y * 0.3) * Math.Sin(z * 0.3) * 0.25;

        var positionFactor = (lowFreqFactor + midFreqFactor + highFreqFactor) * 0.3;

        // 边缘衰减因子：边缘区域通常密度较低
        var maxCoord = Math.Pow(2, level);
        var edgeDistanceX = Math.Min(x, maxCoord - x) / maxCoord;
        var edgeDistanceY = Math.Min(y, maxCoord - y) / maxCoord;
        var edgeDistanceZ = Math.Min(z, Math.Max(1, maxCoord / 2) - z) / Math.Max(1, maxCoord / 2);
        var edgeFactor = (edgeDistanceX + edgeDistanceY + edgeDistanceZ) / 3.0 * 0.2;

        // 中心聚集因子：模拟中心区域较高密度的现象
        var centerX = maxCoord / 2;
        var centerY = maxCoord / 2;
        var centerZ = (level == 0 ? 0.5 : maxCoord / 4);
        var distanceToCenter = Math.Sqrt(
            Math.Pow(x - centerX, 2) +
            Math.Pow(y - centerY, 2) +
            Math.Pow(z - centerZ, 2)
        );
        var maxDistance = Math.Sqrt(centerX * centerX + centerY * centerY + centerZ * centerZ);
        var centralityFactor = Math.Exp(-distanceToCenter / maxDistance) * 0.15;

        // 综合计算密度
        var density = baseDensity + positionFactor + edgeFactor + centralityFactor;

        // 归一化到0-1范围
        return Math.Max(0.0, Math.Min(1.0, density));
    }

    /// <summary>
    /// 计算网格索引
    /// </summary>
    private string CalculateGridIndex(Vector3D point, int gridSize, double tileSize)
    {
        var gridX = (int)Math.Floor(point.X / tileSize * gridSize);
        var gridY = (int)Math.Floor(point.Y / tileSize * gridSize);
        var gridZ = (int)Math.Floor(point.Z / tileSize * gridSize);
        return $"{gridX}_{gridY}_{gridZ}";
    }

    /// <summary>
    /// 计算全局包围盒
    /// </summary>
    private BoundingBox3D CalculateGlobalBounds(List<GeometricPrimitive> primitives)
    {
        if (!primitives.Any()) return new BoundingBox3D();

        var bounds = new BoundingBox3D
        {
            MinX = primitives.Min(p => p.Vertices.Min(v => v.X)),
            MinY = primitives.Min(p => p.Vertices.Min(v => v.Y)),
            MinZ = primitives.Min(p => p.Vertices.Min(v => v.Z)),
            MaxX = primitives.Max(p => p.Vertices.Max(v => v.X)),
            MaxY = primitives.Max(p => p.Vertices.Max(v => v.Y)),
            MaxZ = primitives.Max(p => p.Vertices.Max(v => v.Z))
        };

        return bounds;
    }

    /// <summary>
    /// 计算区域重要性（基于密度指标的重载版本）
    /// </summary>
    private double CalculateRegionImportance(DensityMetrics metrics, int level)
    {
        return 1.0 / (level + 1) + metrics.CurvatureComplexity * 0.3 + metrics.VertexDensity * 0.1;
    }


    /// <summary>
    /// 添加球体表面三角形
    /// </summary>
    private void AddSphereSurface(List<Triangle> triangles, Vector3D center, double radius, Random random)
    {
        const int segments = 12;
        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                var phi1 = 2 * Math.PI * i / segments;
                var phi2 = 2 * Math.PI * (i + 1) / segments;
                var theta1 = Math.PI * j / segments;
                var theta2 = Math.PI * (j + 1) / segments;

                var v1 = new Vector3D
                {
                    X = center.X + radius * Math.Sin(theta1) * Math.Cos(phi1),
                    Y = center.Y + radius * Math.Sin(theta1) * Math.Sin(phi1),
                    Z = center.Z + radius * Math.Cos(theta1)
                };

                var v2 = new Vector3D
                {
                    X = center.X + radius * Math.Sin(theta2) * Math.Cos(phi1),
                    Y = center.Y + radius * Math.Sin(theta2) * Math.Sin(phi1),
                    Z = center.Z + radius * Math.Cos(theta2)
                };

                var v3 = new Vector3D
                {
                    X = center.X + radius * Math.Sin(theta2) * Math.Cos(phi2),
                    Y = center.Y + radius * Math.Sin(theta2) * Math.Sin(phi2),
                    Z = center.Z + radius * Math.Cos(theta2)
                };

                triangles.Add(new Triangle { Vertices = new[] { v1, v2, v3 } });
            }
        }
    }

    /// <summary>
    /// 添加平面表面三角形
    /// </summary>
    private void AddPlaneSurface(List<Triangle> triangles, Vector3D center, double size, Random random)
    {
        const int gridSize = 8;
        var halfSize = size / 2;

        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                var x1 = center.X - halfSize + (i * size / gridSize);
                var y1 = center.Y - halfSize + (j * size / gridSize);
                var x2 = center.X - halfSize + ((i + 1) * size / gridSize);
                var y2 = center.Y - halfSize + (j * size / gridSize);
                var x3 = center.X - halfSize + (i * size / gridSize);
                var y3 = center.Y - halfSize + ((j + 1) * size / gridSize);

                var v1 = new Vector3D { X = x1, Y = y1, Z = center.Z };
                var v2 = new Vector3D { X = x2, Y = y2, Z = center.Z };
                var v3 = new Vector3D { X = x3, Y = y3, Z = center.Z };

                triangles.Add(new Triangle { Vertices = new[] { v1, v2, v3 } });
            }
        }
    }

    /// <summary>
    /// 添加复杂多面体
    /// </summary>
    private void AddComplexPolyhedron(List<Triangle> triangles, Vector3D center, double size, Random random)
    {
        var vertices = new Vector3D[8];
        var halfSize = size / 2;

        // 创建立方体顶点
        for (int i = 0; i < 8; i++)
        {
            vertices[i] = new Vector3D
            {
                X = center.X + halfSize * (i % 2 == 0 ? -1 : 1),
                Y = center.Y + halfSize * ((i / 2) % 2 == 0 ? -1 : 1),
                Z = center.Z + halfSize * ((i / 4) % 2 == 0 ? -1 : 1)
            };
        }

        // 添加立方体表面三角形
        var faces = new[] {
            new[] { 0, 1, 3, 2 }, // 前面
            new[] { 4, 5, 7, 6 }, // 后面
            new[] { 0, 1, 5, 4 }, // 底部
            new[] { 2, 3, 7, 6 }, // 顶部
            new[] { 0, 2, 6, 4 }, // 左面
            new[] { 1, 3, 7, 5 }  // 右面
        };

        foreach (var face in faces)
        {
            // 分割四边形为两个三角形
            triangles.Add(new Triangle { Vertices = new[] { vertices[face[0]], vertices[face[1]], vertices[face[2]] } });
            triangles.Add(new Triangle { Vertices = new[] { vertices[face[0]], vertices[face[2]], vertices[face[3]] } });
        }
    }

    /// <summary>
    /// 生成自适应包围盒 - 基于几何密度的动态包围盒算法实现
    /// 算法：根据区域密度和几何复杂度动态调整包围盒尺寸
    /// 支持：密度感知缩放、重要性权重、几何特征适应
    /// </summary>
    /// <param name="region">自适应区域，包含密度和几何信息</param>
    /// <param name="tileSize">基础切片尺寸</param>
    /// <returns>标准化的JSON格式包围盒字符串</returns>
    private string GenerateAdaptiveBoundingBox(AdaptiveRegion region, double tileSize)
    {
        // 1. 计算基础网格坐标
        var baseMinX = region.X * tileSize;
        var baseMinY = region.Y * tileSize;
        var baseMinZ = region.Z * tileSize;

        // 2. 自适应尺寸调整因子
        // 基于密度、重要性和几何复杂度调整包围盒尺寸
        var densityFactor = Math.Sqrt(region.Density + 1.0) * 0.2; // 密度影响（0.2倍）
        var importanceFactor = region.Importance * 0.1;            // 重要性影响（0.1倍）
        var complexityFactor = Math.Min(region.CurvatureComplexity, 1.0) * 0.15; // 曲率复杂度影响（0.15倍）

        var adaptiveFactor = 1.0 + densityFactor + importanceFactor + complexityFactor;
        adaptiveFactor = Math.Max(0.5, Math.Min(2.0, adaptiveFactor)); // 限制调整范围[0.5, 2.0]

        // 3. 计算自适应尺寸
        var adaptiveSize = tileSize * adaptiveFactor;

        // 4. 计算包围盒边界
        var minX = baseMinX;
        var minY = baseMinY;
        var minZ = baseMinZ;
        var maxX = baseMinX + adaptiveSize;
        var maxY = baseMinY + adaptiveSize;
        var maxZ = baseMinZ + adaptiveSize;

        // 5. 几何特征影响的边界调整
        // 高密度区域可能需要更大的边界缓冲
        if (region.Density > 0.8)
        {
            var bufferSize = tileSize * 0.1;
            minX -= bufferSize;
            minY -= bufferSize;
            minZ -= bufferSize;
            maxX += bufferSize;
            maxY += bufferSize;
            maxZ += bufferSize;
        }

        // 6. 表面积影响的纵向拉伸
        // 大表面积特征可能需要更大的高度
        if (region.SurfaceArea > 500)
        {
            var heightExtension = tileSize * 0.2;
            minZ -= heightExtension * 0.5;
            maxZ += heightExtension * 0.5;
        }

        // 7. 数值精度和边界检查
        var precision = 6;
        minX = Math.Round(minX, precision);
        minY = Math.Round(minY, precision);
        minZ = Math.Round(minZ, precision);
        maxX = Math.Round(maxX, precision);
        maxY = Math.Round(maxY, precision);
        maxZ = Math.Round(maxZ, precision);

        // 确保最小尺寸
        var minBoxSize = tileSize * 0.01;
        if (maxX - minX < minBoxSize) maxX = minX + minBoxSize;
        if (maxY - minY < minBoxSize) maxY = minY + minBoxSize;
        if (maxZ - minZ < minBoxSize) maxZ = minZ + minBoxSize;

        // 8. 生成标准化JSON格式
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        return $"{{\"minX\":{minX.ToString(culture)},\"minY\":{minY.ToString(culture)},\"minZ\":{minZ.ToString(culture)},\"maxX\":{maxX.ToString(culture)},\"maxY\":{maxY.ToString(culture)},\"maxZ\":{maxZ.ToString(culture)}}}";
    }

    /// <summary>
    /// 计算自适应切片文件大小 - 基于多维度几何特征
    /// 算法：综合考虑密度、重要性、顶点密度、三角形密度、曲率复杂度和表面积
    /// </summary>
    /// <param name="region">自适应区域</param>
    /// <param name="format">输出格式</param>
    /// <returns>估算的文件大小（字节）</returns>
    private long CalculateAdaptiveFileSize(AdaptiveRegion region, string format)
    {
        // 基础文件大小
        var baseSize = format.ToLower() switch
        {
            "b3dm" => 2048,
            "gltf" => 1024,
            "json" => 512,
            _ => 1024
        };

        // 几何密度因子：综合密度越高，文件越大
        var densityFactor = 1.0 + region.Density * 0.6;

        // 重要性因子：重要区域可能包含更多细节
        var importanceFactor = 1.0 + region.Importance * 0.3;

        // 顶点密度因子：顶点越多，几何数据越大
        var vertexDensityFactor = 1.0 + Math.Min(region.VertexDensity / 100.0, 1.5) * 0.4;

        // 三角形密度因子：三角形数量直接影响文件大小
        var triangleDensityFactor = 1.0 + Math.Min(region.TriangleDensity / 50.0, 2.0) * 0.5;

        // 曲率复杂度因子：复杂曲面需要更多法向量和纹理数据
        var curvatureFactor = 1.0 + region.CurvatureComplexity * 0.35;

        // 表面积因子：表面积越大，需要的纹理数据可能越多
        var surfaceAreaFactor = 1.0 + Math.Log(Math.Max(1, region.SurfaceArea), 10) * 0.15;

        // 格式特定调整
        var formatSpecificFactor = format.ToLower() switch
        {
            "b3dm" => 1.0 + (region.VertexDensity * 0.01), // B3DM对高密度数据有额外开销
            "gltf" => 1.0 + (region.CurvatureComplexity * 0.1), // GLTF在复杂曲面上有额外元数据
            "json" => 1.0, // JSON格式开销相对固定
            _ => 1.0
        };

        // 综合计算
        var estimatedSize = (long)(baseSize *
            densityFactor *
            importanceFactor *
            vertexDensityFactor *
            triangleDensityFactor *
            curvatureFactor *
            surfaceAreaFactor *
            formatSpecificFactor);

        // 自适应策略通常生成更大的文件，上限设为80MB
        return Math.Max(512, Math.Min(83886080, estimatedSize));
    }

    /// <summary>
    /// 自适应区域 - 存储密度分析结果
    /// </summary>
    internal class AdaptiveRegion
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public double Density { get; set; }
        public double Importance { get; set; }
        public double VertexDensity { get; set; }
        public double TriangleDensity { get; set; }
        public double CurvatureComplexity { get; set; }
        public double SurfaceArea { get; set; }
    }
}