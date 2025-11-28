using System.Text.Json.Serialization;

namespace RealScene3D.Domain.Geometry;

/// <summary>
/// 3D顶点的类
/// </summary>
public class Vertex3
{
    /// <summary>
    /// X坐标
    /// </summary>
    [JsonInclude] public readonly double X;
    /// <summary>
    /// Y坐标
    /// </summary>
    [JsonInclude] public readonly double Y;
    /// <summary>
    /// Z坐标
    /// </summary>
    [JsonInclude] public readonly double Z;

    /// <summary>
    /// 使用坐标值构造Vertex3对象
    /// </summary>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <param name="z">Z坐标</param>
    public Vertex3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// 将顶点转换为字符串表示
    /// </summary>
    /// <returns>字符串表示</returns>
    public override string ToString()
    {
        return $"[({X:0.00}; {Y:0.00}; {Z:0.00})";
    }

    /// <summary>
    /// 比较两个Vertex3对象是否相等
    /// </summary>
    /// <param name="other">要比较的另一个Vertex3对象</param>
    /// <returns>如果相等返回true，否则返回false</returns>
    protected bool Equals(Vertex3 other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
    }

    /// <summary>
    /// 重写Equals方法
    /// </summary>
    /// <param name="obj">要比较的对象</param>
    /// <returns>如果相等返回true，否则返回false</returns>
    public override bool Equals(object? obj)
    {
        return !ReferenceEquals(null, obj) &&
               (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((Vertex3)obj));
    }

    /// <summary>
    /// 重写GetHashCode方法
    /// </summary>
    /// <returns>哈希码</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }

    /// <summary>
    /// 重载==运算符
    /// </summary>
    /// <param name="a">第一个顶点</param>
    /// <param name="b">第二个顶点</param>
    /// <returns>如果相等返回true</returns>
    public static bool operator ==(Vertex3 a, Vertex3 b)
    {
        return Math.Abs(a.X - b.X) < double.Epsilon && Math.Abs(a.Y - b.Y) < double.Epsilon &&
               Math.Abs(a.Z - b.Z) < double.Epsilon;
    }

    /// <summary>
    /// 重载!=运算符
    /// </summary>
    /// <param name="a">第一个顶点</param>
    /// <param name="b">第二个顶点</param>
    /// <returns>如果不相等返回true</returns>
    public static bool operator !=(Vertex3 a, Vertex3 b)
    {
        return Math.Abs(a.X - b.X) > double.Epsilon ||
               Math.Abs(a.Y - b.Y) > double.Epsilon || Math.Abs(a.Z - b.Z) > double.Epsilon;
    }

    /// <summary>
    /// 计算到另一个顶点的距离
    /// </summary>
    /// <param name="other">另一个顶点</param>
    /// <returns>距离</returns>
    public double Distance(Vertex3 other)
    {
        return Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y) + (Z - other.Z) * (Z - other.Z));
    }

    /// <summary>
    /// 重载+运算符
    /// </summary>
    /// <param name="a">第一个顶点</param>
    /// <param name="b">第二个顶点</param>
    /// <returns>相加后的顶点</returns>
    public static Vertex3 operator +(Vertex3 a, Vertex3 b)
    {
        return new Vertex3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }

    /// <summary>
    /// 重载-运算符
    /// </summary>
    /// <param name="a">第一个顶点</param>
    /// <param name="b">第二个顶点</param>
    /// <returns>相减后的顶点</returns>
    public static Vertex3 operator -(Vertex3 a, Vertex3 b)
    {
        return new Vertex3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }

    /// <summary>
    /// 重载*运算符（顶点乘以标量）
    /// </summary>
    /// <param name="a">顶点</param>
    /// <param name="b">标量</param>
    /// <returns>缩放后的顶点</returns>
    public static Vertex3 operator *(Vertex3 a, double b)
    {
        return new Vertex3(a.X * b, a.Y * b, a.Z * b);
    }

    /// <summary>
    /// 重载/运算符
    /// </summary>
    /// <param name="a">顶点</param>
    /// <param name="b">标量</param>
    /// <returns>除法后的顶点</returns>
    public static Vertex3 operator /(Vertex3 a, double b)
    {
        return new Vertex3(a.X / b, a.Y / b, a.Z / b);
    }

    /// <summary>
    /// 重载*运算符（标量乘以顶点）
    /// </summary>
    /// <param name="a">标量</param>
    /// <param name="b">顶点</param>
    /// <returns>缩放后的顶点</returns>
    public static Vertex3 operator *(double a, Vertex3 b)
    {
        return new Vertex3(a * b.X, a * b.Y, a * b.Z);
    }

    /// <summary>
    /// 计算叉积
    /// </summary>
    /// <param name="other">另一个顶点</param>
    /// <returns>叉积结果</returns>
    public Vertex3 Cross(Vertex3 other)
    {
        return new Vertex3(Y * other.Z - Z * other.Y, Z * other.X - X * other.Z, X * other.Y - Y * other.X);
    }

    /// <summary>
    /// 根据百分比在边上截取点
    /// </summary>
    /// <param name="b">边的终点</param>
    /// <param name="perc">百分比</param>
    /// <returns>截取的点</returns>
    public Vertex3 CutEdgePerc(Vertex3 b, double perc)
    {
        return new Vertex3((b.X - X) * perc + X, (b.Y - Y) * perc + Y, (b.Z - Z) * perc + Z);
    }
}