using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// 网格分割器
/// 实现递归轴对齐空间分割（Recursive Axis-Aligned BSP）
/// 支持沿 X、Y、Z 轴分割网格，并处理跨越边界的三角形
/// 完整支持 UV 纹理坐标和顶点法线的插值计算
/// </summary>
public class MeshSplitter
{
    private readonly ILogger<MeshSplitter> _logger;

    public MeshSplitter(ILogger<MeshSplitter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 分割轴枚举
    /// </summary>
    public enum SplitAxis
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    /// <summary>
    /// 三角形分类（相对于分割平面）
    /// </summary>
    private enum TriangleClassification
    {
        Left,      // 完全在左侧
        Right,     // 完全在右侧
        Spanning   // 跨越分割平面
    }

    /// <summary>
    /// 顶点数据结构 - 包含位置、UV、法线的完整顶点信息
    /// </summary>
    private class VertexData
    {
        public Vector3D Position { get; set; } = new Vector3D();
        public Vector2D? UV { get; set; }
        public Vector3D? Normal { get; set; }
    }

    /// <summary>
    /// 分割网格 - 沿指定轴和阈值分割
    /// </summary>
    /// <param name="triangles">输入三角形列表</param>
    /// <param name="materials">材质字典</param>
    /// <param name="axis">分割轴</param>
    /// <param name="threshold">分割阈值（沿轴的坐标值）</param>
    /// <returns>左侧和右侧的三角形列表及材质</returns>
    public (
        List<Triangle> leftTriangles,
        Dictionary<string, Material> leftMaterials,
        List<Triangle> rightTriangles,
        Dictionary<string, Material> rightMaterials) Split(
        List<Triangle> triangles,
        Dictionary<string, Material>? materials,
        SplitAxis axis,
        double threshold)
    {
        var leftTriangles = new List<Triangle>();
        var rightTriangles = new List<Triangle>();
        var leftMaterials = new Dictionary<string, Material>();
        var rightMaterials = new Dictionary<string, Material>();

        int splitCount = 0;
        int leftCount = 0;
        int rightCount = 0;

        foreach (var triangle in triangles)
        {
            var classification = ClassifyTriangle(triangle, axis, threshold);

            switch (classification)
            {
                case TriangleClassification.Left:
                    leftTriangles.Add(triangle);
                    leftCount++;
                    AddMaterialIfNeeded(triangle, materials, leftMaterials);
                    break;

                case TriangleClassification.Right:
                    rightTriangles.Add(triangle);
                    rightCount++;
                    AddMaterialIfNeeded(triangle, materials, rightMaterials);
                    break;

                case TriangleClassification.Spanning:
                    // 分割跨越边界的三角形
                    var (left, right) = SplitSpanningTriangle(triangle, axis, threshold);
                    leftTriangles.AddRange(left);
                    rightTriangles.AddRange(right);
                    splitCount++;
                    AddMaterialIfNeeded(triangle, materials, leftMaterials);
                    AddMaterialIfNeeded(triangle, materials, rightMaterials);
                    break;
            }
        }

        _logger.LogDebug(
            "网格分割完成：轴={Axis}, 阈值={Threshold:F3}, 左={Left}个, 右={Right}个, 分割={Split}个",
            axis, threshold, leftCount, rightCount, splitCount);

        return (leftTriangles, leftMaterials, rightTriangles, rightMaterials);
    }

    /// <summary>
    /// 分类三角形相对于分割平面的位置
    /// </summary>
    private TriangleClassification ClassifyTriangle(Triangle triangle, SplitAxis axis, double threshold)
    {
        const double epsilon = 1e-6; // 容差

        int leftCount = 0;
        int rightCount = 0;

        foreach (var vertex in triangle.Vertices)
        {
            double coord = GetCoordinate(vertex, axis);

            if (coord < threshold - epsilon)
                leftCount++;
            else if (coord > threshold + epsilon)
                rightCount++;
            else
            {
                // 顶点在分割平面上，同时计入左右
                leftCount++;
                rightCount++;
            }
        }

        // 所有顶点都在左侧
        if (leftCount == 3 && rightCount == 0)
            return TriangleClassification.Left;

        // 所有顶点都在右侧
        if (rightCount == 3 && leftCount == 0)
            return TriangleClassification.Right;

        // 跨越分割平面
        return TriangleClassification.Spanning;
    }

    /// <summary>
    /// 分割跨越边界的三角形
    /// 严格按照Obj2Tiles逻辑：避免边界重复，处理所有边界情况
    /// </summary>
    private (List<Triangle> left, List<Triangle> right) SplitSpanningTriangle(
        Triangle triangle,
        SplitAxis axis,
        double threshold)
    {
        const double epsilon = 1e-6;
        var left = new List<Triangle>();
        var right = new List<Triangle>();

        // 获取三个顶点沿分割轴的坐标
        var coords = new double[3];
        for (int i = 0; i < 3; i++)
        {
            coords[i] = GetCoordinate(triangle.Vertices[i], axis);
        }

        // 判断每个顶点的位置
        bool[] isLeft = new bool[3];
        bool[] isRight = new bool[3];
        bool[] isOnPlane = new bool[3];

        for (int i = 0; i < 3; i++)
        {
            if (Math.Abs(coords[i] - threshold) <= epsilon)
                isOnPlane[i] = true;
            else if (coords[i] < threshold)
                isLeft[i] = true;
            else
                isRight[i] = true;
        }

        int leftCount = isLeft.Count(x => x);
        int rightCount = isRight.Count(x => x);
        int onPlaneCount = isOnPlane.Count(x => x);

        // ⭐ 关键：边界情况处理（按Obj2Tiles逻辑）
        if (onPlaneCount == 2)
        {
            // 两个顶点在平面上：只添加到左侧（避免重复）
            left.Add(triangle);
            return (left, right);
        }

        if (onPlaneCount == 3)
        {
            // 三个顶点都在平面上：只添加到左侧
            left.Add(triangle);
            return (left, right);
        }

        // 标准分割情况
        if (leftCount == 1 && rightCount == 2)
        {
            SplitOneLeft(triangle, axis, threshold, left, right);
        }
        else if (leftCount == 2 && rightCount == 1)
        {
            SplitOneRight(triangle, axis, threshold, left, right);
        }
        else if (onPlaneCount == 1)
        {
            // 一个顶点在平面上
            if (leftCount == 2)
            {
                // 左侧2个，平面1个 → 只添加到左侧
                left.Add(triangle);
            }
            else if (rightCount == 2)
            {
                // 右侧2个，平面1个 → 只添加到右侧
                right.Add(triangle);
            }
            else if (leftCount == 1 && rightCount == 1)
            {
                // 左1个，右1个，平面1个 → 需要分割
                SplitWithOnePlaneVertex(triangle, axis, threshold, left, right, isLeft, isRight, isOnPlane);
            }
        }
        else
        {
            // 其他边界情况：简单分配
            if (leftCount > rightCount)
                left.Add(triangle);
            else if (rightCount > leftCount)
                right.Add(triangle);
            else
                left.Add(triangle); // 相等时默认左侧
        }

        return (left, right);
    }

    /// <summary>
    /// 处理有一个顶点在平面上的分割情况
    /// </summary>
    private void SplitWithOnePlaneVertex(
        Triangle triangle,
        SplitAxis axis,
        double threshold,
        List<Triangle> left,
        List<Triangle> right,
        bool[] isLeft,
        bool[] isRight,
        bool[] isOnPlane)
    {
        // 找到平面上的顶点、左侧顶点、右侧顶点
        int planeIdx = Array.IndexOf(isOnPlane, true);
        int leftIdx = Array.IndexOf(isLeft, true);
        int rightIdx = Array.IndexOf(isRight, true);

        var vPlane = triangle.Vertices[planeIdx];
        var vLeft = triangle.Vertices[leftIdx];
        var vRight = triangle.Vertices[rightIdx];

        var uvPlane = GetUV(triangle, planeIdx);
        var uvLeft = GetUV(triangle, leftIdx);
        var uvRight = GetUV(triangle, rightIdx);

        var nPlane = GetNormal(triangle, planeIdx);
        var nLeft = GetNormal(triangle, leftIdx);
        var nRight = GetNormal(triangle, rightIdx);

        // 计算另一个交点（左-右边的交点）
        var intersection = ComputeIntersectionFull(vLeft, vRight, uvLeft, uvRight, nLeft, nRight, axis, threshold);

        // 左侧三角形：(vLeft, vPlane, intersection)
        left.Add(CreateTriangle(
            vLeft, vPlane, intersection.Position,
            uvLeft, uvPlane, intersection.UV,
            nLeft, nPlane, intersection.Normal,
            triangle.MaterialName));

        // 右侧三角形：(vPlane, vRight, intersection)
        right.Add(CreateTriangle(
            vPlane, vRight, intersection.Position,
            uvPlane, uvRight, intersection.UV,
            nPlane, nRight, intersection.Normal,
            triangle.MaterialName));
    }

    /// <summary>
    /// 分割情况：一个顶点在左侧，两个在右侧
    /// 生成：1个左侧三角形 + 2个右侧三角形
    /// 保持正确的顶点顺序以保证法线方向一致
    /// </summary>
    private void SplitOneLeft(
        Triangle triangle,
        SplitAxis axis,
        double threshold,
        List<Triangle> left,
        List<Triangle> right)
    {
        const double epsilon = 1e-6;

        // 找到在左侧的顶点索引
        int leftIdx = -1;
        for (int i = 0; i < 3; i++)
        {
            if (GetCoordinate(triangle.Vertices[i], axis) < threshold)
            {
                leftIdx = i;
                break;
            }
        }

        // 获取顶点索引（保持顺序）
        int idx0 = leftIdx;
        int idx1 = (leftIdx + 1) % 3;
        int idx2 = (leftIdx + 2) % 3;

        // 获取顶点数据
        var v0 = triangle.Vertices[idx0]; // 左侧顶点
        var v1 = triangle.Vertices[idx1]; // 右侧顶点1
        var v2 = triangle.Vertices[idx2]; // 右侧顶点2

        var uv0 = GetUV(triangle, idx0);
        var uv1 = GetUV(triangle, idx1);
        var uv2 = GetUV(triangle, idx2);

        var n0 = GetNormal(triangle, idx0);
        var n1 = GetNormal(triangle, idx1);
        var n2 = GetNormal(triangle, idx2);

        // ⭐ 边界检查：如果两个"右侧"顶点实际上都在平面上，不分割
        double coord1 = GetCoordinate(v1, axis);
        double coord2 = GetCoordinate(v2, axis);
        if (Math.Abs(coord1 - threshold) <= epsilon && Math.Abs(coord2 - threshold) <= epsilon)
        {
            // 两个顶点在平面上，只添加到左侧
            left.Add(triangle);
            return;
        }

        // 计算两个交点（带完整顶点数据）
        var int1 = ComputeIntersectionFull(v0, v1, uv0, uv1, n0, n1, axis, threshold);
        var int2 = ComputeIntersectionFull(v0, v2, uv0, uv2, n0, n2, axis, threshold);

        // 创建左侧三角形：v0 -> int1 -> int2
        left.Add(CreateTriangle(
            v0, int1.Position, int2.Position,
            uv0, int1.UV, int2.UV,
            n0, int1.Normal, int2.Normal,
            triangle.MaterialName));

        // ⭐ 关键修复：右侧需要形成四边形 (int1, v1, v2, int2)，分成两个三角形
        // 右侧三角形1：int1 -> v1 -> v2（大三角形）
        right.Add(CreateTriangle(
            int1.Position, v1, v2,
            int1.UV, uv1, uv2,
            int1.Normal, n1, n2,
            triangle.MaterialName));

        // 右侧三角形2：int1 -> v2 -> int2（填充剩余部分）
        right.Add(CreateTriangle(
            int1.Position, v2, int2.Position,
            int1.UV, uv2, int2.UV,
            int1.Normal, n2, int2.Normal,
            triangle.MaterialName));
    }

    /// <summary>
    /// 分割情况：两个顶点在左侧，一个在右侧
    /// 生成：2个左侧三角形 + 1个右侧三角形
    /// 保持正确的顶点顺序以保证法线方向一致
    /// </summary>
    private void SplitOneRight(
        Triangle triangle,
        SplitAxis axis,
        double threshold,
        List<Triangle> left,
        List<Triangle> right)
    {
        const double epsilon = 1e-6;

        // 找到在右侧的顶点索引
        int rightIdx = -1;
        for (int i = 0; i < 3; i++)
        {
            if (GetCoordinate(triangle.Vertices[i], axis) > threshold)
            {
                rightIdx = i;
                break;
            }
        }

        // 获取顶点索引（保持顺序）
        int idx0 = rightIdx;
        int idx1 = (rightIdx + 1) % 3;
        int idx2 = (rightIdx + 2) % 3;

        // 获取顶点数据
        var v0 = triangle.Vertices[idx0]; // 右侧顶点
        var v1 = triangle.Vertices[idx1]; // 左侧顶点1
        var v2 = triangle.Vertices[idx2]; // 左侧顶点2

        var uv0 = GetUV(triangle, idx0);
        var uv1 = GetUV(triangle, idx1);
        var uv2 = GetUV(triangle, idx2);

        var n0 = GetNormal(triangle, idx0);
        var n1 = GetNormal(triangle, idx1);
        var n2 = GetNormal(triangle, idx2);

        // ⭐ 边界检查：如果两个"左侧"顶点实际上都在平面上，不分割
        double coord1 = GetCoordinate(v1, axis);
        double coord2 = GetCoordinate(v2, axis);
        if (Math.Abs(coord1 - threshold) <= epsilon && Math.Abs(coord2 - threshold) <= epsilon)
        {
            // 两个顶点在平面上，只添加到右侧
            right.Add(triangle);
            return;
        }

        // 计算两个交点（带完整顶点数据）
        var int1 = ComputeIntersectionFull(v0, v1, uv0, uv1, n0, n1, axis, threshold);
        var int2 = ComputeIntersectionFull(v0, v2, uv0, uv2, n0, n2, axis, threshold);

        // 创建右侧三角形：v0 -> int1 -> int2
        right.Add(CreateTriangle(
            v0, int1.Position, int2.Position,
            uv0, int1.UV, int2.UV,
            n0, int1.Normal, int2.Normal,
            triangle.MaterialName));

        // ⭐ 关键修复：左侧需要形成四边形 (int1, v1, v2, int2)，分成两个三角形
        // 左侧三角形1：int1 -> v1 -> v2（大三角形）
        left.Add(CreateTriangle(
            int1.Position, v1, v2,
            int1.UV, uv1, uv2,
            int1.Normal, n1, n2,
            triangle.MaterialName));

        // 左侧三角形2：int1 -> v2 -> int2（填充剩余部分）
        left.Add(CreateTriangle(
            int1.Position, v2, int2.Position,
            int1.UV, uv2, int2.UV,
            int1.Normal, n2, int2.Normal,
            triangle.MaterialName));
    }

    /// <summary>
    /// 创建三角形（包含完整的顶点数据）
    /// </summary>
    private Triangle CreateTriangle(
        Vector3D v1, Vector3D v2, Vector3D v3,
        Vector2D? uv1, Vector2D? uv2, Vector2D? uv3,
        Vector3D? n1, Vector3D? n2, Vector3D? n3,
        string? materialName)
    {
        return new Triangle
        {
            V1 = v1,
            V2 = v2,
            V3 = v3,
            UV1 = uv1,
            UV2 = uv2,
            UV3 = uv3,
            Normal1 = n1,
            Normal2 = n2,
            Normal3 = n3,
            MaterialName = materialName
        };
    }

    /// <summary>
    /// 获取三角形指定顶点的UV坐标
    /// </summary>
    private Vector2D? GetUV(Triangle triangle, int index)
    {
        return index switch
        {
            0 => triangle.UV1,
            1 => triangle.UV2,
            2 => triangle.UV3,
            _ => null
        };
    }

    /// <summary>
    /// 获取三角形指定顶点的法线
    /// </summary>
    private Vector3D? GetNormal(Triangle triangle, int index)
    {
        return index switch
        {
            0 => triangle.Normal1,
            1 => triangle.Normal2,
            2 => triangle.Normal3,
            _ => null
        };
    }

    /// <summary>
    /// 计算边与分割平面的交点（包含完整顶点数据：位置、UV、法线）
    /// 使用线性插值计算所有属性
    /// </summary>
    private VertexData ComputeIntersectionFull(
        Vector3D pos1, Vector3D pos2,
        Vector2D? uv1, Vector2D? uv2,
        Vector3D? normal1, Vector3D? normal2,
        SplitAxis axis, double threshold)
    {
        double coord1 = GetCoordinate(pos1, axis);
        double coord2 = GetCoordinate(pos2, axis);

        // 计算插值参数 t
        double t = (threshold - coord1) / (coord2 - coord1);
        t = Math.Clamp(t, 0.0, 1.0);

        var result = new VertexData
        {
            // 插值位置
            Position = new Vector3D
            {
                X = pos1.X + t * (pos2.X - pos1.X),
                Y = pos1.Y + t * (pos2.Y - pos1.Y),
                Z = pos1.Z + t * (pos2.Z - pos1.Z)
            }
        };

        // 插值UV坐标
        if (uv1 != null && uv2 != null)
        {
            result.UV = new Vector2D
            {
                U = uv1.U + t * (uv2.U - uv1.U),
                V = uv1.V + t * (uv2.V - uv1.V)
            };
        }

        // 插值法线
        if (normal1 != null && normal2 != null)
        {
            result.Normal = new Vector3D
            {
                X = normal1.X + t * (normal2.X - normal1.X),
                Y = normal1.Y + t * (normal2.Y - normal1.Y),
                Z = normal1.Z + t * (normal2.Z - normal1.Z)
            }.Normalize(); // 法线需要归一化
        }

        return result;
    }

    /// <summary>
    /// 获取顶点在指定轴上的坐标
    /// </summary>
    private double GetCoordinate(Vector3D vertex, SplitAxis axis)
    {
        return axis switch
        {
            SplitAxis.X => vertex.X,
            SplitAxis.Y => vertex.Y,
            SplitAxis.Z => vertex.Z,
            _ => throw new ArgumentException($"Invalid axis: {axis}")
        };
    }

    /// <summary>
    /// 添加材质到目标字典（如果需要）
    /// </summary>
    private void AddMaterialIfNeeded(
        Triangle triangle,
        Dictionary<string, Material>? sourceMaterials,
        Dictionary<string, Material> targetMaterials)
    {
        if (sourceMaterials == null || string.IsNullOrEmpty(triangle.MaterialName))
            return;

        if (sourceMaterials.TryGetValue(triangle.MaterialName, out var material))
        {
            if (!targetMaterials.ContainsKey(triangle.MaterialName))
            {
                targetMaterials[triangle.MaterialName] = material;
            }
        }
    }

    /// <summary>
    /// 移除未使用的顶点（可选优化）
    /// </summary>
    public List<Triangle> RemoveUnusedVertices(List<Triangle> triangles)
    {
        // 这个方法在当前实现中不是必需的
        // 因为我们直接操作三角形列表，没有独立的顶点列表
        // 如果需要优化，可以后续实现顶点去重和索引化
        return triangles;
    }

    /// <summary>
    /// 计算网格的包围盒
    /// </summary>
    public BoundingBox3D ComputeBoundingBox(List<Triangle> triangles)
    {
        if (triangles == null || triangles.Count == 0)
        {
            return new BoundingBox3D();
        }

        var bounds = new BoundingBox3D
        {
            MinX = double.MaxValue,
            MinY = double.MaxValue,
            MinZ = double.MaxValue,
            MaxX = double.MinValue,
            MaxY = double.MinValue,
            MaxZ = double.MinValue
        };

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
    /// 选择最佳分割轴（最长轴）
    /// </summary>
    public SplitAxis SelectBestAxis(BoundingBox3D bounds)
    {
        double xLen = bounds.MaxX - bounds.MinX;
        double yLen = bounds.MaxY - bounds.MinY;
        double zLen = bounds.MaxZ - bounds.MinZ;

        if (xLen >= yLen && xLen >= zLen)
            return SplitAxis.X;
        else if (yLen >= xLen && yLen >= zLen)
            return SplitAxis.Y;
        else
            return SplitAxis.Z;
    }

    /// <summary>
    /// 计算分割阈值（中点）
    /// </summary>
    public double ComputeSplitThreshold(BoundingBox3D bounds, SplitAxis axis)
    {
        return axis switch
        {
            SplitAxis.X => (bounds.MinX + bounds.MaxX) / 2.0,
            SplitAxis.Y => (bounds.MinY + bounds.MaxY) / 2.0,
            SplitAxis.Z => (bounds.MinZ + bounds.MaxZ) / 2.0,
            _ => throw new ArgumentException($"Invalid axis: {axis}")
        };
    }
}
