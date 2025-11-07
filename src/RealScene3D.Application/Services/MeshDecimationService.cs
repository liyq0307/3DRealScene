using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using System.Collections.Concurrent;

namespace RealScene3D.Application.Services;

/// <summary>
/// 网格简化服务 - 基于二次误差度量(Quadric Error Metric)的网格简化算法
/// 参考Obj2Tiles的Fast Quadric Mesh Simplification实现
/// 用于生成多层次细节(LOD)的3D模型
/// </summary>
public class MeshDecimationService
{
    private readonly ILogger _logger;

    public MeshDecimationService(ILogger logger)
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
    /// 顶点数据结构 - 用于简化算法
    /// </summary>
    private class Vertex
    {
        public Vector3D Position { get; set; } = new();
        public SymmetricMatrix Q { get; set; } = new(); // 二次误差矩阵
        public List<int> Triangles { get; set; } = new(); // 关联的三角形索引
        public int CollapseTo { get; set; } = -1; // 折叠目标顶点
        public bool IsBorder { get; set; } = false; // 是否边界顶点
    }

    /// <summary>
    /// 三角形数据结构 - 用于简化算法
    /// </summary>
    private class TriangleMesh
    {
        public int V0 { get; set; }
        public int V1 { get; set; }
        public int V2 { get; set; }
        public double Error { get; set; } // 折叠误差
        public bool Deleted { get; set; } = false;
        public bool Dirty { get; set; } = true;
        public Vector3D Normal { get; set; } = new();
    }

    /// <summary>
    /// 对称矩阵 - 用于二次误差度量
    /// 4x4对称矩阵，用10个值存储
    /// </summary>
    private class SymmetricMatrix
    {
        private readonly double[] m = new double[10];

        public SymmetricMatrix() { }

        public SymmetricMatrix(double a, double b, double c, double d)
        {
            m[0] = a * a; m[1] = a * b; m[2] = a * c; m[3] = a * d;
            m[4] = b * b; m[5] = b * c; m[6] = b * d;
            m[7] = c * c; m[8] = c * d;
            m[9] = d * d;
        }

        public double this[int index]
        {
            get => m[index];
            set => m[index] = value;
        }

        // 计算顶点的二次误差
        public double Error(Vector3D v)
        {
            return m[0] * v.X * v.X + 2 * m[1] * v.X * v.Y + 2 * m[2] * v.X * v.Z + 2 * m[3] * v.X
                 + m[4] * v.Y * v.Y + 2 * m[5] * v.Y * v.Z + 2 * m[6] * v.Y
                 + m[7] * v.Z * v.Z + 2 * m[8] * v.Z
                 + m[9];
        }

        public static SymmetricMatrix operator +(SymmetricMatrix a, SymmetricMatrix b)
        {
            var result = new SymmetricMatrix();
            for (int i = 0; i < 10; i++)
                result[i] = a[i] + b[i];
            return result;
        }
    }

    /// <summary>
    /// 简化网格 - 主入口方法
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

        var startTime = DateTime.UtcNow;
        var originalCount = triangles.Count;

        _logger.LogInformation("开始网格简化：原始三角形数量={Count}，目标质量={Quality:F2}",
            originalCount, options.Quality);

        try
        {
            // 1. 构建顶点和三角形数据结构
            var (vertices, meshTriangles) = BuildMeshStructure(triangles);
            _logger.LogDebug("构建网格结构：顶点数={VertexCount}，三角形数={TriangleCount}",
                vertices.Count, meshTriangles.Count);

            // 2. 计算每个顶点的二次误差矩阵
            ComputeVertexQuadrics(vertices, meshTriangles);

            // 3. 标记边界顶点
            if (options.PreserveBoundary)
            {
                MarkBorderVertices(vertices, meshTriangles);
            }

            // 4. 执行迭代简化
            int targetTriangleCount = (int)(originalCount * options.Quality);
            SimplifyMeshIterative(vertices, meshTriangles, targetTriangleCount, options);

            // 5. 重建三角形列表
            var simplifiedTriangles = RebuildTriangleList(vertices, meshTriangles);

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
    /// 参考Obj2Tiles的策略：quality[i] = 1 - ((i + 1) / lods)
    /// </summary>
    /// <param name="triangles">原始三角形列表</param>
    /// <param name="lodLevels">LOD级别数量</param>
    /// <returns>各级LOD的网格列表</returns>
    public List<DecimatedMesh> GenerateLODs(List<Triangle> triangles, int lodLevels)
    {
        var lodMeshes = new List<DecimatedMesh>();

        _logger.LogInformation("开始生成多LOD网格：原始三角形={Count}，LOD级别={Levels}",
            triangles.Count, lodLevels);

        for (int i = 0; i < lodLevels; i++)
        {
            // Obj2Tiles的质量计算公式
            double quality = 1.0 - ((double)(i + 1) / lodLevels);

            // Level 0保持原始质量
            if (i == 0)
            {
                quality = 1.0;
            }

            var options = new DecimationOptions
            {
                Quality = quality,
                PreserveBoundary = true,
                PreserveUV = false,
                Aggressiveness = 7.0
            };

            var decimatedMesh = SimplifyMesh(triangles, options);
            lodMeshes.Add(decimatedMesh);

            _logger.LogInformation("LOD级别{Level}生成完成：质量={Quality:F2}，三角形数={Count}",
                i, quality, decimatedMesh.SimplifiedTriangleCount);
        }

        return lodMeshes;
    }

    /// <summary>
    /// 构建网格数据结构
    /// </summary>
    private (List<Vertex> vertices, List<TriangleMesh> triangles) BuildMeshStructure(List<Triangle> inputTriangles)
    {
        var vertices = new List<Vertex>();
        var triangles = new List<TriangleMesh>();
        var vertexMap = new Dictionary<string, int>(); // 用于顶点去重

        foreach (var triangle in inputTriangles)
        {
            var indices = new int[3];

            for (int i = 0; i < 3; i++)
            {
                var v = triangle.Vertices[i];
                var key = $"{v.X:F6}_{v.Y:F6}_{v.Z:F6}";

                if (!vertexMap.TryGetValue(key, out int index))
                {
                    index = vertices.Count;
                    vertices.Add(new Vertex { Position = v });
                    vertexMap[key] = index;
                }

                indices[i] = index;
            }

            var meshTri = new TriangleMesh
            {
                V0 = indices[0],
                V1 = indices[1],
                V2 = indices[2]
            };

            // 计算法线
            meshTri.Normal = ComputeTriangleNormal(
                vertices[indices[0]].Position,
                vertices[indices[1]].Position,
                vertices[indices[2]].Position);

            triangles.Add(meshTri);

            // 添加顶点-三角形关联
            int triIndex = triangles.Count - 1;
            vertices[indices[0]].Triangles.Add(triIndex);
            vertices[indices[1]].Triangles.Add(triIndex);
            vertices[indices[2]].Triangles.Add(triIndex);
        }

        return (vertices, triangles);
    }

    /// <summary>
    /// 计算顶点的二次误差矩阵
    /// </summary>
    private void ComputeVertexQuadrics(List<Vertex> vertices, List<TriangleMesh> triangles)
    {
        // 初始化所有顶点的Q矩阵
        foreach (var vertex in vertices)
        {
            vertex.Q = new SymmetricMatrix();
        }

        // 累加每个三角形的误差到其顶点
        foreach (var tri in triangles)
        {
            var n = tri.Normal;
            var p = vertices[tri.V0].Position;

            // 平面方程: ax + by + cz + d = 0
            double d = -(n.X * p.X + n.Y * p.Y + n.Z * p.Z);

            // 构建二次误差矩阵
            var q = new SymmetricMatrix(n.X, n.Y, n.Z, d);

            vertices[tri.V0].Q += q;
            vertices[tri.V1].Q += q;
            vertices[tri.V2].Q += q;
        }
    }

    /// <summary>
    /// 标记边界顶点
    /// </summary>
    private void MarkBorderVertices(List<Vertex> vertices, List<TriangleMesh> triangles)
    {
        var edgeCount = new Dictionary<(int, int), int>();

        foreach (var tri in triangles)
        {
            if (tri.Deleted) continue;

            // 记录每条边
            AddEdge(edgeCount, tri.V0, tri.V1);
            AddEdge(edgeCount, tri.V1, tri.V2);
            AddEdge(edgeCount, tri.V2, tri.V0);
        }

        // 只被一个三角形共享的边是边界边
        foreach (var edge in edgeCount.Where(e => e.Value == 1))
        {
            vertices[edge.Key.Item1].IsBorder = true;
            vertices[edge.Key.Item2].IsBorder = true;
        }
    }

    private void AddEdge(Dictionary<(int, int), int> edgeCount, int v0, int v1)
    {
        var edge = v0 < v1 ? (v0, v1) : (v1, v0);
        edgeCount.TryGetValue(edge, out int count);
        edgeCount[edge] = count + 1;
    }

    /// <summary>
    /// 迭代简化网格
    /// </summary>
    private void SimplifyMeshIterative(List<Vertex> vertices, List<TriangleMesh> triangles,
        int targetTriangleCount, DecimationOptions options)
    {
        int deletedTriangles = 0;
        int iteration = 0;

        while (triangles.Count - deletedTriangles > targetTriangleCount && iteration < options.MaxIterations)
        {
            // 更新所有三角形的折叠误差
            if (iteration % 5 == 0) // 每5次迭代重新计算
            {
                UpdateTriangleErrors(vertices, triangles);
            }

            // 找到误差最小的边进行折叠
            int bestTriIndex = FindBestTriangleToCollapse(triangles);
            if (bestTriIndex == -1)
                break;

            // 折叠边
            if (CollapseEdge(vertices, triangles, bestTriIndex, options))
            {
                deletedTriangles++;
            }

            iteration++;

            if (iteration % 1000 == 0)
            {
                _logger.LogDebug("简化迭代进度：第{Iteration}次，剩余三角形={Remaining}",
                    iteration, triangles.Count - deletedTriangles);
            }
        }

        _logger.LogInformation("简化迭代完成：总迭代={Iterations}次，删除三角形={Deleted}个",
            iteration, deletedTriangles);
    }

    /// <summary>
    /// 更新三角形的折叠误差
    /// </summary>
    private void UpdateTriangleErrors(List<Vertex> vertices, List<TriangleMesh> triangles)
    {
        for (int i = 0; i < triangles.Count; i++)
        {
            var tri = triangles[i];
            if (tri.Deleted || !tri.Dirty) continue;

            tri.Error = CalculateEdgeCollapseError(vertices, tri.V0, tri.V1);
            tri.Dirty = false;
        }
    }

    /// <summary>
    /// 计算边折叠误差
    /// </summary>
    private double CalculateEdgeCollapseError(List<Vertex> vertices, int v0Index, int v1Index)
    {
        var v0 = vertices[v0Index];
        var v1 = vertices[v1Index];

        // 合并后的Q矩阵
        var q = v0.Q + v1.Q;

        // 计算最优折叠位置（简化版：使用中点）
        var midpoint = new Vector3D
        {
            X = (v0.Position.X + v1.Position.X) * 0.5,
            Y = (v0.Position.Y + v1.Position.Y) * 0.5,
            Z = (v0.Position.Z + v1.Position.Z) * 0.5
        };

        return q.Error(midpoint);
    }

    /// <summary>
    /// 找到最适合折叠的三角形
    /// </summary>
    private int FindBestTriangleToCollapse(List<TriangleMesh> triangles)
    {
        int bestIndex = -1;
        double bestError = double.MaxValue;

        for (int i = 0; i < triangles.Count; i++)
        {
            var tri = triangles[i];
            if (tri.Deleted) continue;

            if (tri.Error < bestError)
            {
                bestError = tri.Error;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// 折叠边
    /// </summary>
    private bool CollapseEdge(List<Vertex> vertices, List<TriangleMesh> triangles,
        int triIndex, DecimationOptions options)
    {
        var tri = triangles[triIndex];
        var v0 = vertices[tri.V0];
        var v1 = vertices[tri.V1];

        // 不折叠边界顶点
        if (options.PreserveBoundary && (v0.IsBorder || v1.IsBorder))
            return false;

        // 标记三角形为删除
        tri.Deleted = true;

        // 合并顶点（将v1的位置更新为中点，v0标记为折叠到v1）
        v1.Position = new Vector3D
        {
            X = (v0.Position.X + v1.Position.X) * 0.5,
            Y = (v0.Position.Y + v1.Position.Y) * 0.5,
            Z = (v0.Position.Z + v1.Position.Z) * 0.5
        };

        v1.Q = v0.Q + v1.Q;
        v0.CollapseTo = tri.V1;

        // 更新相关三角形
        foreach (var relatedTriIndex in v0.Triangles)
        {
            if (relatedTriIndex == triIndex) continue;
            var relatedTri = triangles[relatedTriIndex];
            if (relatedTri.Deleted) continue;

            // 替换v0为v1
            if (relatedTri.V0 == tri.V0) relatedTri.V0 = tri.V1;
            if (relatedTri.V1 == tri.V0) relatedTri.V1 = tri.V1;
            if (relatedTri.V2 == tri.V0) relatedTri.V2 = tri.V1;

            // 检查是否退化
            if (relatedTri.V0 == relatedTri.V1 || relatedTri.V1 == relatedTri.V2 || relatedTri.V2 == relatedTri.V0)
            {
                relatedTri.Deleted = true;
            }
            else
            {
                relatedTri.Dirty = true;
            }
        }

        return true;
    }

    /// <summary>
    /// 重建三角形列表
    /// </summary>
    private List<Triangle> RebuildTriangleList(List<Vertex> vertices, List<TriangleMesh> meshTriangles)
    {
        var result = new List<Triangle>();

        foreach (var meshTri in meshTriangles)
        {
            if (meshTri.Deleted) continue;

            var triangle = new Triangle
            {
                Vertices = new[]
                {
                    vertices[meshTri.V0].Position,
                    vertices[meshTri.V1].Position,
                    vertices[meshTri.V2].Position
                }
            };

            result.Add(triangle);
        }

        return result;
    }

    /// <summary>
    /// 计算三角形法线
    /// </summary>
    private Vector3D ComputeTriangleNormal(Vector3D v0, Vector3D v1, Vector3D v2)
    {
        var edge1 = new Vector3D
        {
            X = v1.X - v0.X,
            Y = v1.Y - v0.Y,
            Z = v1.Z - v0.Z
        };

        var edge2 = new Vector3D
        {
            X = v2.X - v0.X,
            Y = v2.Y - v0.Y,
            Z = v2.Z - v0.Z
        };

        var normal = new Vector3D
        {
            X = edge1.Y * edge2.Z - edge1.Z * edge2.Y,
            Y = edge1.Z * edge2.X - edge1.X * edge2.Z,
            Z = edge1.X * edge2.Y - edge1.Y * edge2.X
        };

        var length = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);
        if (length > 1e-10)
        {
            normal.X /= length;
            normal.Y /= length;
            normal.Z /= length;
        }

        return normal;
    }
}
