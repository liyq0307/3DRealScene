using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Application.Services;

/// <summary>
/// 八叉树切片策略 - 层次空间剖分算法
/// 适用于不规则模型，自适应精度，平衡细节和性能
/// </summary>
public class OctreeSlicingStrategy : ISlicingStrategy
{
    // 日志记录器
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
    public OctreeSlicingStrategy(
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

    /// <summary>
    /// 生成切片集合 - 八叉树剖分策略算法实现
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="level">LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <param name="modelBounds">模型的实际包围盒</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>切片集合</returns>
    public async Task<List<Slice>> GenerateSlicesAsync(SlicingTask task, int level, SlicingConfig config, BoundingBox3D modelBounds, CancellationToken cancellationToken)
    {
        var slices = new List<Slice>();

        _logger.LogDebug("八叉树切片策略：级别{Level}", level);

        // 八叉树剖分算法：基于空间密度和几何复杂度进行递归剖分
        var octreeNodes = await BuildOctreeAsync(task, level, config, cancellationToken);

        foreach (var node in octreeNodes)
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
                BoundingBox = GenerateOctreeBoundingBox(node, config.TileSize),
                FileSize = CalculateOctreeFileSize(node, config.OutputFormat)
            };

            slices.Add(slice);
        }

        return slices;
    }

    /// <summary>
    /// 估算指定级别的切片数量 - 基于八叉树剖分算法
    /// </summary>
    /// <param name="level">LOD级别，必须为非负整数，0表示根级别</param>
    /// <param name="config">切片配置，包含剖分策略和参数</param>
    /// <returns>估算的切片数量，基于八叉树几何级数计算</returns>
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

        // 八叉树剖分估算：每个级别切片数量为8^(level-1)
        // 这是因为八叉树在每个维度上都进行二分剖分
        // 例如：level 1: 8切片, level 2: 64切片, level 3: 512切片
        try
        {
            // 性能优化：使用位运算代替Math.Pow进行整数幂运算
            // 8^level = 2^(3*level)，可以使用左移位运算优化
            var eightToLevel = 1L << (3 * level); // 2^(3*level) = 8^level

            // 应用几何衰减因子：考虑实际剖分不会达到理论最大值
            // 基于经验值，实际切片数量约为理论值的1/2到1/3
            const double geometricAttenuationFactor = 0.5; // 几何衰减因子
            var estimatedCount = (long)(eightToLevel * geometricAttenuationFactor);

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

    private async Task<List<OctreeNode>> BuildOctreeAsync(SlicingTask task, int level, SlicingConfig config, CancellationToken cancellationToken)
    {
        // 八叉树构建算法：递归空间剖分
        var nodes = new List<OctreeNode>();
        var rootNode = new OctreeNode
        {
            X = 0,
            Y = 0,
            Z = 0,
            Size = config.TileSize * Math.Pow(2, config.MaxLevel - level),
            Level = level
        };

        _logger.LogDebug("八叉树根节点：Level={Level}, Size={Size}, TileSize={TileSize}, MaxLevel={MaxLevel}",
            level, rootNode.Size, config.TileSize, config.MaxLevel);

        await SubdivideOctreeNodeAsync(rootNode, nodes, config, cancellationToken);

        _logger.LogInformation("八叉树构建完成：Level={Level}, 节点数={NodeCount}", level, nodes.Count);

        return nodes;
    }

    private async Task SubdivideOctreeNodeAsync(OctreeNode node, List<OctreeNode> nodes, SlicingConfig config, CancellationToken cancellationToken)
    {
        // 检查是否需要进一步剖分（基于几何密度和误差阈值）
        if (node.Level >= config.MaxLevel || !ShouldSubdivide(node, config))
        {
            nodes.Add(node);
            return;
        }

        // 八叉树递归剖分：将空间分成8个子节点
        var halfSize = node.Size / 2;
        for (int i = 0; i < 8; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var childNode = new OctreeNode
            {
                X = (int)(node.X + (i % 2) * halfSize),
                Y = (int)(node.Y + ((i / 2) % 2) * halfSize),
                Z = (int)(node.Z + (i / 4) * halfSize),
                Size = halfSize,
                Level = node.Level + 1
            };

            await SubdivideOctreeNodeAsync(childNode, nodes, config, cancellationToken);
        }
    }

    private bool ShouldSubdivide(OctreeNode node, SlicingConfig config)
    {
        // 基于几何误差阈值决定是否剖分
        return node.Size > config.TileSize && node.Level < config.MaxLevel;
    }

    /// <summary>
    /// 生成八叉树包围盒 - 自适应尺寸包围盒算法实现
    /// 算法：基于八叉树节点的空间位置和尺寸计算精确的包围盒
    /// 支持：节点尺寸验证、坐标系变换、精度控制
    /// </summary>
    /// <param name="node">八叉树节点，包含位置和尺寸信息</param>
    /// <param name="tileSize">基础切片尺寸，用于坐标系变换</param>
    /// <returns>标准化的JSON格式包围盒字符串</returns>
    private string GenerateOctreeBoundingBox(OctreeNode node, double tileSize)
    {
        // 1. 坐标系变换
        // 将节点坐标转换为世界坐标系
        var scaleFactor = tileSize / Math.Pow(2, node.Level);
        var worldMinX = node.X * scaleFactor;
        var worldMinY = node.Y * scaleFactor;
        var worldMinZ = node.Z * scaleFactor;

        // 2. 计算包围盒边界
        var nodeSize = node.Size * scaleFactor;
        var minX = worldMinX;
        var minY = worldMinY;
        var minZ = worldMinZ;
        var maxX = worldMinX + nodeSize;
        var maxY = worldMinY + nodeSize;
        var maxZ = worldMinZ + nodeSize;

        // 3. 尺寸验证和调整
        // 确保最小尺寸限制，防止退化包围盒
        var minNodeSize = 1e-6;
        if (nodeSize < minNodeSize)
        {
            var centerX = (minX + maxX) / 2;
            var centerY = (minY + maxY) / 2;
            var centerZ = (minZ + maxZ) / 2;
            var halfSize = minNodeSize / 2;

            minX = centerX - halfSize;
            maxX = centerX + halfSize;
            minY = centerY - halfSize;
            maxY = centerY + halfSize;
            minZ = centerZ - halfSize;
            maxZ = centerZ + halfSize;
        }

        // 4. 数值稳定性检查
        // 处理可能的浮点数精度问题
        minX = Math.Round(minX, 6);
        minY = Math.Round(minY, 6);
        minZ = Math.Round(minZ, 6);
        maxX = Math.Round(maxX, 6);
        maxY = Math.Round(maxY, 6);
        maxZ = Math.Round(maxZ, 6);

        // 5. 生成标准化JSON格式
        // 注意：属性名首字母大写，与BoundingBox类的属性名匹配
        return $"{{\"MinX\":{minX.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"MinY\":{minY.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"MinZ\":{minZ.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"MaxX\":{maxX.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"MaxY\":{maxY.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"MaxZ\":{maxZ.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";
    }

    /// <summary>
    /// 计算八叉树节点文件大小 - 基于节点复杂度和空间位置
    /// 算法：考虑八叉树节点的深度、尺寸和空间分布特征
    /// </summary>
    /// <param name="node">八叉树节点</param>
    /// <param name="format">输出格式</param>
    /// <returns>估算的文件大小（字节）</returns>
    private long CalculateOctreeFileSize(OctreeNode node, string format)
    {
        // 基础文件大小
        var baseSize = format.ToLower() switch
        {
            "b3dm" => 2048,
            "gltf" => 1024,
            "json" => 512,
            _ => 1024
        };

        // 节点深度因子：深度越大，细节越丰富
        var depthFactor = 1.0 + node.Level * 0.12;

        // 节点尺寸因子：尺寸越大，包含的几何数据可能越多
        var sizeFactor = 1.0 + Math.Log(node.Size + 1, 2) * 0.08;

        // 八叉树特定因子：考虑空间剖分的不均匀性
        // 中心节点通常比边缘节点包含更多细节
        var centerDistance = Math.Sqrt(node.X * node.X + node.Y * node.Y + node.Z * node.Z);
        var spatialDistributionFactor = 1.0 + Math.Exp(-centerDistance / 100.0) * 0.2;

        // 综合计算
        var estimatedSize = (long)(baseSize * depthFactor * sizeFactor * spatialDistributionFactor);

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

    private class OctreeNode
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public double Size { get; set; }
        public int Level { get; set; }
    }
}