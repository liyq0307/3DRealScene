namespace RealScene3D.Domain.Geometry;

/// <summary>
/// 2D顶点的类
/// </summary>
public class Vertex2
{
    /// <summary>
    /// 比较两个Vertex2对象是否相等
    /// </summary>
    /// <param name="other">要比较的另一个Vertex2对象</param>
    /// <returns>如果相等返回true，否则返回false</returns>
    protected bool Equals(Vertex2 other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y);
    }

    /// <summary>
    /// 重写Equals方法
    /// </summary>
    /// <param name="obj">要比较的对象</param>
    /// <returns>如果相等返回true，否则返回false</returns>
    public override bool Equals(object? obj)
    {
        return !ReferenceEquals(null, obj) &&
               (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((Vertex2)obj));
    }

    /// <summary>
    /// 重写GetHashCode方法
    /// </summary>
    /// <returns>哈希码</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    /// <summary>
    /// 使用坐标值构造Vertex2对象
    /// </summary>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    public Vertex2(double x, double y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// X坐标
    /// </summary>
    public readonly double X;
    /// <summary>
    /// Y坐标
    /// </summary>
    public readonly double Y;

    /// <summary>
    /// 将顶点转换为字符串表示
    /// </summary>
    /// <returns>字符串表示</returns>
    public override string ToString()
    {
        return $"({X}; {Y})";
    }

    /// <summary>
    /// 重载==运算符
    /// </summary>
    /// <param name="a">第一个顶点</param>
    /// <param name="b">第二个顶点</param>
    /// <returns>如果相等返回true</returns>
    public static bool operator ==(Vertex2 a, Vertex2 b)
    {
        return Math.Abs(a.X - b.X) < double.Epsilon && Math.Abs(a.Y - b.Y) < double.Epsilon;
    }

    /// <summary>
    /// 重载!=运算符
    /// </summary>
    /// <param name="a">第一个顶点</param>
    /// <param name="b">第二个顶点</param>
    /// <returns>如果不相等返回true</returns>
    public static bool operator !=(Vertex2 a, Vertex2 b)
    {
        return Math.Abs(a.X - b.X) > double.Epsilon || Math.Abs(a.Y - b.Y) > double.Epsilon;
    }

    /// <summary>
    /// 计算到另一个顶点的距离
    /// </summary>
    /// <param name="other">另一个顶点</param>
    /// <returns>距离</returns>
    public double Distance(Vertex2 other)
    {
        return Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));
    }

    /// <summary>
    /// 根据百分比在边上截取点
    /// </summary>
    /// <param name="b">边的终点</param>
    /// <param name="perc">百分比</param>
    /// <returns>截取的点</returns>
    public Vertex2 CutEdgePerc(Vertex2 b, double perc)
    {
        return new Vertex2((b.X - X) * perc + X, (b.Y - Y) * perc + Y);
    }
}