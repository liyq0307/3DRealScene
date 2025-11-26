namespace RealScene3D.MeshTiling.Geometry;

/// <summary>
/// 表示网格边的内部类
/// </summary>
internal class Edge : IEquatable<Edge>
{
    /// <summary>
    /// 第一个顶点的索引
    /// </summary>
    public readonly int V1Index;
    /// <summary>
    /// 第二个顶点的索引
    /// </summary>
    public readonly int V2Index;

    /// <summary>
    /// 使用两个顶点索引构造边
    /// </summary>
    /// <param name="v1Index">第一个顶点的索引</param>
    /// <param name="v2Index">第二个顶点的索引</param>
    public Edge(int v1Index, int v2Index)
    {
        // 保持顺序
        if (v1Index > v2Index)
        {
            V1Index = v2Index;
            V2Index = v1Index;
        }
        else
        {
            V1Index = v1Index;
            V2Index = v2Index;
        }

    }

    /// <summary>
    /// 重写Equals方法
    /// </summary>
    /// <param name="obj">要比较的对象</param>
    /// <returns>如果相等返回true，否则返回false</returns>
    public override bool Equals(object? obj)
    {
        return !ReferenceEquals(null, obj) &&
               (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((Edge)obj));
    }

    /// <summary>
    /// 重写GetHashCode方法
    /// </summary>
    /// <returns>哈希码</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(V1Index, V2Index);
    }

    /// <summary>
    /// 比较两个Edge对象是否相等
    /// </summary>
    /// <param name="other">要比较的另一个Edge对象</param>
    /// <returns>如果相等返回true，否则返回false</returns>
    public bool Equals(Edge? other)
    {
        return !ReferenceEquals(null, other) &&
               (ReferenceEquals(this, other) || V1Index == other.V1Index && V2Index == other.V2Index);
    }
}