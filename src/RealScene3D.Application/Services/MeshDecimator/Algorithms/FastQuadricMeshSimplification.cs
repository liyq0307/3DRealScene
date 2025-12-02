using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Utils;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Geometry;

namespace RealScene3D.Application.Services.MeshDecimator.Algorithms;

/// <summary>
/// 快速四元网格简化算法。
/// 基于 Garland 和 Heckbert 的论文 "Surface Simplification Using Quadric Error Metrics"。
/// 参考：https://github.com/sp4cerat/Fast-Quadric-Mesh-Simplification
/// 该实现经过优化以提高性能和内存效率。
/// 注意：此算法假设输入网格为三角形网格。
/// </summary>
public sealed class FastQuadricMeshSimplification : DecimationAlgorithm
{
    /// <summary>
    /// 双精度浮点数的微小值，用于数值稳定性。
    /// </summary>
    private const double DoubleEpsilon = 1.0E-3;

    /// <summary>
    /// 三角形结构体, 包含顶点索引、子网格索引、属性索引、误差值和法线等信息。
    /// 
    /// 注意：此结构体是私有的，仅在 FastQuadricMeshSimplification 类中使用。
    /// </summary>
    private struct Triangle
    {
        public int v0;
        public int v1;
        public int v2;
        public int subMeshIndex;

        public int va0;
        public int va1;
        public int va2;

        public double err0;
        public double err1;
        public double err2;
        public double err3;

        public bool deleted;
        public bool dirty;
        public Vector3d n;

        public int this[int index]
        {
            get
            {
                return index == 0 ? v0 : (index == 1 ? v1 : v2);
            }
            set
            {
                switch (index)
                {
                    case 0:
                        v0 = value;
                        break;
                    case 1:
                        v1 = value;
                        break;
                    case 2:
                        v2 = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException();
                }
            }
        }

        public Triangle(int v0, int v1, int v2, int subMeshIndex)
        {
            this.v0 = v0;
            this.v1 = v1;
            this.v2 = v2;
            this.subMeshIndex = subMeshIndex;

            va0 = v0;
            va1 = v1;
            va2 = v2;

            err0 = err1 = err2 = err3 = 0;
            deleted = dirty = false;
            n = new Vector3d();
        }

        public void GetAttributeIndices(int[] attributeIndices)
        {
            attributeIndices[0] = va0;
            attributeIndices[1] = va1;
            attributeIndices[2] = va2;
        }

        public void SetAttributeIndex(int index, int value)
        {
            switch (index)
            {
                case 0:
                    va0 = value;
                    break;
                case 1:
                    va1 = value;
                    break;
                case 2:
                    va2 = value;
                    break;
                default:
                    throw new IndexOutOfRangeException();
            }
        }

        public void GetErrors(double[] err)
        {
            err[0] = err0;
            err[1] = err1;
            err[2] = err2;
        }
    }

    /// <summary>
    /// 顶点结构体，包含位置、三角形引用、四元矩阵和边界标志等信息。
    /// </summary>
    private struct Vertex
    {
        public Vector3d p;
        public int tstart;
        public int tcount;
        public SymmetricMatrix q;
        public bool border;
        public bool seam;
        public bool foldover;

        public Vertex(Vector3d p)
        {
            this.p = p;
            tstart = 0;
            tcount = 0;
            q = new SymmetricMatrix();
            border = true;
            seam = false;
            foldover = false;
        }
    }


    /// <summary>
    /// 引用结构体，表示顶点到三角形的引用关系。
    /// </summary>
    private struct Ref
    {
        public int tid;
        public int tvertex;

        public void Set(int tid, int tvertex)
        {
            this.tid = tid;
            this.tvertex = tvertex;
        }
    }

    /// <summary>
    /// 边界顶点结构体，用于存储边界顶点的索引和哈希值。
    /// </summary>
    private struct BorderVertex
    {
        public int index;
        public int hash;

        public BorderVertex(int index, int hash)
        {
            this.index = index;
            this.hash = hash;
        }
    }

    /// <summary>
    /// 边界顶点比较器，用于根据哈希值对边界顶点进行排序。
    /// </summary>
    private class BorderVertexComparer : IComparer<BorderVertex>
    {
        public static readonly BorderVertexComparer instance = new BorderVertexComparer();

        public int Compare(BorderVertex x, BorderVertex y)
        {
            return x.hash.CompareTo(y.hash);
        }
    }

    /// <summary>
    /// 是否保留翻折。
    /// </summary>
    private bool preserveFoldovers = false;

    /// <summary>
    /// 是否启用智能链接以优化顶点连接。
    /// </summary>
    private bool enableSmartLink = true;

    /// <summary>
    /// 最大迭代次数。
    /// </summary>
    private int maxIterationCount = 100;

    /// <summary>
    /// 简化激进程度。较高的值会导致更快的简化，但可能会降低质量。
    /// </summary>
    private double agressiveness = 7.0;

    /// <summary>
    /// 顶点链接距离的平方，用于确定顶点是否足够接近以进行链接。
    /// </summary>
    private double vertexLinkDistanceSqr = double.Epsilon;

    /// <summary>
    /// 子网格计数。
    /// </summary>
    private int subMeshCount = 0;

    /// <summary>
    /// 三角形数组。
    /// </summary>
    private ResizableArray<Triangle> triangles = null!;

    /// <summary>
    /// 顶点数组。
    /// </summary>
    private ResizableArray<Vertex> vertices = null!;

    /// <summary>
    /// 引用数组。
    /// </summary>
    private ResizableArray<Ref> refs = null!;

    /// <summary>
    /// 顶点属性数组。
    /// </summary>
    private ResizableArray<Vector3>? vertNormals = null;

    /// <summary>
    /// 顶点切线属性数组。
    /// </summary>
    private ResizableArray<Vector4>? vertTangents = null;

    /// <summary>
    /// 顶点 UV 属性数组。
    /// </summary>
    private UVChannels<Vector2>? vertUV2D = null;

    /// <summary>
    /// 顶点 3D UV 属性数组。
    /// </summary>
    private UVChannels<Vector3>? vertUV3D = null;

    /// <summary>
    /// 顶点 4D UV 属性数组。
    /// </summary>
    private UVChannels<Vector4>? vertUV4D = null;

    /// <summary>
    /// 顶点颜色属性数组。
    /// </summary>
    private ResizableArray<Vector4>? vertColors = null;

    /// <summary>
    /// 顶点骨骼权重属性数组。
    /// </summary>
    private ResizableArray<BoneWeight>? vertBoneWeights = null;

    /// <summary>
    /// 剩余顶点数量。
    /// </summary>
    private int remainingVertices = 0;

    /// <summary>
    /// 预分配的误差数组。
    /// </summary>
    private double[] errArr = new double[3];

    /// <summary>
    /// 预分配的属性索引数组。
    /// </summary>
    private int[] attributeIndexArr = new int[3];

    /// <summary>
    /// 日志记录器。
    /// </summary>
    private readonly ILogger? _logger;

    /// <summary>
    /// 获取或设置是否应保留接缝。
    /// 默认值：false
    /// </summary>
    public bool PreserveSeams { get; set; } = false;

    /// <summary>
    /// 创建一个新的快速四元网格简化算法。
    /// </summary>
    /// <param name="logger">可选的日志记录器。</param>
    public FastQuadricMeshSimplification(ILogger<FastQuadricMeshSimplification>? logger = null)
    {
        _logger = logger;
        triangles = new ResizableArray<Triangle>(0);
        vertices = new ResizableArray<Vertex>(0);
        refs = new ResizableArray<Ref>(0);
    }

    /// <summary>
    /// 初始化顶点属性数组。        
    /// </summary>
    /// <typeparam name="T">顶点属性的类型。</typeparam>
    /// <param name="attributeValues">顶点属性值数组。</param>
    ///  <param name="attributeName">顶点属性名称（用于日志记录）。</param>
    /// <returns>初始化后的可调整大小的数组，如果输入无效则返回 null。</returns>
    private ResizableArray<T>? InitializeVertexAttribute<T>(T[]? attributeValues, string attributeName)
    {
        if (attributeValues != null && attributeValues.Length == vertices.Length)
        {
            var newArray = new ResizableArray<T>(attributeValues.Length, attributeValues.Length);
            var newArrayData = newArray.Data;
            Array.Copy(attributeValues, 0, newArrayData, 0, attributeValues.Length);
            return newArray;
        }
        else if (attributeValues != null && attributeValues.Length > 0)
        {
            // 属性数组长度不匹配，忽略该属性
            _logger?.LogWarning("顶点属性 '{AttributeName}' 的数组长度为 {ActualLength}，但需要 {ExpectedLength}",
                attributeName, attributeValues.Length, vertices.Length);
        }
        return null;
    }

    /// <summary>
    /// 计算给定四元矩阵在指定顶点位置的误差。
    /// </summary>
    /// <param name="q">四元矩阵。</param>
    /// <param name="x">顶点的 X 坐标。</param>
    /// <param name="y">顶点的 Y 坐标。</param>
    /// <param name="z">顶点的 Z 坐标。</param>
    /// <returns>误差值。</returns>
    private double VertexError(ref SymmetricMatrix q, double x, double y, double z)
    {
        return q.m0 * x * x + 2 * q.m1 * x * y + 2 * q.m2 * x * z + 2 * q.m3 * x + q.m4 * y * y
            + 2 * q.m5 * y * z + 2 * q.m6 * y + q.m7 * z * z + 2 * q.m8 * z + q.m9;
    }

    /// <summary>
    /// 计算两个顶点之间的误差，并确定最优的折叠顶点位置。
    /// </summary>
    /// <param name="vert0">第一个顶点。</param>
    /// <param name="vert1">第二个顶点。</param>
    /// <param name="result">输出最优折叠顶点位置。</param>
    /// <param name="resultIndex">输出结果索引（0=p1, 1=p2, 2=中点）。</param>
    /// <returns>误差值。</returns>
    private double CalculateError(ref Vertex vert0, ref Vertex vert1, out Vector3d result, out int resultIndex)
    {
        // 计算插值顶点
        SymmetricMatrix q = vert0.q + vert1.q;
        bool border = vert0.border & vert1.border;
        double det = q.Determinant1();
        double error;
        if (det != 0.0 && !border)
        {
            // q_delta 可逆，使用行列式计算最优顶点位置
            result = new Vector3d(
                -1.0 / det * q.Determinant2(),  // vx = A41/det(q_delta)
                1.0 / det * q.Determinant3(),   // vy = A42/det(q_delta)
                -1.0 / det * q.Determinant4()); // vz = A43/det(q_delta)
            error = VertexError(ref q, result.x, result.y, result.z);
            resultIndex = 2;
        }
        else
        {
            // 行列式为0，尝试找到最佳结果
            Vector3d p1 = vert0.p;
            Vector3d p2 = vert1.p;
            Vector3d p3 = (p1 + p2) * 0.5f; // 中点
            double error1 = VertexError(ref q, p1.x, p1.y, p1.z);
            double error2 = VertexError(ref q, p2.x, p2.y, p2.z);
            double error3 = VertexError(ref q, p3.x, p3.y, p3.z);
            error = MathHelper.Min(error1, error2, error3);
            if (error == error3)
            {
                result = p3;
                resultIndex = 2;
            }
            else if (error == error2)
            {
                result = p2;
                resultIndex = 1;
            }
            else if (error == error1)
            {
                result = p1;
                resultIndex = 0;
            }
            else
            {
                result = p3;
                resultIndex = 2;
            }
        }

        return error;
    }

    /// <summary>
    /// 检查移除此边时三角形是否翻转
    /// </summary>
    private bool Flipped(ref Vector3d p, int i0, int i1, ref Vertex v0, bool[] deleted)
    {
        int tcount = v0.tcount;
        var refs = this.refs.Data;
        var triangles = this.triangles.Data;
        var vertices = this.vertices.Data;
        for (int k = 0; k < tcount; k++)
        {
            Ref r = refs[v0.tstart + k];
            if (triangles[r.tid].deleted)
                continue;

            int s = r.tvertex;
            int id1 = triangles[r.tid][(s + 1) % 3];
            int id2 = triangles[r.tid][(s + 2) % 3];
            if (id1 == i1 || id2 == i1)
            {
                deleted[k] = true;
                continue;
            }

            Vector3d d1 = vertices[id1].p - p;
            d1.Normalize();
            Vector3d d2 = vertices[id2].p - p;
            d2.Normalize();
            double dot = Vector3d.Dot(ref d1, ref d2);
            if (System.Math.Abs(dot) > 0.999)
                return true;

            Vector3d n;
            Vector3d.Cross(ref d1, ref d2, out n);
            n.Normalize();
            deleted[k] = false;
            dot = Vector3d.Dot(ref n, ref triangles[r.tid].n);
            if (dot < 0.2)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 边折叠后更新三角形连接和边误差。
    /// </summary>
    private void UpdateTriangles(int i0, int ia0, ref Vertex v, ResizableArray<bool> deleted, ref int deletedTriangles)
    {
        Vector3d p;
        int pIndex;
        int tcount = v.tcount;
        var triangles = this.triangles.Data;
        var vertices = this.vertices.Data;
        for (int k = 0; k < tcount; k++)
        {
            Ref r = refs[v.tstart + k];
            int tid = r.tid;
            Triangle t = triangles[tid];
            if (t.deleted)
                continue;

            if (deleted[k])
            {
                triangles[tid].deleted = true;
                ++deletedTriangles;
                continue;
            }

            t[r.tvertex] = i0;
            if (ia0 != -1)
            {
                t.SetAttributeIndex(r.tvertex, ia0);
            }

            t.dirty = true;
            t.err0 = CalculateError(ref vertices[t.v0], ref vertices[t.v1], out p, out pIndex);
            t.err1 = CalculateError(ref vertices[t.v1], ref vertices[t.v2], out p, out pIndex);
            t.err2 = CalculateError(ref vertices[t.v2], ref vertices[t.v0], out p, out pIndex);
            t.err3 = MathHelper.Min(t.err0, t.err1, t.err2);
            triangles[tid] = t;
            refs.Add(r);
        }
    }

    /// <summary>
    /// 移动顶点属性从 i1 到 i0。
    /// </summary>
    /// <param name="i0">目标顶点索引。</param>
    /// <param name="i1">源顶点索引。</param>
    private void MoveVertexAttributes(int i0, int i1)
    {
        if (vertNormals != null)
        {
            vertNormals[i0] = vertNormals[i1];
        }

        if (vertTangents != null)
        {
            vertTangents[i0] = vertTangents[i1];
        }

        if (vertUV2D != null)
        {
            for (int i = 0; i < SimpleMesh.UVChannelCount; i++)
            {
                var vertUV = vertUV2D[i];
                if (vertUV != null)
                {
                    vertUV[i0] = vertUV[i1];
                }
            }
        }

        if (vertUV3D != null)
        {
            for (int i = 0; i < SimpleMesh.UVChannelCount; i++)
            {
                var vertUV = vertUV3D[i];
                if (vertUV != null)
                {
                    vertUV[i0] = vertUV[i1];
                }
            }
        }

        if (vertUV4D != null)
        {
            for (int i = 0; i < SimpleMesh.UVChannelCount; i++)
            {
                var vertUV = vertUV4D[i];
                if (vertUV != null)
                {
                    vertUV[i0] = vertUV[i1];
                }
            }
        }

        if (vertColors != null)
        {
            vertColors[i0] = vertColors[i1];
        }

        if (vertBoneWeights != null)
        {
            vertBoneWeights[i0] = vertBoneWeights[i1];
        }
    }

    /// <summary>
    /// 合并顶点属性 i0 和 i1 到 i0。
    /// </summary>
    /// <param name="i0">目标顶点索引。</param>
    /// <param name="i1">源顶点索引。</param>
    private void MergeVertexAttributes(int i0, int i1)
    {
        if (vertNormals != null)
        {
            vertNormals[i0] = (vertNormals[i0] + vertNormals[i1]) * 0.5f;
        }

        if (vertTangents != null)
        {
            vertTangents[i0] = (vertTangents[i0] + vertTangents[i1]) * 0.5f;
        }

        if (vertUV2D != null)
        {
            for (int i = 0; i < SimpleMesh.UVChannelCount; i++)
            {
                var vertUV = vertUV2D[i];
                if (vertUV != null)
                {
                    vertUV[i0] = (vertUV[i0] + vertUV[i1]) * 0.5f;
                }
            }
        }

        if (vertUV3D != null)
        {
            for (int i = 0; i < SimpleMesh.UVChannelCount; i++)
            {
                var vertUV = vertUV3D[i];
                if (vertUV != null)
                {
                    vertUV[i0] = (vertUV[i0] + vertUV[i1]) * 0.5f;
                }
            }
        }

        if (vertUV4D != null)
        {
            for (int i = 0; i < SimpleMesh.UVChannelCount; i++)
            {
                var vertUV = vertUV4D[i];
                if (vertUV != null)
                {
                    vertUV[i0] = (vertUV[i0] + vertUV[i1]) * 0.5f;
                }
            }
        }
        
        if (vertColors != null)
        {
            vertColors[i0] = (vertColors[i0] + vertColors[i1]) * 0.5f;
        }

        // TODO: 我们是否需要完全混合骨骼权重，还是可以保持它们在这个场景中的原样？
    }

    /// <summary>
    /// 检查两个顶点的 UV 是否相同。
    /// </summary>
    /// <param name="channel">UV 通道索引。</param>
    /// <param name="indexA">第一个顶点索引。</param>
    /// <param name="indexB">第二个顶点索引。</param>
    /// <returns>如果 UV 相同则返回 true，否则返回 false。</returns>
    private bool AreUVsTheSame(int channel, int indexA, int indexB)
    {
        if (vertUV2D != null)
        {
            var vertUV = vertUV2D[channel];
            if (vertUV != null)
            {
                var uvA = vertUV[indexA];
                var uvB = vertUV[indexB];
                return uvA == uvB;
            }
        }

        if (vertUV3D != null)
        {
            var vertUV = vertUV3D[channel];
            if (vertUV != null)
            {
                var uvA = vertUV[indexA];
                var uvB = vertUV[indexB];
                return uvA == uvB;
            }
        }

        if (vertUV4D != null)
        {
            var vertUV = vertUV4D[channel];
            if (vertUV != null)
            {
                var uvA = vertUV[indexA];
                var uvB = vertUV[indexB];
                return uvA == uvB;
            }
        }

        return false;
    }

    /// <summary>
    /// 移除顶点并标记已删除的三角形
    /// </summary>
    private void RemoveVertexPass(int startTrisCount, int targetTrisCount, double threshold, ResizableArray<bool> deleted0, ResizableArray<bool> deleted1, ref int deletedTris)
    {
        var triangles = this.triangles.Data;
        int triangleCount = this.triangles.Length;
        var vertices = this.vertices.Data;

        bool preserveBorders = base.PreserveBorders;
        int maxVertexCount = base.MaxVertexCount;
        if (maxVertexCount <= 0)
            maxVertexCount = int.MaxValue;

        Vector3d p;
        int pIndex;
        for (int tid = 0; tid < triangleCount; tid++)
        {
            if (triangles[tid].dirty || triangles[tid].deleted || triangles[tid].err3 > threshold)
                continue;

            triangles[tid].GetErrors(errArr);
            triangles[tid].GetAttributeIndices(attributeIndexArr);
            for (int edgeIndex = 0; edgeIndex < 3; edgeIndex++)
            {
                if (errArr[edgeIndex] > threshold)
                    continue;

                int nextEdgeIndex = (edgeIndex + 1) % 3;
                int i0 = triangles[tid][edgeIndex];
                int i1 = triangles[tid][nextEdgeIndex];

                // 边界检查
                if (vertices[i0].border != vertices[i1].border)
                    continue;
                // 接缝检查
                if (vertices[i0].seam != vertices[i1].seam)
                    continue;
                // 翻折检查
                if (vertices[i0].foldover != vertices[i1].foldover)
                    continue;
                // 如果应保留边界
                if (preserveBorders && vertices[i0].border)
                    continue;
                // 如果应保留接缝
                if (PreserveSeams && vertices[i0].seam)
                    continue;
                // 如果应保留翻折
                if (preserveFoldovers && vertices[i0].foldover)
                    continue;

                // 计算要折叠到的顶点
                CalculateError(ref vertices[i0], ref vertices[i1], out p, out pIndex);
                deleted0.Resize(vertices[i0].tcount); // 暂时存储法线
                deleted1.Resize(vertices[i1].tcount); // 暂时存储法线

                // 如果翻转则不移除
                if (Flipped(ref p, i0, i1, ref vertices[i0], deleted0.Data))
                    continue;
                if (Flipped(ref p, i1, i0, ref vertices[i1], deleted1.Data))
                    continue;

                int ia0 = attributeIndexArr[edgeIndex];

                // 没有翻转，所以移除边
                vertices[i0].p = p;
                vertices[i0].q += vertices[i1].q;

                if (pIndex == 1)
                {
                    // 将顶点属性从ia1移动到ia0
                    int ia1 = attributeIndexArr[nextEdgeIndex];
                    MoveVertexAttributes(ia0, ia1);
                }
                else if (pIndex == 2)
                {
                    // 将顶点属性ia0和ia1合并到ia0
                    int ia1 = attributeIndexArr[nextEdgeIndex];
                    MergeVertexAttributes(ia0, ia1);
                }

                if (vertices[i0].seam)
                {
                    ia0 = -1;
                }

                int tstart = refs.Length;
                UpdateTriangles(i0, ia0, ref vertices[i0], deleted0, ref deletedTris);
                UpdateTriangles(i0, ia0, ref vertices[i1], deleted1, ref deletedTris);

                int tcount = refs.Length - tstart;
                if (tcount <= vertices[i0].tcount)
                {
                    // 节省内存
                    if (tcount > 0)
                    {
                        var refsArr = refs.Data;
                        Array.Copy(refsArr, tstart, refsArr, vertices[i0].tstart, tcount);
                    }
                }
                else
                {
                    // 追加
                    vertices[i0].tstart = tstart;
                }

                vertices[i0].tcount = tcount;
                --remainingVertices;
                break;
            }

            // 检查是否已经完成
            if ((startTrisCount - deletedTris) <= targetTrisCount && remainingVertices < maxVertexCount)
                break;
        }
    }

    /// <summary>
    /// 压缩三角形，计算边误差并构建引用列表。
    /// </summary>
    /// <param name="iteration">迭代索引。</param>
    private void UpdateMesh(int iteration)
    {
        var triangles = this.triangles.Data;
        var vertices = this.vertices.Data;

        int triangleCount = this.triangles.Length;
        int vertexCount = this.vertices.Length;
        if (iteration > 0) // 压缩三角形
        {
            int dst = 0;
            for (int i = 0; i < triangleCount; i++)
            {
                if (!triangles[i].deleted)
                {
                    if (dst != i)
                    {
                        triangles[dst] = triangles[i];
                    }
                    dst++;
                }
            }
            this.triangles.Resize(dst);
            triangles = this.triangles.Data;
            triangleCount = dst;
        }

        UpdateReferences();

        // 标识边界：vertices[].border=0,1
        if (iteration == 0)
        {
            var refs = this.refs.Data;

            var vcount = new List<int>(8);
            var vids = new List<int>(8);
            int vsize = 0;
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i].border = false;
                vertices[i].seam = false;
                vertices[i].foldover = false;
            }

            int ofs;
            int id;
            int borderVertexCount = 0;
            double borderMinX = double.MaxValue;
            double borderMaxX = double.MinValue;
            for (int i = 0; i < vertexCount; i++)
            {
                int tstart = vertices[i].tstart;
                int tcount = vertices[i].tcount;
                vcount.Clear();
                vids.Clear();
                vsize = 0;

                for (int j = 0; j < tcount; j++)
                {
                    int tid = refs[tstart + j].tid;
                    for (int k = 0; k < 3; k++)
                    {
                        ofs = 0;
                        id = triangles[tid][k];
                        while (ofs < vsize)
                        {
                            if (vids[ofs] == id)
                                break;

                            ++ofs;
                        }

                        if (ofs == vsize)
                        {
                            vcount.Add(1);
                            vids.Add(id);
                            ++vsize;
                        }
                        else
                        {
                            ++vcount[ofs];
                        }
                    }
                }

                for (int j = 0; j < vsize; j++)
                {
                    if (vcount[j] == 1)
                    {
                        id = vids[j];
                        vertices[id].border = true;
                        ++borderVertexCount;

                        if (enableSmartLink)
                        {
                            if (vertices[id].p.x < borderMinX)
                            {
                                borderMinX = vertices[id].p.x;
                            }

                            if (vertices[id].p.x > borderMaxX)
                            {
                                borderMaxX = vertices[id].p.x;
                            }
                        }
                    }
                }
            }

            if (enableSmartLink)
            {
                // 首先找到所有边界顶点
                var borderVertices = new BorderVertex[borderVertexCount];
                int borderIndexCount = 0;
                double borderAreaWidth = borderMaxX - borderMinX;
                for (int i = 0; i < vertexCount; i++)
                {
                    if (vertices[i].border)
                    {
                        int vertexHash = (int)((((vertices[i].p.x - borderMinX) / borderAreaWidth * 2.0) - 1.0) * int.MaxValue);
                        borderVertices[borderIndexCount] = new BorderVertex(i, vertexHash);
                        ++borderIndexCount;
                    }
                }

                // 按哈希值排序边界顶点
                Array.Sort(borderVertices, 0, borderIndexCount, BorderVertexComparer.instance);

                // 根据最大顶点链接距离计算最大哈希距离
                double vertexLinkDistance = System.Math.Sqrt(vertexLinkDistanceSqr);
                int hashMaxDistance = System.Math.Max((int)(vertexLinkDistance / borderAreaWidth * int.MaxValue), 1);

                // 然后找到相同的边界顶点并将它们绑定在一起
                for (int i = 0; i < borderIndexCount; i++)
                {
                    int myIndex = borderVertices[i].index;
                    if (myIndex == -1)
                        continue;

                    var myPoint = vertices[myIndex].p;
                    for (int j = i + 1; j < borderIndexCount; j++)
                    {
                        int otherIndex = borderVertices[j].index;
                        if (otherIndex == -1)
                            continue;
                        else if ((borderVertices[j].hash - borderVertices[i].hash) > hashMaxDistance) // There is no point to continue beyond this point
                            break;

                        var otherPoint = vertices[otherIndex].p;
                        var sqrX = (myPoint.x - otherPoint.x) * (myPoint.x - otherPoint.x);
                        var sqrY = (myPoint.y - otherPoint.y) * (myPoint.y - otherPoint.y);
                        var sqrZ = (myPoint.z - otherPoint.z) * (myPoint.z - otherPoint.z);
                        var sqrMagnitude = sqrX + sqrY + sqrZ;

                        if (sqrMagnitude <= vertexLinkDistanceSqr)
                        {
                            borderVertices[j].index = -1; // 注意：这确保了"其他"顶点不会被再次处理
                            vertices[myIndex].border = false;
                            vertices[otherIndex].border = false;

                            if (AreUVsTheSame(0, myIndex, otherIndex))
                            {
                                vertices[myIndex].foldover = true;
                                vertices[otherIndex].foldover = true;
                            }
                            else
                            {
                                vertices[myIndex].seam = true;
                                vertices[otherIndex].seam = true;
                            }

                            int otherTriangleCount = vertices[otherIndex].tcount;
                            int otherTriangleStart = vertices[otherIndex].tstart;
                            for (int k = 0; k < otherTriangleCount; k++)
                            {
                                var r = refs[otherTriangleStart + k];
                                triangles[r.tid][r.tvertex] = myIndex;
                            }
                        }
                    }
                }

                // 再次更新引用
                UpdateReferences();
            }

            // 通过平面和边误差初始化四元矩阵
            //
            // 在开始时需要 ( iteration == 0 )
            // 在简化过程中重新计算不是必需的，
            // 但对封闭网格通常会改善结果
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i].q = new SymmetricMatrix();
            }

            int v0, v1, v2;
            Vector3d n, p0, p1, p2, p10, p20, dummy;
            int dummy2;
            SymmetricMatrix sm;
            for (int i = 0; i < triangleCount; i++)
            {
                v0 = triangles[i].v0;
                v1 = triangles[i].v1;
                v2 = triangles[i].v2;

                p0 = vertices[v0].p;
                p1 = vertices[v1].p;
                p2 = vertices[v2].p;
                p10 = p1 - p0;
                p20 = p2 - p0;
                Vector3d.Cross(ref p10, ref p20, out n);
                n.Normalize();
                triangles[i].n = n;

                sm = new SymmetricMatrix(n.x, n.y, n.z, -Vector3d.Dot(ref n, ref p0));
                vertices[v0].q += sm;
                vertices[v1].q += sm;
                vertices[v2].q += sm;
            }

            for (int i = 0; i < triangleCount; i++)
            {
                // 计算边误差
                var triangle = triangles[i];
                triangles[i].err0 = CalculateError(ref vertices[triangle.v0], ref vertices[triangle.v1], out dummy, out dummy2);
                triangles[i].err1 = CalculateError(ref vertices[triangle.v1], ref vertices[triangle.v2], out dummy, out dummy2);
                triangles[i].err2 = CalculateError(ref vertices[triangle.v2], ref vertices[triangle.v0], out dummy, out dummy2);
                triangles[i].err3 = MathHelper.Min(triangles[i].err0, triangles[i].err1, triangles[i].err2);
            }
        }
    }

    /// <summary>
    /// 更新顶点的引用列表，用于跟踪每个顶点被哪些三角形引用。
    /// 这个方法重新构建了顶点到三角形的映射关系。
    /// </summary>
    private void UpdateReferences()
    {
        int triangleCount = this.triangles.Length;
        int vertexCount = this.vertices.Length;
        var triangles = this.triangles.Data;
        var vertices = this.vertices.Data;

        // 初始化引用ID列表
        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i].tstart = 0;
            vertices[i].tcount = 0;
        }

        // 计算每个顶点被多少个三角形引用
        for (int i = 0; i < triangleCount; i++)
        {
            ++vertices[triangles[i].v0].tcount;
            ++vertices[triangles[i].v1].tcount;
            ++vertices[triangles[i].v2].tcount;
        }

        int tstart = 0;
        remainingVertices = 0;
        // 计算每个顶点的起始位置，并重置计数器
        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i].tstart = tstart;
            if (vertices[i].tcount > 0)
            {
                tstart += vertices[i].tcount;
                vertices[i].tcount = 0;
                ++remainingVertices;
            }
        }

        // 写入引用信息
        this.refs.Resize(tstart);
        var refs = this.refs.Data;
        for (int i = 0; i < triangleCount; i++)
        {
            int v0 = triangles[i].v0;
            int v1 = triangles[i].v1;
            int v2 = triangles[i].v2;
            int start0 = vertices[v0].tstart;
            int count0 = vertices[v0].tcount;
            int start1 = vertices[v1].tstart;
            int count1 = vertices[v1].tcount;
            int start2 = vertices[v2].tstart;
            int count2 = vertices[v2].tcount;

            // 设置引用关系：三角形索引和顶点在三角形中的位置
            refs[start0 + count0].Set(i, 0);
            refs[start1 + count1].Set(i, 1);
            refs[start2 + count2].Set(i, 2);

            ++vertices[v0].tcount;
            ++vertices[v1].tcount;
            ++vertices[v2].tcount;
        }
    }

    /// <summary>
    /// 在退出前最终压缩网格。
    /// </summary>
    private void CompactMesh()
    {
        int dst = 0;
        var vertices = this.vertices.Data;
        int vertexCount = this.vertices.Length;
        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i].tcount = 0;
        }

        var vertNormals = this.vertNormals != null ? this.vertNormals.Data : null;
        var vertTangents = this.vertTangents != null ? this.vertTangents.Data : null;
        var vertUV2D = this.vertUV2D != null ? this.vertUV2D.Data : null;
        var vertUV3D = this.vertUV3D != null ? this.vertUV3D.Data : null;
        var vertUV4D = this.vertUV4D != null ? this.vertUV4D.Data : null;
        var vertColors = this.vertColors != null ? this.vertColors.Data : null;
        var vertBoneWeights = this.vertBoneWeights != null ? this.vertBoneWeights.Data : null;

        var triangles = this.triangles.Data;
        int triangleCount = this.triangles.Length;
        for (int i = 0; i < triangleCount; i++)
        {
            var triangle = triangles[i];
            if (!triangle.deleted)
            {
                if (triangle.va0 != triangle.v0)
                {
                    int iDest = triangle.va0;
                    int iSrc = triangle.v0;
                    vertices[iDest].p = vertices[iSrc].p;
                    if (vertBoneWeights != null)
                    {
                        vertBoneWeights[iDest] = vertBoneWeights[iSrc];
                    }
                    triangle.v0 = triangle.va0;
                }
                if (triangle.va1 != triangle.v1)
                {
                    int iDest = triangle.va1;
                    int iSrc = triangle.v1;
                    vertices[iDest].p = vertices[iSrc].p;
                    if (vertBoneWeights != null)
                    {
                        vertBoneWeights[iDest] = vertBoneWeights[iSrc];
                    }
                    triangle.v1 = triangle.va1;
                }
                if (triangle.va2 != triangle.v2)
                {
                    int iDest = triangle.va2;
                    int iSrc = triangle.v2;
                    vertices[iDest].p = vertices[iSrc].p;
                    if (vertBoneWeights != null)
                    {
                        vertBoneWeights[iDest] = vertBoneWeights[iSrc];
                    }
                    triangle.v2 = triangle.va2;
                }

                triangles[dst++] = triangle;

                vertices[triangle.v0].tcount = 1;
                vertices[triangle.v1].tcount = 1;
                vertices[triangle.v2].tcount = 1;
            }
        }

        triangleCount = dst;
        this.triangles.Resize(triangleCount);
        triangles = this.triangles.Data;

        dst = 0;
        for (int i = 0; i < vertexCount; i++)
        {
            var vert = vertices[i];
            if (vert.tcount > 0)
            {
                vert.tstart = dst;
                vertices[i] = vert;

                if (dst != i)
                {
                    vertices[dst].p = vert.p;
                    if (vertNormals != null) vertNormals[dst] = vertNormals[i];
                    if (vertTangents != null) vertTangents[dst] = vertTangents[i];
                    if (vertUV2D != null)
                    {
                        for (int j = 0; j < SimpleMesh.UVChannelCount; j++)
                        {
                            var vertUV = vertUV2D[j];
                            if (vertUV != null)
                            {
                                vertUV[dst] = vertUV[i];
                            }
                        }
                    }
                    if (vertUV3D != null)
                    {
                        for (int j = 0; j < SimpleMesh.UVChannelCount; j++)
                        {
                            var vertUV = vertUV3D[j];
                            if (vertUV != null)
                            {
                                vertUV[dst] = vertUV[i];
                            }
                        }
                    }
                    if (vertUV4D != null)
                    {
                        for (int j = 0; j < SimpleMesh.UVChannelCount; j++)
                        {
                            var vertUV = vertUV4D[j];
                            if (vertUV != null)
                            {
                                vertUV[dst] = vertUV[i];
                            }
                        }
                    }
                    if (vertColors != null) vertColors[dst] = vertColors[i];
                    if (vertBoneWeights != null) vertBoneWeights[dst] = vertBoneWeights[i];
                }
                ++dst;
            }
        }

        for (int i = 0; i < triangleCount; i++)
        {
            var triangle = triangles[i];
            triangle.v0 = vertices[triangle.v0].tstart;
            triangle.v1 = vertices[triangle.v1].tstart;
            triangle.v2 = vertices[triangle.v2].tstart;
            triangles[i] = triangle;
        }

        vertexCount = dst;
        this.vertices.Resize(vertexCount);
        if (vertNormals != null) this.vertNormals!.Resize(vertexCount, true);
        if (vertTangents != null) this.vertTangents!.Resize(vertexCount, true);
        if (vertUV2D != null) this.vertUV2D!.Resize(vertexCount, true);
        if (vertUV3D != null) this.vertUV3D!.Resize(vertexCount, true);
        if (vertUV4D != null) this.vertUV4D!.Resize(vertexCount, true);
        if (vertColors != null) this.vertColors!.Resize(vertexCount, true);
        if (vertBoneWeights != null) this.vertBoneWeights!.Resize(vertexCount, true);
    }

    /// <summary>
    /// 使用原始网格初始化算法。
    /// </summary>
    /// <param name="mesh">网格。</param>
    public override void Initialize(SimpleMesh mesh)
    {
        if (mesh == null)
            throw new ArgumentNullException("mesh");

        int meshSubMeshCount = mesh.SubMeshCount;
        int meshTriangleCount = mesh.TriangleCount;
        var meshVertices = mesh.Vertices;
        var meshNormals = mesh.Normals;
        var meshTangents = mesh.Tangents;
        var meshColors = mesh.Colors;
        var meshBoneWeights = mesh.BoneWeights;
        subMeshCount = meshSubMeshCount;

        vertices.Resize(meshVertices.Length);
        var vertArr = vertices.Data;
        for (int i = 0; i < meshVertices.Length; i++)
        {
            vertArr[i] = new Vertex(meshVertices[i]);
        }

        triangles.Resize(meshTriangleCount);
        var trisArr = triangles.Data;
        int triangleIndex = 0;
        for (int subMeshIndex = 0; subMeshIndex < meshSubMeshCount; subMeshIndex++)
        {
            int[] subMeshIndices = mesh.GetIndices(subMeshIndex);
            int subMeshTriangleCount = subMeshIndices.Length / 3;
            for (int i = 0; i < subMeshTriangleCount; i++)
            {
                int offset = i * 3;
                int v0 = subMeshIndices[offset];
                int v1 = subMeshIndices[offset + 1];
                int v2 = subMeshIndices[offset + 2];
                trisArr[triangleIndex++] = new Triangle(v0, v1, v2, subMeshIndex);
            }
        }

        vertNormals = InitializeVertexAttribute(meshNormals, "normals");
        vertTangents = InitializeVertexAttribute(meshTangents, "tangents");
        vertColors = InitializeVertexAttribute(meshColors, "colors");
        vertBoneWeights = InitializeVertexAttribute(meshBoneWeights, "boneWeights");

        for (int i = 0; i < SimpleMesh.UVChannelCount; i++)
        {
            int uvDim = mesh.GetUVDimension(i);
            string uvAttributeName = string.Format("uv{0}", i);
            if (uvDim == 2)
            {
                if (vertUV2D == null)
                    vertUV2D = new UVChannels<Vector2>();

                var uvs = mesh.GetUVs2D(i);
                vertUV2D[i] = InitializeVertexAttribute(uvs, uvAttributeName);
            }
            else if (uvDim == 3)
            {
                if (vertUV3D == null)
                    vertUV3D = new UVChannels<Vector3>();

                var uvs = mesh.GetUVs3D(i);
                vertUV3D[i] = InitializeVertexAttribute(uvs, uvAttributeName);
            }
            else if (uvDim == 4)
            {
                if (vertUV4D == null)
                    vertUV4D = new UVChannels<Vector4>();

                var uvs = mesh.GetUVs4D(i);
                vertUV4D[i] = InitializeVertexAttribute(uvs, uvAttributeName);
            }
        }
    }

    /// <summary>
    /// 简化网格。
    /// </summary>
    /// <param name="targetTrisCount">目标三角形数量。</param>
    public override void DecimateMesh(int targetTrisCount)
    {
        // 验证目标三角形数量参数
        if (targetTrisCount < 0)
            throw new ArgumentOutOfRangeException("targetTrisCount");

        // 初始化变量
        int deletedTris = 0; // 已删除的三角形数量
        ResizableArray<bool> deleted0 = new ResizableArray<bool>(20); // 临时数组，存储第一个顶点的删除状态
        ResizableArray<bool> deleted1 = new ResizableArray<bool>(20); // 临时数组，存储第二个顶点的删除状态
        var triangles = this.triangles.Data; // 三角形数组
        int triangleCount = this.triangles.Length; // 当前三角形数量
        int startTrisCount = triangleCount; // 初始三角形数量
        var vertices = this.vertices.Data; // 顶点数组

        // 获取最大顶点数量限制
        int maxVertexCount = base.MaxVertexCount;
        if (maxVertexCount <= 0)
            maxVertexCount = int.MaxValue;

        // 迭代简化网格，直到达到最大迭代次数
        for (int iteration = 0; iteration < maxIterationCount; iteration++)
        {
            // 报告当前状态
            ReportStatus(iteration, startTrisCount, (startTrisCount - deletedTris), targetTrisCount);

            // 检查是否已达到目标三角形数量和顶点数量限制
            if ((startTrisCount - deletedTris) <= targetTrisCount && remainingVertices < maxVertexCount)
                break;

            // 每5次迭代更新一次网格结构
            if ((iteration % 5) == 0)
            {
                UpdateMesh(iteration);
                triangles = this.triangles.Data;
                triangleCount = this.triangles.Length;
                vertices = this.vertices.Data;
            }

            // 清除所有三角形的脏标志
            for (int i = 0; i < triangleCount; i++)
            {
                triangles[i].dirty = false;
            }

            // 计算当前迭代的误差阈值
            // 所有边误差低于阈值的三角形将被移除
            // 以下数值对大多数模型效果良好
            // 如果效果不佳，可尝试调整3个参数
            double threshold = 0.000000001 * System.Math.Pow(iteration + 3, agressiveness);

            // 详细日志输出
            if (Verbose && (iteration % 5) == 0)
            {
                _logger?.LogDebug("迭代 {Iteration} - 三角形 {TriangleCount} 阈值 {Threshold}",
                    iteration, startTrisCount - deletedTris, threshold);
            }

            // 移除顶点并标记删除的三角形
            RemoveVertexPass(startTrisCount, targetTrisCount, threshold, deleted0, deleted1, ref deletedTris);
        }

        // 压缩网格，移除未使用的顶点和三角形
        CompactMesh();
    }

    /// <summary>
    /// 在不损失任何质量的情况下简化网格。
    /// 该方法尝试移除所有误差低于阈值的边，以实现无损简化。
    /// </summary>
    public override void DecimateMeshLossless()
    {
        // 初始化已删除三角形计数器
        int deletedTris = 0;

        // 临时数组，用于存储第一个顶点相关的删除状态
        ResizableArray<bool> deleted0 = new ResizableArray<bool>(0);

        // 临时数组，用于存储第二个顶点相关的删除状态
        ResizableArray<bool> deleted1 = new ResizableArray<bool>(0);

        // 获取三角形数组引用
        var triangles = this.triangles.Data;

        // 当前三角形数量
        int triangleCount = this.triangles.Length;

        // 初始三角形数量
        int startTrisCount = triangleCount;

        // 获取顶点数组引用
        var vertices = this.vertices.Data;

        // 报告初始状态
        ReportStatus(0, startTrisCount, startTrisCount, -1);

        // 迭代简化过程，最多9999次迭代
        for (int iteration = 0; iteration < 9999; iteration++)
        {
            // 持续更新网格结构和边误差
            UpdateMesh(iteration);
            triangles = this.triangles.Data;
            triangleCount = this.triangles.Length;
            vertices = this.vertices.Data;

            // 报告当前迭代状态
            ReportStatus(iteration, startTrisCount, triangleCount, -1);

            // 清除所有三角形的脏标志，为新一轮处理做准备
            for (int i = 0; i < triangleCount; i++)
            {
                triangles[i].dirty = false;
            }

            // 设置误差阈值，所有边误差低于此阈值的三角形将被移除
            // 此数值对大多数模型效果良好
            // 如果效果不佳，可尝试调整相关参数
            double threshold = DoubleEpsilon;

            // 如果启用详细日志，输出当前迭代信息
            if (Verbose)
            {
                _logger?.LogDebug("无损迭代 {Iteration}", iteration);
            }

            // 执行顶点移除过程，标记将被删除的三角形
            RemoveVertexPass(startTrisCount, 0, threshold, deleted0, deleted1, ref deletedTris);

            // 如果没有删除任何三角形，说明已达到无损简化极限，退出循环
            if (deletedTris <= 0)
                break;

            // 重置删除计数器，为下一轮迭代做准备
            deletedTris = 0;
        }

        // 压缩网格，移除未使用的顶点和三角形
        CompactMesh();
    }

    /// <summary>
    /// 返回结果网格。
    /// </summary>
    /// <returns>结果网格。</returns>
    public override SimpleMesh ToMesh()
    {
        // 获取顶点和三角形数量
        int vertexCount = this.vertices.Length;
        int triangleCount = this.triangles.Length;
        // 创建新的顶点数组和子网格索引数组
        var vertices = new Vector3d[vertexCount];
        var indices = new int[subMeshCount][];

        // 复制顶点位置数据
        var vertArr = this.vertices.Data;
        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i] = vertArr[i].p;
        }

        // 首先获取子网格偏移量
        var triArr = this.triangles.Data;
        int[] subMeshOffsets = new int[subMeshCount];
        int lastSubMeshOffset = -1;
        for (int i = 0; i < triangleCount; i++)
        {
            var triangle = triArr[i];
            if (triangle.subMeshIndex != lastSubMeshOffset)
            {
                for (int j = lastSubMeshOffset + 1; j < triangle.subMeshIndex; j++)
                {
                    subMeshOffsets[j] = i;
                }
                subMeshOffsets[triangle.subMeshIndex] = i;
                lastSubMeshOffset = triangle.subMeshIndex;
            }
        }
        for (int i = lastSubMeshOffset + 1; i < subMeshCount; i++)
        {
            subMeshOffsets[i] = triangleCount;
        }

        // 然后设置子网格
        for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
        {
            int startOffset = subMeshOffsets[subMeshIndex];
            if (startOffset < triangleCount)
            {
                // 计算子网格的结束偏移量
                int endOffset = (subMeshIndex + 1) < subMeshCount ? subMeshOffsets[subMeshIndex + 1] : triangleCount;

                // 计算子网格中的三角形数量
                int subMeshTriangleCount = endOffset - startOffset;

                if (subMeshTriangleCount < 0) subMeshTriangleCount = 0;

                // 创建子网格索引数组，每个三角形需要3个索引
                int[] subMeshIndices = new int[subMeshTriangleCount * 3];

                // 填充子网格索引数组
                for (int triangleIndex = startOffset; triangleIndex < endOffset; triangleIndex++)
                {
                    var triangle = triArr[triangleIndex];
                    int offset = (triangleIndex - startOffset) * 3;
                    subMeshIndices[offset] = triangle.v0;
                    subMeshIndices[offset + 1] = triangle.v1;
                    subMeshIndices[offset + 2] = triangle.v2;
                }

                indices[subMeshIndex] = subMeshIndices;
            }
            else
            {
                // 该网格已经没有任何三角形了
                indices[subMeshIndex] = [];
            }
        }

        // 创建新的网格对象
        SimpleMesh newMesh = new SimpleMesh(vertices, indices);

        // 设置顶点法线
        if (vertNormals != null)
        {
            newMesh.Normals = vertNormals.Data;
        }
        // 设置顶点切线
        if (vertTangents != null)
        {
            newMesh.Tangents = vertTangents.Data;
        }
        // 设置顶点颜色
        if (vertColors != null)
        {
            newMesh.Colors = vertColors.Data;
        }
        // 设置骨骼权重
        if (vertBoneWeights != null)
        {
            newMesh.BoneWeights = vertBoneWeights.Data;
        }

        // 设置2D UV坐标
        if (vertUV2D != null)
        {
            for (int i = 0; i < SimpleMesh.UVChannelCount; i++)
            {
                if (vertUV2D[i] != null)
                {
                    var uvSet = vertUV2D[i]!.Data;
                    newMesh.SetUVs(i, uvSet);
                }
            }
        }

        // 设置3D UV坐标
        if (vertUV3D != null)
        {
            for (int i = 0; i < SimpleMesh.UVChannelCount; i++)
            {
                if (vertUV3D[i] != null)
                {
                    var uvSet = vertUV3D[i]!.Data;
                    newMesh.SetUVs(i, uvSet);
                }
            }
        }

        // 设置4D UV坐标
        if (vertUV4D != null)
        {
            for (int i = 0; i < SimpleMesh.UVChannelCount; i++)
            {
                if (vertUV4D[i] != null)
                {
                    var uvSet = vertUV4D[i]!.Data;
                    newMesh.SetUVs(i, uvSet);
                }
            }
        }

        return newMesh;
    }

    /// <summary>
    /// 使用网格初始化算法 - 支持有纹理和无纹理的网格。
    /// </summary>
    /// <param name="mesh">网格对象（IMesh接口）。</param>
    public override void Initialize(IMesh mesh)
    {
        if (mesh == null)
            throw new ArgumentNullException(nameof(mesh));

        // 检查是否有纹理数据
        bool hasTexture = mesh.HasTexture;

        // 按材质分组面,确定子网格数量
        var facesByMaterial = new Dictionary<int, List<int>>();
        for (int i = 0; i < mesh.Faces.Count; i++)
        {
            var face = mesh.Faces[i];
            int materialIndex = hasTexture ? face.MaterialIndex : 0;
            if (!facesByMaterial.ContainsKey(materialIndex))
            {
                facesByMaterial[materialIndex] = new List<int>();
            }
            facesByMaterial[materialIndex].Add(i);
        }

        subMeshCount = facesByMaterial.Count;

        List<Vector3d> expandedVertices;
        List<Vector2> expandedUVs;
        List<List<int>> materialIndices;

        if (hasTexture)
        {
            // 有纹理：使用字典存储唯一的 (顶点索引, 纹理索引) 组合
            var vertexMap = new Dictionary<(int vertexIndex, int textureIndex), int>();
            expandedVertices = new List<Vector3d>();
            expandedUVs = new List<Vector2>();

            // 按材质索引排序的索引数组
            materialIndices = new List<List<int>>();
            foreach (var _ in facesByMaterial.OrderBy(kvp => kvp.Key))
            {
                materialIndices.Add(new List<int>());
            }

            // 展开顶点并构建映射
            var sortedMaterials = facesByMaterial.OrderBy(kvp => kvp.Key).ToList();

            for (int matIdx = 0; matIdx < sortedMaterials.Count; matIdx++)
            {
                var materialFaces = sortedMaterials[matIdx].Value;

                foreach (var faceIndex in materialFaces)
                {
                    var face = mesh.Faces[faceIndex];

                    var faceKeys = new[] {
                        (face.IndexA, face.TextureIndexA),
                        (face.IndexB, face.TextureIndexB),
                        (face.IndexC, face.TextureIndexC)
                    };

                    foreach (var (vertexIndex, textureIndex) in faceKeys)
                    {
                        if (!vertexMap.ContainsKey((vertexIndex, textureIndex)))
                        {
                            var vertex = mesh.Vertices[vertexIndex];
                            expandedVertices.Add(new Vector3d(vertex.X, vertex.Y, vertex.Z));

                            if (textureIndex >= 0 && textureIndex < mesh.TextureVertices!.Count)
                            {
                                var uv = mesh.TextureVertices[textureIndex];
                                expandedUVs.Add(new Vector2((float)uv.X, (float)uv.Y));
                            }
                            else
                            {
                                expandedUVs.Add(new Vector2(0f, 0f));
                            }

                            vertexMap[(vertexIndex, textureIndex)] = expandedVertices.Count - 1;
                        }

                        materialIndices[matIdx].Add(vertexMap[(vertexIndex, textureIndex)]);
                    }
                }
            }
        }
        else
        {
            // 无纹理：直接使用顶点，不需要展开
            expandedVertices = new List<Vector3d>();
            expandedUVs = new List<Vector2>();
            materialIndices = new List<List<int>>();

            // 收集所有顶点
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                var vertex = mesh.Vertices[i];
                expandedVertices.Add(new Vector3d(vertex.X, vertex.Y, vertex.Z));
            }

            // 按材质组织索引
            var sortedMaterials = facesByMaterial.OrderBy(kvp => kvp.Key).ToList();
            for (int matIdx = 0; matIdx < sortedMaterials.Count; matIdx++)
            {
                materialIndices.Add(new List<int>());
                var materialFaces = sortedMaterials[matIdx].Value;

                foreach (var faceIndex in materialFaces)
                {
                    var face = mesh.Faces[faceIndex];
                    materialIndices[matIdx].Add(face.IndexA);
                    materialIndices[matIdx].Add(face.IndexB);
                    materialIndices[matIdx].Add(face.IndexC);
                }
            }
        }

        // 初始化顶点数组
        vertices.Resize(expandedVertices.Count);
        var vertArr = vertices.Data;
        for (int i = 0; i < expandedVertices.Count; i++)
        {
            vertArr[i] = new Vertex(expandedVertices[i]);
        }

        // 初始化三角形数组
        int totalTriangles = mesh.Faces.Count;
        triangles.Resize(totalTriangles);
        var trisArr = triangles.Data;
        int triangleIndex = 0;

        for (int subMeshIndex = 0; subMeshIndex < materialIndices.Count; subMeshIndex++)
        {
            var indices = materialIndices[subMeshIndex];
            for (int i = 0; i < indices.Count; i += 3)
            {
                int v0 = indices[i];
                int v1 = indices[i + 1];
                int v2 = indices[i + 2];
                trisArr[triangleIndex++] = new Triangle(v0, v1, v2, subMeshIndex);
            }
        }

        // 初始化 UV（仅当有纹理时）
        if (hasTexture && expandedUVs.Count > 0 && expandedUVs.Count == expandedVertices.Count)
        {
            vertUV2D = new UVChannels<Vector2>();
            var uvChannel = new ResizableArray<Vector2>(expandedUVs.Count, expandedUVs.Count);
            for (int i = 0; i < expandedUVs.Count; i++)
            {
                uvChannel.Data[i] = expandedUVs[i];
            }
            vertUV2D[0] = uvChannel;
        }
        else
        {
            vertUV2D = null;
        }

        // 不包含法线、切线、颜色和骨骼权重
        vertNormals = null;
        vertTangents = null;
        vertColors = null;
        vertBoneWeights = null;
        vertUV3D = null;
        vertUV4D = null;
    }

    /// <summary>
    /// 将简化后的网格转换为IMesh - 根据原始网格是否有纹理返回Mesh或MeshT。
    /// </summary>
    /// <param name="originalMesh">原始网格,用于保留材质信息和判断网格类型。</param>
    /// <returns>简化后的网格。</returns>
    public override IMesh ToIMesh(IMesh? originalMesh = null)
    {
        // 判断是否需要纹理支持
        bool hasTexture = originalMesh?.HasTexture ?? (vertUV2D != null && vertUV2D[0] != null);

        // 获取顶点和三角形数量
        int vertexCount = this.vertices.Length;
        int triangleCount = this.triangles.Length;

        // 转换顶点 - 从 Vector3d 转换为 Vertex3
        var vertices = new List<Vertex3>(vertexCount);
        var vertArr = this.vertices.Data;
        for (int i = 0; i < vertexCount; i++)
        {
            var v = vertArr[i].p;
            vertices.Add(new Vertex3(v.x, v.y, v.z));
        }

        if (hasTexture)
        {
            // 返回带纹理的网格 (MeshT)
            // 转换纹理顶点 (UV坐标)
            var textureVertices = new List<Vertex2>();
            if (vertUV2D != null && vertUV2D[0] != null)
            {
                var uvData = vertUV2D[0]!.Data;
                for (int i = 0; i < vertexCount; i++)
                {
                    textureVertices.Add(new Vertex2(uvData[i].x, uvData[i].y));
                }
            }
            else
            {
                // 如果没有 UV,创建默认 UV
                for (int i = 0; i < vertexCount; i++)
                {
                    textureVertices.Add(new Vertex2(0.0, 0.0));
                }
            }

            // 转换面 - 按子网格组织
            var faces = new List<Face>();
            var triArr = this.triangles.Data;

            // 计算子网格偏移量
            int[] subMeshOffsets = new int[subMeshCount];
            int lastSubMeshOffset = -1;
            for (int i = 0; i < triangleCount; i++)
            {
                var triangle = triArr[i];
                if (triangle.subMeshIndex != lastSubMeshOffset)
                {
                    for (int j = lastSubMeshOffset + 1; j < triangle.subMeshIndex; j++)
                    {
                        subMeshOffsets[j] = i;
                    }
                    subMeshOffsets[triangle.subMeshIndex] = i;
                    lastSubMeshOffset = triangle.subMeshIndex;
                }
            }
            for (int i = lastSubMeshOffset + 1; i < subMeshCount; i++)
            {
                subMeshOffsets[i] = triangleCount;
            }

            // 为每个子网格创建面
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                int startOffset = subMeshOffsets[subMeshIndex];
                if (startOffset < triangleCount)
                {
                    int endOffset = (subMeshIndex + 1) < subMeshCount ? subMeshOffsets[subMeshIndex + 1] : triangleCount;

                    for (int triangleIndex = startOffset; triangleIndex < endOffset; triangleIndex++)
                    {
                        var triangle = triArr[triangleIndex];

                        // MeshT 中,顶点索引和纹理索引是分开的,这里我们使用相同的索引
                        faces.Add(new Face(
                            triangle.v0, triangle.v1, triangle.v2,  // 顶点索引
                            triangle.v0, triangle.v1, triangle.v2,  // 纹理索引 (1:1映射)
                            subMeshIndex));  // 材质索引
                    }
                }
            }

            // 创建材质列表
            var materials = new List<Domain.Materials.Material>();
            if (originalMesh != null && originalMesh.Materials != null && originalMesh.Materials.Count > 0)
            {
                // 复制原始材质
                int materialsNeeded = Math.Max(subMeshCount, originalMesh.Materials.Count);
                for (int i = 0; i < materialsNeeded; i++)
                {
                    if (i < originalMesh.Materials.Count)
                    {
                        materials.Add((Domain.Materials.Material)originalMesh.Materials[i].Clone());
                    }
                    else
                    {
                        materials.Add(new Domain.Materials.Material($"Material_{i}"));
                    }
                }
            }
            else
            {
                // 创建默认材质
                for (int i = 0; i < Math.Max(1, subMeshCount); i++)
                {
                    materials.Add(new Domain.Materials.Material($"Material_{i}"));
                }
            }

            return new MeshT(vertices, textureVertices, faces, materials);
        }
        else
        {
            // 返回无纹理的网格 (Mesh)
            var faces = new List<Face>();
            var triArr = this.triangles.Data;

            for (int i = 0; i < triangleCount; i++)
            {
                var triangle = triArr[i];
                faces.Add(new Face(triangle.v0, triangle.v1, triangle.v2));
            }

            return new Mesh(vertices, faces);
        }
    }
}