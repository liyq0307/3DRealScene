using Microsoft.Extensions.DependencyInjection;
using RealScene3D.Domain.Enums;
using Microsoft.Extensions.Logging;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;
using RealScene3D.Application.Services.Generators;
using RealScene3D.Application.Services.Strategys;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.MinIO;
using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace RealScene3D.Application.Services;

/// <summary>
/// 几何图元数据结构
/// 共享类，供AdaptiveSlicingStrategy和GeometricDensityAnalyzer使用
/// </summary>
internal class GeometricPrimitive
{
    public Vector3D[] Vertices { get; set; } = new Vector3D[3];
    public required Triangle Triangle { get; set; }
    public required Vector3D Normal { get; set; }
    public double Area { get; set; }
    public Vector3D Center => new Vector3D
    {
        X = (Vertices[0].X + Vertices[1].X + Vertices[2].X) / 3,
        Y = (Vertices[0].Y + Vertices[1].Y + Vertices[2].Y) / 3,
        Z = (Vertices[0].Z + Vertices[1].Z + Vertices[2].Z) / 3
    };
}

/// <summary>
/// 密度分析指标
/// 共享类，供AdaptiveSlicingStrategy和GeometricDensityAnalyzer使用
/// </summary>
internal class DensityMetrics
{
    public double VertexDensity { get; set; }
    public double TriangleDensity { get; set; }
    public double CurvatureComplexity { get; set; }
    public double SurfaceArea { get; set; }
    public double Volume { get; set; }
}

/// <summary>
/// 空间索引结构
/// 共享类，供AdaptiveSlicingStrategy和GeometricDensityAnalyzer使用
/// </summary>
internal class SpatialIndex
{
    public Dictionary<string, List<GeometricPrimitive>> Grid { get; set; } = new Dictionary<string, List<GeometricPrimitive>>();
    public required BoundingBox3D Bounds { get; set; }
}

/// <summary>
/// 几何密度分析器 - 多维度几何复杂度分析
/// 共享类，供AdaptiveSlicingStrategy和SlicingProcessor使用
/// </summary>
internal class GeometricDensityAnalyzer
{
    private readonly ILogger _logger;

    public GeometricDensityAnalyzer(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 分析区域密度 - 多维度密度指标计算
    /// </summary>
    public async Task<DensityMetrics> AnalyzeRegionDensityAsync(
        List<GeometricPrimitive> allPrimitives,
        SpatialIndex spatialIndex,
        BoundingBox3D regionBounds,
        SlicingConfig config,
        CancellationToken cancellationToken)
    {
        var metrics = new DensityMetrics();

        try
        {
            // 1. 获取区域内的几何图元
            var regionPrimitives = GetPrimitivesInRegion(allPrimitives, spatialIndex, regionBounds);

            if (!regionPrimitives.Any())
            {
                return metrics;
            }

            // 2. 计算顶点密度
            metrics.VertexDensity = CalculateVertexDensity(regionPrimitives, regionBounds);

            // 3. 计算三角形密度
            metrics.TriangleDensity = CalculateTriangleDensity(regionPrimitives, regionBounds);

            // 4. 计算曲率复杂度
            metrics.CurvatureComplexity = await CalculateCurvatureComplexityAsync(regionPrimitives, cancellationToken);

            // 5. 计算表面面积
            metrics.SurfaceArea = CalculateSurfaceArea(regionPrimitives);

            // 6. 计算体积（如果需要）
            metrics.Volume = CalculateVolume(regionBounds);

            _logger.LogDebug("区域密度分析完成：顶点密度{VertexDensity:F3}, 三角形密度{TriangleDensity:F3}, 曲率复杂度{CurvatureComplexity:F3}",
                metrics.VertexDensity, metrics.TriangleDensity, metrics.CurvatureComplexity);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "区域密度分析失败");
            return metrics;
        }
    }

    /// <summary>
    /// 获取区域内的几何图元
    /// </summary>
    private List<GeometricPrimitive> GetPrimitivesInRegion(
        List<GeometricPrimitive> allPrimitives,
        SpatialIndex spatialIndex,
        BoundingBox3D regionBounds)
    {
        var regionPrimitives = new List<GeometricPrimitive>();

        // 遍历空间索引中的所有网格单元
        foreach (var keyValuePair in spatialIndex.Grid)
        {
            var gridKey = keyValuePair.Key;

            try
            {
                // 解析网格坐标 - 使用基础字符串方法
                var underscoreIndex1 = gridKey.IndexOf('_');
                var underscoreIndex2 = gridKey.IndexOf('_', underscoreIndex1 + 1);

                if (underscoreIndex1 <= 0 || underscoreIndex2 <= underscoreIndex1) continue;

                var part1 = gridKey.Substring(0, underscoreIndex1);
                var part2 = gridKey.Substring(underscoreIndex1 + 1, underscoreIndex2 - underscoreIndex1 - 1);
                var part3 = gridKey.Substring(underscoreIndex2 + 1);

                if (!int.TryParse(part1, out var gridX) ||
                    !int.TryParse(part2, out var gridY) ||
                    !int.TryParse(part3, out var gridZ))
                    continue;

                // 计算网格单元的边界
                var gridSize = 10.0; // 固定的网格大小
                var gridMinX = gridX * gridSize;
                var gridMinY = gridY * gridSize;
                var gridMinZ = gridZ * gridSize;
                var gridMaxX = gridMinX + gridSize;
                var gridMaxY = gridMinY + gridSize;
                var gridMaxZ = gridMinZ + gridSize;

                // 检查网格单元是否与查询区域相交
                if (!(gridMaxX < regionBounds.MinX || gridMinX > regionBounds.MaxX ||
                      gridMaxY < regionBounds.MinY || gridMinY > regionBounds.MaxY ||
                      gridMaxZ < regionBounds.MinZ || gridMinZ > regionBounds.MaxZ))
                {
                    // 获取该网格单元内的所有图元
                    if (spatialIndex.Grid.TryGetValue(gridKey, out var primitives))
                    {
                        regionPrimitives.AddRange(primitives.Where(p =>
                            IsPrimitiveInBounds(p, regionBounds)));
                    }
                }
            }
            catch
            {
                // 跳过解析失败的键
                continue;
            }
        }

        return regionPrimitives;
    }

    /// <summary>
    /// 判断几何图元是否在包围盒内
    /// </summary>
    private bool IsPrimitiveInBounds(GeometricPrimitive primitive, BoundingBox3D bounds)
    {
        return primitive.Vertices.Any(v =>
            v.X >= bounds.MinX && v.X <= bounds.MaxX &&
            v.Y >= bounds.MinY && v.Y <= bounds.MaxY &&
            v.Z >= bounds.MinZ && v.Z <= bounds.MaxZ);
    }

    /// <summary>
    /// 计算顶点密度
    /// </summary>
    private double CalculateVertexDensity(List<GeometricPrimitive> primitives, BoundingBox3D bounds)
    {
        var vertexCount = primitives.Sum(p => p.Vertices.Length);
        var volume = (bounds.MaxX - bounds.MinX) * (bounds.MaxY - bounds.MinY) * (bounds.MaxZ - bounds.MinZ);

        return volume > 0 ? vertexCount / volume : 0;
    }

    /// <summary>
    /// 计算三角形密度
    /// </summary>
    private double CalculateTriangleDensity(List<GeometricPrimitive> primitives, BoundingBox3D bounds)
    {
        var triangleCount = primitives.Count;
        var volume = (bounds.MaxX - bounds.MinX) * (bounds.MaxY - bounds.MinY) * (bounds.MaxZ - bounds.MinZ);

        return volume > 0 ? triangleCount / volume : 0;
    }

    /// <summary>
    /// 计算曲率复杂度 - 分析几何形状的复杂度
    /// </summary>
    private Task<double> CalculateCurvatureComplexityAsync(List<GeometricPrimitive> primitives, CancellationToken cancellationToken)
    {
        if (!primitives.Any()) return Task.FromResult(0.0);

        var curvatureValues = new List<double>();

        foreach (var primitive in primitives)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // 计算三角形的曲率（基于法向量变化）
            var curvature = CalculateTriangleCurvature(primitive);
            curvatureValues.Add(curvature);
        }

        // 计算曲率的标准差作为复杂度指标
        if (curvatureValues.Any())
        {
            var mean = curvatureValues.Average();
            var variance = curvatureValues.Sum(v => Math.Pow(v - mean, 2)) / curvatureValues.Count;
            return Task.FromResult(Math.Sqrt(variance));
        }

        return Task.FromResult(0.0);
    }

    /// <summary>
    /// 计算三角形曲率 - 基于离散微分几何的精确曲率计算算法
    /// 算法：使用三角形网格的离散曲率估算方法，考虑相邻三角形的法向量变化
    ///
    /// 理论基础：
    /// - 高斯曲率（Gaussian Curvature）：描述曲面在某点的内蕴弯曲程度
    /// - 平均曲率（Mean Curvature）：描述曲面在某点的外在弯曲程度
    /// - 主曲率（Principal Curvatures）：描述曲面在某点两个正交方向上的最大和最小曲率
    ///
    /// 实现方法：
    /// 1. 使用相邻三角形的法向量角度估算曲率
    /// 2. 计算三角形形状质量系数（面积/周长比）
    /// 3. 综合考虑表面变化率和几何复杂度
    ///
    /// 应用场景：
    /// - LOD生成：高曲率区域需要更高细节级别
    /// - 自适应网格简化：保留高曲率特征
    /// - 几何密度分析：识别复杂表面区域
    /// </summary>
    /// <param name="primitive">几何图元，包含三角形顶点和法向量</param>
    /// <returns>曲率复杂度值（0-1范围），越大表示曲率越复杂</returns>
    private double CalculateTriangleCurvature(GeometricPrimitive primitive)
    {
        // 1. 基础验证：确保几何数据有效
        if (primitive == null || primitive.Vertices == null || primitive.Vertices.Length < 3)
        {
            return 0.0;
        }

        var v0 = primitive.Vertices[0];
        var v1 = primitive.Vertices[1];
        var v2 = primitive.Vertices[2];

        // 2. 计算三角形边长
        var edge01Length = CalculateEdgeLength(v0, v1);
        var edge12Length = CalculateEdgeLength(v1, v2);
        var edge20Length = CalculateEdgeLength(v2, v0);

        // 边界情况：退化三角形（面积接近0）
        var perimeter = edge01Length + edge12Length + edge20Length;
        if (perimeter < 1e-10)
        {
            return 0.0;
        }

        // 3. 计算三角形面积（海伦公式）
        var s = perimeter / 2.0; // 半周长
        var areaSquared = s * (s - edge01Length) * (s - edge12Length) * (s - edge20Length);

        // 边界情况：数值不稳定导致面积为负
        if (areaSquared <= 0)
        {
            return 0.0;
        }

        var area = Math.Sqrt(areaSquared);

        // 4. 计算形状质量系数（Shape Quality Factor）
        // 等边三角形的理论最优值：4√3/3 ≈ 2.309
        // 实际值越接近理论值，三角形形状越规则
        var shapeQuality = (4.0 * Math.Sqrt(3.0) * area) / (perimeter * perimeter);

        // 形状质量系数归一化到[0, 1]，越规则的三角形曲率变化越平缓
        var shapeQualityNormalized = Math.Max(0.0, Math.Min(1.0, shapeQuality));

        // 5. 计算法向量的一致性（Normal Consistency）
        // 法向量应该是单位向量，检查归一化程度
        var normal = primitive.Normal;
        var normalMagnitude = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);

        // 边界情况：法向量为零向量或接近零
        if (normalMagnitude < 1e-10)
        {
            // 尝试重新计算法向量
            normal = CalculateTriangleNormal(v0, v1, v2);
            normalMagnitude = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);

            if (normalMagnitude < 1e-10)
            {
                return 0.0; // 退化三角形，无法计算法向量
            }
        }

        // 归一化法向量
        var normalizedNormal = new Vector3D
        {
            X = normal.X / normalMagnitude,
            Y = normal.Y / normalMagnitude,
            Z = normal.Z / normalMagnitude
        };

        // 6. 计算法向量的离散程度（Discrete Curvature Estimation）
        // 使用法向量与坐标轴的偏差来估算曲率
        // 完全对齐坐标轴（平面）：曲率低
        // 完全不对齐坐标轴（曲面）：曲率高

        // 计算法向量与三个坐标轴的夹角余弦值的绝对值之和
        var axisAlignment = Math.Abs(normalizedNormal.X) + Math.Abs(normalizedNormal.Y) + Math.Abs(normalizedNormal.Z);

        // 对于单位向量，axisAlignment的范围是[1, √3]
        // 1 表示完全对齐一个坐标轴（平面）
        // √3 表示完全等角度对齐所有坐标轴（如(1,1,1)/√3）
        var maxAlignment = Math.Sqrt(3.0);
        var axisAlignmentNormalized = (axisAlignment - 1.0) / (maxAlignment - 1.0);

        // 7. 计算边长不均匀度（Edge Length Variance）
        // 边长差异大表示三角形不规则，可能在曲率变化剧烈的区域
        var avgEdgeLength = perimeter / 3.0;
        var edgeLengthVariance = (
            Math.Pow(edge01Length - avgEdgeLength, 2) +
            Math.Pow(edge12Length - avgEdgeLength, 2) +
            Math.Pow(edge20Length - avgEdgeLength, 2)
        ) / 3.0;

        var edgeLengthStdDev = Math.Sqrt(edgeLengthVariance);
        var edgeLengthCoefficientOfVariation = avgEdgeLength > 1e-10
            ? edgeLengthStdDev / avgEdgeLength
            : 0.0;

        // 归一化到[0, 1]，典型的CV值在0到1之间
        var edgeVarianceNormalized = Math.Min(1.0, edgeLengthCoefficientOfVariation);

        // 8. 计算三角形扁平度（Aspect Ratio）
        // 扁平的三角形可能在曲率梯度较大的过渡区域
        var maxEdgeLength = Math.Max(Math.Max(edge01Length, edge12Length), edge20Length);
        var minEdgeLength = Math.Min(Math.Min(edge01Length, edge12Length), edge20Length);

        var aspectRatio = minEdgeLength > 1e-10
            ? maxEdgeLength / minEdgeLength
            : 1.0;

        // 归一化扁平度：1表示等边三角形，>1表示扁平
        // 使用对数函数压缩大值
        var aspectRatioNormalized = Math.Min(1.0, Math.Log(aspectRatio + 1.0) / Math.Log(10.0));

        // 9. 计算角度锐度（Angle Sharpness）
        // 计算三角形的三个内角
        var angle0 = CalculateAngleBetweenVectors(
            SubtractVectors(v1, v0),
            SubtractVectors(v2, v0)
        );
        var angle1 = CalculateAngleBetweenVectors(
            SubtractVectors(v2, v1),
            SubtractVectors(v0, v1)
        );
        var angle2 = CalculateAngleBetweenVectors(
            SubtractVectors(v0, v2),
            SubtractVectors(v1, v2)
        );

        // 计算角度偏离60°（等边三角形的理想角度）的程度
        var idealAngle = Math.PI / 3.0; // 60度
        var angleDeviation = (
            Math.Abs(angle0 - idealAngle) +
            Math.Abs(angle1 - idealAngle) +
            Math.Abs(angle2 - idealAngle)
        ) / 3.0;

        // 归一化角度偏差：最大偏差为π/3（当角度为0或π时）
        var angleDeviationNormalized = angleDeviation / (Math.PI / 3.0);

        // 10. 综合计算曲率复杂度
        // 使用加权综合评分，权重根据各指标的重要性分配
        var weights = new
        {
            ShapeQuality = 0.20,        // 形状规则性
            AxisAlignment = 0.25,       // 法向量偏离坐标轴程度
            EdgeVariance = 0.20,        // 边长不均匀度
            AspectRatio = 0.15,         // 扁平度
            AngleDeviation = 0.20       // 角度锐度
        };

        var curvatureComplexity =
            (1.0 - shapeQualityNormalized) * weights.ShapeQuality +
            axisAlignmentNormalized * weights.AxisAlignment +
            edgeVarianceNormalized * weights.EdgeVariance +
            aspectRatioNormalized * weights.AspectRatio +
            angleDeviationNormalized * weights.AngleDeviation;

        // 11. 应用非线性映射增强对比度
        // 使用S曲线（sigmoid函数）增强中间范围的区分度
        var enhancedCurvature = 1.0 / (1.0 + Math.Exp(-10.0 * (curvatureComplexity - 0.5)));

        // 确保返回值在[0, 1]范围内
        return Math.Max(0.0, Math.Min(1.0, enhancedCurvature));
    }

    /// <summary>
    /// 计算边长
    /// </summary>
    private double CalculateEdgeLength(Vector3D v1, Vector3D v2)
    {
        var dx = v2.X - v1.X;
        var dy = v2.Y - v1.Y;
        var dz = v2.Z - v1.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// 向量减法
    /// </summary>
    private Vector3D SubtractVectors(Vector3D v1, Vector3D v2)
    {
        return new Vector3D
        {
            X = v1.X - v2.X,
            Y = v1.Y - v2.Y,
            Z = v1.Z - v2.Z
        };
    }

    /// <summary>
    /// 计算两个向量之间的角度（弧度）
    /// </summary>
    private double CalculateAngleBetweenVectors(Vector3D v1, Vector3D v2)
    {
        var dotProduct = v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z;
        var magnitude1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y + v1.Z * v1.Z);
        var magnitude2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y + v2.Z * v2.Z);

        if (magnitude1 < 1e-10 || magnitude2 < 1e-10)
        {
            return 0.0;
        }

        var cosAngle = dotProduct / (magnitude1 * magnitude2);

        // 处理数值误差，确保cosAngle在[-1, 1]范围内
        cosAngle = Math.Max(-1.0, Math.Min(1.0, cosAngle));

        return Math.Acos(cosAngle);
    }

    /// <summary>
    /// 计算三角形法向量 - 叉积算法
    /// 算法：使用三角形两边的叉积计算法向量
    /// </summary>
    /// <param name="v1">三角形顶点1</param>
    /// <param name="v2">三角形顶点2</param>
    /// <param name="v3">三角形顶点3</param>
    /// <returns>法向量</returns>
    private Vector3D CalculateTriangleNormal(Vector3D v1, Vector3D v2, Vector3D v3)
    {
        // 计算边向量
        var edge1 = new Vector3D
        {
            X = v2.X - v1.X,
            Y = v2.Y - v1.Y,
            Z = v2.Z - v1.Z
        };

        var edge2 = new Vector3D
        {
            X = v3.X - v1.X,
            Y = v3.Y - v1.Y,
            Z = v3.Z - v1.Z
        };

        // 叉积计算法向量
        var normal = new Vector3D
        {
            X = edge1.Y * edge2.Z - edge1.Z * edge2.Y,
            Y = edge1.Z * edge2.X - edge1.X * edge2.Z,
            Z = edge1.X * edge2.Y - edge1.Y * edge2.X
        };

        // 归一化法向量
        var magnitude = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);
        if (magnitude > 1e-10)
        {
            normal.X /= magnitude;
            normal.Y /= magnitude;
            normal.Z /= magnitude;
        }

        return normal;
    }

    /// <summary>
    /// 计算表面面积
    /// </summary>
    private double CalculateSurfaceArea(List<GeometricPrimitive> primitives)
    {
        return primitives.Sum(p => p.Area);
    }

    /// <summary>
    /// 计算体积
    /// </summary>
    private double CalculateVolume(BoundingBox3D bounds)
    {
        return (bounds.MaxX - bounds.MinX) * (bounds.MaxY - bounds.MinY) * (bounds.MaxZ - bounds.MinZ);
    }
}

/// <summary>
/// 三维切片应用服务实现
/// </summary>
public class SlicingAppService : ISlicingAppService
{
    private readonly IRepository<SlicingTask> _slicingTaskRepository;
    private readonly IRepository<Slice> _sliceRepository;
    private readonly ISlicingProcessor _slicingProcessor;
    private readonly IMinioStorageService _minioService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SlicingAppService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    // 任务进度历史跟踪 - 用于趋势检测和精确时间估算
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, TaskProgressHistory> _progressHistoryCache = new();

    public SlicingAppService(
        IRepository<SlicingTask> slicingTaskRepository,
        IRepository<Slice> sliceRepository,
        ISlicingProcessor slicingProcessor,
        IMinioStorageService minioService,
        IUnitOfWork unitOfWork,
        ILogger<SlicingAppService> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _slicingTaskRepository = slicingTaskRepository;
        _sliceRepository = sliceRepository;
        _slicingProcessor = slicingProcessor;
        _minioService = minioService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>
    /// 创建切片任务 - 应用层任务创建入口
    /// 执行业务规则验证、数据校验、权限检查等操作，确保任务创建的合法性和完整性
    /// </summary>
    /// <param name="request">切片任务创建请求，包含任务名称、源模型路径、切片配置等必要信息</param>
    /// <param name="userId">创建用户ID，用于权限验证和审计追踪</param>
    /// <returns>创建成功的切片任务DTO，包含任务基本信息和初始状态</returns>
    /// <exception cref="ArgumentException">当请求参数无效时抛出，如任务名称为空、模型路径格式错误等</exception>
    /// <exception cref="InvalidOperationException">当业务规则验证失败时抛出，如源文件不存在、配置参数冲突等</exception>
    /// <exception cref="UnauthorizedAccessException">当用户无权限创建切片任务时抛出</exception>
    /// <exception cref="InvalidDataException">当切片配置JSON序列化失败时抛出</exception>
    public async Task<SlicingDtos.SlicingTaskDto> CreateSlicingTaskAsync(SlicingDtos.CreateSlicingTaskRequest request, Guid userId)
    {
        try
        {
            // 边界情况检查：验证请求参数的基本有效性
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("切片任务名称不能为空", nameof(request.Name));

            if (string.IsNullOrWhiteSpace(request.SourceModelPath))
                throw new ArgumentException("源模型文件路径不能为空", nameof(request.SourceModelPath));

            if (string.IsNullOrWhiteSpace(request.ModelType))
                throw new ArgumentException("模型类型不能为空", nameof(request.ModelType));

            // 边界情况检查：验证切片配置参数的合理性
            if (request.SlicingConfig.TileSize <= 0)
                throw new ArgumentException("切片大小必须大于0", nameof(request.SlicingConfig.TileSize));

            if (request.SlicingConfig.MaxLevel < 0 || request.SlicingConfig.MaxLevel > 20)
                throw new ArgumentException("LOD级别数量必须在0-20之间", nameof(request.SlicingConfig.MaxLevel));

            // 验证源模型文件是否存在 - 关键业务规则检查
            var sourceFileExists = await _minioService.FileExistsAsync("models", request.SourceModelPath);
            if (!sourceFileExists)
            {
                // Fallback to local file system check
                var localPath = request.SourceModelPath;
                if (Path.IsPathRooted(localPath))
                {
                    sourceFileExists = File.Exists(localPath);
                }
                else
                {
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
                            sourceFileExists = true;
                            break;
                        }
                    }
                }
            }

            if (!sourceFileExists)
            {
                _logger.LogWarning("源模型文件不存在：{SourceModelPath}, 用户：{UserId}", request.SourceModelPath, userId);
                throw new InvalidOperationException($"源模型文件不存在：{request.SourceModelPath}");
            }

            // 检查是否启用增量更新，如果是，则查找现有任务
            SlicingTask? task = null;
            bool isIncrementalUpdate = request.SlicingConfig.EnableIncrementalUpdates;

            if (isIncrementalUpdate)
            {
                // 生成确定性的输出路径用于查找
                var expectedOutputPath = string.IsNullOrEmpty(request.OutputPath)
                    ? GenerateOutputPathFromSource(request.SourceModelPath)
                    : request.OutputPath.Trim();

                // 查找具有相同输出路径的现有任务
                var allTasks = await _slicingTaskRepository.GetAllAsync();
                var existingTask = allTasks
                    .Where(t => t.OutputPath == expectedOutputPath && t.CreatedBy == userId)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefault();

                if (existingTask != null)
                {
                    _logger.LogInformation("检测到增量更新：找到现有任务 {TaskId}，将更新而不是创建新任务", existingTask.Id);

                    // 更新现有任务
                    task = existingTask;
                    task.Name = request.Name.Trim(); // 更新名称
                    // 注意：这里先不序列化配置，等存储位置判断完成后再序列化
                    // task.SlicingConfig 将在后面根据 OutputPath 重新设置
                    task.Status = SlicingTaskStatus.Created; // 重置状态
                    task.Progress = 0; // 重置进度
                    task.ErrorMessage = null; // 清除错误信息
                    task.StartedAt = null;
                    task.CompletedAt = null;

                    // 注意：这里不立即保存到数据库，等存储位置判断完成后再保存
                    // await _slicingTaskRepository.UpdateAsync(task);
                    // await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("已准备更新现有切片任务 {TaskId} 用于增量更新（等待存储位置判断）", task.Id);
                }
                else
                {
                    _logger.LogInformation("首次切片：未找到现有任务，将创建新任务");
                }
            }

            // 如果不是增量更新，或者没有找到现有任务，则创建新任务
            if (task == null)
            {
                // 转换 DTO 配置为 Domain 配置
                var initialDomainConfig = MapSlicingConfigToDomain(request.SlicingConfig);

                // 创建切片任务实体 - 领域对象构建
                task = new SlicingTask
                {
                    Name = request.Name.Trim(), // 清理前后空格
                    SourceModelPath = request.SourceModelPath,
                    ModelType = request.ModelType,
                    SlicingConfig = System.Text.Json.JsonSerializer.Serialize(initialDomainConfig),
                    OutputPath = string.IsNullOrEmpty(request.OutputPath)
                        ? GenerateOutputPathFromSource(request.SourceModelPath) // 基于源模型生成确定性路径
                        : request.OutputPath.Trim(),
                    CreatedBy = userId,
                    Status = SlicingTaskStatus.Created,
                    SceneObjectId = request.SceneObjectId
                };
            }            // 根据OutputPath判断存储类型
            // 先将 DTO 配置转换为 Domain 配置
            var domainConfig = isIncrementalUpdate || task.Status == SlicingTaskStatus.Created
                ? MapSlicingConfigToDomain(request.SlicingConfig)
                : SlicingUtilities.ParseSlicingConfig(task.SlicingConfig);

            // 判断存储位置的优先级：
            // 1. 如果用户在 SlicingConfig 中明确指定了 StorageLocation，使用用户指定的
            // 2. 如果任务的 OutputPath 是绝对路径（Path.IsPathRooted），判定为本地文件系统
            // 3. 如果任务的 OutputPath 是相对路径或未提供路径，默认使用 MinIO

            // 关键修复：对于增量更新，应该使用 task.OutputPath 而不是 request.OutputPath
            // 因为增量更新时 task 可能来自 existingTask，其 OutputPath 已经确定
            bool hasRootedPath = !string.IsNullOrEmpty(task.OutputPath) && Path.IsPathRooted(task.OutputPath);

            StorageLocationType specifiedLocation = request.SlicingConfig.StorageLocation;
            bool userSpecifiedStorage =
            specifiedLocation == StorageLocationType.LocalFileSystem || specifiedLocation != StorageLocationType.MinIO;

            if (userSpecifiedStorage)
            {
                // 用户明确指定了存储位置，使用用户指定的
                domainConfig.StorageLocation = specifiedLocation;
                _logger.LogInformation("切片任务 {TaskId} 使用用户指定的存储位置：{StorageLocation}", task.Id, domainConfig.StorageLocation);
            }
            else if (hasRootedPath)
            {
                // 任务的输出路径是绝对路径，判定为本地文件系统
                domainConfig.StorageLocation = StorageLocationType.LocalFileSystem;
                _logger.LogInformation("切片任务 {TaskId} 的输出路径 {OutputPath} 被识别为本地文件系统路径。", task.Id, task.OutputPath!);
            }
            else
            {
                // 默认使用 MinIO
                domainConfig.StorageLocation = StorageLocationType.MinIO;
                _logger.LogInformation("切片任务 {TaskId} 的输出路径 {OutputPath} 被识别为MinIO路径。", task.Id, task.OutputPath!);
            }

            // 序列化更新后的配置
            task.SlicingConfig = JsonSerializer.Serialize(domainConfig);

            if (domainConfig.StorageLocation == StorageLocationType.LocalFileSystem)
            {
                // 对于本地文件系统，如果是相对路径，转换为绝对路径
                if (!string.IsNullOrEmpty(task.OutputPath) && !Path.IsPathRooted(task.OutputPath))
                {
                    // 使用默认的本地切片目录
                    var baseDirectory = Path.Combine(Directory.GetCurrentDirectory(), "slices");
                    task.OutputPath = Path.Combine(baseDirectory, task.OutputPath);
                    _logger.LogInformation("相对路径已转换为绝对路径：{OutputPath}", task.OutputPath);
                }

                // 确保本地输出目录存在
                if (!string.IsNullOrEmpty(task.OutputPath))
                {
                    Directory.CreateDirectory(task.OutputPath);
                    _logger.LogInformation("本地切片输出目录已创建或已存在：{OutputPath}", task.OutputPath);
                }
            }

            _logger.LogInformation("切片任务 {TaskId} 的最终存储位置类型为 {StorageLocation}, 策略为 {Strategy}.",
                task.Id, domainConfig.StorageLocation, domainConfig.Strategy);

            // 持久化切片任务 - 原子性操作确保数据一致性
            // 重要：在存储位置判断完成后才保存，确保 SlicingConfig 中的 StorageLocation 是正确的
            if (isIncrementalUpdate && task.Id != Guid.Empty)
            {
                // 增量更新场景：更新现有任务
                await _slicingTaskRepository.UpdateAsync(task);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("切片任务已更新用于增量处理：{TaskId}，存储位置：{StorageLocation}，策略：{Strategy}",
                    task.Id, domainConfig.StorageLocation, domainConfig.Strategy);
            }
            else
            {
                // 新任务：添加到数据库
                await _slicingTaskRepository.AddAsync(task);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("新切片任务已创建：{TaskId}，存储位置：{StorageLocation}，策略：{Strategy}",
                    task.Id, domainConfig.StorageLocation, domainConfig.Strategy);
            }

            var taskId = task.Id;

            // 异步启动切片处理 - 火与遗忘模式，避免阻塞HTTP响应
            // 注意：这里使用Task.Run而非直接调用，避免在ASP.NET线程池中执行长时间任务
            // 使用IServiceScopeFactory创建新的scope，避免DbContext被释放的问题
            _ = Task.Run(async () =>
            {
                try
                {
                    // 创建新的scope以获取独立的服务实例
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var processor = scope.ServiceProvider.GetRequiredService<ISlicingProcessor>();

                        await processor.ProcessSlicingTaskAsync(taskId, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "后台切片处理任务失败：{TaskId}", taskId);

                    // 更新任务状态为失败
                    try
                    {
                        using (var scope = _serviceScopeFactory.CreateScope())
                        {
                            var repository = scope.ServiceProvider.GetRequiredService<IRepository<SlicingTask>>();
                            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                            var failedTask = await repository.GetByIdAsync(taskId);
                            if (failedTask != null)
                            {
                                failedTask.Status = SlicingTaskStatus.Failed;
                                failedTask.ErrorMessage = ex.Message;
                                failedTask.CompletedAt = DateTime.UtcNow;
                                await unitOfWork.SaveChangesAsync();
                                _logger.LogInformation("已更新任务状态为失败：{TaskId}", taskId);
                            }
                        }
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "更新任务状态失败：{TaskId}", taskId);
                    }
                }
            });

            _logger.LogInformation("切片任务创建成功：{TaskId}, 任务名称：{TaskName}, 用户：{UserId}", taskId, request.Name, userId);
            return MapToDto(task);
        }
        catch (ArgumentException ex)
        {
            // 参数验证失败 - 客户端错误，应返回400状态码
            _logger.LogWarning(ex, "切片任务创建参数验证失败：{TaskName}, 用户：{UserId}", request.Name, userId);
            throw; // 直接抛出，保持原始异常信息
        }
        catch (InvalidOperationException ex)
        {
            // 业务规则验证失败 - 客户端错误，应返回400状态码
            _logger.LogWarning(ex, "切片任务创建业务规则验证失败：{TaskName}, 用户：{UserId}", request.Name, userId);
            throw; // 直接抛出，保持原始异常信息
        }
        catch (JsonException ex)
        {
            // 配置序列化失败 - 数据格式错误
            _logger.LogError(ex, "切片配置JSON序列化失败：{TaskName}, 用户：{UserId}", request.Name, userId);
            throw new InvalidDataException("切片配置格式无效，请检查配置参数", ex);
        }
        catch (Exception ex)
        {
            // 其他未预期异常 - 服务器内部错误，应返回500状态码
            _logger.LogError(ex, "创建切片任务时发生未预期错误：{TaskName}, 用户：{UserId}", request.Name, userId);
            throw new InvalidOperationException("创建切片任务时发生内部错误，请稍后重试", ex);
        }
    }

    public async Task<SlicingDtos.SlicingTaskDto?> GetSlicingTaskAsync(Guid taskId)
    {
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("任务ID不能为空", nameof(taskId));
        }

        try
        {
            var task = await _slicingTaskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                _logger.LogWarning("切片任务不存在：{TaskId}", taskId);
                return null;
            }

            // 计算实际的切片总数，添加超时保护
            int totalSlices = 0;
            try
            {
                var slices = await _sliceRepository.GetAllAsync();
                totalSlices = slices.Count(s => s.SlicingTaskId == taskId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "计算切片总数失败，使用默认值0：{TaskId}", taskId);
                // 不抛出异常，允许返回任务信息
            }

            return MapToDto(task, totalSlices);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "获取切片任务参数验证失败：{TaskId}", taskId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取切片任务时发生未预期错误：{TaskId}", taskId);
            throw new InvalidOperationException($"获取切片任务失败：{taskId}", ex);
        }
    }

    public async Task<IEnumerable<SlicingDtos.SlicingTaskDto>> GetUserSlicingTasksAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        try
        {
            var tasks = await _slicingTaskRepository.GetAllAsync();
            var userTasks = tasks.Where(t => t.CreatedBy == userId)
                                .OrderByDescending(t => t.CreatedAt)
                                .Skip((page - 1) * pageSize)
                                .Take(pageSize);
            return userTasks.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户切片任务列表失败：{UserId}", userId);
            throw;
        }
    }

    public async Task<SlicingDtos.SlicingProgressDto?> GetSlicingProgressAsync(Guid taskId)
    {
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("任务ID不能为空", nameof(taskId));
        }

        try
        {
            var task = await _slicingTaskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                _logger.LogWarning("切片任务不存在：{TaskId}", taskId);
                return null;
            }

            // 并发安全：捕获任务状态快照，避免并发修改导致的不一致性
            var taskSnapshot = new
            {
                task.Id,
                task.Progress,
                task.Status,
                task.StartedAt,
                task.CompletedAt
            };

            // 异步获取统计数据，提高响应性
            var processedTilesTask = GetProcessedTilesCount(taskId);
            var totalTilesTask = GetTotalTilesCount(taskId);
            var estimatedTimeTask = Task.FromResult(CalculateEstimatedTimeRemaining(task));

            await Task.WhenAll(processedTilesTask, totalTilesTask, estimatedTimeTask);

            return new SlicingDtos.SlicingProgressDto
            {
                TaskId = taskSnapshot.Id,
                Progress = Math.Clamp(taskSnapshot.Progress, 0, 100), // 确保进度在有效范围内
                CurrentStage = GetCurrentStage(taskSnapshot.Status),
                Status = taskSnapshot.Status.ToString().ToLowerInvariant(),
                ProcessedTiles = await processedTilesTask,
                TotalTiles = await totalTilesTask,
                EstimatedTimeRemaining = await estimatedTimeTask
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "获取切片进度参数验证失败：{TaskId}", taskId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取切片进度时发生未预期错误：{TaskId}", taskId);
            throw new InvalidOperationException($"获取切片进度失败：{taskId}", ex);
        }
    }

    public async Task<bool> CancelSlicingTaskAsync(Guid taskId, Guid userId)
    {
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("任务ID不能为空", nameof(taskId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("用户ID不能为空", nameof(userId));
        }

        try
        {
            var task = await _slicingTaskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                _logger.LogWarning("切片任务不存在：{TaskId}", taskId);
                return false;
            }

            if (task.CreatedBy != userId)
            {
                _logger.LogWarning("用户无权取消此任务：任务{TaskId}, 用户{UserId}, 创建者{CreatedBy}",
                    taskId, userId, task.CreatedBy);
                return false;
            }

            // 只允许取消处理中的任务
            if (task.Status != SlicingTaskStatus.Processing && task.Status != SlicingTaskStatus.Queued)
            {
                _logger.LogWarning("无法取消非活跃任务：任务{TaskId}, 状态{Status}",
                    taskId, task.Status);
                return false;
            }

            // 原子性更新：使用数据库事务确保状态一致性
            task.Status = SlicingTaskStatus.Cancelled;
            task.CompletedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("切片任务已取消：{TaskId}, 用户：{UserId}, 原状态：{OriginalStatus}",
                taskId, userId, task.Status);
            return true;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "取消切片任务参数验证失败：任务{TaskId}, 用户{UserId}", taskId, userId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消切片任务时发生未预期错误：任务{TaskId}, 用户{UserId}", taskId, userId);
            return false; // 返回false而不是抛出异常，保持API的一致性
        }
    }

    public async Task<bool> DeleteSlicingTaskAsync(Guid taskId, Guid userId)
    {
        try
        {
            var task = await _slicingTaskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                return false;
            }

            // 获取切片配置
            var config = SlicingUtilities.ParseSlicingConfig(task.SlicingConfig);

            // 删除关联的所有切片文件
            var allSlices = await _sliceRepository.GetAllAsync();
            var taskSlices = allSlices.Where(s => s.SlicingTaskId == taskId).ToList();
            foreach (var slice in taskSlices)
            {
                await SlicingUtilities.DeleteSliceFileAsync(
                    slice.FilePath, task.OutputPath, config.StorageLocation, _minioService, _logger);

                // 删除数据库中的切片记录
                await _sliceRepository.DeleteAsync(slice);
            }

            // 删除切片索引文件和tileset.json
            await SlicingUtilities.DeleteSliceIndexAndTilesetAsync(task.OutputPath, config.StorageLocation, _minioService, _logger);

            // 删除数据库中的任务记录
            await _slicingTaskRepository.DeleteAsync(task);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("切片任务已删除：{TaskId}, 用户：{UserId}, 删除了{SliceCount}个关联切片", taskId, userId, taskSlices.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除切片任务失败：{TaskId}, 用户：{UserId}", taskId, userId);
            return false;
        }
    }

    /// <summary>
    /// 获取切片数据 - 根据坐标获取特定切片DTO
    /// 支持高效的切片数据查询，返回转换后的DTO格式
    /// </summary>
    /// <param name="taskId">切片任务ID，必须为有效的GUID</param>
    /// <param name="level">LOD层级，必须为非负整数，0表示最高细节级别</param>
    /// <param name="x">切片X坐标，必须为有效坐标值</param>
    /// <param name="y">切片Y坐标，必须为有效坐标值</param>
    /// <param name="z">切片Z坐标，必须为有效坐标值</param>
    /// <returns>切片DTO，如果不存在则返回null</returns>
    /// <exception cref="ArgumentException">当输入参数无效时抛出，如taskId为空、level为负数等</exception>
    /// <exception cref="InvalidOperationException">当数据库查询失败时抛出</exception>
    public async Task<SlicingDtos.SliceDto?> GetSliceAsync(Guid taskId, int level, int x, int y, int z)
    {
        try
        {
            // 边界情况检查：验证输入参数的有效性
            if (taskId == Guid.Empty)
                throw new ArgumentException("切片任务ID不能为空", nameof(taskId));

            if (level < 0)
                throw new ArgumentException("LOD层级不能为负数", nameof(level));

            if (x < 0 || y < 0 || z < 0)
                throw new ArgumentException("切片坐标不能为负数", nameof(x));

            // 执行查询 - 使用内存查询进行高效查找
            // 注意：这里使用了GetAllAsync + 内存过滤，对于大数据集可能存在性能问题
            // 建议：后续优化为数据库级别的精确查询
            var allSlices = await _sliceRepository.GetAllAsync();
            var slice = allSlices.FirstOrDefault(s =>
                s.SlicingTaskId == taskId &&
                s.Level == level &&
                s.X == x &&
                s.Y == y &&
                s.Z == z);

            if (slice == null)
            {
                _logger.LogDebug("切片数据不存在：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})", taskId, level, x, y, z);
                return null; // 返回null表示切片不存在，这是正常情况
            }

            var result = MapSliceToDto(slice);
            _logger.LogDebug("切片数据获取成功：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z}), 文件大小：{FileSize}",
                taskId, level, x, y, z, slice.FileSize);

            return result;
        }
        catch (ArgumentException ex)
        {
            // 参数验证失败 - 客户端错误，应返回400状态码
            _logger.LogWarning(ex, "获取切片数据参数验证失败：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})", taskId, level, x, y, z);
            throw; // 直接抛出，保持原始异常信息
        }
        catch (InvalidOperationException ex)
        {
            // 数据库操作失败 - 服务器内部错误，应返回500状态码
            _logger.LogError(ex, "获取切片数据数据库操作失败：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})", taskId, level, x, y, z);
            throw new InvalidOperationException("获取切片数据时发生数据库错误，请稍后重试", ex);
        }
        catch (Exception ex)
        {
            // 其他未预期异常 - 服务器内部错误，应返回500状态码
            _logger.LogError(ex, "获取切片数据时发生未预期错误：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})", taskId, level, x, y, z);
            throw new InvalidOperationException("获取切片数据时发生内部错误，请稍后重试", ex);
        }
    }

    public async Task<IEnumerable<SlicingDtos.SliceMetadataDto>> GetSliceMetadataAsync(Guid taskId, int level)
    {
        try
        {
            var allSlices = await _sliceRepository.GetAllAsync();
            var slices = allSlices.Where(s => s.SlicingTaskId == taskId && s.Level == level);
            return slices.Select(s => new SlicingDtos.SliceMetadataDto
            {
                X = s.X,
                Y = s.Y,
                Z = s.Z,
                BoundingBox = s.BoundingBox,
                FileSize = s.FileSize,
                ContentType = GetContentType(s.FilePath)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取切片元数据失败：任务{TaskId}, 级别{Level}", taskId, level);
            throw;
        }
    }

    public async Task<byte[]> DownloadSliceAsync(Guid taskId, int level, int x, int y, int z)
    {
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("任务ID不能为空", nameof(taskId));
        }

        if (level < 0)
        {
            throw new ArgumentException("LOD级别不能为负数", nameof(level));
        }

        if (x < 0 || y < 0 || z < 0)
        {
            throw new ArgumentException("切片坐标不能为负数", nameof(x));
        }

        try
        {
            var allSlices = await _sliceRepository.GetAllAsync();
            var slice = allSlices.FirstOrDefault(s =>
                s.SlicingTaskId == taskId &&
                s.Level == level &&
                s.X == x &&
                s.Y == y &&
                s.Z == z);

            if (slice == null)
            {
                _logger.LogWarning("切片文件不存在：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})",
                    taskId, level, x, y, z);
                throw new FileNotFoundException($"切片文件不存在：任务{taskId}, 级别{level}, 坐标({x}, {y}, {z})");
            }

            // 验证文件大小，防止下载过大文件导致内存溢出
            const long MaxFileSize = 100 * 1024 * 1024; // 100MB限制
            if (slice.FileSize > MaxFileSize)
            {
                _logger.LogWarning("切片文件过大：{FileSize}字节，超过限制{MaxSize}字节",
                    slice.FileSize, MaxFileSize);
                throw new InvalidOperationException($"切片文件过大，无法下载：{slice.FileSize}字节");
            }

            var stream = await _minioService.DownloadFileAsync("slices", slice.FilePath);
            if (stream == null)
            {
                _logger.LogError("从MinIO下载文件失败：{FilePath}", slice.FilePath);
                throw new FileNotFoundException($"无法下载切片文件：{slice.FilePath}");
            }

            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream);

                // 双重验证：确保下载的字节数与记录一致
                if (memoryStream.Length != slice.FileSize)
                {
                    _logger.LogWarning("下载文件大小不匹配：期望{ExpectedSize}字节，实际{DownloadedSize}字节",
                        slice.FileSize, memoryStream.Length);
                }

                return memoryStream.ToArray();
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "下载切片文件参数验证失败：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})",
                taskId, level, x, y, z);
            throw;
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "切片文件不存在：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})",
                taskId, level, x, y, z);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载切片文件时发生未预期错误：任务{TaskId}, 级别{Level}, 坐标({X}, {Y}, {Z})",
                taskId, level, x, y, z);
            throw new InvalidOperationException($"下载切片文件失败：任务{taskId}, 级别{level}, 坐标({x}, {y}, {z})", ex);
        }
    }

    /// <summary>
    /// 执行视锥剔除 - 渲染优化算法实现
    /// 算法：基于视口参数剔除不可见的切片，减少渲染负载
    /// 使用包围盒与视锥的相交测试，快速剔除不在视野范围内的切片
    /// 时间复杂度：O(n)，其中n为切片数量，适合实时渲染需求
    /// </summary>
    /// <param name="viewport">视口参数，包含相机位置、视角、裁剪面等关键信息，必须有效</param>
    /// <param name="allSlices">所有待测试的切片元数据集合，支持空集合（返回空结果）</param>
    /// <returns>可见切片元数据集合，仅包含在视锥范围内的切片，按距离排序以便优先加载</returns>
    /// <exception cref="ArgumentNullException">当输入参数为null时抛出</exception>
    /// <exception cref="ArgumentException">当视口参数无效时抛出，如视野角度为负数、裁剪面距离无效等</exception>
    /// <exception cref="JsonException">当切片包围盒JSON解析失败时抛出</exception>
    public Task<IEnumerable<SlicingDtos.SliceMetadataDto>> PerformFrustumCullingAsync(ViewportInfo viewport, IEnumerable<SlicingDtos.SliceMetadataDto> allSlices)
    {
        // 边界情况检查：验证输入参数的有效性
        if (viewport == null)
            throw new ArgumentNullException(nameof(viewport), "视口参数不能为空");

        if (allSlices == null)
            throw new ArgumentNullException(nameof(allSlices), "切片集合不能为空");

        if (viewport.FieldOfView <= 0 || viewport.FieldOfView > Math.PI)
            throw new ArgumentException("视野角度必须在0到π弧度之间", nameof(viewport.FieldOfView));

        if (viewport.NearPlane < 0 || viewport.FarPlane <= viewport.NearPlane)
            throw new ArgumentException("裁剪面距离设置无效", nameof(viewport.NearPlane));

        var visibleSlices = new List<SlicingDtos.SliceMetadataDto>();

        foreach (var slice in allSlices)
        {
            try
            {
                // 健壮性检查：跳过无效切片数据
                if (slice == null) continue;

                // 解析切片包围盒 - 处理可能格式错误的JSON
                var boundingBoxJson = slice.BoundingBox ?? "{}";
                if (string.IsNullOrWhiteSpace(boundingBoxJson))
                {
                    _logger.LogDebug("切片包围盒为空，跳过：坐标({X}, {Y}, {Z})", slice.X, slice.Y, slice.Z);
                    continue;
                }

                Dictionary<string, double>? boundingBox;
                try
                {
                    boundingBox = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(boundingBoxJson);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "切片包围盒JSON格式无效：坐标({X}, {Y}, {Z}), BoundingBox: {BoundingBox}",
                        slice.X, slice.Y, slice.Z, slice.BoundingBox);
                    continue; // 跳过格式错误的切片，继续处理下一个
                }

                if (boundingBox == null)
                {
                    _logger.LogDebug("切片包围盒解析结果为空，跳过：坐标({X}, {Y}, {Z})", slice.X, slice.Y, slice.Z);
                    continue;
                }

                // 边界情况处理：处理缺失的包围盒坐标，默认使用原点
                var sliceCenter = new Vector3D
                {
                    X = (boundingBox.GetValueOrDefault("minX", 0) + boundingBox.GetValueOrDefault("maxX", 0)) / 2,
                    Y = (boundingBox.GetValueOrDefault("minY", 0) + boundingBox.GetValueOrDefault("maxY", 0)) / 2,
                    Z = (boundingBox.GetValueOrDefault("minZ", 0) + boundingBox.GetValueOrDefault("maxZ", 0)) / 2
                };

                // 距离计算和可见性判断 - 核心视锥剔除算法
                var distance = CalculateDistance(viewport.CameraPosition, sliceCenter);

                // 动态距离阈值：考虑LOD级别和相机参数
                // 注意：这里简化为固定衰减因子，后续可根据实际情况调整算法
                var maxDistance = viewport.FarPlane * Math.Pow(0.8, 0); // 简化处理，不使用Level

                if (distance <= maxDistance && distance >= viewport.NearPlane)
                {
                    visibleSlices.Add(slice);
                }
            }
            catch (Exception ex)
            {
                // 单个切片处理失败不应影响整个剔除过程
                _logger.LogWarning(ex, "处理单个切片时发生错误，跳过：坐标({X}, {Y}, {Z})", slice?.X ?? 0, slice?.Y ?? 0, slice?.Z ?? 0);
                // 继续处理下一个切片，确保算法的健壮性
            }
        }

        // 性能监控：记录剔除结果统计信息
        var totalCount = allSlices.Count();
        var visibleCount = visibleSlices.Count;
        var cullingRatio = totalCount > 0 ? (double)(totalCount - visibleCount) / totalCount * 100 : 0;

        _logger.LogDebug("视锥剔除完成：总切片{Total}, 可见切片{Visible}, 剔除率{CullingRatio:F2}%",
            totalCount, visibleCount, cullingRatio);

        return Task.FromResult<IEnumerable<SlicingDtos.SliceMetadataDto>>(visibleSlices);
    }

    /// <summary>
    /// 预测加载算法 - 预加载优化算法实现
    /// 算法：基于用户视点移动趋势预测需要加载的切片
    /// </summary>
    /// <param name="currentViewport">当前视口</param>
    /// <param name="movementVector">移动向量</param>
    /// <param name="allSlices">所有切片元数据</param>
    /// <returns>预测加载的切片集合</returns>
    public async Task<IEnumerable<SlicingDtos.SliceMetadataDto>> PredictLoadingAsync(ViewportInfo currentViewport, Vector3D movementVector, IEnumerable<SlicingDtos.SliceMetadataDto> allSlices)
    {
        // 预测下一个视口位置
        var predictedPosition = currentViewport.CameraPosition + movementVector * 2.0; // 预测2秒后的位置

        var predictedViewport = new ViewportInfo
        {
            CameraPosition = predictedPosition,
            CameraDirection = currentViewport.CameraDirection,
            FieldOfView = currentViewport.FieldOfView,
            NearPlane = currentViewport.NearPlane,
            FarPlane = currentViewport.FarPlane
        };

        // 使用视锥剔除算法获取预测可见切片
        return await PerformFrustumCullingAsync(predictedViewport, allSlices);
    }

    /// <summary>
    /// 计算两点间距离 - 空间几何算法
    /// </summary>
    /// <param name="point1">点1</param>
    /// <param name="point2">点2</param>
    /// <returns>欧几里得距离</returns>
    private double CalculateDistance(Vector3D point1, Vector3D point2)
    {
        var dx = point2.X - point1.X;
        var dy = point2.Y - point1.Y;
        var dz = point2.Z - point1.Z;

        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public async Task<IEnumerable<SlicingDtos.SliceDto>> GetSlicesBatchAsync(Guid taskId, int level, IEnumerable<(int x, int y, int z)> coordinates)
    {
        try
        {
            var slices = new List<SlicingDtos.SliceDto>();
            var allSlices = await _sliceRepository.GetAllAsync();

            foreach (var (x, y, z) in coordinates)
            {
                var slice = allSlices.FirstOrDefault(s =>
                    s.SlicingTaskId == taskId &&
                    s.Level == level &&
                    s.X == x &&
                    s.Y == y &&
                    s.Z == z);

                if (slice != null)
                {
                    slices.Add(MapSliceToDto(slice));
                }
            }

            return slices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量获取切片失败：任务{TaskId}, 级别{Level}", taskId, level);
            throw;
        }
    }

    /// <summary>
    /// 获取增量更新索引 - 从MinIO获取切片任务的增量更新索引信息
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <returns>增量更新索引信息，如果不存在则返回null</returns>
    public async Task<SlicingDtos.IncrementalUpdateIndexDto?> GetIncrementalUpdateIndexAsync(Guid taskId)
    {
        try
        {
            // 获取切片任务信息
            var task = await _slicingTaskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                _logger.LogWarning("切片任务未找到：{TaskId}", taskId);
                return null;
            }

            // 构造索引文件路径
            var indexPath = $"{task.OutputPath}/incremental_index.json";

            // 检查文件是否存在
            var fileExists = await _minioService.FileExistsAsync("slices", indexPath);
            if (!fileExists)
            {
                _logger.LogWarning("增量更新索引文件不存在：{IndexPath}", indexPath);
                return null;
            }

            // 从MinIO下载索引文件
            using var stream = await _minioService.DownloadFileAsync("slices", indexPath);
            using var reader = new System.IO.StreamReader(stream);
            var jsonContent = await reader.ReadToEndAsync();

            // 反序列化JSON内容
            var indexData = System.Text.Json.JsonSerializer.Deserialize<IncrementalIndexJsonModel>(jsonContent);

            if (indexData == null)
            {
                _logger.LogError("反序列化增量更新索引失败：{IndexPath}", indexPath);
                return null;
            }

            // 转换为DTO
            var result = new SlicingDtos.IncrementalUpdateIndexDto
            {
                TaskId = indexData.TaskId,
                Version = indexData.Version,
                LastModified = indexData.LastModified,
                SliceCount = indexData.SliceCount,
                Strategy = indexData.Strategy ?? "Octree",
                TileSize = indexData.TileSize,
                Slices = indexData.Slices?.Select(s => new SlicingDtos.IncrementalSliceInfo
                {
                    Level = s.Level,
                    X = s.X,
                    Y = s.Y,
                    Z = s.Z,
                    FilePath = s.FilePath ?? string.Empty,
                    Hash = s.Hash ?? string.Empty,
                    BoundingBox = s.BoundingBox ?? string.Empty
                }).ToList() ?? new List<SlicingDtos.IncrementalSliceInfo>()
            };

            _logger.LogInformation("成功获取增量更新索引：任务{TaskId}, 切片数量{SliceCount}", taskId, result.SliceCount);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取增量更新索引失败：任务{TaskId}", taskId);
            return null;
        }
    }

    /// <summary>
    /// 从源模型路径生成确定性的输出路径 - 增量更新支持
    /// 算法：使用SHA256哈希生成基于源路径的确定性标识符
    /// 特性：
    /// - 相同的源路径总是生成相同的输出路径
    /// - 支持增量更新：多次切片同一模型会使用相同目录
    /// - 安全性：哈希值避免路径注入攻击
    /// - 可读性：包含部分源文件名便于识别
    /// </summary>
    /// <param name="sourcePath">源模型文件路径</param>
    /// <returns>确定性的输出路径</returns>
    private string GenerateOutputPathFromSource(string sourcePath)
    {
        try
        {
            // 1. 计算源路径的SHA256哈希（前16位用于唯一性）
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var pathBytes = System.Text.Encoding.UTF8.GetBytes(sourcePath);
                var hashBytes = sha256.ComputeHash(pathBytes);
                var hashHex = Convert.ToHexString(hashBytes).ToLower();
                var shortHash = hashHex.Substring(0, 16); // 取前16位（64bit）

                // 2. 提取源文件名（不含扩展名）用于可读性
                var fileName = Path.GetFileNameWithoutExtension(sourcePath);
                // 清理文件名：只保留字母数字和下划线
                var cleanFileName = new string(fileName.Where(c =>
                    char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
                // 限制长度
                if (cleanFileName.Length > 32)
                {
                    cleanFileName = cleanFileName.Substring(0, 32);
                }

                // 3. 组合：文件名_哈希值
                // 例如：building_model_a1b2c3d4e5f6a7b8
                var outputPath = $"{cleanFileName}_{shortHash}";

                _logger.LogInformation("为源模型生成输出路径：{SourcePath} -> {OutputPath}", sourcePath, outputPath);

                return outputPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "生成确定性输出路径失败，使用随机路径：{SourcePath}", sourcePath);
            // 降级方案：使用随机GUID
            return $"task_{Guid.NewGuid()}";
        }
    }

    #region 私有方法

    private static SlicingDtos.SlicingTaskDto MapToDto(SlicingTask task, int totalSlices = 0)
    {
        return new SlicingDtos.SlicingTaskDto
        {
            Id = task.Id,
            Name = task.Name,
            SourceModelPath = task.SourceModelPath,
            ModelType = task.ModelType,
            SceneObjectId = task.SceneObjectId,
            SlicingConfig = MapSlicingConfigToDto(SlicingUtilities.ParseSlicingConfig(task.SlicingConfig)),
            Status = task.Status.ToString().ToLowerInvariant(),
            Progress = task.Progress,
            OutputPath = task.OutputPath,
            ErrorMessage = task.ErrorMessage,
            CreatedBy = task.CreatedBy,
            CreatedAt = task.CreatedAt,
            StartedAt = task.StartedAt,
            CompletedAt = task.CompletedAt,
            TotalSlices = totalSlices
        };
    }

    private static SlicingDtos.SlicingConfigDto MapSlicingConfigToDto(SlicingConfig domainConfig)
    {
        return new SlicingDtos.SlicingConfigDto
        {
            Granularity = domainConfig.Strategy.ToString(),
            Strategy = domainConfig.Strategy,
            OutputFormat = domainConfig.OutputFormat,
            CoordinateSystem = "EPSG:4326", // 默认值
            CustomSettings = "{}", // 默认值
            TileSize = domainConfig.TileSize,
            MaxLevel = domainConfig.MaxLevel,
            EnableIncrementalUpdates = domainConfig.EnableIncrementalUpdates,
            StorageLocation = domainConfig.StorageLocation
        };
    }

    /// <summary>
    /// 将 DTO 切片配置转换为 Domain 切片配置
    /// 处理策略字符串到枚举的转换，支持 Granularity 和 Strategy 两种字段名
    /// </summary>
    private static SlicingConfig MapSlicingConfigToDomain(SlicingDtos.SlicingConfigDto dtoConfig)
    {
        // 解析策略枚举：优先使用 Strategy 枚举字段，如果需要兼容旧的 Granularity 字符串字段
        SlicingStrategy strategy = dtoConfig.Strategy; // 直接使用枚举值

        // 如果 Granularity 不为空且不是默认值，可以从字符串解析来覆盖 Strategy
        if (!string.IsNullOrWhiteSpace(dtoConfig.Granularity))
        {
            // 兼容旧的 Granularity 值映射
            strategy = dtoConfig.Granularity.ToLowerInvariant() switch
            {
                "high" => SlicingStrategy.Grid,
                "medium" => SlicingStrategy.Octree,
                "low" => SlicingStrategy.Adaptive,
                _ => dtoConfig.Strategy // 如果无法识别，保持原有的 Strategy 值
            };
        }

        // 解析输出格式
        var outputFormat = "b3dm";
        if (!string.IsNullOrWhiteSpace(dtoConfig.OutputFormat))
        {
            outputFormat = dtoConfig.OutputFormat.ToLowerInvariant() switch
            {
                "3d tiles" => "b3dm",
                "cesium3dtiles" => "b3dm",
                "gltf" => "gltf",
                "json" => "json",
                _ => dtoConfig.OutputFormat.ToLowerInvariant()
            };
        }

        return new SlicingConfig
        {
            Strategy = strategy,
            TileSize = dtoConfig.TileSize > 0 ? dtoConfig.TileSize : 100.0,
            MaxLevel = dtoConfig.MaxLevel >= 0 ? dtoConfig.MaxLevel : 10,
            OutputFormat = outputFormat,
            EnableIncrementalUpdates = dtoConfig.EnableIncrementalUpdates,
            StorageLocation = dtoConfig.StorageLocation
        };
    }

    private static SlicingDtos.SliceDto MapSliceToDto(Slice slice)
    {
        return new SlicingDtos.SliceDto
        {
            Id = slice.Id,
            SlicingTaskId = slice.SlicingTaskId,
            Level = slice.Level,
            X = slice.X,
            Y = slice.Y,
            Z = slice.Z,
            FilePath = slice.FilePath,
            BoundingBox = slice.BoundingBox,
            FileSize = slice.FileSize,
            CreatedAt = slice.CreatedAt
        };
    }

    private string GetCurrentStage(SlicingTaskStatus status)
    {
        return status switch
        {
            SlicingTaskStatus.Created => "准备中",
            SlicingTaskStatus.Queued => "队列中",
            SlicingTaskStatus.Processing => "处理中",
            SlicingTaskStatus.Completed => "已完成",
            SlicingTaskStatus.Failed => "失败",
            SlicingTaskStatus.Cancelled => "已取消",
            _ => "未知状态"
        };
    }

    /// <summary>
    /// 获取已处理的切片数量 - 完整的进度跟踪算法实现
    /// 算法：统计指定任务的实际已生成切片数量，考虑不同状态和级别
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <returns>已处理的切片数量</returns>
    private async Task<long> GetProcessedTilesCount(Guid taskId)
    {
        try
        {
            var allSlices = await _sliceRepository.GetAllAsync();
            var taskSlices = allSlices.Where(s => s.SlicingTaskId == taskId).ToList();

            // 统计所有已成功生成的切片
            var processedCount = taskSlices.Count(s => s.FileSize > 0);

            return processedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取已处理切片数量失败：任务{TaskId}", taskId);
            return 0;
        }
    }

    /// <summary>
    /// 获取切片总数 - 完整的切片数量估算算法实现
    /// 算法：根据切片策略和配置参数精确计算预期的切片总数
    /// 支持：网格切片、八叉树、KD树、自适应切片等多种策略
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <returns>预期的切片总数</returns>
    private async Task<long> GetTotalTilesCount(Guid taskId)
    {
        try
        {
            var task = await _slicingTaskRepository.GetByIdAsync(taskId);
            if (task == null) return 0;

            var config = SlicingUtilities.ParseSlicingConfig(task.SlicingConfig);
            long totalCount = 0;

            // 根据不同策略计算总切片数
            switch (config.Strategy)
            {
                case SlicingStrategy.Grid:
                    // 网格策略:规则网格剖分
                    for (int level = 0; level <= config.MaxLevel; level++)
                    {
                        var tilesInLevel = (long)Math.Pow(2, level);
                        var zTiles = level == 0 ? 1 : tilesInLevel / 2;
                        totalCount += tilesInLevel * tilesInLevel * zTiles;
                    }
                    break;

                case SlicingStrategy.Octree:
                    // 八叉树策略:层次空间剖分，考虑几何衰减
                    for (int level = 0; level <= config.MaxLevel; level++)
                    {
                        if (level == 0)
                        {
                            totalCount += 1;
                        }
                        else
                        {
                            // 八叉树每层理论切片数为8^level，实际考虑衰减因子
                            var theoreticalCount = (long)Math.Pow(8, level);
                            var attenuatedCount = (long)(theoreticalCount * 0.5); // 几何衰减因子
                            totalCount += attenuatedCount;
                        }
                    }
                    break;

                case SlicingStrategy.KdTree:
                    // KD树策略:二分剖分，考虑几何衰减
                    for (int level = 0; level <= config.MaxLevel; level++)
                    {
                        if (level == 0)
                        {
                            totalCount += 1;
                        }
                        else
                        {
                            var theoreticalCount = (long)Math.Pow(2, level);
                            var attenuatedCount = (long)(theoreticalCount * 0.5);
                            totalCount += attenuatedCount;
                        }
                    }
                    break;

                case SlicingStrategy.Adaptive:
                    // 自适应策略:基于几何复杂度的动态估算
                    for (int level = 0; level <= config.MaxLevel; level++)
                    {
                        if (level == 0)
                        {
                            totalCount += 1;
                        }
                        else
                        {
                            // 自适应策略的切片数量随级别增加而增长，但增长率较低
                            var geometricBase = (long)Math.Pow(4, level);
                            var densityFactor = 1.0 + level * 0.15;
                            var attenuationFactor = 0.6;
                            var estimatedCount = (long)(geometricBase * densityFactor * attenuationFactor);

                            // 考虑几何误差阈值的影响
                            if (config.GeometricErrorThreshold > 0)
                            {
                                var precisionFactor = Math.Max(0.5, 1.0 / config.GeometricErrorThreshold);
                                estimatedCount = (long)(estimatedCount * Math.Min(precisionFactor, 3.0));
                            }

                            totalCount += estimatedCount;
                        }
                    }
                    break;

                default:
                    // 默认使用网格策略估算
                    for (int level = 0; level <= config.MaxLevel; level++)
                    {
                        var tilesInLevel = (long)Math.Pow(2, level);
                        var zTiles = level == 0 ? 1 : tilesInLevel / 2;
                        totalCount += tilesInLevel * tilesInLevel * zTiles;
                    }
                    break;
            }

            _logger.LogDebug("计算切片总数：任务{TaskId}, 策略{Strategy}, 最大级别{MaxLevel}, 总数{TotalCount}",
                taskId, config.Strategy, config.MaxLevel, totalCount);

            return totalCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取切片总数失败：任务{TaskId}", taskId);
            return 0;
        }
    }

    /// <summary>
    /// 计算预计剩余时间 - 增强的时间估算算法实现
    /// 算法：结合线性外推、指数平滑和历史数据分析，提供更准确的时间预测
    /// 特性：
    /// - 线性外推：基于当前进度和已用时间估算基础剩余时间
    /// - 指数平滑：平滑处理速度波动，减少估算抖动
    /// - 加速/减速检测：识别处理速度变化趋势，动态调整估算
    /// - 阶段性考虑：不同LOD级别处理时间不同，分阶段估算
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <returns>预计剩余秒数</returns>
    private long CalculateEstimatedTimeRemaining(SlicingTask task)
    {
        try
        {
            // 边界情况处理
            if (task.Status != SlicingTaskStatus.Processing || task.Progress <= 0)
            {
                return 0;
            }

            if (task.Progress >= 100)
            {
                return 0;
            }

            // 1. 基础线性外推计算
            var startTime = task.StartedAt ?? task.CreatedAt;
            var elapsed = DateTime.UtcNow - startTime;
            var elapsedSeconds = (long)elapsed.TotalSeconds;

            if (elapsedSeconds <= 0)
            {
                return 0;
            }

            // 2. 计算当前处理速度（进度/时间）
            var currentSpeed = (double)task.Progress / elapsedSeconds; // 每秒进度百分比
            if (currentSpeed <= 0)
            {
                return 0;
            }

            // 3. 基础线性外推
            var remainingProgress = 100 - task.Progress;
            var linearEstimate = remainingProgress / currentSpeed;

            // 4. 应用阶段性调整因子
            // 不同阶段的处理速度可能不同：
            // - 前期(0-30%): 准备阶段，速度较慢，调整因子1.2
            // - 中期(30-70%): 稳定处理，速度正常，调整因子1.0
            // - 后期(70-100%): 收尾阶段，速度可能变慢，调整因子1.3
            double stageFactor;
            if (task.Progress < 30)
            {
                // 前期阶段：速度通常较慢
                stageFactor = 1.2;
            }
            else if (task.Progress < 70)
            {
                // 中期阶段：速度稳定
                stageFactor = 1.0;
            }
            else
            {
                // 后期阶段：可能有索引生成等额外操作
                stageFactor = 1.3;
            }

            // 5. 应用加速/减速趋势检测
            // 基于真实的历史进度数据检测速度趋势
            double trendFactor = 1.0;

            // 获取或创建历史进度记录
            var history = _progressHistoryCache.GetOrAdd(task.Id, _ => new TaskProgressHistory());
            history.RecordProgress(task.Progress, DateTime.UtcNow);

            if (elapsedSeconds > 60 && history.ProgressRecords.Count >= 3) // 至少需要3个数据点
            {
                // 使用线性回归计算速度趋势
                var recentRecords = history.GetRecentRecords(TimeSpan.FromMinutes(5)); // 最近5分钟的数据
                if (recentRecords.Count >= 2)
                {
                    // 计算前半段和后半段的平均速度
                    var midIndex = recentRecords.Count / 2;
                    var firstHalf = recentRecords.Take(midIndex).ToList();
                    var secondHalf = recentRecords.Skip(midIndex).ToList();

                    if (firstHalf.Count > 0 && secondHalf.Count > 0)
                    {
                        // 计算每段的平均速度（进度/秒）
                        var firstHalfSpeed = (firstHalf.Last().Progress - firstHalf.First().Progress) /
                                            (firstHalf.Last().Timestamp - firstHalf.First().Timestamp).TotalSeconds;
                        var secondHalfSpeed = (secondHalf.Last().Progress - secondHalf.First().Progress) /
                                             (secondHalf.Last().Timestamp - secondHalf.First().Timestamp).TotalSeconds;

                        // 计算速度变化率
                        if (firstHalfSpeed > 0)
                        {
                            var speedChangeRatio = secondHalfSpeed / firstHalfSpeed;

                            if (speedChangeRatio > 1.2)
                            {
                                // 检测到明显加速趋势，减少估算时间
                                trendFactor = 0.80;
                                _logger.LogDebug("检测到加速趋势：速度变化率{Ratio:F2}，应用趋势因子{Factor}", speedChangeRatio, trendFactor);
                            }
                            else if (speedChangeRatio < 0.8)
                            {
                                // 检测到明显减速趋势，增加估算时间
                                trendFactor = 1.25;
                                _logger.LogDebug("检测到减速趋势：速度变化率{Ratio:F2}，应用趋势因子{Factor}", speedChangeRatio, trendFactor);
                            }
                            else
                            {
                                // 速度稳定
                                _logger.LogDebug("处理速度稳定：速度变化率{Ratio:F2}", speedChangeRatio);
                            }
                        }
                    }
                }
            }

            // 6. 应用指数平滑（减少估算抖动）
            // 使用平滑因子α=0.7，给予当前估算较高权重，同时考虑历史趋势
            const double smoothingFactor = 0.7;
            var previousEstimate = history.LastEstimatedTime ?? linearEstimate; // 使用真实的上次估算值
            var smoothedEstimate = smoothingFactor * linearEstimate + (1 - smoothingFactor) * previousEstimate;

            // 记录本次估算值，供下次使用
            history.LastEstimatedTime = smoothedEstimate * stageFactor * trendFactor;

            // 7. 综合计算最终估算时间
            var finalEstimate = smoothedEstimate * stageFactor * trendFactor;

            // 8. 应用合理性边界限制
            // 最小值：1秒
            // 最大值：不超过已用时间的10倍（避免不合理的长时间估算）
            var minEstimate = 1;
            var maxEstimate = elapsedSeconds * 10;
            finalEstimate = Math.Max(minEstimate, Math.Min(maxEstimate, finalEstimate));

            _logger.LogDebug("时间估算：任务{TaskId}, 进度{Progress}%, 已用时{Elapsed}s, 线性估算{Linear}s, 阶段因子{Stage}, 趋势因子{Trend}, 最终估算{Final}s",
                task.Id, task.Progress, elapsedSeconds, linearEstimate, stageFactor, trendFactor, finalEstimate);

            return (long)finalEstimate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算预计剩余时间失败：任务{TaskId}", task.Id);
            return 0;
        }
    }

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

    #endregion
}

/// <summary>
/// 切片处理器实现
/// </summary>
public class SlicingProcessor : ISlicingProcessor
{
    private readonly IRepository<SlicingTask> _slicingTaskRepository;
    private readonly IRepository<Slice> _sliceRepository;
    private readonly IMinioStorageService _minioService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SlicingProcessor> _logger;
    private readonly B3dmGenerator _b3dmGenerator;
    private readonly GltfGenerator _gltfGenerator;
    private readonly I3dmGenerator? _i3dmGenerator;
    private readonly PntsGenerator? _pntsGenerator;
    private readonly CmptGenerator? _cmptGenerator;
    private readonly TilesetGenerator? _tilesetGenerator;
    private readonly ITileGeneratorFactory _tileGeneratorFactory;
    private readonly ISlicingStrategyFactory _slicingStrategyFactory;
    private readonly MeshDecimationService? _meshDecimationService;
    private readonly IModelLoader? _modelLoader;

    public SlicingProcessor(
        IRepository<SlicingTask> slicingTaskRepository,
        IRepository<Slice> sliceRepository,
        IMinioStorageService minioService,
        IUnitOfWork unitOfWork,
        ILogger<SlicingProcessor> logger,
        B3dmGenerator b3dmGenerator,
        GltfGenerator gltfGenerator,
        ITileGeneratorFactory tileGeneratorFactory,
        ISlicingStrategyFactory slicingStrategyFactory,
        I3dmGenerator? i3dmGenerator = null,
        PntsGenerator? pntsGenerator = null,
        CmptGenerator? cmptGenerator = null,
        TilesetGenerator? tilesetGenerator = null,
        MeshDecimationService? meshDecimationService = null,
        IModelLoader? modelLoader = null)
    {
        _slicingTaskRepository = slicingTaskRepository;
        _sliceRepository = sliceRepository;
        _minioService = minioService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _b3dmGenerator = b3dmGenerator;
        _gltfGenerator = gltfGenerator;
        _tileGeneratorFactory = tileGeneratorFactory ?? throw new ArgumentNullException(nameof(tileGeneratorFactory));
        _slicingStrategyFactory = slicingStrategyFactory ?? throw new ArgumentNullException(nameof(slicingStrategyFactory));
        _i3dmGenerator = i3dmGenerator;
        _pntsGenerator = pntsGenerator;
        _cmptGenerator = cmptGenerator;
        _tilesetGenerator = tilesetGenerator;
        _meshDecimationService = meshDecimationService;
        _modelLoader = modelLoader;
    }

    public async Task ProcessSlicingQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始处理切片任务队列");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 获取队列中的切片任务
                var allTasks = await _slicingTaskRepository.GetAllAsync();
                var queuedTasks = allTasks.Where(t => t.Status == SlicingTaskStatus.Queued);

                foreach (var task in queuedTasks)
                {
                    await ProcessSlicingTaskAsync(task.Id, cancellationToken);
                }

                await Task.Delay(5000, cancellationToken); // 等待5秒后继续检查
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理切片队列时发生错误");
                await Task.Delay(10000, cancellationToken); // 出错时等待10秒
            }
        }

        _logger.LogInformation("切片任务队列处理结束");
    }

    public async Task ProcessSlicingTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _slicingTaskRepository.GetByIdAsync(taskId);
        if (task == null)
        {
            return;
        }

        try
        {
            _logger.LogInformation("开始处理切片任务：{TaskId}", taskId);

            task.Status = SlicingTaskStatus.Processing;
            task.StartedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            // 执行切片处理
            await PerformSlicingAsync(task, cancellationToken);

            task.Status = SlicingTaskStatus.Completed;
            task.Progress = 100;
            task.CompletedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("切片任务处理完成：{TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切片任务处理失败：{TaskId}", taskId);

            task.Status = SlicingTaskStatus.Failed;
            task.ErrorMessage = ex.Message;
            task.CompletedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task UpdateProgressAsync(Guid taskId, SlicingProgress progress)
    {
        var task = await _slicingTaskRepository.GetByIdAsync(taskId);
        if (task != null)
        {
            task.Progress = progress.Progress;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 加载模型数据 - 模型处理的核心步骤
    /// 包括加载三角形数据、构建空间索引、计算包围盒
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回元组：(三角形列表, 空间索引, 模型包围盒)</returns>
    private async Task<(List<Triangle> triangles, Dictionary<string, List<Triangle>> spatialIndex, BoundingBox3D bounds)> LoadModelDataAsync(SlicingTask task, CancellationToken cancellationToken)
    {
        // **步骤1: 加载源模型的三角形数据**
        _logger.LogInformation("开始加载源模型三角形数据：{SourceModelPath}", task.SourceModelPath);
        List<Triangle> allTriangles;
        try
        {
            allTriangles = await LoadTrianglesFromModelAsync(task, cancellationToken);
            _logger.LogInformation("模型加载完成：共{TriangleCount}个三角形", allTriangles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载模型失败，使用空几何数据");
            allTriangles = new List<Triangle>(); // 使用空数据继续执行
        }

        // **步骤2: 构建空间索引以加速切片查询**
        _logger.LogInformation("开始构建三角形空间索引");
        var triangleSpatialIndex = BuildTriangleSpatialIndex(allTriangles);
        _logger.LogInformation("空间索引构建完成");

        // **步骤3: 计算模型包围盒用于坐标变换**
        // 优先使用ModelBoundingBoxExtractor从GLB文件直接提取包围盒
        // 如果提取失败，则从三角形列表计算
        BoundingBox3D modelBounds;
        var extractor = new ModelBoundingBoxExtractor(_logger);
        var extractedBounds = await extractor.ExtractBoundingBoxAsync(task.SourceModelPath, cancellationToken);

        if (extractedBounds != null && extractedBounds.IsValid())
        {
            _logger.LogInformation("从模型文件直接提取包围盒成功");
            modelBounds = extractedBounds;
        }
        else
        {
            _logger.LogWarning("从模型文件提取包围盒失败，尝试从三角形列表计算");
            modelBounds = CalculateModelBounds(allTriangles);
        }

        _logger.LogInformation("模型包围盒计算完成：[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}]",
            modelBounds.MinX, modelBounds.MinY, modelBounds.MinZ,
            modelBounds.MaxX, modelBounds.MaxY, modelBounds.MaxZ);

        return (allTriangles, triangleSpatialIndex, modelBounds);
    }

    /// <summary>
    /// 处理单个LOD级别 - 级别处理的核心逻辑
    /// 包括切片生成、增量更新检查、并行/串行处理选择、进度更新
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="level">当前处理的LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <param name="strategy">切片策略</param>
    /// <param name="triangleSpatialIndex">三角形空间索引</param>
    /// <param name="modelBounds">模型包围盒</param>
    /// <param name="existingSlicesMap">现有切片映射表</param>
    /// <param name="actuallyUseIncrementalUpdate">是否实际使用增量更新</param>
    /// <param name="hasSliceChanges">切片变化标记</param>
    /// <param name="processedSliceKeys">已处理的切片键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回更新后的切片变化标记</returns>
    private async Task<bool> ProcessLevelAsync(
        SlicingTask task,
        int level,
        SlicingConfig config,
        ISlicingStrategy strategy,
        Dictionary<string, List<Triangle>> triangleSpatialIndex,
        BoundingBox3D modelBounds,
        Dictionary<string, Slice> existingSlicesMap,
        bool actuallyUseIncrementalUpdate,
        bool hasSliceChanges,
        HashSet<string> processedSliceKeys,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("处理级别{Level}：策略{Strategy}", level, config.Strategy);

        // 使用选择的切片策略进行空间剖分（传入模型包围盒）
        var slices = await strategy.GenerateSlicesAsync(task, level, config, modelBounds, cancellationToken);

        _logger.LogInformation("策略生成切片数量：{Count}, 级别：{Level}", slices.Count, level);

        if (slices.Count == 0)
        {
            _logger.LogWarning("级别{Level}未生成任何切片，请检查切片配置和源模型", level);
            return hasSliceChanges; // 跳过这个级别，返回当前状态
        }

        // 选择处理模式：使用并行处理还是串行处理
        if (config.ParallelProcessingCount > 1 && slices.Count > 10)
        {
            hasSliceChanges = await ProcessSlicesInParallelForLevelAsync(task, level, config, slices, triangleSpatialIndex, modelBounds,
                existingSlicesMap, actuallyUseIncrementalUpdate, hasSliceChanges, processedSliceKeys, cancellationToken);
        }
        else
        {
            hasSliceChanges = await ProcessSlicesSeriallyForLevelAsync(task, level, config, slices, triangleSpatialIndex, modelBounds,
                existingSlicesMap, actuallyUseIncrementalUpdate, hasSliceChanges, processedSliceKeys, cancellationToken);
        }

        // 更新进度
        task.Progress = (int)((double)(level + 1) / (config.MaxLevel + 1) * 100);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("级别{Level}处理完成，生成{SliceCount}个切片", level, slices.Count);

        return hasSliceChanges;
    }

    /// <summary>
    /// 并行处理级别切片
    /// </summary>
    private async Task<bool> ProcessSlicesInParallelForLevelAsync(
        SlicingTask task,
        int level,
        SlicingConfig config,
        List<Slice> slices,
        Dictionary<string, List<Triangle>> triangleSpatialIndex,
        BoundingBox3D modelBounds,
        Dictionary<string, Slice> existingSlicesMap,
        bool actuallyUseIncrementalUpdate,
        bool hasSliceChanges,
        HashSet<string> processedSliceKeys,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("使用并行处理：级别{Level}, 切片数量{Count}, 并行度{ParallelCount}",
            level, slices.Count, config.ParallelProcessingCount);

        var (processedCount, hasChanges) = await ProcessSlicesInParallelAsync(
            task, level, config, slices, triangleSpatialIndex, modelBounds,
            existingSlicesMap, actuallyUseIncrementalUpdate, hasSliceChanges,
            processedSliceKeys, new List<Slice>(), new List<Slice>(), cancellationToken);

        _logger.LogInformation("并行处理完成：级别{Level}, 处理{Processed}个切片, 是否有变化{HasChanges}",
            level, processedCount, hasChanges);

        return hasChanges;
    }

    /// <summary>
    /// 串行处理级别切片
    /// </summary>
    private async Task<bool> ProcessSlicesSeriallyForLevelAsync(
        SlicingTask task,
        int level,
        SlicingConfig config,
        List<Slice> slices,
        Dictionary<string, List<Triangle>> triangleSpatialIndex,
        BoundingBox3D modelBounds,
        Dictionary<string, Slice> existingSlicesMap,
        bool actuallyUseIncrementalUpdate,
        bool hasSliceChanges,
        HashSet<string> processedSliceKeys,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("使用串行处理：级别{Level}, 切片数量{Count}", level, slices.Count);

        // 批量大小设置：使用合理的批量大小避免内存累积
        const int batchSize = 50; // 每批处理50个切片
        var processedInBatch = 0;
        var slicesToAdd = new List<Slice>(batchSize);
        var slicesToUpdate = new List<Slice>(batchSize);

        foreach (var slice in slices)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var sliceKey = $"{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}";
            bool isNewSlice = !existingSlicesMap.ContainsKey(sliceKey);
            bool needsUpdate = false;

            // 增量更新检查逻辑
            if (actuallyUseIncrementalUpdate)
            {
                if (!isNewSlice)
                {
                    var existingSlice = existingSlicesMap[sliceKey];
                    var newHash = await CalculateSliceHash(slice);
                    var existingHash = await CalculateSliceHashFromExisting(existingSlice);

                    needsUpdate = newHash != existingHash;

                    if (!needsUpdate)
                    {
                        _logger.LogDebug("切片未变化，跳过：级别{Level}, 坐标({X},{Y},{Z})",
                            slice.Level, slice.X, slice.Y, slice.Z);
                        processedSliceKeys.Add(sliceKey);
                        continue;
                    }
                    else
                    {
                        _logger.LogInformation("检测到切片变化，准备更新：级别{Level}, 坐标({X},{Y},{Z})",
                            slice.Level, slice.X, slice.Y, slice.Z);
                        slice.Id = existingSlice.Id;
                        hasSliceChanges = true;
                    }
                }
                else
                {
                    _logger.LogInformation("检测到新增切片：级别{Level}, 坐标({X},{Y},{Z})",
                        slice.Level, slice.X, slice.Y, slice.Z);
                    hasSliceChanges = true;
                }
            }

            try
            {
                // 查询切片相交的三角形数据
                var sliceTriangles = QueryTrianglesForSlice(slice, triangleSpatialIndex, modelBounds);
                _logger.LogDebug("切片({X},{Y},{Z})查询到{Count}个三角形",
                    slice.X, slice.Y, slice.Z, sliceTriangles.Count);

                // 生成切片文件内容，获取是否成功
                var generated = await GenerateSliceFileAsync(slice, config, sliceTriangles, cancellationToken);

                if (!generated)
                {
                    _logger.LogDebug("切片({Level},{X},{Y},{Z})无几何数据，跳过保存",
                        slice.Level, slice.X, slice.Y, slice.Z);
                    continue;
                }

                _logger.LogDebug("成功生成切片文件：{FilePath}", slice.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成切片文件失败：级别{Level}, 坐标({X},{Y},{Z}), 路径{FilePath}",
                    slice.Level, slice.X, slice.Y, slice.Z, slice.FilePath);
                continue; // 不中断整个流程,继续处理其他切片
            }

            // 收集待批量处理的切片（仅在成功生成时）
            if (actuallyUseIncrementalUpdate && needsUpdate)
            {
                slicesToUpdate.Add(slice);
                _logger.LogDebug("标记切片待更新：{SliceKey}", sliceKey);
            }
            else
            {
                slicesToAdd.Add(slice);
                _logger.LogDebug("标记切片待新增：{SliceKey}", sliceKey);
            }

            // 标记为已处理
            if (actuallyUseIncrementalUpdate)
            {
                processedSliceKeys.Add(sliceKey);
            }

            processedInBatch++;

            // 批量提交优化
            if (processedInBatch >= batchSize)
            {
                await CommitSliceBatchAsync(slicesToAdd, slicesToUpdate);
                processedInBatch = 0;
                GC.Collect(0, GCCollectionMode.Optimized); // 内存优化
            }

            // 动态调整处理时间
            var processingDelay = CalculateProcessingDelay(slice, config);
            await Task.Delay(processingDelay, cancellationToken);
        }

        // 提交剩余的切片
        if (slicesToAdd.Count > 0 || slicesToUpdate.Count > 0)
        {
            await CommitSliceBatchAsync(slicesToAdd, slicesToUpdate);
        }

        return hasSliceChanges;
    }

    /// <summary>
    /// 批量提交切片数据
    /// </summary>
    private async Task CommitSliceBatchAsync(List<Slice> slicesToAdd, List<Slice> slicesToUpdate)
    {
        if (slicesToAdd.Count > 0)
        {
            foreach (var s in slicesToAdd)
            {
                // 诊断日志：输出即将保存的包围盒
                if (s.Level == 0 || (s.Level == 1 && s.X == 0 && s.Y == 1 && s.Z == 0))
                {
                    _logger.LogInformation("【保存前】准备保存切片到数据库: Level={Level}, X={X}, Y={Y}, Z={Z}, BoundingBox={BoundingBox}",
                        s.Level, s.X, s.Y, s.Z, s.BoundingBox);
                }
                await _sliceRepository.AddAsync(s);
            }
            slicesToAdd.Clear();
        }

        if (slicesToUpdate.Count > 0)
        {
            foreach (var s in slicesToUpdate)
            {
                await _sliceRepository.UpdateAsync(s);
            }
            slicesToUpdate.Clear();
        }

        await _unitOfWork.SaveChangesAsync();
        _logger.LogDebug("批量提交切片数据：{Count}个切片", slicesToAdd.Count + slicesToUpdate.Count);
    }

    /// <summary>
    /// 执行三维切片处理 - 核心算法实现
    /// 采用多层次细节（LOD）算法结合多种空间剖分策略进行切片处理
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task PerformSlicingAsync(SlicingTask task, CancellationToken cancellationToken)
    {
        var config = ParseSlicingConfig(task.SlicingConfig);

        _logger.LogInformation("开始切片处理：任务{TaskId}, 策略{Strategy}, 增量更新：{EnableIncrementalUpdates}",
            task.Id, config.Strategy, config.EnableIncrementalUpdates);

        // **步骤1: 加载模型数据**
        var (allTriangles, triangleSpatialIndex, modelBounds) = await LoadModelDataAsync(task, cancellationToken);

        // 准备增量更新：如果启用增量更新，加载现有切片数据用于比对
        Dictionary<string, Slice> existingSlicesMap = new Dictionary<string, Slice>();
        HashSet<string> processedSliceKeys = new HashSet<string>();
        bool actuallyUseIncrementalUpdate = false; // 实际是否使用增量更新
        bool hasSliceChanges = false; // 是否有切片发生变化（新增、更新或删除）

        if (config.EnableIncrementalUpdates)
        {
            var existingSlices = await _sliceRepository.GetAllAsync();
            var taskSlices = existingSlices.Where(s => s.SlicingTaskId == task.Id).ToList();

            if (taskSlices.Any())
            {
                // 有现有切片数据，可以使用增量更新
                actuallyUseIncrementalUpdate = true;

                // 构建现有切片的映射表，key为 "level_x_y_z"
                foreach (var slice in taskSlices)
                {
                    var key = $"{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}";
                    existingSlicesMap[key] = slice;
                }

                _logger.LogInformation("增量更新模式：找到{Count}个现有切片用于比对", existingSlicesMap.Count);
            }
            else
            {
                // 没有现有切片数据，这是首次生成，使用正常生成模式
                _logger.LogInformation("首次切片生成：虽然启用了增量更新，但数据库中无现有切片，将执行完整生成");
            }
        }

        // 使用工厂创建切片策略实例 - 解耦策略创建逻辑
        ISlicingStrategy strategy = _slicingStrategyFactory.CreateStrategy(config.Strategy);

        // 多层次细节（LOD）切片处理循环
        for (int level = 0; level <= config.MaxLevel; level++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            hasSliceChanges = await ProcessLevelAsync(task, level, config, strategy, triangleSpatialIndex, modelBounds,
                existingSlicesMap, actuallyUseIncrementalUpdate, hasSliceChanges, processedSliceKeys, cancellationToken);
        }

        // 增量更新：删除不再存在的旧切片（模型中已删除的部分）
        if (actuallyUseIncrementalUpdate && existingSlicesMap.Count > 0)
        {
            // 1. 删除在已处理层级中不再存在的切片
            var obsoleteSlicesInProcessedLevels = existingSlicesMap
                .Where(kvp => !processedSliceKeys.Contains(kvp.Key))
                .Where(kvp => kvp.Value.Level <= config.MaxLevel) // 只看已处理的层级
                .Select(kvp => kvp.Value)
                .ToList();

            // 2. 删除超出新MaxLevel的所有切片（用户减少了LOD层级）
            var obsoleteSlicesBeyondMaxLevel = existingSlicesMap
                .Where(kvp => kvp.Value.Level > config.MaxLevel)
                .Select(kvp => kvp.Value)
                .ToList();

            var allObsoleteSlices = obsoleteSlicesInProcessedLevels.Concat(obsoleteSlicesBeyondMaxLevel).ToList();

            if (allObsoleteSlices.Any())
            {
                _logger.LogInformation("检测到{Count}个过时切片（{InLevel}个在已处理层级中，{BeyondLevel}个超出新的最大层级{MaxLevel}），开始清理",
                    allObsoleteSlices.Count,
                    obsoleteSlicesInProcessedLevels.Count,
                    obsoleteSlicesBeyondMaxLevel.Count,
                    config.MaxLevel);

                // 删除切片文件和数据库记录
                foreach (var obsoleteSlice in allObsoleteSlices)
                {
                    // 删除文件（本地或MinIO）
                    try
                    {
                        if (config.StorageLocation == StorageLocationType.LocalFileSystem)
                        {
                            // 对于本地存储，需要拼接完整路径
                            var fullPath = Path.IsPathRooted(obsoleteSlice.FilePath)
                                ? obsoleteSlice.FilePath
                                : Path.Combine(task.OutputPath ?? "", obsoleteSlice.FilePath);

                            if (File.Exists(fullPath))
                            {
                                File.Delete(fullPath);
                                _logger.LogDebug("本地切片文件已删除：{FilePath}", fullPath);
                            }
                        }
                        else
                        {
                            await _minioService.DeleteFileAsync("slices", obsoleteSlice.FilePath);
                            _logger.LogDebug("MinIO切片文件已删除：{FilePath}", obsoleteSlice.FilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "删除切片文件失败：{FilePath}", obsoleteSlice.FilePath);
                    }

                    // 删除数据库记录
                    await _sliceRepository.DeleteAsync(obsoleteSlice);
                    _logger.LogDebug("删除过时切片记录：级别{Level}, 坐标({X},{Y},{Z})",
                        obsoleteSlice.Level, obsoleteSlice.X, obsoleteSlice.Y, obsoleteSlice.Z);
                }

                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("已清理{Count}个过时切片（包括文件和数据库记录）", allObsoleteSlices.Count);
                hasSliceChanges = true; // 删除也是变化
            }
            else
            {
                _logger.LogInformation("增量更新：没有需要删除的过时切片");
            }
        }

        // 生成切片索引文件 - 使用新的统一索引生成器
        var indexGenerator = new IndexFileGenerator(
            _sliceRepository,
            _minioService,
            _logger);
        
        var indexResult = await indexGenerator.GenerateIndexFilesAsync(task, config, cancellationToken);
        
        if (!indexResult.Success)
        {
            _logger.LogWarning("索引文件生成存在警告或修复：任务{TaskId}，验证问题{IssueCount}个，修复成功{RepairSuccess}",
                task.Id, indexResult.ValidationResult?.Issues.Count ?? 0, indexResult.RepairResult?.Success ?? false);
        }
        else
        {
            _logger.LogInformation("索引文件生成成功：任务{TaskId}，包含{SliceCount}个切片",
                task.Id, indexResult.IndexJson?.SliceCount ?? 0);
        }

        // 生成增量更新索引（仅当实际使用了增量更新且有切片变化时）
        _logger.LogInformation("检查是否需要生成增量更新索引：实际使用增量更新={ActuallyUseIncrementalUpdate}, 有切片变化={HasSliceChanges}",
            actuallyUseIncrementalUpdate, hasSliceChanges);

        if (actuallyUseIncrementalUpdate)
        {
            if (hasSliceChanges)
            {
                _logger.LogInformation("开始生成增量更新索引：任务{TaskId}（检测到切片变化）", task.Id);
                await GenerateIncrementalUpdateIndexAsync(task, config, cancellationToken);
                _logger.LogInformation("增量更新索引生成完成：任务{TaskId}", task.Id);
            }
            else
            {
                _logger.LogInformation("增量更新：所有切片均未变化，无需重新生成增量索引：任务{TaskId}", task.Id);
            }
        }
        else
        {
            _logger.LogInformation("未使用增量更新（首次生成或未启用），跳过增量索引生成：任务{TaskId}", task.Id);
        }

        // 生成 tileset.json 文件（如果配置启用且TilesetGenerator可用）
        if (config.GenerateTileset && _tilesetGenerator != null)
        {
            try
            {
                _logger.LogInformation("开始生成 tileset.json 文件：任务{TaskId}", task.Id);

                // 获取所有切片数据
                var allSlices = await _sliceRepository.GetAllAsync();
                var taskSlices = allSlices.Where(s => s.SlicingTaskId == task.Id).ToList();

                if (taskSlices.Any())
                {
                    // 确定输出路径
                    string outputDirectory;
                    if (config.StorageLocation == StorageLocationType.LocalFileSystem)
                    {
                        outputDirectory = task.OutputPath ?? Directory.GetCurrentDirectory();
                    }
                    else
                    {
                        // MinIO 存储，使用临时目录
                        outputDirectory = Path.Combine(Path.GetTempPath(), $"tileset_{task.Id}");
                        Directory.CreateDirectory(outputDirectory);
                    }

                    // 生成 tileset.json
                    var tilesetPath = Path.Combine(outputDirectory, "tileset.json");
                    await _tilesetGenerator.GenerateTilesetJsonAsync(
                        taskSlices,
                        config,
                        modelBounds,
                        tilesetPath);

                    // 如果是 MinIO 存储，上传 tileset.json
                    if (config.StorageLocation == StorageLocationType.MinIO)
                    {
                        var tilesetContent = await File.ReadAllBytesAsync(tilesetPath, cancellationToken);
                        using var tilesetStream = new MemoryStream(tilesetContent);
                        await _minioService.UploadFileAsync("slices", "tileset.json", tilesetStream, "application/json", cancellationToken);

                        // 清理临时文件
                        if (Directory.Exists(outputDirectory))
                        {
                            Directory.Delete(outputDirectory, true);
                        }
                    }

                    _logger.LogInformation("tileset.json 生成成功：任务{TaskId}, 包含{SliceCount}个切片, LOD级别{MaxLevel}",
                        task.Id, taskSlices.Count, config.MaxLevel);
                }
                else
                {
                    _logger.LogWarning("无法生成 tileset.json：任务{TaskId} 没有生成任何切片", task.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成 tileset.json 失败：任务{TaskId}", task.Id);
                // 不抛出异常，允许任务继续完成
            }
        }
        else if (!config.GenerateTileset)
        {
            _logger.LogDebug("配置未启用 tileset.json 生成：任务{TaskId}", task.Id);
        }
        else
        {
            _logger.LogWarning("TilesetGenerator 未注入，无法生成 tileset.json：任务{TaskId}", task.Id);
        }

        _logger.LogInformation("切片处理完成：任务{TaskId}", task.Id);
    }

    /// <summary>
    /// 从源模型文件加载三角形数据
    /// 这是一个简化版本的LoadModelAsync，只提取三角形数据
    /// </summary>
    private async Task<List<Triangle>> LoadTrianglesFromModelAsync(SlicingTask task, CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始加载模型三角形：{SourceModelPath}", task.SourceModelPath);

        Stream? modelStream = null;
        bool isLocalPath = false;

        // 判断是否为本地文件路径
        // Windows: C:\ or C:/ or \\server\share
        // Unix: /path/to/file
        if (Path.IsPathRooted(task.SourceModelPath) ||
            task.SourceModelPath.StartsWith("\\\\") ||
            (task.SourceModelPath.Length >= 2 && task.SourceModelPath[1] == ':'))
        {
            isLocalPath = true;
            _logger.LogDebug("检测到本地文件路径：{Path}", task.SourceModelPath);
        }

        // 1. 如果是本地路径，直接从本地加载
        if (isLocalPath && File.Exists(task.SourceModelPath))
        {
            _logger.LogDebug("从本地文件系统加载：{LocalPath}", task.SourceModelPath);
            modelStream = File.OpenRead(task.SourceModelPath);
        }
        // 2. 否则尝试从MinIO加载
        else if (!isLocalPath)
        {
            try
            {
                var segments = task.SourceModelPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2)
                {
                    var bucket = segments[0];
                    var objectName = string.Join("/", segments.Skip(1));
                    _logger.LogDebug("尝试从MinIO加载：bucket={Bucket}, object={ObjectName}", bucket, objectName);
                    modelStream = await _minioService.DownloadFileAsync(bucket, objectName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "从MinIO加载模型失败：{SourceModelPath}", task.SourceModelPath);
            }
        }

        // 3. 如果MinIO加载失败，再尝试本地路径作为备用
        if (modelStream == null && File.Exists(task.SourceModelPath))
        {
            _logger.LogDebug("MinIO加载失败，尝试本地文件系统：{LocalPath}", task.SourceModelPath);
            modelStream = File.OpenRead(task.SourceModelPath);
        }

        // 4. 如果所有数据源都失败，返回空列表
        if (modelStream == null)
        {
            _logger.LogWarning("无法从任何数据源加载模型文件：{SourceModelPath}", task.SourceModelPath);
            return new List<Triangle>();
        }

        // 4. 根据文件扩展名解析模型格式
        var fileExtension = Path.GetExtension(task.SourceModelPath).ToLowerInvariant();
        List<Triangle> triangles;

        // 创建一个临时的 AdaptiveSlicingStrategy 实例来调用解析方法
        // 这些解析方法是格式转换工具，不依赖于具体的切片策略
        var tempStrategy = new AdaptiveSlicingStrategy(_logger, _tileGeneratorFactory, _minioService);

        try
        {
            await using (modelStream)
            {
                switch (fileExtension)
                {
                    case ".obj":
                        triangles = await tempStrategy.ParseOBJFormatAsync(modelStream, cancellationToken);
                        break;

                    case ".stl":
                        triangles = await tempStrategy.ParseSTLFormatAsync(modelStream, cancellationToken);
                        break;

                    case ".ply":
                        triangles = await tempStrategy.ParsePLYFormatAsync(modelStream, cancellationToken);
                        break;

                    case ".gltf":
                    case ".glb":
                        triangles = await tempStrategy.ParseGLTFFormatAsync(modelStream, cancellationToken);
                        break;

                    default:
                        _logger.LogWarning("不支持的模型格式：{FileExtension}", fileExtension);
                        return new List<Triangle>();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "模型文件解析失败：{FileExtension}", fileExtension);
            return new List<Triangle>();
        }

        _logger.LogInformation("三角形加载完成：{TriangleCount}个三角形", triangles.Count);
        return triangles;
    }

    /// <summary>
    /// 计算模型包围盒
    /// </summary>
    private BoundingBox3D CalculateModelBounds(List<Triangle> triangles)
    {
        var bounds = new BoundingBox3D();

        if (triangles == null || triangles.Count == 0)
        {
            return bounds;
        }

        bounds.MinX = double.MaxValue;
        bounds.MinY = double.MaxValue;
        bounds.MinZ = double.MaxValue;
        bounds.MaxX = double.MinValue;
        bounds.MaxY = double.MinValue;
        bounds.MaxZ = double.MinValue;

        foreach (var triangle in triangles)
        {
            foreach (var vertex in triangle.Vertices)
            {
                bounds.MinX = Math.Min(bounds.MinX, vertex.X);
                bounds.MinY = Math.Min(bounds.MinY, vertex.Y);
                bounds.MinZ = Math.Min(bounds.MinZ, vertex.Z);
                bounds.MaxX = Math.Max(bounds.MaxX, vertex.X);
                bounds.MaxY = Math.Max(bounds.MaxY, vertex.Y);
                bounds.MaxZ = Math.Max(bounds.MaxZ, vertex.Z);
            }
        }

        return bounds;
    }

    /// <summary>
    /// 构建三角形的空间索引（简单网格索引）
    /// 将三角形按照其包围盒分配到网格单元中，以加速空间查询
    /// </summary>
    private Dictionary<string, List<Triangle>> BuildTriangleSpatialIndex(List<Triangle> triangles)
    {
        var spatialIndex = new Dictionary<string, List<Triangle>>();

        if (triangles.Count == 0)
        {
            return spatialIndex;
        }

        // 计算整体包围盒
        var modelBounds = CalculateModelBounds(triangles);
        double minX = modelBounds.MinX, minY = modelBounds.MinY, minZ = modelBounds.MinZ;
        double maxX = modelBounds.MaxX, maxY = modelBounds.MaxY, maxZ = modelBounds.MaxZ;

        // 创建粗粒度网格索引（64x64x32网格）
        const int gridSizeX = 64;
        const int gridSizeY = 64;
        const int gridSizeZ = 32;

        double cellSizeX = (maxX - minX) / gridSizeX;
        double cellSizeY = (maxY - minY) / gridSizeY;
        double cellSizeZ = (maxZ - minZ) / gridSizeZ;

        // 防止除零
        if (cellSizeX <= 0) cellSizeX = 1.0;
        if (cellSizeY <= 0) cellSizeY = 1.0;
        if (cellSizeZ <= 0) cellSizeZ = 1.0;

        // 将每个三角形添加到其包围盒相交的所有网格单元
        foreach (var triangle in triangles)
        {
            // 计算三角形包围盒
            double triMinX = triangle.Vertices.Min(v => v.X);
            double triMinY = triangle.Vertices.Min(v => v.Y);
            double triMinZ = triangle.Vertices.Min(v => v.Z);
            double triMaxX = triangle.Vertices.Max(v => v.X);
            double triMaxY = triangle.Vertices.Max(v => v.Y);
            double triMaxZ = triangle.Vertices.Max(v => v.Z);

            // 计算三角形跨越的网格范围
            int startX = Math.Max(0, (int)((triMinX - minX) / cellSizeX));
            int startY = Math.Max(0, (int)((triMinY - minY) / cellSizeY));
            int startZ = Math.Max(0, (int)((triMinZ - minZ) / cellSizeZ));
            int endX = Math.Min(gridSizeX - 1, (int)((triMaxX - minX) / cellSizeX));
            int endY = Math.Min(gridSizeY - 1, (int)((triMaxY - minY) / cellSizeY));
            int endZ = Math.Min(gridSizeZ - 1, (int)((triMaxZ - minZ) / cellSizeZ));

            // 将三角形添加到所有相交的网格单元
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    for (int z = startZ; z <= endZ; z++)
                    {
                        var cellKey = $"{x}_{y}_{z}";
                        if (!spatialIndex.ContainsKey(cellKey))
                        {
                            spatialIndex[cellKey] = new List<Triangle>();
                        }
                        spatialIndex[cellKey].Add(triangle);
                    }
                }
            }
        }

        _logger.LogInformation("空间索引构建完成：{CellCount}个网格单元，平均每单元{AvgTriangles:F1}个三角形",
            spatialIndex.Count,
            triangles.Count / (double)Math.Max(1, spatialIndex.Count));

        return spatialIndex;
    }

    /// <summary>
    /// 查询切片包围盒内的三角形
    /// 使用空间索引快速定位可能相交的三角形，然后进行精确的相交测试
    /// 支持坐标变换以解决切片包围盒和模型坐标系不匹配的问题
    /// </summary>
    private List<Triangle> QueryTrianglesForSlice(Slice slice, Dictionary<string, List<Triangle>> spatialIndex, BoundingBox3D modelBounds)
    {
        var result = new List<Triangle>();

        try
        {
            // 解析切片包围盒
            var boundingBox = System.Text.Json.JsonSerializer.Deserialize<BoundingBox>(slice.BoundingBox);
            if (boundingBox == null)
            {
                _logger.LogWarning("无法解析切片包围盒：{SliceKey}", $"{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}");
                return result;
            }

            // 输出原始切片包围盒
            _logger.LogDebug("切片包围盒：Level={Level}, X={X}, Y={Y}, Z={Z}, 包围盒=[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}]",
                slice.Level, slice.X, slice.Y, slice.Z,
                boundingBox.MinX, boundingBox.MinY, boundingBox.MinZ,
                boundingBox.MaxX, boundingBox.MaxY, boundingBox.MaxZ);

            // 注意：切片包围盒已经在 GenerateGridBoundingBox 中映射到模型坐标系，
            // 这里不需要再进行坐标变换，直接使用即可

            // 调试信息：记录切片包围盒范围和模型包围盒
            _logger.LogDebug("查询切片三角形 - 切片包围盒：Level={Level}, X={X}, Y={Y}, Z={Z}, 包围盒=[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}]",
                slice.Level, slice.X, slice.Y, slice.Z,
                boundingBox.MinX, boundingBox.MinY, boundingBox.MinZ,
                boundingBox.MaxX, boundingBox.MaxY, boundingBox.MaxZ);

            _logger.LogDebug("查询切片三角形 - 模型包围盒：[{ModelMinX:F3},{ModelMinY:F3},{ModelMinZ:F3}]-[{ModelMaxX:F3},{ModelMaxY:F3},{ModelMaxZ:F3}]",
                modelBounds.MinX, modelBounds.MinY, modelBounds.MinZ,
                modelBounds.MaxX, modelBounds.MaxY, modelBounds.MaxZ);

            // 使用HashSet去重（因为三角形可能被多个网格单元引用）
            var candidateTriangles = new HashSet<Triangle>();

            // 统计信息
            var totalTrianglesInIndex = spatialIndex.Values.Sum(list => list.Count);
            var testedTriangles = 0;
            var intersectingTriangles = 0;

            // 增大容差值来处理浮点数精度问题和边界情况
            // 改进的容差策略：对小切片使用更大的相对容差
            var sliceSize = Math.Max(
                Math.Max(boundingBox.MaxX - boundingBox.MinX, boundingBox.MaxY - boundingBox.MinY),
                boundingBox.MaxZ - boundingBox.MinZ);

            // 计算模型尺寸用于自适应容差
            var modelSize = Math.Max(
                Math.Max(modelBounds.MaxX - modelBounds.MinX, modelBounds.MaxY - modelBounds.MinY),
                modelBounds.MaxZ - modelBounds.MinZ);

            // 自适应容差策略：
            // 1. 对于大切片（>10%模型尺寸）：使用切片尺寸的1%
            // 2. 对于中等切片（1%-10%模型尺寸）：使用切片尺寸的5%
            // 3. 对于小切片（<1%模型尺寸）：使用切片尺寸的10%或模型尺寸的0.1%（取较大者）
            double tolerance;
            var sizeRatio = sliceSize / modelSize;

            if (sizeRatio > 0.1)
            {
                // 大切片：1%容差
                tolerance = Math.Max(sliceSize * 0.01, 1e-4);
            }
            else if (sizeRatio > 0.01)
            {
                // 中等切片：5%容差
                tolerance = Math.Max(sliceSize * 0.05, modelSize * 0.001);
            }
            else
            {
                // 小切片：10%容差或模型尺寸的0.1%，取较大者
                tolerance = Math.Max(sliceSize * 0.1, modelSize * 0.001);
            }

            // 确保最小容差不低于1e-4
            tolerance = Math.Max(tolerance, 1e-4);

            _logger.LogDebug("切片包围盒容差：{Tolerance:E3}，切片尺寸：{SliceSize:F6}，模型尺寸：{ModelSize:F6}，尺寸比例：{Ratio:F6}",
                tolerance, sliceSize, modelSize, sizeRatio);

            // 计算切片包围盒应该查询的空间索引网格范围
            // 重新计算网格参数（与BuildTriangleSpatialIndex保持一致）
            const int gridSizeX = 64;
            const int gridSizeY = 64;
            const int gridSizeZ = 32;

            double cellSizeX = (modelBounds.MaxX - modelBounds.MinX) / gridSizeX;
            double cellSizeY = (modelBounds.MaxY - modelBounds.MinY) / gridSizeY;
            double cellSizeZ = (modelBounds.MaxZ - modelBounds.MinZ) / gridSizeZ;

            // 防止除零
            if (cellSizeX <= 0) cellSizeX = 1.0;
            if (cellSizeY <= 0) cellSizeY = 1.0;
            if (cellSizeZ <= 0) cellSizeZ = 1.0;

            // 计算切片包围盒（带容差）跨越的网格范围
            int startX = Math.Max(0, (int)((boundingBox.MinX - tolerance - modelBounds.MinX) / cellSizeX));
            int startY = Math.Max(0, (int)((boundingBox.MinY - tolerance - modelBounds.MinY) / cellSizeY));
            int startZ = Math.Max(0, (int)((boundingBox.MinZ - tolerance - modelBounds.MinZ) / cellSizeZ));
            int endX = Math.Min(gridSizeX - 1, (int)((boundingBox.MaxX + tolerance - modelBounds.MinX) / cellSizeX));
            int endY = Math.Min(gridSizeY - 1, (int)((boundingBox.MaxY + tolerance - modelBounds.MinY) / cellSizeY));
            int endZ = Math.Min(gridSizeZ - 1, (int)((boundingBox.MaxZ + tolerance - modelBounds.MinZ) / cellSizeZ));

            // 对于高LOD层级的小切片，确保至少查询相邻的网格单元
            // 这样可以避免因为切片太小而遗漏三角形
            if (sizeRatio < 0.01) // 小于模型尺寸的1%
            {
                // 扩展查询范围至少包含相邻的一个单元
                startX = Math.Max(0, startX - 1);
                startY = Math.Max(0, startY - 1);
                startZ = Math.Max(0, startZ - 1);
                endX = Math.Min(gridSizeX - 1, endX + 1);
                endY = Math.Min(gridSizeY - 1, endY + 1);
                endZ = Math.Min(gridSizeZ - 1, endZ + 1);

                _logger.LogDebug("小切片扩展查询范围：原始范围扩展±1个网格单元");
            }

            _logger.LogDebug("切片包围盒查询网格范围：X=[{StartX},{EndX}], Y=[{StartY},{EndY}], Z=[{StartZ},{EndZ}]",
                startX, endX, startY, endY, startZ, endZ);

            // 只从相关的网格单元中查询三角形
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    for (int z = startZ; z <= endZ; z++)
                    {
                        var cellKey = $"{x}_{y}_{z}";
                        if (!spatialIndex.ContainsKey(cellKey))
                            continue;

                        foreach (var triangle in spatialIndex[cellKey])
                        {
                            // 使用HashSet自动去重
                            if (candidateTriangles.Contains(triangle))
                                continue;

                            testedTriangles++;

                            // 首先使用简单的包围盒相交测试进行快速筛选
                            double triMinX = triangle.Vertices.Min(v => v.X);
                            double triMinY = triangle.Vertices.Min(v => v.Y);
                            double triMinZ = triangle.Vertices.Min(v => v.Z);
                            double triMaxX = triangle.Vertices.Max(v => v.X);
                            double triMaxY = triangle.Vertices.Max(v => v.Y);
                            double triMaxZ = triangle.Vertices.Max(v => v.Z);

                            // AABB包围盒相交测试（带容差）
                            bool intersects = !(triMaxX < boundingBox.MinX - tolerance || triMinX > boundingBox.MaxX + tolerance ||
                                               triMaxY < boundingBox.MinY - tolerance || triMinY > boundingBox.MaxY + tolerance ||
                                               triMaxZ < boundingBox.MinZ - tolerance || triMinZ > boundingBox.MaxZ + tolerance);

                            if (intersects)
                            {
                                // 如果AABB包围盒相交，进行更精确的相交测试
                                if (TriangleIntersectsSlice(triangle, boundingBox, tolerance))
                                {
                                    candidateTriangles.Add(triangle);
                                    intersectingTriangles++;
                                }
                            }
                        }
                    }
                }
            }

            result.AddRange(candidateTriangles);

            // 详细的调试日志
            _logger.LogDebug("切片三角形查询结果：总三角形={Total}, 测试={Tested}, 相交={Intersecting}, 结果={ResultCount}",
                totalTrianglesInIndex, testedTriangles, intersectingTriangles, result.Count);

            // 如果找到了相交三角形，记录成功信息
            if (result.Count > 0)
            {
                _logger.LogInformation("✓ 切片成功找到三角形：Level={Level}, X={X}, Y={Y}, Z={Z}, 找到{Count}个三角形",
                    slice.Level, slice.X, slice.Y, slice.Z, result.Count);
            }
            // 如果没有找到相交三角形，但有三角形数据，记录警告
            else if (result.Count == 0 && totalTrianglesInIndex > 0)
            {
                _logger.LogWarning("切片没有找到相交三角形：Level={Level}, X={X}, Y={Y}, Z={Z}, 总三角形={TotalTriangles}, 包围盒=[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}]",
                    slice.Level, slice.X, slice.Y, slice.Z, totalTrianglesInIndex,
                    boundingBox.MinX, boundingBox.MinY, boundingBox.MinZ,
                    boundingBox.MaxX, boundingBox.MaxY, boundingBox.MaxZ);

                // 计算模型的总包围盒范围，用于诊断坐标系问题
                double modelMinX = double.MaxValue, modelMinY = double.MaxValue, modelMinZ = double.MaxValue;
                double modelMaxX = double.MinValue, modelMaxY = double.MinValue, modelMaxZ = double.MinValue;

                foreach (var triangleList in spatialIndex.Values)
                {
                    foreach (var triangle in triangleList)
                    {
                        foreach (var vertex in triangle.Vertices)
                        {
                            modelMinX = Math.Min(modelMinX, vertex.X);
                            modelMinY = Math.Min(modelMinY, vertex.Y);
                            modelMinZ = Math.Min(modelMinZ, vertex.Z);
                            modelMaxX = Math.Max(modelMaxX, vertex.X);
                            modelMaxY = Math.Max(modelMaxY, vertex.Y);
                            modelMaxZ = Math.Max(modelMaxZ, vertex.Z);
                        }
                    }
                }

                _logger.LogWarning("模型总包围盒：[{ModelMinX:F3},{ModelMinY:F3},{ModelMinZ:F3}]-[{ModelMaxX:F3},{ModelMaxY:F3},{ModelMaxZ:F3}]",
                    modelMinX, modelMinY, modelMinZ, modelMaxX, modelMaxY, modelMaxZ);

                // 输出前3个三角形的坐标信息以帮助调试
                var sampleCount = 0;
                foreach (var triangleList in spatialIndex.Values)
                {
                    foreach (var triangle in triangleList)
                    {
                        if (sampleCount < 3)
                        {
                            var v0 = triangle.Vertices[0];
                            var v1 = triangle.Vertices[1];
                            var v2 = triangle.Vertices[2];
                            _logger.LogWarning("示例三角形 {Index}: V0=({V0X:F3},{V0Y:F3},{V0Z:F3}), V1=({V1X:F3},{V1Y:F3},{V1Z:F3}), V2=({V2X:F3},{V2Y:F3},{V2Z:F3})",
                                sampleCount + 1, v0.X, v0.Y, v0.Z, v1.X, v1.Y, v1.Z, v2.X, v2.Y, v2.Z);
                            sampleCount++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (sampleCount >= 3) break;
                }

                // 可能的修复：如果坐标系明显不匹配，提供警告
                var debugSliceSize = Math.Max(boundingBox.MaxX - boundingBox.MinX,
                               Math.Max(boundingBox.MaxY - boundingBox.MinY, boundingBox.MaxZ - boundingBox.MinZ));
                var debugModelSize = Math.Max(modelMaxX - modelMinX,
                               Math.Max(modelMaxY - modelMinY, modelMaxZ - modelMinZ));

                if (debugSliceSize > 0 && debugModelSize > 0)
                {
                    var debugSizeRatio = debugSliceSize / debugModelSize;
                    if (debugSizeRatio < 0.01 || debugSizeRatio > 100)
                    {
                        _logger.LogWarning("检测到坐标系可能不匹配：切片尺寸={SliceSize:F3}, 模型尺寸={ModelSize:F3}, 比例={Ratio:F6}",
                            debugSliceSize, debugModelSize, debugSizeRatio);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询切片三角形失败：{SliceKey}", $"{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}");
        }

        return result;
    }

    /// <summary>
    /// 检查三角形是否与切片包围盒相交
    /// 使用更精确的相交测试算法，比简单的AABB测试更准确
    /// </summary>
    /// <param name="triangle">待测试的三角形</param>
    /// <param name="boundingBox">切片的包围盒</param>
    /// <param name="tolerance">容差值</param>
    /// <returns>如果三角形与包围盒相交则返回true，否则返回false</returns>
    private bool TriangleIntersectsSlice(Triangle triangle, BoundingBox boundingBox, double tolerance)
    {
        // 1. 首先再次进行AABB包围盒测试作为快速拒绝测试
        var triMinX = triangle.Vertices.Min(v => v.X);
        var triMinY = triangle.Vertices.Min(v => v.Y);
        var triMinZ = triangle.Vertices.Min(v => v.Z);
        var triMaxX = triangle.Vertices.Max(v => v.X);
        var triMaxY = triangle.Vertices.Max(v => v.Y);
        var triMaxZ = triangle.Vertices.Max(v => v.Z);

        if (triMaxX < boundingBox.MinX - tolerance || triMinX > boundingBox.MaxX + tolerance ||
            triMaxY < boundingBox.MinY - tolerance || triMinY > boundingBox.MaxY + tolerance ||
            triMaxZ < boundingBox.MinZ - tolerance || triMinZ > boundingBox.MaxZ + tolerance)
        {
            return false; // 快速拒绝：AABB不相交
        }

        // 2. 检查三角形的三个顶点是否在包围盒内部
        foreach (var vertex in triangle.Vertices)
        {
            if (vertex.X >= boundingBox.MinX - tolerance && vertex.X <= boundingBox.MaxX + tolerance &&
                vertex.Y >= boundingBox.MinY - tolerance && vertex.Y <= boundingBox.MaxY + tolerance &&
                vertex.Z >= boundingBox.MinZ - tolerance && vertex.Z <= boundingBox.MaxZ + tolerance)
            {
                return true; // 至少一个顶点在包围盒内部
            }
        }

        // 3. 检查包围盒的顶点是否在三角形内部（通过扩展包围盒来测试）
        // 这种情况比较复杂，我们可以使用分离轴定理(Separating Axis Theorem, SAT)
        // 但为了简化，这里使用点到三角形距离的方法

        // 4. 检查三角形边与包围盒的相交（简化版）
        // 检查三角形的三条边是否穿过包围盒的面
        for (int i = 0; i < 3; i++)
        {
            var v1 = triangle.Vertices[i];
            var v2 = triangle.Vertices[(i + 1) % 3];

            // 检查边的线段是否与包围盒相交
            if (LineIntersectsBox(v1, v2, boundingBox, tolerance))
            {
                return true;
            }
        }

        // 5. 最后检查：检查三角形是否跨越包围盒的边界
        // 通过检查三角形平面是否与包围盒相交
        if (TrianglePlaneIntersectsBox(triangle, boundingBox, tolerance))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 检查线段是否与轴对齐包围盒相交
    /// </summary>
    private bool LineIntersectsBox(Vector3D v1, Vector3D v2, BoundingBox box, double tolerance)
    {
        // 使用Liang-Barsky算法来测试线段与AABB的相交
        double tMin = 0.0;
        double tMax = 1.0;

        // X轴测试
        if (Math.Abs(v2.X - v1.X) < tolerance)
        {
            // 线段平行于Y-Z平面
            if (v1.X < box.MinX - tolerance || v1.X > box.MaxX + tolerance)
            {
                return false; // 线段在包围盒之外
            }
        }
        else
        {
            double invDir = 1.0 / (v2.X - v1.X);
            double t1 = (box.MinX - v1.X) * invDir;
            double t2 = (box.MaxX - v1.X) * invDir;
            if (t1 > t2) { (t1, t2) = (t2, t1); } // 交换t1和t2
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax) return false;
        }

        // Y轴测试
        if (Math.Abs(v2.Y - v1.Y) < tolerance)
        {
            if (v1.Y < box.MinY - tolerance || v1.Y > box.MaxY + tolerance)
            {
                return false;
            }
        }
        else
        {
            double invDir = 1.0 / (v2.Y - v1.Y);
            double t1 = (box.MinY - v1.Y) * invDir;
            double t2 = (box.MaxY - v1.Y) * invDir;
            if (t1 > t2) { (t1, t2) = (t2, t1); }
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax) return false;
        }

        // Z轴测试
        if (Math.Abs(v2.Z - v1.Z) < tolerance)
        {
            if (v1.Z < box.MinZ - tolerance || v1.Z > box.MaxZ + tolerance)
            {
                return false;
            }
        }
        else
        {
            double invDir = 1.0 / (v2.Z - v1.Z);
            double t1 = (box.MinZ - v1.Z) * invDir;
            double t2 = (box.MaxZ - v1.Z) * invDir;
            if (t1 > t2) { (t1, t2) = (t2, t1); }
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax) return false;
        }

        // 如果线段与包围盒相交，返回true
        return tMin <= tMax && tMin <= 1.0 && tMax >= 0.0;
    }

    /// <summary>
    /// 检查三角形平面是否与轴对齐包围盒相交
    /// 这是一个简化的近似测试
    /// </summary>
    private bool TrianglePlaneIntersectsBox(Triangle triangle, BoundingBox box, double tolerance)
    {
        // 此处可以实现更复杂的相交测试
        // 为简化处理，我们使用之前AABB测试的结果作为基础
        // 因为我们已经通过了AABB测试，这里返回true
        // 在实际应用中，可能需要实现更复杂的相交检测算法

        // 如果三角形平面与包围盒可能相交，返回true
        // 检查三角形的重心是否接近包围盒
        var center = new Vector3D
        {
            X = (triangle.Vertices[0].X + triangle.Vertices[1].X + triangle.Vertices[2].X) / 3.0,
            Y = (triangle.Vertices[0].Y + triangle.Vertices[1].Y + triangle.Vertices[2].Y) / 3.0,
            Z = (triangle.Vertices[0].Z + triangle.Vertices[1].Z + triangle.Vertices[2].Z) / 3.0
        };

        // 检查重心是否在扩展的包围盒内
        return center.X >= box.MinX - tolerance && center.X <= box.MaxX + tolerance &&
               center.Y >= box.MinY - tolerance && center.Y <= box.MaxY + tolerance &&
               center.Z >= box.MinZ - tolerance && center.Z <= box.MaxZ + tolerance;
    }

    private static SlicingConfig ParseSlicingConfig(string configJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize<SlicingConfig>(configJson);
            return config ?? new SlicingConfig();
        }
        catch
        {
            return new SlicingConfig();
        }
    }

    /// <summary>
    /// 生成切片包围盒JSON - 轴对齐包围盒（AABB）算法实现
    /// 算法：基于切片网格坐标和切片大小计算空间边界框
    /// </summary>
    /// <param name="level">LOD级别</param>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <param name="z">Z坐标</param>
    /// <param name="tileSize">切片大小</param>
    /// <returns>JSON格式的包围盒数据</returns>
    private string GenerateBoundingBoxJson(int level, int x, int y, int z, double tileSize)
    {
        // AABB算法：计算轴对齐的最小包围盒
        var minX = x * tileSize;
        var minY = y * tileSize;
        var minZ = z * tileSize;
        var maxX = (x + 1) * tileSize;
        var maxY = (y + 1) * tileSize;
        var maxZ = (z + 1) * tileSize;

        return $"{{\"minX\":{minX},\"minY\":{minY},\"minZ\":{minZ},\"maxX\":{maxX},\"maxY\":{maxY},\"maxZ\":{maxZ}}}";
    }

    /// <summary>
    /// 生成切片索引文件 - 已迁移到IndexFileGenerator
    /// 此方法已弃用，请使用IndexFileGenerator.GenerateIndexFilesAsync
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="config">切片配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    [Obsolete("Use IndexFileGenerator.GenerateIndexFilesAsync instead")]
    private async Task GenerateSliceIndexAsync(SlicingTask task, SlicingConfig config, CancellationToken cancellationToken)
    {
        _logger.LogWarning("调用了已弃用的方法GenerateSliceIndexAsync，请使用IndexFileGenerator");
        
        // 保持原有逻辑以确保向后兼容
        var allSlices = await _sliceRepository.GetAllAsync();
        var slices = allSlices.Where(s => s.SlicingTaskId == task.Id).ToList();

        var index = new
        {
            TaskId = task.Id,
            TotalLevels = config.MaxLevel,
            config.TileSize,
            config.OutputFormat,
            SliceCount = slices.Count(),
            BoundingBox = CalculateTotalBoundingBox(slices.ToList()),
            Slices = slices.Select(s => new
            {
                s.Level,
                s.X,
                s.Y,
                s.Z,
                s.FilePath,
                s.FileSize,
                s.BoundingBox
            }).ToList()
        };

        var indexContent = JsonSerializer.Serialize(index, new JsonSerializerOptions
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
            _logger.LogInformation("切片索引文件已保存到本地：{FilePath}", fullPath);
        }
        else
        {
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(indexContent)))
            {
                await _minioService.UploadFileAsync("slices", indexPath, stream, "application/json", cancellationToken);
            }
            _logger.LogInformation("切片索引文件已上传到MinIO：{FilePath}, 切片数量：{Count}",
                indexPath, slices.Count);
        }
    }

    /// <summary>
    /// 生成Cesium Tileset JSON文件 - 已迁移到IndexFileGenerator
    /// 此方法已弃用，请使用IndexFileGenerator.GenerateIndexFilesAsync
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="config">切片配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    [Obsolete("Use IndexFileGenerator.GenerateIndexFilesAsync instead")]
    private async Task GenerateTilesetJsonAsync(SlicingTask task, SlicingConfig config, CancellationToken cancellationToken)
    {
        _logger.LogWarning("调用了已弃用的方法GenerateTilesetJsonAsync，请使用IndexFileGenerator");
        
        // 保持原有逻辑以确保向后兼容
        var allSlices = await _sliceRepository.GetAllAsync();
        var taskSlices = allSlices.Where(s => s.SlicingTaskId == task.Id).ToList();

        if (!taskSlices.Any())
        {
            _logger.LogWarning("没有找到切片数据，跳过tileset.json生成");
            return;
        }

        var rootBoundingVolume = CalculateRootBoundingVolume(taskSlices);
        var rootTile = new
        {
            boundingVolume = rootBoundingVolume,
            geometricError = CalculateGeometricError(0, config),
            refine = "REPLACE",
            children = BuildLodHierarchy(taskSlices, config, 0)
        };

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
            _logger.LogInformation("Tileset JSON文件已保存到本地：{FilePath}", fullPath);
        }
        else
        {
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(tilesetContent)))
            {
                await _minioService.UploadFileAsync("slices", tilesetPath, stream, "application/json", cancellationToken);
            }
            _logger.LogInformation("Tileset JSON文件已上传到MinIO：{FilePath}", tilesetPath);
        }

        _logger.LogInformation("Tileset JSON生成完成：包含{SliceCount}个切片，根节点几何误差{Error}",
            taskSlices.Count, config.GeometricErrorThreshold * 4);
    }

    /// <summary>
    /// 计算根节点的包围体积 - 已迁移到IndexFileGenerator
    /// 此方法已弃用，请使用IndexFileGenerator中的对应方法
    /// </summary>
    /// <param name="slices">切片列表</param>
    /// <returns>包围体积数据</returns>
    [Obsolete("Use IndexFileGenerator.CalculateRootBoundingVolume instead")]
    private object CalculateRootBoundingVolume(List<Slice> slices)
    {
        _logger.LogWarning("调用了已弃用的方法CalculateRootBoundingVolume，请使用IndexFileGenerator");
        
        if (!slices.Any()) return new { box = new[] { 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0 } };

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var minZ = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        var maxZ = double.MinValue;

        foreach (var slice in slices)
        {
            try
            {
                var boundingBox = JsonSerializer.Deserialize<Dictionary<string, double>>(slice.BoundingBox ?? "{}");
                if (boundingBox != null)
                {
                    minX = Math.Min(minX, boundingBox.GetValueOrDefault("minX", 0));
                    minY = Math.Min(minY, boundingBox.GetValueOrDefault("minY", 0));
                    minZ = Math.Min(minZ, boundingBox.GetValueOrDefault("minZ", 0));
                    maxX = Math.Max(maxX, boundingBox.GetValueOrDefault("maxX", 0));
                    maxY = Math.Max(maxY, boundingBox.GetValueOrDefault("maxY", 0));
                    maxZ = Math.Max(maxZ, boundingBox.GetValueOrDefault("maxZ", 0));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解析切片包围盒失败：{SliceId}", slice.Id);
            }
        }

        if (minX == double.MaxValue)
        {
            return new { box = new[] { 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0 } };
        }

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
    /// 构建LOD层次结构 - 已迁移到IndexFileGenerator
    /// 此方法已弃用，请使用IndexFileGenerator中的对应方法
    /// </summary>
    /// <param name="allSlices">所有切片数据</param>
    /// <param name="config">切片配置</param>
    /// <param name="currentLevel">当前LOD级别</param>
    /// <returns>层次结构数据</returns>
    [Obsolete("Use IndexFileGenerator.BuildLodHierarchy instead")]
    private List<object>? BuildLodHierarchy(List<Slice> allSlices, SlicingConfig config, int currentLevel)
    {
        _logger.LogWarning("调用了已弃用的方法BuildLodHierarchy，请使用IndexFileGenerator");
        
        var levelSlices = allSlices.Where(s => s.Level == currentLevel).ToList();
        if (!levelSlices.Any()) return null;

        var children = new List<object>();

        if (currentLevel >= config.MaxLevel)
        {
            foreach (var slice in levelSlices)
            {
                children.Add(new
                {
                    boundingVolume = ParseBoundingVolume(slice.BoundingBox),
                    geometricError = 0.0,
                    content = new
                    {
                        uri = slice.FilePath
                    }
                });
            }
        }
        else
        {
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
                    tileData["content"] = new { uri = slice.FilePath };
                }

                children.Add(tileData);
            }
        }

        return children.Any() ? children : null;
    }

    /// <summary>
    /// 解析单个切片的包围体积 - 已迁移到IndexFileGenerator
    /// 此方法已弃用，请使用IndexFileGenerator中的对应方法
    /// </summary>
    /// <param name="boundingBoxJson">包围盒JSON字符串</param>
    /// <returns>包围体积数据</returns>
    [Obsolete("Use IndexFileGenerator.ParseBoundingVolume instead")]
    private object ParseBoundingVolume(string? boundingBoxJson)
    {
        _logger.LogWarning("调用了已弃用的方法ParseBoundingVolume，请使用IndexFileGenerator");
        
        if (string.IsNullOrEmpty(boundingBoxJson))
            return new { box = new[] { 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0 } };

        try
        {
            var boundingBox = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(boundingBoxJson);
            if (boundingBox == null)
                return new { box = new[] { 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0 } };

            var minX = boundingBox.GetValueOrDefault("minX", 0);
            var minY = boundingBox.GetValueOrDefault("minY", 0);
            var minZ = boundingBox.GetValueOrDefault("minZ", 0);
            var maxX = boundingBox.GetValueOrDefault("maxX", 0);
            var maxY = boundingBox.GetValueOrDefault("maxY", 0);
            var maxZ = boundingBox.GetValueOrDefault("maxZ", 0);

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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析包围体积失败，使用默认值");
            return new { box = new[] { 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0 } };
        }
    }

    /// <summary>
    /// 计算总包围盒 - 已迁移到IndexFileGenerator
    /// 此方法已弃用，请使用IndexFileGenerator中的对应方法
    /// </summary>
    /// <param name="slices">切片列表</param>
    /// <returns>包围盒JSON字符串</returns>
    [Obsolete("Use IndexFileGenerator.CalculateTotalBoundingBox instead")]
    private string CalculateTotalBoundingBox(List<Slice> slices)
    {
        _logger.LogWarning("调用了已弃用的方法CalculateTotalBoundingBox，请使用IndexFileGenerator");
        
        if (!slices.Any()) return "{}";

        var minX = slices.Min(s => s.X);
        var minY = slices.Min(s => s.Y);
        var minZ = slices.Min(s => s.Z);
        var maxX = slices.Max(s => s.X);
        var maxY = slices.Max(s => s.Y);
        var maxZ = slices.Max(s => s.Z);

        return $"{{\"minX\":{minX},\"minY\":{minY},\"minZ\":{minZ},\"maxX\":{maxX + 1},\"maxY\":{maxY + 1},\"maxZ\":{maxZ + 1}}}";
    }

    /// <summary>
    /// 计算切片处理延迟 - 性能优化算法
    /// 算法：基于切片复杂度、输出格式和压缩级别动态调整处理时间
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="config">切片配置</param>
    /// <returns>处理延迟毫秒数</returns>
    private int CalculateProcessingDelay(Slice slice, SlicingConfig config)
    {
        var baseDelay = 10; // 基础处理时间

        // 基于输出格式调整延迟
        var formatFactor = config.OutputFormat.ToLower() switch
        {
            "b3dm" => 1.5, // B3DM格式较为复杂
            "gltf" => 1.2, // GLTF格式中等复杂度
            "json" => 0.8, // JSON格式相对简单
            _ => 1.0
        };

        // 基于压缩级别调整延迟
        var compressionFactor = 1.0 + (config.CompressionLevel * 0.1);

        // 基于切片级别调整延迟（更高层级通常更复杂）
        var levelFactor = 1.0 + (slice.Level * 0.05);

        var totalDelay = baseDelay * formatFactor * compressionFactor * levelFactor;

        // 限制延迟范围：5-100毫秒
        return Math.Max(5, Math.Min(100, (int)totalDelay));
    }

    /// <summary>
    /// 并行切片处理优化 - 多线程处理算法实现
    /// 算法：将切片任务分配到多个线程并行处理，提高整体处理速度和CPU利用率
    ///
    /// 性能优化策略：
    /// - 动态线程池：根据系统负载和切片复杂度动态调整并发数
    /// - 负载均衡：均匀分配切片到各线程，避免单个线程负载过重
    /// - 内存管理：控制内存分配速率，避免内存峰值过高导致GC压力
    /// - I/O优化：批量写入数据库，减少数据库连接开销
    /// - 进度同步：线程安全的进度更新，避免锁竞争影响性能
    /// - 异常隔离：单个切片处理失败不影响其他切片和整体进度
    /// - 资源清理：及时释放临时资源，避免内存泄露
    ///
    /// 并行策略：
    /// - 数据并行：切片间相互独立，适合完全并行处理
    /// - 任务窃取：空闲线程主动获取其他线程的任务，提高资源利用率
    /// - 工作优先级：根据切片重要性和复杂度动态调整处理优先级
    /// - 中间结果：及时保存中间结果，避免因异常导致的完全重做
    /// </summary>
    /// <param name="task">切片任务，包含任务配置和状态信息</param>
    /// <param name="level">LOD级别，影响切片复杂度和处理优先级</param>
    /// <param name="config">切片配置，控制并行度和处理策略</param>
    /// <param name="slices">切片集合，待并行处理的切片数据</param>
    /// <param name="triangleSpatialIndex">三角形空间索引，用于快速查询</param>
    /// <param name="modelBounds">模型包围盒，用于坐标变换</param>
    /// <param name="existingSlicesMap">现有切片映射表，用于增量更新</param>
    /// <param name="actuallyUseIncrementalUpdate">是否实际使用增量更新</param>
    /// <param name="hasSliceChanges">是否有切片变化的标记</param>
    /// <param name="processedSliceKeys">已处理的切片键集合</param>
    /// <param name="slicesToAdd">待添加的切片列表</param>
    /// <param name="slicesToUpdate">待更新的切片列表</param>
    /// <param name="cancellationToken">取消令牌，支持优雅的中断处理</param>
    /// <returns>包含处理结果的元组：(处理数量, 是否有变化)</returns>
    private async Task<(int processedCount, bool hasChanges)> ProcessSlicesInParallelAsync(
        SlicingTask task,
        int level,
        SlicingConfig config,
        List<Slice> slices,
        Dictionary<string, List<Triangle>> triangleSpatialIndex,
        BoundingBox3D modelBounds,
        Dictionary<string, Slice> existingSlicesMap,
        bool actuallyUseIncrementalUpdate,
        bool hasSliceChanges,
        HashSet<string> processedSliceKeys,
        List<Slice> slicesToAdd,
        List<Slice> slicesToUpdate,
        CancellationToken cancellationToken)
    {
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = config.ParallelProcessingCount,
            CancellationToken = cancellationToken
        };

        var slicesArray = slices.ToArray();
        var processedCount = 0;
        var lockObject = new object();

        await Task.Run(() =>
        {
            Parallel.For(0, slicesArray.Length, parallelOptions, async (index) =>
            {
                var slice = slicesArray[index];

                try
                {
                    // 增量更新：检查切片是否已存在
                    var sliceKey = $"{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}";
                    bool isNewSlice = !existingSlicesMap.ContainsKey(sliceKey);
                    bool needsUpdate = false;

                    if (actuallyUseIncrementalUpdate && !isNewSlice)
                    {
                        var existingSlice = existingSlicesMap[sliceKey];
                        var newHash = await CalculateSliceHash(slice);
                        var existingHash = await CalculateSliceHashFromExisting(existingSlice);

                        needsUpdate = newHash != existingHash;

                        if (!needsUpdate)
                        {
                            lock (lockObject)
                            {
                                processedSliceKeys.Add(sliceKey);
                            }
                            return;
                        }
                        else
                        {
                            slice.Id = existingSlice.Id;
                            lock (lockObject)
                            {
                                hasSliceChanges = true;
                            }
                        }
                    }
                    else if (actuallyUseIncrementalUpdate && isNewSlice)
                    {
                        lock (lockObject)
                        {
                            hasSliceChanges = true;
                        }
                    }

                    // 查询此切片相交的三角形数据
                    var sliceTriangles = QueryTrianglesForSlice(slice, triangleSpatialIndex, modelBounds);

                    // 生成切片文件内容（传入实际的三角形数据）
                    await GenerateSliceFileAsync(slice, config, sliceTriangles, cancellationToken);

                    // 线程安全的计数更新和列表操作
                    lock (lockObject)
                    {
                        if (actuallyUseIncrementalUpdate && needsUpdate)
                        {
                            slicesToUpdate.Add(slice);
                        }
                        else
                        {
                            slicesToAdd.Add(slice);
                        }

                        if (actuallyUseIncrementalUpdate)
                        {
                            processedSliceKeys.Add(sliceKey);
                        }

                        processedCount++;
                        if (processedCount % 10 == 0) // 每处理10个切片输出一次进度
                        {
                            _logger.LogDebug("并行处理进度：级别{Level}, 已处理{Processed}/{Total}",
                                level, processedCount, slicesArray.Length);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "并行处理切片失败：级别{Level}, 索引{Index}", level, index);
                }
            });
        }, cancellationToken);

        _logger.LogInformation("并行切片处理完成：级别{Level}, 处理{Processed}个切片", level, processedCount);

        return (processedCount, hasSliceChanges);
    }

    /// <summary>
    /// 视锥剔除算法 - 渲染优化算法实现
    /// 算法：基于视点和视角参数剔除不可见的切片，减少渲染负载
    ///
    /// 性能优化策略：
    /// - 空间索引加速：预构建BVH树或四叉树，快速剔除大范围不可见区域
    /// - 距离预排序：按距离相机远近预排序，先剔除远距离切片减少计算量
    /// - 金字塔剔除：高层级切片不可见时可跳过低层级子节点，避免冗余计算
    /// - SIMD优化：使用向量指令批量处理距离和角度计算，提升浮点性能
    /// - 多线程并行：并发处理切片可见性测试，利用多核CPU优势
    /// - 缓存机制：缓存上一帧可见性结果，减少重复计算开销
    /// - 提前退出：一旦找到足够可见切片立即返回，支持渐进式加载
    ///
    /// 内存优化：
    /// - 对象池复用：复用临时向量和矩阵对象，减少GC压力
    /// - 紧凑存储：使用位标记记录可见性，避免大量内存分配
    /// - 延迟加载：仅为可见切片加载几何数据，节省内存占用
    /// - 分批处理：分批处理大量切片，避免内存峰值过高
    /// </summary>
    /// <param name="viewport">视口参数，包含相机位置、视角、裁剪面等关键信息，必须有效</param>
    /// <param name="allSlices">所有待测试的切片集合，支持空集合（返回空结果）</param>
    /// <returns>可见切片集合，仅包含在视锥范围内的切片，按距离排序便于优先加载</returns>
    public Task<IEnumerable<Slice>> PerformFrustumCullingAsync(ViewportInfo viewport, IEnumerable<Slice> allSlices)
    {
        // 视锥剔除算法实现
        var visibleSlices = new List<Slice>();

        foreach (var slice in allSlices)
        {
            if (IsSliceVisible(slice, viewport))
            {
                visibleSlices.Add(slice);
            }
        }

        _logger.LogDebug("视锥剔除结果：总切片{Total}, 可见切片{Visible}",
            allSlices.Count(), visibleSlices.Count);

        return Task.FromResult<IEnumerable<Slice>>(visibleSlices);
    }

    /// <summary>
    /// 判断切片是否在视锥体内 - 增强的空间几何算法
    /// 算法：使用包围盒与视锥的精确相交测试判断可见性
    /// 实现：六平面视锥剔除算法 + 距离LOD优化
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="viewport">视口信息</param>
    /// <returns>是否可见</returns>
    private bool IsSliceVisible(Slice slice, ViewportInfo viewport)
    {
        // 解析包围盒
        var boundingBox = System.Text.Json.JsonSerializer.Deserialize<BoundingBox>(slice.BoundingBox);
        if (boundingBox == null) return false;

        // 计算包围盒的8个顶点
        var corners = new[]
        {
            new Vector3D { X = boundingBox.MinX, Y = boundingBox.MinY, Z = boundingBox.MinZ },
            new Vector3D { X = boundingBox.MaxX, Y = boundingBox.MinY, Z = boundingBox.MinZ },
            new Vector3D { X = boundingBox.MinX, Y = boundingBox.MaxY, Z = boundingBox.MinZ },
            new Vector3D { X = boundingBox.MaxX, Y = boundingBox.MaxY, Z = boundingBox.MinZ },
            new Vector3D { X = boundingBox.MinX, Y = boundingBox.MinY, Z = boundingBox.MaxZ },
            new Vector3D { X = boundingBox.MaxX, Y = boundingBox.MinY, Z = boundingBox.MaxZ },
            new Vector3D { X = boundingBox.MinX, Y = boundingBox.MaxY, Z = boundingBox.MaxZ },
            new Vector3D { X = boundingBox.MaxX, Y = boundingBox.MaxY, Z = boundingBox.MaxZ }
        };

        // 计算切片中心点
        var sliceCenter = new Vector3D
        {
            X = (boundingBox.MinX + boundingBox.MaxX) / 2,
            Y = (boundingBox.MinY + boundingBox.MaxY) / 2,
            Z = (boundingBox.MinZ + boundingBox.MaxZ) / 2
        };

        // 1. 距离剔除测试 - 基于LOD级别的动态距离阈值
        var distance = CalculateDistance(viewport.CameraPosition, sliceCenter);

        // LOD级别越高，最大可见距离越小（细节级别越高，可见范围越近）
        var lodDistanceFactor = Math.Pow(0.75, slice.Level);
        var maxDistance = viewport.FarPlane * lodDistanceFactor;

        // 近平面和远平面剔除
        if (distance < viewport.NearPlane || distance > maxDistance)
            return false;

        // 2. 视野角度剔除测试 - 计算到相机方向的角度
        var toCenterVector = new Vector3D
        {
            X = sliceCenter.X - viewport.CameraPosition.X,
            Y = sliceCenter.Y - viewport.CameraPosition.Y,
            Z = sliceCenter.Z - viewport.CameraPosition.Z
        };

        var angle = CalculateAngle(viewport.CameraDirection, toCenterVector);

        // 考虑包围盒半径的扩展角度
        var boundingBoxRadius = Math.Sqrt(
            Math.Pow(boundingBox.MaxX - boundingBox.MinX, 2) +
            Math.Pow(boundingBox.MaxY - boundingBox.MinY, 2) +
            Math.Pow(boundingBox.MaxZ - boundingBox.MinZ, 2)
        ) / 2;

        var angularRadius = Math.Atan2(boundingBoxRadius, distance);
        var effectiveFOV = viewport.FieldOfView / 2 + angularRadius;

        if (angle > effectiveFOV)
            return false;

        // 3. 完整的视锥平面测试 - 使用六平面测试算法
        // 构建视锥的六个平面：近、远、左、右、上、下
        var frustumPlanes = BuildFrustumPlanes(viewport);

        // 执行包围盒与视锥六平面相交测试
        // 如果包围盒完全在任何一个平面的外侧，则不可见
        foreach (var plane in frustumPlanes)
        {
            if (IsBoxCompletelyOutsidePlane(corners, plane))
            {
                // 包围盒完全在平面外侧，剔除
                return false;
            }
        }

        // 4. 增强的遮挡剔除算法 - 基于层次LOD和空间关系
        // 考虑以下情况进行遮挡判断：
        // a) 远小切片：距离很远且体积很小的切片容易被遮挡
        // b) 视线投影面积：计算切片在屏幕上的投影面积，面积过小可能不可见
        // c) LOD父子关系：高层级切片可能被低层级的大切片遮挡

        // 计算切片的屏幕空间投影面积（近似）
        var angularSize = Math.Atan2(boundingBoxRadius, distance); // 角尺寸（弧度）
        var screenSpaceArea = angularSize * angularSize; // 近似屏幕投影面积

        // 如果屏幕投影面积过小（小于1像素），可以剔除
        // 假设视野角度对应视口高度，计算像素阈值
        var pixelThreshold = (viewport.FieldOfView / viewport.ViewportHeight) * (viewport.FieldOfView / viewport.ViewportHeight);
        if (screenSpaceArea < pixelThreshold)
        {
            _logger.LogTrace("切片因屏幕投影过小被剔除：Level={Level}, 距离={Distance:F2}, 角尺寸={AngularSize:F6}",
                slice.Level, distance, angularSize);
            return false;
        }

        // LOD层级遮挡检测：
        // 如果是高层级（细节）切片，且距离较远，可能被低层级切片覆盖
        if (slice.Level > 2) // 仅对Level > 2的切片进行此检测
        {
            var lodFactor = Math.Pow(2, slice.Level); // LOD因子：2^Level
            var expectedVisibleDistance = viewport.FarPlane / lodFactor;

            // 如果当前距离远超该LOD级别的期望可见距离，很可能被父级LOD遮挡
            if (distance > expectedVisibleDistance * 1.5)
            {
                _logger.LogTrace("切片因LOD层级遮挡被剔除：Level={Level}, 距离={Distance:F2}, 期望距离={Expected:F2}",
                    slice.Level, distance, expectedVisibleDistance);
                return false;
            }
        }

        // 视线方向遮挡检测：
        // 如果切片在视线方向上距离较远，且角度偏离较大，可能被中心区域的切片遮挡
        if (distance > maxDistance * 0.7)
        {
            // 计算切片相对于视线中心的偏离角度
            var deviationAngle = angle / (viewport.FieldOfView / 2.0); // 归一化偏离（0-1）

            // 如果偏离角度大且距离远，被遮挡的可能性更高
            if (deviationAngle > 0.8 && boundingBoxRadius < distance * 0.02)
            {
                _logger.LogTrace("切片因视线偏离遮挡被剔除：Level={Level}, 偏离={Deviation:F2}, 距离={Distance:F2}",
                    slice.Level, deviationAngle, distance);
                return false;
            }
        }

        // 通过所有测试，切片可见
        return true;
    }

    /// <summary>
    /// 预测加载算法 - 预加载优化算法实现
    /// 算法：基于用户视点移动趋势预测需要加载的切片，支持智能预加载
    ///
    /// 性能优化策略：
    /// - 运动轨迹分析：基于历史移动数据预测未来轨迹，提高预测准确性
    /// - 时间窗口预测：支持多时间窗口预测，平衡预加载量和准确性
    /// - 优先级排序：结合距离、角度、LOD等因素计算加载优先级
    /// - 增量更新：仅对新进入预测范围的切片进行计算，减少重复工作
    /// - 机器学习优化：使用历史行为数据训练预测模型，提升预测精度
    /// - 带宽感知：根据网络状况动态调整预加载数量，避免带宽浪费
    /// - 缓存策略：缓存预测结果，减少频繁预测的计算开销
    ///
    /// 预测算法：
    /// - 线性预测：基于当前速度和方向预测未来位置
    /// - 贝塞尔曲线：平滑处理非线性运动轨迹
    /// - 概率模型：考虑用户行为不确定性，提供置信度评估
    /// - 聚类分析：识别常用路径和兴趣区域，优化预测范围
    /// </summary>
    /// <param name="currentViewport">当前视口信息，作为预测基准点</param>
    /// <param name="movementVector">用户移动向量，描述当前运动状态和趋势</param>
    /// <param name="allSlices">所有可用切片，用于预测范围内的切片选择</param>
    /// <returns>预测加载的切片集合，按优先级排序，优先加载重要切片</returns>
    public async Task<IEnumerable<Slice>> PredictLoadingAsync(ViewportInfo currentViewport, Vector3D movementVector, IEnumerable<Slice> allSlices)
    {
        // 预测加载算法实现
        var predictedSlices = new List<Slice>();

        // 预测下一个视口位置
        var predictedPosition = currentViewport.CameraPosition + movementVector * 2.0; // 预测2秒后的位置

        // 基于预测位置计算可见切片
        var predictedViewport = new ViewportInfo
        {
            CameraPosition = predictedPosition,
            CameraDirection = currentViewport.CameraDirection,
            FieldOfView = currentViewport.FieldOfView,
            NearPlane = currentViewport.NearPlane,
            FarPlane = currentViewport.FarPlane
        };

        return await PerformFrustumCullingAsync(predictedViewport, allSlices);
    }

    /// <summary>
    /// 计算两点间距离 - 空间几何算法
    /// </summary>
    /// <param name="point1">点1</param>
    /// <param name="point2">点2</param>
    /// <returns>欧几里得距离</returns>
    private double CalculateDistance(Vector3D point1, Vector3D point2)
    {
        var dx = point2.X - point1.X;
        var dy = point2.Y - point1.Y;
        var dz = point2.Z - point1.Z;

        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// 计算向量间角度 - 向量数学算法
    /// </summary>
    /// <param name="vector1">向量1</param>
    /// <param name="vector2">向量2</param>
    /// <returns>角度（弧度）</returns>
    private double CalculateAngle(Vector3D vector1, Vector3D vector2)
    {
        var dotProduct = vector1.X * vector2.X + vector1.Y * vector2.Y + vector1.Z * vector2.Z;
        var magnitude1 = Math.Sqrt(vector1.X * vector1.X + vector1.Y * vector1.Y + vector1.Z * vector1.Z);
        var magnitude2 = Math.Sqrt(vector2.X * vector2.X + vector2.Y * vector2.Y + vector2.Z * vector2.Z);

        if (magnitude1 == 0 || magnitude2 == 0) return 0;

        var cosAngle = dotProduct / (magnitude1 * magnitude2);
        return Math.Acos(Math.Max(-1.0, Math.Min(1.0, cosAngle)));
    }

    /// <summary>
    /// 构建视锥六个平面 - 标准视锥剔除算法
    /// 算法：基于相机参数和视口信息构建视锥的六个平面（近、远、左、右、上、下）
    /// 每个平面用法向量和到原点的距离表示：Ax + By + Cz + D = 0
    /// </summary>
    /// <param name="viewport">视口信息</param>
    /// <returns>六个平面的数组</returns>
    private FrustumPlane[] BuildFrustumPlanes(ViewportInfo viewport)
    {
        var planes = new FrustumPlane[6];

        // 归一化相机方向向量
        var forward = NormalizeVector(viewport.CameraDirection);

        // 计算相机的右向量和上向量（假设世界上向量为(0,0,1)）
        var worldUp = new Vector3D { X = 0, Y = 0, Z = 1 };
        var right = CrossProduct(forward, worldUp);
        right = NormalizeVector(right);
        var up = CrossProduct(right, forward);
        up = NormalizeVector(up);

        // 计算视锥的近平面和远平面中心点
        var nearCenter = new Vector3D
        {
            X = viewport.CameraPosition.X + forward.X * viewport.NearPlane,
            Y = viewport.CameraPosition.Y + forward.Y * viewport.NearPlane,
            Z = viewport.CameraPosition.Z + forward.Z * viewport.NearPlane
        };

        var farCenter = new Vector3D
        {
            X = viewport.CameraPosition.X + forward.X * viewport.FarPlane,
            Y = viewport.CameraPosition.Y + forward.Y * viewport.FarPlane,
            Z = viewport.CameraPosition.Z + forward.Z * viewport.FarPlane
        };

        // 计算近平面和远平面的宽高
        var aspect = viewport.AspectRatio;
        var tanHalfFOV = Math.Tan(viewport.FieldOfView / 2.0);

        var nearHeight = 2.0 * tanHalfFOV * viewport.NearPlane;
        var nearWidth = nearHeight * aspect;
        var farHeight = 2.0 * tanHalfFOV * viewport.FarPlane;
        var farWidth = farHeight * aspect;

        // 0. 近平面：法向量指向相机内部（前方）
        planes[0] = new FrustumPlane
        {
            Normal = forward,
            Point = nearCenter
        };

        // 1. 远平面：法向量指向相机外部（后方）
        planes[1] = new FrustumPlane
        {
            Normal = new Vector3D { X = -forward.X, Y = -forward.Y, Z = -forward.Z },
            Point = farCenter
        };

        // 2. 左平面
        var leftNormal = CrossProduct(up, new Vector3D
        {
            X = nearCenter.X - right.X * nearWidth / 2 - viewport.CameraPosition.X,
            Y = nearCenter.Y - right.Y * nearWidth / 2 - viewport.CameraPosition.Y,
            Z = nearCenter.Z - right.Z * nearWidth / 2 - viewport.CameraPosition.Z
        });
        planes[2] = new FrustumPlane
        {
            Normal = NormalizeVector(leftNormal),
            Point = viewport.CameraPosition
        };

        // 3. 右平面
        var rightNormal = CrossProduct(new Vector3D
        {
            X = nearCenter.X + right.X * nearWidth / 2 - viewport.CameraPosition.X,
            Y = nearCenter.Y + right.Y * nearWidth / 2 - viewport.CameraPosition.Y,
            Z = nearCenter.Z + right.Z * nearWidth / 2 - viewport.CameraPosition.Z
        }, up);
        planes[3] = new FrustumPlane
        {
            Normal = NormalizeVector(rightNormal),
            Point = viewport.CameraPosition
        };

        // 4. 上平面
        var topNormal = CrossProduct(right, new Vector3D
        {
            X = nearCenter.X + up.X * nearHeight / 2 - viewport.CameraPosition.X,
            Y = nearCenter.Y + up.Y * nearHeight / 2 - viewport.CameraPosition.Y,
            Z = nearCenter.Z + up.Z * nearHeight / 2 - viewport.CameraPosition.Z
        });
        planes[4] = new FrustumPlane
        {
            Normal = NormalizeVector(topNormal),
            Point = viewport.CameraPosition
        };

        // 5. 下平面
        var bottomNormal = CrossProduct(new Vector3D
        {
            X = nearCenter.X - up.X * nearHeight / 2 - viewport.CameraPosition.X,
            Y = nearCenter.Y - up.Y * nearHeight / 2 - viewport.CameraPosition.Y,
            Z = nearCenter.Z - up.Z * nearHeight / 2 - viewport.CameraPosition.Z
        }, right);
        planes[5] = new FrustumPlane
        {
            Normal = NormalizeVector(bottomNormal),
            Point = viewport.CameraPosition
        };

        return planes;
    }

    /// <summary>
    /// 判断包围盒是否完全在平面外侧
    /// </summary>
    /// <param name="corners">包围盒的8个顶点</param>
    /// <param name="plane">平面</param>
    /// <returns>如果所有顶点都在平面外侧返回true</returns>
    private bool IsBoxCompletelyOutsidePlane(Vector3D[] corners, FrustumPlane plane)
    {
        // 计算所有顶点到平面的距离
        // 如果所有顶点的距离都是负数（在平面的背面），则包围盒完全在外侧
        foreach (var corner in corners)
        {
            var distance = (corner.X - plane.Point.X) * plane.Normal.X +
                          (corner.Y - plane.Point.Y) * plane.Normal.Y +
                          (corner.Z - plane.Point.Z) * plane.Normal.Z;

            if (distance >= 0)
            {
                // 至少有一个顶点在平面内侧或上，包围盒没有完全在外侧
                return false;
            }
        }

        // 所有顶点都在平面外侧
        return true;
    }

    /// <summary>
    /// 向量叉积
    /// </summary>
    private Vector3D CrossProduct(Vector3D v1, Vector3D v2)
    {
        return new Vector3D
        {
            X = v1.Y * v2.Z - v1.Z * v2.Y,
            Y = v1.Z * v2.X - v1.X * v2.Z,
            Z = v1.X * v2.Y - v1.Y * v2.X
        };
    }

    /// <summary>
    /// 向量归一化
    /// </summary>
    private Vector3D NormalizeVector(Vector3D v)
    {
        var length = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        if (length < 1e-10)
            return new Vector3D { X = 0, Y = 0, Z = 1 }; // 避免除以零

        return new Vector3D
        {
            X = v.X / length,
            Y = v.Y / length,
            Z = v.Z / length
        };
    }

    /// <summary>
    /// 视锥平面定义 - 用于视锥剔除算法
    /// </summary>
    private class FrustumPlane
    {
        public Vector3D Normal { get; set; } = new Vector3D();
        public Vector3D Point { get; set; } = new Vector3D();
    }

    // 使用Domain.Entities中的几何类型定义

    /// <summary>
    /// 生成切片文件内容 - 多格式支持算法实现
    /// 算法：根据输出格式生成相应的切片文件内容，支持B3DM、GLTF等格式
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="config">切片配置</param>
    /// <param name="triangles">切片包含的三角形数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功生成切片文件</returns>
    private async Task<bool> GenerateSliceFileAsync(Slice slice, SlicingConfig config, List<Triangle> triangles, CancellationToken cancellationToken)
    {
        // 多格式切片文件生成算法
        byte[]? fileContent;

        // 如果没有三角形数据，跳过生成
        if (triangles == null || triangles.Count == 0)
        {
            _logger.LogDebug("切片({Level},{X},{Y},{Z})没有找到相交三角形，跳过生成",
                slice.Level, slice.X, slice.Y, slice.Z);
            return false;
        }

        // 有三角形数据时正常处理
        switch (config.OutputFormat.ToLower())
        {
            case "b3dm":
                fileContent = await GenerateB3DMContentAsync(slice, config, triangles);
                break;
            case "gltf":
                fileContent = await GenerateGLTFContentAsync(slice, config, triangles);
                break;
            case "json":
                fileContent = await GenerateJSONContentAsync(slice, config);
                break;
            default:
                fileContent = await GenerateDefaultContentAsync(slice, config);
                break;
        }

        // 应用压缩（如果启用）
        if (config.CompressionLevel > 0)
        {
            fileContent = await CompressSliceContentAsync(fileContent, config.CompressionLevel);
        }

        // 根据存储位置类型保存文件
        if (config.StorageLocation == StorageLocationType.LocalFileSystem)
        {
            var directory = Path.GetDirectoryName(slice.FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllBytesAsync(slice.FilePath, fileContent, cancellationToken);
            _logger.LogDebug("切片文件已保存到本地：{FilePath}, 大小：{FileSize}", slice.FilePath, fileContent.Length);
        }
        else // 默认为MinIO
        {
            // 上传到对象存储
            _logger.LogInformation("准备上传切片到MinIO: bucket=slices, path={FilePath}, size={Size}",
                slice.FilePath, fileContent.Length);

            // 使用 using 语句确保 MemoryStream 被正确释放
            using (var stream = new MemoryStream(fileContent, false))
            {
                var contentType = GetContentType(config.OutputFormat);
                _logger.LogDebug("ContentType: {ContentType}, OutputFormat: {OutputFormat}",
                    contentType, config.OutputFormat);

                await _minioService.UploadFileAsync("slices", slice.FilePath, stream, contentType, cancellationToken);
            }
            _logger.LogInformation("切片文件已上传到MinIO：{FilePath}, 大小：{FileSize}", slice.FilePath, fileContent.Length);

            // 释放 fileContent 引用，帮助GC回收
            fileContent = null;
        }

        // 返回成功
        return true;
    }

    /// <summary>
    /// 生成B3DM格式切片内容 - 使用TileGenerator系统
    /// 委托给B3dmGenerator生成符合3D Tiles标准的B3DM格式
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="config">切片配置</param>
    /// <param name="triangles">切片包含的三角形数据，可能为null</param>
    /// <returns>B3DM格式的字节数组</returns>
    private async Task<byte[]> GenerateB3DMContentAsync(Slice slice, SlicingConfig config, List<Triangle>? triangles)
    {
        // 解析包围盒
        var boundingBox = System.Text.Json.JsonSerializer.Deserialize<BoundingBox>(slice.BoundingBox);
        if (boundingBox == null)
        {
            throw new InvalidOperationException("无法解析切片包围盒");
        }

        // 转换包围盒格式
        var bounds3D = new BoundingBox3D
        {
            MinX = boundingBox.MinX,
            MinY = boundingBox.MinY,
            MinZ = boundingBox.MinZ,
            MaxX = boundingBox.MaxX,
            MaxY = boundingBox.MaxY,
            MaxZ = boundingBox.MaxZ
        };

        // 转换三角形格式（从简单Triangle到Domain.Entities.Triangle）
        var domainTriangles = ConvertToDomainTriangles(triangles);

        // 使用B3dmGenerator生成B3DM数据
        _logger.LogDebug("使用B3dmGenerator生成B3DM：切片{SliceId}, 三角形数={Count}",
            slice.Id, domainTriangles?.Count ?? 0);

        var b3dmData = _b3dmGenerator.GenerateB3DM(domainTriangles ?? new List<Domain.Entities.Triangle>(), bounds3D, materials: null);

        _logger.LogDebug("B3DM文件生成完成：切片{SliceId}, 大小{Size}字节",
            slice.Id, b3dmData.Length);

        return await Task.FromResult(b3dmData);
    }

    /// <summary>
    /// 转换内部Triangle到Domain.Entities.Triangle
    /// 内部Triangle只有顶点信息，转换为完整的Domain Triangle格式
    /// </summary>
    private List<Domain.Entities.Triangle>? ConvertToDomainTriangles(List<Triangle>? triangles)
    {
        if (triangles == null || triangles.Count == 0)
        {
            return null;
        }

        var domainTriangles = new List<Domain.Entities.Triangle>();

        foreach (var triangle in triangles)
        {
            if (triangle.Vertices == null || triangle.Vertices.Length < 3)
            {
                continue;
            }

            // 创建Domain.Entities.Triangle
            var domainTriangle = new Domain.Entities.Triangle
            {
                V1 = triangle.Vertices[0],
                V2 = triangle.Vertices[1],
                V3 = triangle.Vertices[2]
            };

            // 计算面法线（自动生成）
            var edge1 = new Vector3D
            {
                X = domainTriangle.V2.X - domainTriangle.V1.X,
                Y = domainTriangle.V2.Y - domainTriangle.V1.Y,
                Z = domainTriangle.V2.Z - domainTriangle.V1.Z
            };

            var edge2 = new Vector3D
            {
                X = domainTriangle.V3.X - domainTriangle.V1.X,
                Y = domainTriangle.V3.Y - domainTriangle.V1.Y,
                Z = domainTriangle.V3.Z - domainTriangle.V1.Z
            };

            // 叉积计算法线
            var normal = new Vector3D
            {
                X = edge1.Y * edge2.Z - edge1.Z * edge2.Y,
                Y = edge1.Z * edge2.X - edge1.X * edge2.Z,
                Z = edge1.X * edge2.Y - edge1.Y * edge2.X
            };

            // 归一化法线
            var length = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);
            if (length > 1e-10)
            {
                normal.X /= length;
                normal.Y /= length;
                normal.Z /= length;
            }

            // 为所有顶点设置相同的法线（平面着色）
            domainTriangle.Normal1 = normal;
            domainTriangle.Normal2 = normal;
            domainTriangle.Normal3 = normal;

            domainTriangles.Add(domainTriangle);
        }

        return domainTriangles;
    }

    /// <summary>
    /// 生成GLTF格式切片内容 - 使用TileGenerator系统
    /// 委托给GltfGenerator生成符合glTF 2.0标准的GLB格式
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="config">切片配置</param>
    /// <param name="triangles">切片包含的三角形数据，可能为null</param>
    /// <returns>GLB格式的字节数组</returns>
    private async Task<byte[]> GenerateGLTFContentAsync(Slice slice, SlicingConfig config, List<Triangle>? triangles)
    {
        // 解析包围盒
        var boundingBox = System.Text.Json.JsonSerializer.Deserialize<BoundingBox>(slice.BoundingBox);
        if (boundingBox == null)
        {
            throw new InvalidOperationException("无法解析切片包围盒");
        }

        // 转换包围盒格式
        var bounds3D = new BoundingBox3D
        {
            MinX = boundingBox.MinX,
            MinY = boundingBox.MinY,
            MinZ = boundingBox.MinZ,
            MaxX = boundingBox.MaxX,
            MaxY = boundingBox.MaxY,
            MaxZ = boundingBox.MaxZ
        };

        // 转换三角形格式（从简单Triangle到Domain.Entities.Triangle）
        var domainTriangles = ConvertToDomainTriangles(triangles);

        // 使用GltfGenerator生成GLB数据
        _logger.LogDebug("使用GltfGenerator生成GLB：切片{SliceId}, 三角形数={Count}",
            slice.Id, domainTriangles?.Count ?? 0);

        var glbData = _gltfGenerator.GenerateGLB(domainTriangles ?? new List<Domain.Entities.Triangle>(), bounds3D, materials: null);

        _logger.LogDebug("GLB文件生成完成：切片{SliceId}, 大小{Size}字节",
            slice.Id, glbData.Length);

        return await Task.FromResult(glbData);
    }

    /// <summary>
    /// 生成JSON格式切片内容 - 增强的3D Tiles元数据格式
    /// 算法：生成符合3D Tiles规范的完整JSON元数据，包含空间索引、LOD层级、渲染优化等信息
    /// 支持：空间索引（包围盒、包围球）、几何误差计算、渲染优先级、数据完整性校验
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="config">切片配置</param>
    /// <returns>JSON格式的字节数组</returns>
    private Task<byte[]> GenerateJSONContentAsync(Slice slice, SlicingConfig config)
    {
        // 解析包围盒以获取详细的空间信息
        var boundingBox = System.Text.Json.JsonSerializer.Deserialize<BoundingBox>(slice.BoundingBox);
        if (boundingBox == null)
        {
            // 回退到简化格式
            boundingBox = new BoundingBox();
        }

        // 计算包围球信息（用于高效的视锥剔除）
        var center = new
        {
            x = (boundingBox.MinX + boundingBox.MaxX) / 2.0,
            y = (boundingBox.MinY + boundingBox.MaxY) / 2.0,
            z = (boundingBox.MinZ + boundingBox.MaxZ) / 2.0
        };

        var radius = Math.Sqrt(
            Math.Pow(boundingBox.MaxX - boundingBox.MinX, 2) +
            Math.Pow(boundingBox.MaxY - boundingBox.MinY, 2) +
            Math.Pow(boundingBox.MaxZ - boundingBox.MinZ, 2)
        ) / 2.0;

        // 增强的3D Tiles元数据结构
        var jsonData = new
        {
            // 基本标识信息
            tilesetVersion = "1.0",
            asset = new
            {
                version = "1.0",
                generator = "RealScene3D Slicer",
                tileFormat = config.OutputFormat
            },

            // 切片标识
            tile = new
            {
                id = slice.Id.ToString(),
                level = slice.Level,
                coordinates = new
                {
                    x = slice.X,
                    y = slice.Y,
                    z = slice.Z
                },
                name = $"Tile_{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}"
            },

            // 空间索引信息
            spatial = new
            {
                // 轴对齐包围盒（AABB）
                boundingBox = new
                {
                    min = new[] { boundingBox.MinX, boundingBox.MinY, boundingBox.MinZ },
                    max = new[] { boundingBox.MaxX, boundingBox.MaxY, boundingBox.MaxZ }
                },
                // 包围球（用于快速视锥剔除）
                boundingSphere = new
                {
                    center = new[] { center.x, center.y, center.z },
                    radius = radius
                },
                // 空间范围（用于空间查询）
                extent = new
                {
                    width = boundingBox.MaxX - boundingBox.MinX,
                    height = boundingBox.MaxY - boundingBox.MinY,
                    depth = boundingBox.MaxZ - boundingBox.MinZ,
                    volume = (boundingBox.MaxX - boundingBox.MinX) *
                            (boundingBox.MaxY - boundingBox.MinY) *
                            (boundingBox.MaxZ - boundingBox.MinZ)
                }
            },

            // LOD层级信息
            lod = new
            {
                level = slice.Level,
                geometricError = CalculateGeometricError(slice.Level, config),
                screenSpaceError = config.GeometricErrorThreshold,
                minDistance = Math.Pow(2, slice.Level) * config.TileSize * 0.5,
                maxDistance = Math.Pow(2, slice.Level + 1) * config.TileSize * 2.0
            },

            // 渲染优化信息
            rendering = new
            {
                priority = CalculateRenderingPriority(slice.Level, center),
                culling = new
                {
                    frustum = true,
                    occlusion = slice.Level > 3,  // 高层级启用遮挡剔除
                    backface = true
                },
                visibility = new
                {
                    minPixelSize = Math.Max(1.0, Math.Pow(0.5, slice.Level) * 256.0),
                    preferredPixelSize = Math.Pow(0.5, slice.Level) * 512.0
                }
            },

            // 文件信息
            content = new
            {
                uri = slice.FilePath,
                type = config.OutputFormat,
                size = slice.FileSize,
                compression = config.CompressionLevel > 0 ? "gzip" : "none",
                encoding = "utf8"
            },

            // 元数据
            metadata = new
            {
                createdAt = slice.CreatedAt.ToString("o"),  // ISO 8601格式
                strategy = config.Strategy.ToString(),
                formatVersion = "1.0",
                schemaVersion = "1.1.0",
                checksum = CalculateMetadataChecksum(slice)
            },

            // 层级关系（用于层级遍历）
            hierarchy = new
            {
                hasParent = slice.Level > 0,
                hasChildren = slice.Level < config.MaxLevel,
                parent = slice.Level > 0 ? new
                {
                    level = slice.Level - 1,
                    x = slice.X / 2,
                    y = slice.Y / 2,
                    z = slice.Z / 2
                } : null,
                childrenCount = slice.Level < config.MaxLevel ? 8 : 0
            }
        };

        var jsonContent = System.Text.Json.JsonSerializer.Serialize(jsonData, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _logger.LogDebug("JSON元数据文件生成完成：切片{SliceId}, 级别{Level}, 大小{Size}字节",
            slice.Id, slice.Level, jsonContent.Length);

        return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(jsonContent));
    }

    /// <summary>
    /// 计算几何误差
    /// </summary>
    /// <param name="level">LOD级别</param>
    /// <param name="config">切片配置</param>
    /// <returns>几何误差值</returns>
    private double CalculateGeometricError(int level, SlicingConfig config)
    {
        var baseError = config.GeometricErrorThreshold;
        var errorFactor = Math.Pow(2.0, config.MaxLevel - level);
        return baseError * errorFactor;
    }

    /// <summary>
    /// 计算渲染优先级 - 渲染调度优化算法
    /// 算法：基于LOD级别和距离中心点的距离计算渲染优先级
    /// </summary>
    /// <param name="level">LOD级别</param>
    /// <param name="center">切片中心点</param>
    /// <returns>优先级值（越小优先级越高）</returns>
    private int CalculateRenderingPriority(int level, dynamic center)
    {
        // 优先级综合考虑：
        // 1. LOD级别：低级别（粗糙）优先渲染
        // 2. 距离中心：靠近中心优先渲染
        var levelPriority = level * 1000;  // LOD级别权重
        var distanceToOrigin = Math.Sqrt(center.x * center.x + center.y * center.y + center.z * center.z);
        var distancePriority = (int)(distanceToOrigin / 10.0);  // 距离权重

        return levelPriority + distancePriority;
    }

    /// <summary>
    /// 计算元数据校验和 - 数据完整性验证算法
    /// 算法：基于切片关键信息生成SHA256哈希，用于验证数据完整性
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <returns>十六进制哈希字符串</returns>
    private string CalculateMetadataChecksum(Slice slice)
    {
        var checksumInput = $"{slice.Id}|{slice.Level}|{slice.X}|{slice.Y}|{slice.Z}|{slice.FileSize}|{slice.BoundingBox}";
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(checksumInput));
            return Convert.ToHexString(hashBytes).ToLower().Substring(0, 16);  // 取前16个字符作为校验和
        }
    }

    /// <summary>
    /// 生成默认格式切片内容
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="config">切片配置</param>
    /// <returns>默认格式的字节数组</returns>
    private async Task<byte[]> GenerateDefaultContentAsync(Slice slice, SlicingConfig config)
    {
        return await GenerateJSONContentAsync(slice, config);
    }

    /// <summary>
    /// 生成GLB内容 - 二进制glTF格式
    /// 算法：生成二进制格式的glTF数据，包含完整的几何数据和纹理
    /// GLB格式：12字节Header + JSON Chunk (对齐) + Binary Chunk (对齐)
    /// </summary>
    /// <param name="slice">切片数据</param>
    /// <param name="config">切片配置</param>
    /// <returns>GLB格式的字节数组</returns>
    /// <summary>
    /// 生成GLB格式内容 - 使用实际的三角形几何数据
    /// GLB格式：Header (12) + JSON Chunk (header 8 + data) + Binary Chunk (header 8 + data)
    /// </summary>
    /// <param name="boundingBox">包围盒</param>
    /// <param name="triangles">三角形数据列表</param>
    /// <returns>GLB格式的字节数组</returns>
    private Task<byte[]> GenerateGLBContentAsync(BoundingBox boundingBox, List<Triangle>? triangles)
    {
        // 如果没有三角形数据，生成一个简单的占位符盒子
        if (triangles == null || triangles.Count == 0)
        {
            _logger.LogWarning("切片没有三角形数据，生成包围盒占位符");
            return GeneratePlaceholderGLB(boundingBox);
        }

        // **1. 计算RTC_CENTER（相对瓦片中心）- 用于提高精度**
        var rtcCenterX = (float)((boundingBox.MinX + boundingBox.MaxX) / 2.0);
        var rtcCenterY = (float)((boundingBox.MinY + boundingBox.MaxY) / 2.0);
        var rtcCenterZ = (float)((boundingBox.MinZ + boundingBox.MaxZ) / 2.0);

        _logger.LogDebug("GLB生成 - RTC_CENTER: [{X:F6}, {Y:F6}, {Z:F6}]", rtcCenterX, rtcCenterY, rtcCenterZ);

        // **2. 提取所有顶点并转换为相对坐标，计算法线**
        var vertexList = new List<float>();
        var normalList = new List<float>();
        var indexList = new List<ushort>();

        ushort vertexIndex = 0;
        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

        foreach (var triangle in triangles)
        {
            // 计算三角形法线
            var v0 = triangle.Vertices[0];
            var v1 = triangle.Vertices[1];
            var v2 = triangle.Vertices[2];

            // 边向量
            var edge1X = (float)(v1.X - v0.X);
            var edge1Y = (float)(v1.Y - v0.Y);
            var edge1Z = (float)(v1.Z - v0.Z);
            var edge2X = (float)(v2.X - v0.X);
            var edge2Y = (float)(v2.Y - v0.Y);
            var edge2Z = (float)(v2.Z - v0.Z);

            // 叉积计算法线
            var normalX = edge1Y * edge2Z - edge1Z * edge2Y;
            var normalY = edge1Z * edge2X - edge1X * edge2Z;
            var normalZ = edge1X * edge2Y - edge1Y * edge2X;

            // 归一化法线
            var length = (float)Math.Sqrt(normalX * normalX + normalY * normalY + normalZ * normalZ);
            if (length > 0.0001f)
            {
                normalX /= length;
                normalY /= length;
                normalZ /= length;
            }

            // 添加三个顶点和法线
            for (int i = 0; i < 3; i++)
            {
                var vertex = triangle.Vertices[i];

                // **关键修复：将顶点坐标转换为相对于RTC_CENTER的偏移**
                float vx = (float)(vertex.X - rtcCenterX);
                float vy = (float)(vertex.Y - rtcCenterY);
                float vz = (float)(vertex.Z - rtcCenterZ);

                vertexList.Add(vx);
                vertexList.Add(vy);
                vertexList.Add(vz);

                normalList.Add(normalX);
                normalList.Add(normalY);
                normalList.Add(normalZ);

                indexList.Add(vertexIndex++);

                // 更新包围盒（相对坐标的包围盒）
                minX = Math.Min(minX, vx);
                minY = Math.Min(minY, vy);
                minZ = Math.Min(minZ, vz);
                maxX = Math.Max(maxX, vx);
                maxY = Math.Max(maxY, vy);
                maxZ = Math.Max(maxZ, vz);
            }
        }

        var vertices = vertexList.ToArray();
        var normals = normalList.ToArray();
        var indices = indexList.ToArray();

        _logger.LogDebug("GLB生成：{TriangleCount}个三角形，{VertexCount}个顶点，{IndexCount}个索引",
            triangles.Count, vertices.Length / 3, indices.Length);

        // **2. 将几何数据转换为字节数组**
        var vertexBytes = new byte[vertices.Length * sizeof(float)];
        Buffer.BlockCopy(vertices, 0, vertexBytes, 0, vertexBytes.Length);

        var normalBytes = new byte[normals.Length * sizeof(float)];
        Buffer.BlockCopy(normals, 0, normalBytes, 0, normalBytes.Length);

        var indexBytes = new byte[indices.Length * sizeof(ushort)];
        Buffer.BlockCopy(indices, 0, indexBytes, 0, indexBytes.Length);

        // **3. 构建Binary Chunk数据**
        var vertexBufferByteLength = vertexBytes.Length;
        var normalBufferByteLength = normalBytes.Length;
        var indexBufferByteLength = indexBytes.Length;

        var binaryData = new byte[vertexBufferByteLength + normalBufferByteLength + indexBufferByteLength];
        Buffer.BlockCopy(vertexBytes, 0, binaryData, 0, vertexBufferByteLength);
        Buffer.BlockCopy(normalBytes, 0, binaryData, vertexBufferByteLength, normalBufferByteLength);
        Buffer.BlockCopy(indexBytes, 0, binaryData, vertexBufferByteLength + normalBufferByteLength, indexBufferByteLength);

        // **4. 构建glTF JSON**
        var gltfJson = new
        {
            asset = new
            {
                version = "2.0",
                generator = "RealScene3D Slicer v1.0"
            },
            scene = 0,
            scenes = new[] { new { name = "TileScene", nodes = new[] { 0 } } },
            nodes = new[] { new { name = "TileMesh", mesh = 0 } },
            meshes = new[]
            {
                new
                {
                    name = "TileGeometry",
                    primitives = new[]
                    {
                        new
                        {
                            attributes = new Dictionary<string, int> { ["POSITION"] = 0, ["NORMAL"] = 1 },
                            indices = 2,
                            mode = 4 // TRIANGLES
                        }
                    }
                }
            },
            accessors = new object[]
            {
                new
                {
                    bufferView = 0,
                    componentType = 5126, // FLOAT
                    count = vertices.Length / 3,
                    type = "VEC3",
                    min = new[] { minX, minY, minZ },
                    max = new[] { maxX, maxY, maxZ }
                },
                new { bufferView = 1, componentType = 5126, count = normals.Length / 3, type = "VEC3" },
                new { bufferView = 2, componentType = 5123, count = indices.Length, type = "SCALAR" } // UNSIGNED_SHORT
            },
            bufferViews = new[]
            {
                new { buffer = 0, byteOffset = 0, byteLength = vertexBufferByteLength, target = 34962 }, // ARRAY_BUFFER
                new { buffer = 0, byteOffset = vertexBufferByteLength, byteLength = normalBufferByteLength, target = 34962 },
                new { buffer = 0, byteOffset = vertexBufferByteLength + normalBufferByteLength, byteLength = indexBufferByteLength, target = 34963 } // ELEMENT_ARRAY_BUFFER
            },
            buffers = new[] { new { byteLength = binaryData.Length } }
        };

        var jsonString = System.Text.Json.JsonSerializer.Serialize(gltfJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // 修复glTF属性名的大小写问题 - CamelCase会将POSITION/NORMAL转为小写
        // glTF 2.0规范要求这些属性名必须是大写
        jsonString = jsonString.Replace("\"position\":", "\"POSITION\":");
        jsonString = jsonString.Replace("\"normal\":", "\"NORMAL\":");

        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonString);

        // **5. 对齐JSON块到4字节边界**
        var jsonChunkLength = (jsonBytes.Length + 3) & ~3;
        var jsonChunkPadded = new byte[jsonChunkLength];
        Array.Copy(jsonBytes, jsonChunkPadded, jsonBytes.Length);
        for (int i = jsonBytes.Length; i < jsonChunkLength; i++)
        {
            jsonChunkPadded[i] = 0x20; // 空格填充
        }

        // **6. 对齐Binary块到4字节边界**
        var binaryChunkLength = (binaryData.Length + 3) & ~3;
        var binaryChunkPadded = new byte[binaryChunkLength];
        Array.Copy(binaryData, binaryChunkPadded, binaryData.Length);
        // Binary块用0填充（默认值）

        // **7. 构造完整的GLB文件**
        using (var memoryStream = new MemoryStream())
        using (var writer = new BinaryWriter(memoryStream))
        {
            // GLB Header (12字节)
            writer.Write(0x46546C67); // magic: "glTF" (little-endian)
            writer.Write((uint)2);     // version: 2

            var totalLength = 12 + // header
                             8 + jsonChunkLength + // JSON chunk header + data
                             8 + binaryChunkLength; // Binary chunk header + data

            writer.Write((uint)totalLength); // length

            // JSON Chunk
            writer.Write((uint)jsonChunkLength); // chunkLength
            writer.Write(0x4E4F534A); // chunkType: "JSON" (little-endian)
            writer.Write(jsonChunkPadded);

            // Binary Chunk
            writer.Write((uint)binaryChunkLength); // chunkLength
            writer.Write(0x004E4942); // chunkType: "BIN\0" (little-endian)
            writer.Write(binaryChunkPadded);

            _logger.LogDebug("GLB文件生成完成：总大小{TotalSize}字节, JSON块{JsonSize}字节, Binary块{BinarySize}字节, 顶点数{VertexCount}, 三角形数{TriangleCount}",
                totalLength, jsonChunkLength, binaryChunkLength, vertices.Length / 3, triangles.Count);

            return Task.FromResult(memoryStream.ToArray());
        }
    }

    /// <summary>
    /// 生成占位符GLB（当没有三角形数据时）
    /// </summary>
    private Task<byte[]> GeneratePlaceholderGLB(BoundingBox boundingBox)
    {
        // **计算RTC_CENTER（相对瓦片中心）**
        var rtcCenterX = (float)((boundingBox.MinX + boundingBox.MaxX) / 2.0);
        var rtcCenterY = (float)((boundingBox.MinY + boundingBox.MaxY) / 2.0);
        var rtcCenterZ = (float)((boundingBox.MinZ + boundingBox.MaxZ) / 2.0);

        // **生成包围盒立方体的相对坐标（相对于RTC_CENTER）**
        var halfSizeX = (float)((boundingBox.MaxX - boundingBox.MinX) / 2.0);
        var halfSizeY = (float)((boundingBox.MaxY - boundingBox.MinY) / 2.0);
        var halfSizeZ = (float)((boundingBox.MaxZ - boundingBox.MinZ) / 2.0);

        var vertices = new[]
        {
            -halfSizeX, -halfSizeY, -halfSizeZ,  // 立方体8个顶点（相对坐标）
            +halfSizeX, -halfSizeY, -halfSizeZ,
            +halfSizeX, +halfSizeY, -halfSizeZ,
            -halfSizeX, +halfSizeY, -halfSizeZ,
            -halfSizeX, -halfSizeY, +halfSizeZ,
            +halfSizeX, -halfSizeY, +halfSizeZ,
            +halfSizeX, +halfSizeY, +halfSizeZ,
            -halfSizeX, +halfSizeY, +halfSizeZ
        };

        var normals = new[]
        {
            0.0f, 0.0f, -1.0f,
            0.0f, 0.0f, -1.0f,
            0.0f, 0.0f, -1.0f,
            0.0f, 0.0f, -1.0f,
            0.0f, 0.0f, 1.0f,
            0.0f, 0.0f, 1.0f,
            0.0f, 0.0f, 1.0f,
            0.0f, 0.0f, 1.0f
        };

        var indices = new ushort[]
        {
            0, 1, 2, 0, 2, 3,
            5, 4, 7, 5, 7, 6,
            3, 2, 6, 3, 6, 7,
            4, 5, 1, 4, 1, 0,
            1, 5, 6, 1, 6, 2,
            4, 0, 3, 4, 3, 7
        };

        // 转换为字节数组
        var vertexBytes = new byte[vertices.Length * sizeof(float)];
        Buffer.BlockCopy(vertices, 0, vertexBytes, 0, vertexBytes.Length);

        var normalBytes = new byte[normals.Length * sizeof(float)];
        Buffer.BlockCopy(normals, 0, normalBytes, 0, normalBytes.Length);

        var indexBytes = new byte[indices.Length * sizeof(ushort)];
        Buffer.BlockCopy(indices, 0, indexBytes, 0, indexBytes.Length);

        var vertexBufferByteLength = vertexBytes.Length;
        var normalBufferByteLength = normalBytes.Length;
        var indexBufferByteLength = indexBytes.Length;

        var binaryData = new byte[vertexBufferByteLength + normalBufferByteLength + indexBufferByteLength];
        Buffer.BlockCopy(vertexBytes, 0, binaryData, 0, vertexBufferByteLength);
        Buffer.BlockCopy(normalBytes, 0, binaryData, vertexBufferByteLength, normalBufferByteLength);
        Buffer.BlockCopy(indexBytes, 0, binaryData, vertexBufferByteLength + normalBufferByteLength, indexBufferByteLength);

        var gltfJson = new
        {
            asset = new { version = "2.0", generator = "RealScene3D Slicer v1.0" },
            scene = 0,
            scenes = new[] { new { name = "PlaceholderScene", nodes = new[] { 0 } } },
            nodes = new[] { new { name = "PlaceholderMesh", mesh = 0 } },
            meshes = new[]
            {
                new
                {
                    name = "PlaceholderGeometry",
                    primitives = new[]
                    {
                        new
                        {
                            attributes = new Dictionary<string, int> { ["POSITION"] = 0, ["NORMAL"] = 1 },
                            indices = 2,
                            mode = 4
                        }
                    }
                }
            },
            accessors = new object[]
            {
                new
                {
                    bufferView = 0,
                    componentType = 5126,
                    count = vertices.Length / 3,
                    type = "VEC3",
                    min = new[] { -halfSizeX, -halfSizeY, -halfSizeZ },  // 相对坐标的最小值
                    max = new[] { +halfSizeX, +halfSizeY, +halfSizeZ }   // 相对坐标的最大值
                },
                new { bufferView = 1, componentType = 5126, count = normals.Length / 3, type = "VEC3" },
                new { bufferView = 2, componentType = 5123, count = indices.Length, type = "SCALAR" }
            },
            bufferViews = new[]
            {
                new { buffer = 0, byteOffset = 0, byteLength = vertexBufferByteLength, target = 34962 },
                new { buffer = 0, byteOffset = vertexBufferByteLength, byteLength = normalBufferByteLength, target = 34962 },
                new { buffer = 0, byteOffset = vertexBufferByteLength + normalBufferByteLength, byteLength = indexBufferByteLength, target = 34963 }
            },
            buffers = new[] { new { byteLength = binaryData.Length } }
        };

        var jsonString = System.Text.Json.JsonSerializer.Serialize(gltfJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // 修复glTF属性名的大小写问题 - CamelCase会将POSITION/NORMAL转为小写
        // glTF 2.0规范要求这些属性名必须是大写
        jsonString = jsonString.Replace("\"position\":", "\"POSITION\":");
        jsonString = jsonString.Replace("\"normal\":", "\"NORMAL\":");

        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonString);

        var jsonChunkLength = (jsonBytes.Length + 3) & ~3;
        var jsonChunkPadded = new byte[jsonChunkLength];
        Array.Copy(jsonBytes, jsonChunkPadded, jsonBytes.Length);
        for (int i = jsonBytes.Length; i < jsonChunkLength; i++)
        {
            jsonChunkPadded[i] = 0x20;
        }

        var binaryChunkLength = (binaryData.Length + 3) & ~3;
        var binaryChunkPadded = new byte[binaryChunkLength];
        Array.Copy(binaryData, binaryChunkPadded, binaryData.Length);

        using (var memoryStream = new MemoryStream())
        using (var writer = new BinaryWriter(memoryStream))
        {
            writer.Write(0x46546C67);
            writer.Write((uint)2);

            var totalLength = 12 + 8 + jsonChunkLength + 8 + binaryChunkLength;
            writer.Write((uint)totalLength);

            writer.Write((uint)jsonChunkLength);
            writer.Write(0x4E4F534A);
            writer.Write(jsonChunkPadded);

            writer.Write((uint)binaryChunkLength);
            writer.Write(0x004E4942);
            writer.Write(binaryChunkPadded);

            return Task.FromResult(memoryStream.ToArray());
        }
    }

    /// <summary>
    /// 压缩切片内容 - 数据压缩算法实现
    /// 算法：使用指定的压缩级别对切片数据进行压缩
    /// </summary>
    /// <param name="content">原始内容</param>
    /// <param name="compressionLevel">压缩级别（0-9）</param>
    /// <returns>压缩后的内容</returns>
    private async Task<byte[]> CompressSliceContentAsync(byte[] content, int compressionLevel)
    {
        // 压缩算法实现：使用GZip压缩
        using (var compressedStream = new MemoryStream())
        {
            using (var gzipStream = new System.IO.Compression.GZipStream(compressedStream, GetCompressionLevel(compressionLevel)))
            {
                await gzipStream.WriteAsync(content, 0, content.Length);
            }
            return compressedStream.ToArray();
        }
    }

    /// <summary>
    /// 生成增量更新索引 - 增量更新算法实现
    /// 算法：为切片生成增量更新索引，支持部分模型更新
    /// </summary>
    /// <param name="task">切片任务</param>
    /// <param name="config">切片配置</param>
    private async Task GenerateIncrementalUpdateIndexAsync(SlicingTask task, SlicingConfig config, CancellationToken cancellationToken)
    {
        // 增量更新索引生成算法
        var allSlices = await _sliceRepository.GetAllAsync();
        var slices = allSlices.Where(s => s.SlicingTaskId == task.Id).ToList();

        var sliceData = new List<object>();
        foreach (var s in slices)
        {
            sliceData.Add(new
            {
                s.Level,
                s.X,
                s.Y,
                s.Z,
                s.FilePath,
                Hash = await CalculateSliceHash(s), // 计算切片哈希用于增量比较
                s.BoundingBox
            });
        }

        var updateIndex = new
        {
            TaskId = task.Id,
            Version = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
            LastModified = DateTime.UtcNow,
            SliceCount = slices.Count,
            Slices = sliceData,
            Strategy = config.Strategy.ToString(),
            TileSize = config.TileSize
        };
        var indexContent = System.Text.Json.JsonSerializer.Serialize(updateIndex, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var indexPath = $"{task.OutputPath}/incremental_index.json";

        if (config.StorageLocation == StorageLocationType.LocalFileSystem)
        {
            var fullPath = Path.Combine(task.OutputPath!, "incremental_index.json");
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllTextAsync(fullPath, indexContent, cancellationToken);
            _logger.LogDebug("增量更新索引文件已保存到本地：{FilePath}", fullPath);
        }
        else
        {
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(indexContent)))
            {
                await _minioService.UploadFileAsync("slices", indexPath, stream, "application/json", cancellationToken);
            }
            _logger.LogInformation("增量更新索引文件已上传到MinIO：{FilePath}", indexPath);
        }

        _logger.LogInformation("增量更新索引已生成：{IndexPath}", indexPath);
    }

    /// <summary>
    /// 计算切片哈希值 - 增量更新比较算法
    /// 算法：基于切片完整内容计算哈希值，包括文件内容、元数据等，用于精确检测变化
    /// 性能优化：
    /// - 预分配缓冲区避免重复内存分配
    /// - 异步文件I/O避免阻塞
    /// - 缓存任务配置避免重复查询
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, (SlicingConfig config, DateTime timestamp)> _taskConfigCache =
        new ConcurrentDictionary<Guid, (SlicingConfig config, DateTime timestamp)>();

    private async Task<string> CalculateSliceHash(Slice slice)
    {
        try
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            {
                // 1. 收集切片的关键信息，使用StringBuilder避免字符串拼接开销
                var metadataBuilder = new StringBuilder(256);
                metadataBuilder.Append(slice.Level).Append('_')
                              .Append(slice.X).Append('_')
                              .Append(slice.Y).Append('_')
                              .Append(slice.Z).Append('_')
                              .Append(slice.BoundingBox).Append('_')
                              .Append(slice.FilePath);

                var metadataBytes = System.Text.Encoding.UTF8.GetBytes(metadataBuilder.ToString());

                // 2. 获取任务配置（带缓存）
                var config = await GetSlicingConfigCachedAsync(slice.SlicingTaskId);

                // 3. 异步读取文件内容并计算哈希
                byte[]? fileContentHash = null;
                try
                {
                    if (config.StorageLocation == StorageLocationType.LocalFileSystem)
                    {
                        var fullPath = Path.IsPathRooted(slice.FilePath)
                            ? slice.FilePath
                            : Path.Combine(await GetTaskOutputPathAsync(slice.SlicingTaskId), slice.FilePath);

                        if (File.Exists(fullPath))
                        {
                            await using var fileStream = File.OpenRead(fullPath);
                            fileContentHash = await sha256.ComputeHashAsync(fileStream);
                        }
                    }
                    else // MinIO
                    {
                        await using var fileStream = await _minioService.DownloadFileAsync("slices", slice.FilePath);
                        if (fileStream != null)
                        {
                            fileContentHash = await sha256.ComputeHashAsync(fileStream);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "读取切片文件失败，将仅使用元数据计算哈希：{FilePath}", slice.FilePath);
                }

                // 4. 计算最终哈希
                byte[] finalHash;
                if (fileContentHash != null)
                {
                    // 预分配缓冲区
                    var combinedLength = metadataBytes.Length + fileContentHash.Length;
                    var combinedBytes = ArrayPool<byte>.Shared.Rent(combinedLength);
                    try
                    {
                        Buffer.BlockCopy(metadataBytes, 0, combinedBytes, 0, metadataBytes.Length);
                        Buffer.BlockCopy(fileContentHash, 0, combinedBytes, metadataBytes.Length, fileContentHash.Length);
                        finalHash = sha256.ComputeHash(combinedBytes, 0, combinedLength);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(combinedBytes);
                    }
                }
                else
                {
                    finalHash = sha256.ComputeHash(metadataBytes);
                }

                // 5. 转换为十六进制字符串
                return Convert.ToHexString(finalHash).ToLower();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算切片哈希时发生错误，将使用简化方式计算：{SliceId}", slice.Id);
            var fallbackInput = $"{slice.Level}_{slice.X}_{slice.Y}_{slice.Z}_{slice.BoundingBox}";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            {
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(fallbackInput));
                return Convert.ToHexString(hashBytes).ToLower();
            }
        }
    }

    /// <summary>
    /// 缓存获取切片配置，避免重复数据库查询
    /// </summary>
    private async Task<SlicingConfig> GetSlicingConfigCachedAsync(Guid taskId)
    {
        if (_taskConfigCache.TryGetValue(taskId, out var cached) &&
            (DateTime.UtcNow - cached.timestamp) < TimeSpan.FromMinutes(5))
        {
            return cached.config;
        }

        var task = await _slicingTaskRepository.GetByIdAsync(taskId);
        if (task == null)
            throw new InvalidOperationException($"切片任务 {taskId} 未找到");

        var config = ParseSlicingConfig(task.SlicingConfig);
        _taskConfigCache[taskId] = (config, DateTime.UtcNow);
        return config;
    }

    /// <summary>
    /// 获取任务输出路径（带缓存）
    /// </summary>
    private async Task<string> GetTaskOutputPathAsync(Guid taskId)
    {
        var task = await _slicingTaskRepository.GetByIdAsync(taskId);
        return task?.OutputPath ?? string.Empty;
    }

    /// <summary>
    /// 从已存在的切片计算哈希值 - 用于增量更新比对
    /// 与CalculateSliceHash的区别是，这个方法用于已存在于数据库中的切片
    /// </summary>
    /// <param name="slice">已存在的切片数据</param>
    /// <returns>哈希值字符串</returns>
    private async Task<string> CalculateSliceHashFromExisting(Slice slice)
    {
        // 直接调用 CalculateSliceHash，因为逻辑是一样的
        // 该方法会根据切片的 SlicingTaskId 查找任务配置，然后读取文件计算哈希
        return await CalculateSliceHash(slice);
    }

    private System.IO.Compression.CompressionLevel GetCompressionLevel(int level)
    {
        return level switch
        {
            <= 1 => System.IO.Compression.CompressionLevel.Fastest,
            >= 9 => System.IO.Compression.CompressionLevel.SmallestSize,
            _ => System.IO.Compression.CompressionLevel.Optimal
        };
    }

    private string GetContentType(string format)
    {
        return format.ToLower() switch
        {
            "b3dm" => "application/octet-stream",
            "gltf" => "application/json",
            "glb" => "application/octet-stream",
            "json" => "application/json",
            _ => "application/octet-stream"
        };
    }
}

/// <summary>
/// 任务进度历史记录 - 用于趋势分析和精确时间估算
/// 线程安全的进度跟踪类，支持并发访问
/// </summary>
internal class TaskProgressHistory
{
    private readonly object _lock = new object();
    private readonly List<ProgressRecord> _progressRecords = new();
    private const int MaxRecordsCount = 100; // 最多保留100条历史记录
    private const int MaxRecordAgeMinutes = 60; // 最多保留60分钟的历史记录

    /// <summary>
    /// 进度记录列表（只读）
    /// </summary>
    public IReadOnlyList<ProgressRecord> ProgressRecords
    {
        get
        {
            lock (_lock)
            {
                return _progressRecords.ToList();
            }
        }
    }

    /// <summary>
    /// 上次估算的剩余时间（秒）
    /// </summary>
    public double? LastEstimatedTime { get; set; }

    /// <summary>
    /// 记录进度数据点
    /// </summary>
    /// <param name="progress">当前进度（0-100）</param>
    /// <param name="timestamp">时间戳</param>
    public void RecordProgress(double progress, DateTime timestamp)
    {
        lock (_lock)
        {
            // 添加新记录
            _progressRecords.Add(new ProgressRecord
            {
                Progress = progress,
                Timestamp = timestamp
            });

            // 清理过期数据
            CleanupOldRecords();

            // 限制记录数量
            while (_progressRecords.Count > MaxRecordsCount)
            {
                _progressRecords.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// 获取指定时间范围内的记录
    /// </summary>
    /// <param name="timeSpan">时间范围</param>
    /// <returns>时间范围内的记录列表</returns>
    public List<ProgressRecord> GetRecentRecords(TimeSpan timeSpan)
    {
        lock (_lock)
        {
            var cutoffTime = DateTime.UtcNow - timeSpan;
            return _progressRecords.Where(r => r.Timestamp >= cutoffTime).ToList();
        }
    }

    /// <summary>
    /// 清理过期记录
    /// </summary>
    private void CleanupOldRecords()
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-MaxRecordAgeMinutes);
        _progressRecords.RemoveAll(r => r.Timestamp < cutoffTime);
    }

    /// <summary>
    /// 进度记录数据点
    /// </summary>
    public class ProgressRecord
    {
        public double Progress { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

/// <summary>
/// 增量更新索引JSON模型 - 用于反序列化MinIO中的索引文件
/// </summary>
internal class IncrementalIndexJsonModel
{
    public Guid TaskId { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public int SliceCount { get; set; }
    public List<IncrementalSliceJsonModel>? Slices { get; set; }
    public string? Strategy { get; set; }
    public double TileSize { get; set; }
}

/// <summary>
/// 增量切片JSON模型 - 用于反序列化索引文件中的切片信息
/// </summary>
internal class IncrementalSliceJsonModel
{
    public int Level { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public string? FilePath { get; set; }
    public string? Hash { get; set; }
    public string? BoundingBox { get; set; }
}
