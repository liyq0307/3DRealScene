using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Enums;
using RealScene3D.Application.Interfaces;
using RealScene3D.Application.Services.Generators;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Application.Services.Strategys;

/// <summary>
/// 递归空间剖分策略 - 基于八叉树/四叉树的递归剖分算法
/// 从粗粒度父节点开始，递归剖分为子节点
/// 支持动态剖分深度决策，基于几何密度自适应调整
/// 类似于Obj2Tiles的递归剖分方法
/// </summary>
public class RecursiveSubdivisionStrategy : ISlicingStrategy
{
    private readonly ILogger<RecursiveSubdivisionStrategy> _logger;
    private readonly MeshDecimationService? _meshDecimationService;
    private readonly ITileGeneratorFactory _tileGeneratorFactory;
    private readonly IModelLoader _modelLoader;

    // 剖分参数
    private const int DefaultSubdivisionFactor = 2; // 默认每个维度剖分为2份（2x2x2=8个子节点）
    private const int MinTrianglesPerSlice = 100; // 每个切片最少三角形数
    private const int MaxTrianglesPerSlice = 5000; // 每个切片最多三角形数
    private const int MaxDepth = 10; // 最大递归深度

    public RecursiveSubdivisionStrategy(
        ILogger<RecursiveSubdivisionStrategy> logger,
        ITileGeneratorFactory tileGeneratorFactory,
        IModelLoader modelLoader,
        MeshDecimationService? meshDecimationService = null)
    {
        _logger = logger;
        _tileGeneratorFactory = tileGeneratorFactory ?? throw new ArgumentNullException(nameof(tileGeneratorFactory));
        _modelLoader = modelLoader ?? throw new ArgumentNullException(nameof(modelLoader));
        _meshDecimationService = meshDecimationService;
    }

    /// <summary>
    /// 生成切片 - 递归剖分实现
    /// </summary>
    public async Task<List<Slice>> GenerateSlicesAsync(
        SlicingTask task,
        int level,
        SlicingConfig config,
        BoundingBox3D modelBounds,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始递归空间剖分，LOD级别: {Level}", level);

        var allSlices = new List<Slice>();

        // 首先需要加载模型以获取三角形数据
        // 注意：这里假设 task.InputPath 是模型文件路径
        // 实际使用中可能需要调整这个逻辑
        var triangles = await LoadTrianglesFromTask(task, cancellationToken);

        if (triangles == null || triangles.Count == 0)
        {
            _logger.LogWarning("未能加载三角形数据");
            return allSlices;
        }

        _logger.LogInformation("已加载 {Count} 个三角形", triangles.Count);

        // 从根节点开始递归剖分
        await RecursiveSubdivide(
            task,
            triangles,
            modelBounds,
            level,
            0, 0, 0, // 根节点坐标
            0, // 当前深度
            config,
            allSlices,
            cancellationToken);

        _logger.LogInformation("递归剖分完成，生成 {Count} 个切片", allSlices.Count);

        return allSlices;
    }

    /// <summary>
    /// 估算切片数量 - 递归剖分的估算
    /// 由于递归深度是动态的，这里提供保守估算
    /// </summary>
    public int EstimateSliceCount(int level, SlicingConfig config)
    {
        // 保守估算：假设平均递归深度为 MaxDepth/2
        var avgDepth = MaxDepth / 2;

        // 每层节点数: 8^depth (八叉树)
        var nodesPerDepth = (int)Math.Pow(8, avgDepth);

        // 每个LOD级别的切片数
        return nodesPerDepth;
    }

    /// <summary>
    /// 递归剖分节点
    /// </summary>
    private async Task RecursiveSubdivide(
        SlicingTask task,
        List<Triangle> allTriangles,
        BoundingBox3D nodeBounds,
        int lodLevel,
        int x, int y, int z,
        int depth,
        SlicingConfig config,
        List<Slice> slices,
        CancellationToken cancellationToken)
    {
        // 检查终止条件
        if (depth >= MaxDepth)
        {
            _logger.LogDebug("达到最大深度 {Depth}，停止剖分", depth);
            return;
        }

        // 提取当前节点内的三角形
        var nodeTriangles = ExtractTrianglesInBounds(allTriangles, nodeBounds);

        if (nodeTriangles.Count == 0)
        {
            _logger.LogDebug("节点 [{X},{Y},{Z}] 无三角形，跳过", x, y, z);
            return;
        }

        // 判断是否需要继续剖分
        bool shouldSubdivide = ShouldSubdivideNode(nodeTriangles, nodeBounds, depth);

        if (!shouldSubdivide || nodeTriangles.Count < MinTrianglesPerSlice)
        {
            // 创建叶子节点切片
            var slice = await CreateSlice(
                task,
                nodeTriangles,
                nodeBounds,
                lodLevel,
                x, y, z,
                depth,
                config,
                cancellationToken);

            if (slice != null)
            {
                slices.Add(slice);
            }
            return;
        }

        // 剖分当前节点
        var childBounds = SubdivideNodeBounds(nodeBounds);

        // 递归处理所有子节点
        int childIndex = 0;
        for (int cx = 0; cx < DefaultSubdivisionFactor; cx++)
        {
            for (int cy = 0; cy < DefaultSubdivisionFactor; cy++)
            {
                for (int cz = 0; cz < DefaultSubdivisionFactor; cz++)
                {
                    var childBound = childBounds[childIndex];
                    int childX = x * DefaultSubdivisionFactor + cx;
                    int childY = y * DefaultSubdivisionFactor + cy;
                    int childZ = z * DefaultSubdivisionFactor + cz;

                    await RecursiveSubdivide(
                        task,
                        allTriangles,
                        childBound,
                        lodLevel,
                        childX, childY, childZ,
                        depth + 1,
                        config,
                        slices,
                        cancellationToken);

                    childIndex++;
                }
            }
        }
    }

    /// <summary>
    /// 判断节点是否需要继续剖分
    /// </summary>
    private bool ShouldSubdivideNode(List<Triangle> triangles, BoundingBox3D bounds, int depth)
    {
        // 基于三角形数量判断
        if (triangles.Count > MaxTrianglesPerSlice)
        {
            return true;
        }

        // 基于几何密度判断
        var volume = CalculateVolume(bounds);
        if (volume <= 0)
            return false;

        var density = triangles.Count / volume;

        // 密度阈值（可调整）
        const double densityThreshold = 10.0;

        return density > densityThreshold && depth < MaxDepth - 1;
    }

    /// <summary>
    /// 剖分节点包围盒为8个子节点
    /// </summary>
    private List<BoundingBox3D> SubdivideNodeBounds(BoundingBox3D bounds)
    {
        var result = new List<BoundingBox3D>();

        var midX = (bounds.MinX + bounds.MaxX) / 2;
        var midY = (bounds.MinY + bounds.MaxY) / 2;
        var midZ = (bounds.MinZ + bounds.MaxZ) / 2;

        // 生成8个子包围盒
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    var child = new BoundingBox3D
                    {
                        MinX = x == 0 ? bounds.MinX : midX,
                        MaxX = x == 0 ? midX : bounds.MaxX,
                        MinY = y == 0 ? bounds.MinY : midY,
                        MaxY = y == 0 ? midY : bounds.MaxY,
                        MinZ = z == 0 ? bounds.MinZ : midZ,
                        MaxZ = z == 0 ? midZ : bounds.MaxZ
                    };
                    result.Add(child);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 创建切片
    /// </summary>
    private async Task<Slice?> CreateSlice(
        SlicingTask task,
        List<Triangle> triangles,
        BoundingBox3D bounds,
        int level,
        int x, int y, int z,
        int depth,
        SlicingConfig config,
        CancellationToken cancellationToken)
    {
        try
        {
            // 应用LOD简化
            var simplifiedTriangles = SimplifyMeshForLOD(triangles, level, config.MaxLevel);

            if (simplifiedTriangles.Count == 0)
            {
                return null;
            }

            // 生成文件路径
            var fileName = $"{depth}_{x}_{y}_{z}.{config.OutputFormat}";
            var levelDir = Path.Combine(task.OutputPath ?? "output", level.ToString());
            var outputPath = Path.Combine(levelDir, fileName);

            // 确保目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 根据配置的瓦片格式动态创建生成器并保存文件
            var generator = _tileGeneratorFactory.CreateGenerator(config.TileFormat);

            // 使用统一的SaveTileAsync方法保存文件
            await (generator as TileGenerator)!.SaveTileAsync(simplifiedTriangles, bounds, outputPath);

            var fileInfo = new FileInfo(outputPath);

            return new Slice
            {
                SlicingTaskId = task.Id,
                Level = level,
                X = x,
                Y = y,
                Z = z,
                FilePath = outputPath,
                FileSize = fileInfo.Exists ? fileInfo.Length : 0,
                BoundingBox = SerializeBoundingBox(bounds),
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建切片失败: Level={Level}, X={X}, Y={Y}, Z={Z}", level, x, y, z);
            return null;
        }
    }

    /// <summary>
    /// 提取包围盒内的三角形
    /// </summary>
    private List<Triangle> ExtractTrianglesInBounds(List<Triangle> triangles, BoundingBox3D bounds)
    {
        var result = new List<Triangle>();

        foreach (var triangle in triangles)
        {
            // 检查三角形是否与包围盒相交
            if (TriangleIntersectsBounds(triangle, bounds))
            {
                result.Add(triangle);
            }
        }

        return result;
    }

    /// <summary>
    /// 检查三角形是否与包围盒相交
    /// </summary>
    private bool TriangleIntersectsBounds(Triangle triangle, BoundingBox3D bounds)
    {
        // 简单的包围盒检测：检查三角形的任一顶点是否在包围盒内
        // 更精确的实现可以使用SAT（分离轴定理）

        foreach (var vertex in triangle.Vertices)
        {
            if (vertex.X >= bounds.MinX && vertex.X <= bounds.MaxX &&
                vertex.Y >= bounds.MinY && vertex.Y <= bounds.MaxY &&
                vertex.Z >= bounds.MinZ && vertex.Z <= bounds.MaxZ)
            {
                return true;
            }
        }

        // 也检查包围盒中心是否在三角形内
        var center = bounds.GetCenter();
        var centerPoint = new Vector3D(center.X, center.Y, center.Z);

        return triangle.Contains(centerPoint);
    }

    /// <summary>
    /// 应用LOD简化
    /// </summary>
    private List<Triangle> SimplifyMeshForLOD(List<Triangle> triangles, int level, int maxLevel)
    {
        if (_meshDecimationService == null || triangles.Count < 10)
        {
            return triangles;
        }

        try
        {
            var quality = CalculateLODQuality(level, maxLevel);

            var options = new MeshDecimationService.DecimationOptions
            {
                Quality = quality,
                PreserveBoundary = true,
                MaxIterations = 100
            };

            var decimated = _meshDecimationService.SimplifyMesh(triangles, options);
            return decimated.Triangles;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "网格简化失败，使用原始网格");
            return triangles;
        }
    }

    /// <summary>
    /// 计算LOD质量因子
    /// </summary>
    private double CalculateLODQuality(int level, int maxLevel)
    {
        if (level >= maxLevel)
            return 1.0;

        // 使用平方根衰减，Level越高质量越好
        var ratio = (double)level / maxLevel;
        return Math.Pow(ratio, 0.5);
    }

    /// <summary>
    /// 计算包围盒体积
    /// </summary>
    private double CalculateVolume(BoundingBox3D bounds)
    {
        var width = bounds.MaxX - bounds.MinX;
        var height = bounds.MaxY - bounds.MinY;
        var depth = bounds.MaxZ - bounds.MinZ;

        return width * height * depth;
    }

    /// <summary>
    /// 序列化包围盒为JSON
    /// </summary>
    private string SerializeBoundingBox(BoundingBox3D bounds)
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            bounds.MinX,
            bounds.MinY,
            bounds.MinZ,
            bounds.MaxX,
            bounds.MaxY,
            bounds.MaxZ
        });
    }

    /// <summary>
    /// 从切片任务加载三角形数据
    /// </summary>
    /// <param name="task">切片任务，包含源模型路径和配置信息</param>
    /// <param name="cancellationToken">取消令牌，支持异步操作取消</param>
    /// <returns>加载的三角形列表，失败时返回null</returns>
    /// <exception cref="ArgumentNullException">当task参数为null时抛出</exception>
    /// <exception cref="ArgumentException">当任务配置无效时抛出</exception>
    /// <exception cref="InvalidOperationException">当模型加载失败时抛出</exception>
    private async Task<List<Triangle>?> LoadTrianglesFromTask(SlicingTask task, CancellationToken cancellationToken)
    {
        // 参数验证
        if (task == null)
            throw new ArgumentNullException(nameof(task), "切片任务不能为空");

        if (string.IsNullOrWhiteSpace(task.SourceModelPath))
            throw new ArgumentException("源模型路径不能为空", nameof(task.SourceModelPath));

        if (string.IsNullOrWhiteSpace(task.ModelType))
            throw new ArgumentException("模型类型不能为空", nameof(task.ModelType));

        try
        {
            // 检查模型加载器是否支持该文件格式
            var fileExtension = Path.GetExtension(task.SourceModelPath)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(fileExtension))
            {
                _logger.LogError("无法确定模型文件的扩展名：{SourceModelPath}", task.SourceModelPath);
                throw new InvalidOperationException($"无法确定模型文件的扩展名：{task.SourceModelPath}");
            }

            if (!_modelLoader.SupportsFormat(fileExtension))
            {
                _logger.LogError("模型加载器不支持此文件格式：{FileExtension}，支持的格式：{SupportedFormats}",
                    fileExtension, string.Join(", ", _modelLoader.GetSupportedFormats()));
                throw new InvalidOperationException($"不支持的模型文件格式：{fileExtension}");
            }

            // 记录加载开始
            _logger.LogInformation("开始加载模型文件：{SourceModelPath}，类型：{ModelType}，格式：{FileExtension}",
                task.SourceModelPath, task.ModelType, fileExtension);

            // 异步加载模型数据，带超时保护
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(10)); // 设置10分钟超时

            var (triangles, boundingBox, materials) = await _modelLoader.LoadModelAsync(task.SourceModelPath, cts.Token);

            // 验证加载结果
            if (triangles == null || triangles.Count == 0)
            {
                _logger.LogWarning("模型文件加载成功但未包含任何三角形数据：{SourceModelPath}", task.SourceModelPath);
                return new List<Triangle>(); // 返回空列表而不是null，避免上层代码处理null
            }

            // 验证三角形数据完整性
            var invalidTriangles = triangles.Count(t =>
                t == null ||
                t.Vertices == null ||
                t.Vertices.Length != 3 ||
                t.Vertices.Any(v => v == null));

            if (invalidTriangles > 0)
            {
                _logger.LogWarning("发现{InvalidCount}个无效三角形，已过滤", invalidTriangles);
                triangles = triangles.Where(t =>
                    t != null &&
                    t.Vertices != null &&
                    t.Vertices.Length == 3 &&
                    t.Vertices.All(v => v != null)).ToList();
            }

            // 记录加载统计信息
            _logger.LogInformation("模型加载完成：{TriangleCount}个三角形，包围盒：[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}]",
                triangles.Count,
                boundingBox.MinX, boundingBox.MinY, boundingBox.MinZ,
                boundingBox.MaxX, boundingBox.MaxY, boundingBox.MaxZ);

            return triangles;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("模型加载操作被用户取消：{SourceModelPath}", task.SourceModelPath);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("模型加载超时（10分钟）：{SourceModelPath}", task.SourceModelPath);
            throw new TimeoutException($"模型加载超时：{task.SourceModelPath}");
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "模型文件不存在：{SourceModelPath}", task.SourceModelPath);
            throw new InvalidOperationException($"模型文件不存在：{task.SourceModelPath}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "无权限访问模型文件：{SourceModelPath}", task.SourceModelPath);
            throw new InvalidOperationException($"无权限访问模型文件：{task.SourceModelPath}", ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "模型文件读取I/O错误：{SourceModelPath}", task.SourceModelPath);
            throw new InvalidOperationException($"模型文件读取失败：{task.SourceModelPath}", ex);
        }
        catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
        {
            // 对于未预期的异常，记录详细错误信息
            _logger.LogError(ex, "模型加载过程中发生未预期错误：{SourceModelPath}，错误类型：{ExceptionType}",
                task.SourceModelPath, ex.GetType().Name);
            throw new InvalidOperationException($"模型加载失败：{task.SourceModelPath}，{ex.Message}", ex);
        }
    }
}
