using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using System.Collections.Concurrent;
using MeshDecimatorCore;
using MeshDecimatorCore.Math;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace RealScene3D.Application.Services;

/// <summary>
/// 网格简化服务 - Fast Quadric Mesh Simplification网格简化算法
/// 用于生成多层次细节(LOD)的3D模型
/// </summary>
public class MeshDecimationService
{
    private readonly ILogger _logger;

    public MeshDecimationService(ILogger<MeshDecimationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 网格简化配置
    /// </summary>
    public class DecimationOptions
    {
        /// <summary>
        /// 目标质量因子 (0.0-1.0)，1.0表示保持原始质量，0.0表示最大简化
        /// </summary>
        public double Quality { get; set; } = 1.0;

        /// <summary>
        /// 是否保留边界
        /// </summary>
        public bool PreserveBoundary { get; set; } = true;

        /// <summary>
        /// 是否保留UV坐标
        /// </summary>
        public bool PreserveUV { get; set; } = false;

        /// <summary>
        /// 最大迭代次数
        /// 100次迭代，通过动态阈值实现批量删除
        /// 每次迭代删除所有误差低于阈值的三角形，而不是只删除一个
        /// </summary>
        public int MaxIterations { get; set; } = 100;

        /// <summary>
        /// 聚合度 (影响简化速度)
        /// </summary>
        public double Aggressiveness { get; set; } = 7.0;
    }

    /// <summary>
    /// 简化后的网格结果
    /// </summary>
    public class DecimatedMesh
    {
        public List<Triangle> Triangles { get; set; } = new();
        public int OriginalTriangleCount { get; set; }
        public int SimplifiedTriangleCount { get; set; }
        public double ReductionRatio { get; set; }
        public double QualityFactor { get; set; }
    }

    /// <summary>
    /// 简化网格 - 主入口方法 (使用 MeshDecimatorCore 高性能库)
    /// </summary>
    /// <param name="triangles">输入的三角形列表</param>
    /// <param name="options">简化选项</param>
    /// <returns>简化后的网格</returns>
    public DecimatedMesh SimplifyMesh(List<Triangle> triangles, DecimationOptions options)
    {
        if (triangles == null || triangles.Count == 0)
        {
            _logger.LogWarning("输入网格为空，无法进行简化");
            return new DecimatedMesh { Triangles = new List<Triangle>() };
        }

        var originalCount = triangles.Count;

        // 质量因子为 1.0 时直接返回原始网格（LOD-0 情况）
        if (options.Quality >= 1.0)
        {
            _logger.LogInformation("质量因子=1.0，保持原始网格不简化：{Count}个三角形", originalCount);
            return new DecimatedMesh
            {
                Triangles = triangles,
                OriginalTriangleCount = originalCount,
                SimplifiedTriangleCount = originalCount,
                ReductionRatio = 0.0,
                QualityFactor = 1.0
            };
        }

        var startTime = DateTime.UtcNow;

        _logger.LogInformation("开始网格简化（MeshDecimatorCore）：原始三角形数量={Count}，目标质量={Quality:F2}",
            originalCount, options.Quality);

        try
        {
            // 1. 转换为 MeshDecimatorCore.Mesh 格式
            var mesh = ConvertToMeshDecimatorMesh(triangles);
            _logger.LogDebug("转换为 MeshDecimatorCore.Mesh：顶点数={VertexCount}，三角形数={TriangleCount}",
                mesh.VertexCount, mesh.TriangleCount);

            // 2. 配置简化算法
            var algorithm = new MeshDecimatorCore.Algorithms.FastQuadricMeshSimplification
            {
                PreserveBorders = options.PreserveBoundary,
                PreserveSeams = true, // 保留接缝，避免UV撕裂
                Verbose = false
            };

            // 3. 执行简化
            int targetTriangleCount = (int)(originalCount * options.Quality);
            var decimatedMesh = MeshDecimation.DecimateMesh(algorithm, mesh, targetTriangleCount);

            // 4. 转换回 Triangle 列表
            var simplifiedTriangles = ConvertFromMeshDecimatorMesh(decimatedMesh, triangles);

            var elapsed = DateTime.UtcNow - startTime;
            var reductionRatio = 1.0 - ((double)simplifiedTriangles.Count / originalCount);

            _logger.LogInformation("网格简化完成：原始={Original}个，简化={Simplified}个，" +
                "简化率={Ratio:F2}%，耗时={Elapsed:F2}秒",
                originalCount, simplifiedTriangles.Count, reductionRatio * 100, elapsed.TotalSeconds);

            return new DecimatedMesh
            {
                Triangles = simplifiedTriangles,
                OriginalTriangleCount = originalCount,
                SimplifiedTriangleCount = simplifiedTriangles.Count,
                ReductionRatio = reductionRatio,
                QualityFactor = options.Quality
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "网格简化失败");
            throw;
        }
    }

    /// <summary>
    /// 生成多LOD网格
    /// 质量公式：quality[i] = 1 - ((i + 1) / lods)
    /// </summary>
    /// <param name="triangles">原始三角形列表</param>
    /// <param name="lodLevels">LOD级别数量</param>
    /// <returns>各级LOD的网格列表</returns>
    public List<DecimatedMesh> GenerateLODs(List<Triangle> triangles, int lodLevels)
    {
        return GenerateLODs(triangles, lodLevels, enableParallel: true);
    }

    /// <summary>
    /// 生成多LOD网格（支持并行处理）
    /// 质量公式：quality[i] = 1 - ((i + 1) / lods)
    /// </summary>
    /// <param name="triangles">原始三角形列表</param>
    /// <param name="lodLevels">LOD级别数量</param>
    /// <param name="enableParallel">是否启用并行处理（默认启用）</param>
    /// <returns>各级LOD的网格列表</returns>
    public List<DecimatedMesh> GenerateLODs(List<Triangle> triangles, int lodLevels, bool enableParallel = true)
    {
        _logger.LogInformation("开始生成多LOD网格：原始三角形={Count}，LOD级别={Levels}，并行={Parallel}",
            triangles.Count, lodLevels, enableParallel);

        var startTime = DateTime.UtcNow;

        if (!enableParallel || lodLevels == 1)
        {
            // 顺序处理（单个LOD或禁用并行时）
            return GenerateLODsSequential(triangles, lodLevels);
        }
        else
        {
            // 并行处理（多个LOD时）
            var result = GenerateLODsParallel(triangles, lodLevels);

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("并行LOD生成总耗时={Elapsed:F2}秒，平均每级={Average:F2}秒",
                elapsed.TotalSeconds, elapsed.TotalSeconds / lodLevels);

            return result;
        }
    }

    /// <summary>
    /// 顺序生成LOD（原始方法）
    /// </summary>
    private List<DecimatedMesh> GenerateLODsSequential(List<Triangle> triangles, int lodLevels)
    {
        var lodMeshes = new List<DecimatedMesh>();

        for (int i = 0; i < lodLevels; i++)
        {
            var quality = CalculateLODQuality(i, lodLevels);
            var options = CreateDecimationOptions(quality);
            var decimatedMesh = SimplifyMesh(triangles, options);

            lodMeshes.Add(decimatedMesh);

            _logger.LogInformation("LOD级别{Level}生成完成：质量={Quality:F2}，三角形数={Count}",
                i, quality, decimatedMesh.SimplifiedTriangleCount);
        }

        return lodMeshes;
    }

    /// <summary>
    /// 并行生成LOD（性能优化版本）
    /// 使用Parallel.For实现多核心并行处理
    /// </summary>
    private List<DecimatedMesh> GenerateLODsParallel(List<Triangle> triangles, int lodLevels)
    {
        // 使用ConcurrentBag保证线程安全
        var lodMeshBag = new ConcurrentBag<(int level, DecimatedMesh mesh)>();

        // 并行生成各级LOD
        Parallel.For(0, lodLevels, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        }, i =>
        {
            var quality = CalculateLODQuality(i, lodLevels);
            var options = CreateDecimationOptions(quality);

            var decimatedMesh = SimplifyMesh(triangles, options);

            lodMeshBag.Add((i, decimatedMesh));

            _logger.LogInformation("LOD级别{Level}生成完成：质量={Quality:F2}，三角形数={Count}，线程ID={ThreadId}",
                i, quality, decimatedMesh.SimplifiedTriangleCount, Environment.CurrentManagedThreadId);
        });

        // 按照LOD级别排序
        var lodMeshes = lodMeshBag
            .OrderBy(x => x.level)
            .Select(x => x.mesh)
            .ToList();

        return lodMeshes;
    }

    /// <summary>
    /// 计算LOD质量因子
    /// 质量因子范围：LOD-0 = 1.0（无简化），LOD-N 约 0.0-0.67（逐级简化）
    /// </summary>
    private double CalculateLODQuality(int level, int maxLevels)
    {
        if (level == 0)
        {
            // LOD-0 保持原始模型，不进行简化
            return 1.0;
        }

        // 其他LOD级别：quality = 1 - (level / maxLevels)
        double quality = 1.0 - ((double)level / maxLevels);

        // 确保质量因子不会太低（至少保留 5% 的三角形）
        return Math.Max(quality, 0.05);
    }

    /// <summary>
    /// 创建简化配置
    /// </summary>
    private DecimationOptions CreateDecimationOptions(double quality)
    {
        return new DecimationOptions
        {
            Quality = quality,
            PreserveBoundary = true,
            PreserveUV = false,
            Aggressiveness = 7.0
        };
    }

    /// <summary>
    /// 转换为 MeshDecimatorCore.Mesh 格式
    /// 保留材质、UV 坐标和法线信息
    /// </summary>
    private MeshDecimatorCore.Mesh ConvertToMeshDecimatorMesh(List<Triangle> triangles)
    {
        // 构建顶点和索引（考虑UV坐标，同一位置不同UV视为不同顶点）
        var vertices = new List<Vector3d>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();
        var vertexMap = new Dictionary<string, int>();

        foreach (var triangle in triangles)
        {
            for (int i = 0; i < 3; i++)
            {
                var pos = triangle.Vertices[i];
                var uv = i == 0 ? triangle.UV1 : (i == 1 ? triangle.UV2 : triangle.UV3);
                var normal = i == 0 ? triangle.Normal1 : (i == 1 ? triangle.Normal2 : triangle.Normal3);

                // 构建顶点键：位置 + UV（如果存在）
                string key;
                if (uv != null)
                {
                    key = $"{pos.X:F6}_{pos.Y:F6}_{pos.Z:F6}_{uv.U:F6}_{uv.V:F6}";
                }
                else
                {
                    key = $"{pos.X:F6}_{pos.Y:F6}_{pos.Z:F6}";
                }

                if (!vertexMap.TryGetValue(key, out int index))
                {
                    index = vertices.Count;
                    vertices.Add(new Vector3d(pos.X, pos.Y, pos.Z));

                    // 添加法线（如果存在）
                    if (normal != null)
                    {
                        normals.Add(new Vector3((float)normal.X, (float)normal.Y, (float)normal.Z));
                    }
                    else
                    {
                        normals.Add(new Vector3(0, 1, 0)); // 默认向上法线
                    }

                    // 添加UV（如果存在）
                    if (uv != null)
                    {
                        uvs.Add(new Vector2((float)uv.U, (float)uv.V));
                    }
                    else
                    {
                        uvs.Add(new Vector2(0, 0)); // 默认UV
                    }

                    vertexMap[key] = index;
                }

                indices.Add(index);
            }
        }

        var mesh = new MeshDecimatorCore.Mesh(vertices.ToArray(), indices.ToArray());

        // 设置顶点属性
        if (normals.Count == vertices.Count)
        {
            mesh.Normals = normals.ToArray();
        }

        if (uvs.Count == vertices.Count)
        {
            mesh.UV1 = uvs.ToArray();
        }

        return mesh;
    }

    /// <summary>
    /// 从 MeshDecimatorCore.Mesh 转换回 Triangle 列表
    /// 恢复材质信息
    /// </summary>
    private List<Triangle> ConvertFromMeshDecimatorMesh(MeshDecimatorCore.Mesh mesh, List<Triangle> originalTriangles)
    {
        var result = new List<Triangle>();

        var vertices = mesh.Vertices;
        var indices = mesh.Indices;
        var normals = mesh.Normals;
        var uvs = mesh.UV1;

        // 提取第一个材质作为默认材质（简化后的网格共享材质）
        var defaultMaterial = originalTriangles.FirstOrDefault()?.MaterialName;

        // 重建三角形
        for (int i = 0; i < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            var triangle = new Triangle
            {
                Vertices = new[]
                {
                    new Vector3D { X = vertices[i0].x, Y = vertices[i0].y, Z = vertices[i0].z },
                    new Vector3D { X = vertices[i1].x, Y = vertices[i1].y, Z = vertices[i1].z },
                    new Vector3D { X = vertices[i2].x, Y = vertices[i2].y, Z = vertices[i2].z }
                },
                MaterialName = defaultMaterial
            };

            // 恢复法线
            if (normals != null && normals.Length > i2)
            {
                triangle.Normal1 = new Vector3D { X = normals[i0].x, Y = normals[i0].y, Z = normals[i0].z };
                triangle.Normal2 = new Vector3D { X = normals[i1].x, Y = normals[i1].y, Z = normals[i1].z };
                triangle.Normal3 = new Vector3D { X = normals[i2].x, Y = normals[i2].y, Z = normals[i2].z };
            }

            // 恢复UV
            if (uvs != null && uvs.Length > i2)
            {
                triangle.UV1 = new Vector2D { U = uvs[i0].x, V = uvs[i0].y };
                triangle.UV2 = new Vector2D { U = uvs[i1].x, V = uvs[i1].y };
                triangle.UV3 = new Vector2D { U = uvs[i2].x, V = uvs[i2].y };
            }

            result.Add(triangle);
        }

        return result;
    }
}
