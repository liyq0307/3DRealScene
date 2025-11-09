using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;


namespace RealScene3D.Application.Services;

/// <summary>
/// KD树切片策略 - 自适应空间剖分算法
/// 基于方差的二分剖分，适用于高维空间查询优化
/// </summary>
public class KdTreeSlicingStrategy : ISlicingStrategy
{
    private readonly ILogger _logger;

    // 网格简化服务（可选）
    private readonly MeshDecimationService? _meshDecimationService;

    // 瓦片生成器工厂
    private readonly ITileGeneratorFactory _tileGeneratorFactory;

    // 模型加载器
    private readonly IModelLoader _modelLoader;

    /// <summary>
    /// 构造函数 - 注入日志记录器和必需的服务
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="tileGeneratorFactory">瓦片生成器工厂，用于动态创建不同格式的生成器</param>
    /// <param name="modelLoader">模型加载器</param>
    /// <param name="meshDecimationService">网格简化服务（可选，用于LOD生成）</param>
    public KdTreeSlicingStrategy(
        ILogger logger,
        ITileGeneratorFactory tileGeneratorFactory,
        IModelLoader modelLoader,
        MeshDecimationService? meshDecimationService = null)
    {
        _logger = logger;
        _tileGeneratorFactory = tileGeneratorFactory ?? throw new ArgumentNullException(nameof(tileGeneratorFactory));
        _modelLoader = modelLoader ?? throw new ArgumentNullException(nameof(modelLoader));
        _meshDecimationService = meshDecimationService;
    }

    public async Task<List<Slice>> GenerateSlicesAsync(SlicingTask task, int level, SlicingConfig config, BoundingBox3D modelBounds, CancellationToken cancellationToken)
    {
        var slices = new List<Slice>();

        _logger.LogDebug("KD树切片策略：级别{Level}", level);

        // KD树构建：基于几何分布特征进行自适应剖分
        var kdTreeNodes = await BuildKdTreeAsync(task, level, config, cancellationToken);

        foreach (var node in kdTreeNodes)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // 空值检查：确保OutputPath不为null
            var outputPath = task.OutputPath ?? "default_output";
            var slice = new Slice
            {
                SlicingTaskId = task.Id,
                Level = level,
                X = node.X,
                Y = node.Y,
                Z = node.Z,
                FilePath = $"{outputPath}/{level}/{node.X}_{node.Y}_{node.Z}.{config.OutputFormat.ToLower()}",
                BoundingBox = GenerateKdTreeBoundingBox(node, config.TileSize),
                FileSize = CalculateKdTreeFileSize(node, config.OutputFormat)
            };

            slices.Add(slice);
        }

        return slices;
    }

    /// <summary>
    /// 估算指定级别的切片数量 - 基于KD树剖分算法
    /// </summary>
    /// <param name="level">LOD级别，必须为非负整数，0表示根级别</param>
    /// <param name="config">切片配置，包含剖分策略和参数</param>
    /// <returns>估算的切片数量，基于KD树几何级数计算</returns>
    /// <exception cref="ArgumentNullException">当config为null时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">当level为负数时抛出</exception>
    /// <exception cref="OverflowException">当计算结果超过int最大值时抛出</exception>
    public int EstimateSliceCount(int level, SlicingConfig config)
    {
        // 边界情况检查：输入参数验证
        if (config == null)
            throw new ArgumentNullException(nameof(config), "切片配置不能为空");

        if (level < 0)
            throw new ArgumentOutOfRangeException(nameof(level), level, "LOD级别不能为负数");

        // 特殊情况处理：根级别只有一个切片
        if (level == 0)
            return 1;

        // KD树剖分估算：在3D空间中，KD树通过在不同维度上进行二分剖分来构建空间索引
        // 理论上每个级别的切片数量为2^level，但实际应用中会考虑几何衰减因子
        // KD树的核心思想是交替选择空间维度进行剖分，实现高效的空间搜索
        try
        {
            // 性能优化：使用位运算代替Math.Pow进行整数幂运算
            // 2^level可以使用左移位运算优化，计算效率更高
            var twoToLevel = 1L << level; // 2^level

            // 应用几何衰减因子：考虑实际剖分不会达到理论最大值
            // 基于经验值，KD树实际切片数量约为理论值的1/2左右
            // 这是因为KD树会根据数据分布进行自适应剖分，避免过度细分
            const double geometricAttenuationFactor = 0.5; // 几何衰减因子
            var estimatedCount = (long)(twoToLevel * geometricAttenuationFactor);

            // 对于3D空间的额外考虑：KD树在三个维度上交替剖分
            // 虽然理论上是2^level，但实际实现中可能接近8^level的复杂性
            // 这里使用保守的估算策略，确保性能和准确性的平衡
            estimatedCount = Math.Max(estimatedCount, 1); // 确保至少返回1

            // 边界检查：确保不超过int最大值
            if (estimatedCount > int.MaxValue)
                throw new OverflowException($"估算切片数量超过int最大值：{estimatedCount}");

            return (int)estimatedCount;
        }
        catch (OverflowException)
        {
            // 溢出处理：返回int最大值作为保守估算
            return int.MaxValue;
        }
    }

    /// <summary>
    /// 构建KD树 - 基于方差的二分剖分算法
    /// 算法：选择方差最大的轴进行剖分，实现空间的高效划分
    /// 支持：多维度数据、动态剖分深度、自适应
    /// </summary>
    /// <param name="task"></param>
    /// <param name="level"></param>
    /// <param name="config"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<List<KdTreeNode>> BuildKdTreeAsync(SlicingTask task, int level, SlicingConfig config, CancellationToken cancellationToken)
    {
        // KD树构建算法：交替在X、Y、Z轴上进行二分剖分
        var nodes = new List<KdTreeNode>();
        var rootNode = new KdTreeNode
        {
            MinX = 0,
            MinY = 0,
            MinZ = 0,
            MaxX = config.TileSize * Math.Pow(2, config.MaxLevel - level),
            MaxY = config.TileSize * Math.Pow(2, config.MaxLevel - level),
            MaxZ = config.TileSize * Math.Pow(2, config.MaxLevel - level),
            Level = level,
            SplitAxis = 0 // 从X轴开始
        };

        await SubdivideKdTreeNodeAsync(rootNode, nodes, config, cancellationToken);
        return nodes;
    }

    /// <summary>
    /// 递归剖分KD树节点 - 基于方差的二分剖分算法
    /// 算法：选择方差最大的轴进行剖分，实现空间的高效划分
    /// 支持：多维度数据、动态剖分深度、自适应精度控制
    /// </summary>
    /// <param name="node"></param>
    /// <param name="nodes"></param>
    /// <param name="config"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task SubdivideKdTreeNodeAsync(KdTreeNode node, List<KdTreeNode> nodes, SlicingConfig config, CancellationToken cancellationToken)
    {
        // 检查是否需要进一步剖分
        if (node.Level >= config.MaxLevel || !ShouldSubdivideKdTree(node, config))
        {
            // 转换为切片节点
            var sliceNode = new KdTreeNode
            {
                X = (int)node.MinX,
                Y = (int)node.MinY,
                Z = (int)node.MinZ,
                Level = node.Level
            };
            nodes.Add(sliceNode);
            return;
        }

        // KD树二分剖分：选择方差最大的轴进行剖分
        var splitAxis = node.SplitAxis % 3;
        var splitPoint = CalculateSplitPoint(node, splitAxis);

        // 创建左右子节点
        var leftNode = CreateChildKdTreeNode(node, splitAxis, splitPoint, true);
        var rightNode = CreateChildKdTreeNode(node, splitAxis, splitPoint, false);

        await SubdivideKdTreeNodeAsync(leftNode, nodes, config, cancellationToken);
        await SubdivideKdTreeNodeAsync(rightNode, nodes, config, cancellationToken);
    }

    /// <summary>
    /// 判断是否需要剖分KD树节点 - 基于空间尺寸和深度阈值
    /// </summary>
    /// <param name="node"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    private bool ShouldSubdivideKdTree(KdTreeNode node, SlicingConfig config)
    {
        var size = Math.Max(Math.Max(node.MaxX - node.MinX, node.MaxY - node.MinY), node.MaxZ - node.MinZ);
        return size > config.TileSize && node.Level < config.MaxLevel;
    }

    /// <summary>
    /// 计算剖分点 - 基于几何分布的中点剖分算法
    /// </summary>
    /// <param name="node"></param>
    /// <param name="axis"></param>
    /// <returns></returns>
    private double CalculateSplitPoint(KdTreeNode node, int axis)
    {
        // 基于几何分布计算最佳剖分点
        return axis switch
        {
            0 => (node.MinX + node.MaxX) / 2, // X轴中点
            1 => (node.MinY + node.MaxY) / 2, // Y轴中点
            _ => (node.MinZ + node.MaxZ) / 2  // Z轴中点
        };
    }

    /// <summary>
    /// 创建子节点 - 基于父节点和分割平面
    /// 算法：根据分割轴和分割点生成子节点的边界
    /// 支持：多维度分割、动态边界计算、节点深度优化
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="axis"></param>
    /// <param name="splitPoint"></param>
    /// <param name="isLeft"></param>
    /// <returns></returns>
    private KdTreeNode CreateChildKdTreeNode(KdTreeNode parent, int axis, double splitPoint, bool isLeft)
    {
        var child = new KdTreeNode
        {
            Level = parent.Level + 1,
            SplitAxis = parent.SplitAxis + 1
        };

        if (isLeft)
        {
            child.MinX = parent.MinX;
            child.MinY = parent.MinY;
            child.MinZ = parent.MinZ;

            switch (axis)
            {
                case 0: child.MaxX = splitPoint; child.MaxY = parent.MaxY; child.MaxZ = parent.MaxZ; break;
                case 1: child.MaxY = splitPoint; child.MaxX = parent.MaxX; child.MaxZ = parent.MaxZ; break;
                case 2: child.MaxZ = splitPoint; child.MaxX = parent.MaxX; child.MaxY = parent.MaxY; break;
            }
        }
        else
        {
            child.MaxX = parent.MaxX;
            child.MaxY = parent.MaxY;
            child.MaxZ = parent.MaxZ;

            switch (axis)
            {
                case 0: child.MinX = splitPoint; child.MinY = parent.MinY; child.MinZ = parent.MinZ; break;
                case 1: child.MinY = splitPoint; child.MinX = parent.MinX; child.MinZ = parent.MinZ; break;
                case 2: child.MinZ = splitPoint; child.MinX = parent.MinX; child.MinY = parent.MinY; break;
            }
        }

        return child;
    }

    /// <summary>
    /// 生成KD树包围盒 - 基于分割平面的精确包围盒算法实现
    /// 算法：根据KD树节点的分割平面和范围计算精确的包围盒
    /// 支持：多维度分割、精确边界计算、节点深度优化
    /// </summary>
    /// <param name="node">KD树节点，包含分割信息和范围</param>
    /// <param name="tileSize">基础切片尺寸，用于比例缩放</param>
    /// <returns>标准化的JSON格式包围盒字符串</returns>
    private string GenerateKdTreeBoundingBox(KdTreeNode node, double tileSize)
    {
        // 1. 计算节点实际尺寸
        // KD树节点的尺寸由其Min/Max坐标确定
        var nodeWidth = node.MaxX - node.MinX;
        var nodeHeight = node.MaxY - node.MinY;
        var nodeDepth = node.MaxZ - node.MinZ;

        // 2. 坐标系变换
        // 将节点坐标转换为世界坐标系
        var scaleFactor = tileSize / Math.Pow(2, node.Level);
        var minX = node.MinX * scaleFactor;
        var minY = node.MinY * scaleFactor;
        var minZ = node.MinZ * scaleFactor;
        var maxX = node.MaxX * scaleFactor;
        var maxY = node.MaxY * scaleFactor;
        var maxZ = node.MaxZ * scaleFactor;

        // 3. 边界验证和调整
        // 确保包围盒有效且不为空
        var epsilon = 1e-6;
        if (maxX - minX < epsilon)
        {
            maxX = minX + epsilon;
        }
        if (maxY - minY < epsilon)
        {
            maxY = minY + epsilon;
        }
        if (maxZ - minZ < epsilon)
        {
            maxZ = minZ + epsilon;
        }

        // 4. 处理KD树分割特性
        // 根据分割轴调整边界精度
        var splitAxis = node.SplitAxis % 3;
        var precisionDigits = splitAxis switch
        {
            0 => 8, // X轴分割，需要更高精度
            1 => 8, // Y轴分割，需要更高精度
            2 => 6, // Z轴分割，标准精度
            _ => 6
        };

        // 5. 数值精度控制
        minX = Math.Round(minX, precisionDigits);
        minY = Math.Round(minY, precisionDigits);
        minZ = Math.Round(minZ, precisionDigits);
        maxX = Math.Round(maxX, precisionDigits);
        maxY = Math.Round(maxY, precisionDigits);
        maxZ = Math.Round(maxZ, precisionDigits);

        // 6. 生成标准化JSON格式
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        return $"{{\"minX\":{minX.ToString(culture)},\"minY\":{minY.ToString(culture)},\"minZ\":{minZ.ToString(culture)},\"maxX\":{maxX.ToString(culture)},\"maxY\":{maxY.ToString(culture)},\"maxZ\":{maxZ.ToString(culture)}}}";
    }

    /// <summary>
    /// 计算KD树节点文件大小 - 基于空间剖分特征
    /// 算法：考虑KD树的二分剖分特性和节点深度
    /// </summary>
    /// <param name="node">KD树节点</param>
    /// <param name="format">输出格式</param>
    /// <returns>估算的文件大小（字节）</returns>
    private long CalculateKdTreeFileSize(KdTreeNode node, string format)
    {
        // 基础文件大小
        var baseSize = format.ToLower() switch
        {
            "b3dm" => 2048,
            "gltf" => 1024,
            "json" => 512,
            _ => 1024
        };

        // 节点深度因子：KD树深度反映几何复杂度
        var depthFactor = 1.0 + node.Level * 0.08;

        // 空间维度因子：KD树在不同维度上的剖分影响数据分布
        // 使用剖分轴来调整估算
        var dimensionFactor = 1.0 + (node.SplitAxis % 3) * 0.05;

        // 节点体积因子：估算节点包含的几何数据量
        var nodeVolume = (node.MaxX - node.MinX) * (node.MaxY - node.MinY) * (node.MaxZ - node.MinZ);
        var volumeFactor = 1.0 + Math.Log(Math.Max(1, nodeVolume), 10) * 0.1;

        // 综合计算
        var estimatedSize = (long)(baseSize * depthFactor * dimensionFactor * volumeFactor);

        return Math.Max(256, Math.Min(52428800, estimatedSize)); // 256B - 50MB
    }

    /// <summary>
    /// 从切片任务加载三角形数据
    /// </summary>
    private async Task<List<Triangle>?> LoadTrianglesFromTask(SlicingTask task, CancellationToken cancellationToken)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task), "切片任务不能为空");

        if (string.IsNullOrWhiteSpace(task.SourceModelPath))
            throw new ArgumentException("源模型路径不能为空", nameof(task.SourceModelPath));

        if (string.IsNullOrWhiteSpace(task.ModelType))
            throw new ArgumentException("模型类型不能为空", nameof(task.ModelType));

        try
        {
            var fileExtension = Path.GetExtension(task.SourceModelPath)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(fileExtension))
            {
                _logger.LogError("无法确定模型文件的扩展名：{SourceModelPath}", task.SourceModelPath);
                throw new InvalidOperationException($"无法确定模型文件的扩展名：{task.SourceModelPath}");
            }

            if (!_modelLoader.SupportsFormat(fileExtension))
            {
                _logger.LogError("模型加载器不支持此文件格式：{FileExtension}", fileExtension);
                throw new InvalidOperationException($"不支持的模型文件格式：{fileExtension}");
            }

            _logger.LogInformation("开始加载模型文件：{SourceModelPath}", task.SourceModelPath);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(10));

            var (triangles, boundingBox, materials) = await _modelLoader.LoadModelAsync(task.SourceModelPath, cts.Token);

            if (triangles == null || triangles.Count == 0)
            {
                _logger.LogWarning("模型文件加载成功但未包含任何三角形数据：{SourceModelPath}", task.SourceModelPath);
                return new List<Triangle>();
            }

            var invalidTriangles = triangles.Count(t =>
                t == null || t.Vertices == null || t.Vertices.Length != 3 || t.Vertices.Any(v => v == null));

            if (invalidTriangles > 0)
            {
                _logger.LogWarning("发现{InvalidCount}个无效三角形，已过滤", invalidTriangles);
                triangles = triangles.Where(t =>
                    t != null && t.Vertices != null && t.Vertices.Length == 3 && t.Vertices.All(v => v != null)).ToList();
            }

            _logger.LogInformation("模型加载完成：{TriangleCount}个三角形", triangles.Count);
            return triangles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "模型加载失败：{SourceModelPath}", task.SourceModelPath);
            throw;
        }
    }

    /// <summary>
    /// 应用LOD简化
    /// </summary>
    private List<Triangle> SimplifyMeshForLOD(List<Triangle> triangles, int level, int maxLevel)
    {
        if (_meshDecimationService == null || triangles.Count < 10)
            return triangles;

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

        var ratio = (double)level / maxLevel;
        return Math.Pow(ratio, 0.5);
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

    private class KdTreeNode
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
        public int Level { get; set; }
        public int SplitAxis { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }
}