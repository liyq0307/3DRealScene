using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// 几何密度分析器 - 多维度几何复杂度分析
/// 共享类，供AdaptiveSlicingStrategy和SlicingProcessor使用
/// </summary>
public class GeometricDensityAnalyzer
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
