using System.Globalization;

namespace RealScene3D.Domain.Geometry;

/// <summary>
/// 二维双精度浮点向量。
/// </summary>
public struct Vector2d : IEquatable<Vector2d>
{
    /// <summary>
    /// 零向量。
    /// </summary>
    public static readonly Vector2d zero = new Vector2d(0, 0);

    /// <summary>
    /// 向量 epsilon 值。
    /// </summary>
    public const double Epsilon = double.Epsilon;

    /// <summary>
    /// x 分量。
    /// </summary>
    public double x;
    /// <summary>
    /// y 分量。
    /// </summary>
    public double y;

    /// <summary>
    /// 获取此向量的模。
    /// </summary>
    public double Magnitude
    {
        get { return System.Math.Sqrt(x * x + y * y); }
    }

    /// <summary>
    /// 获取此向量的平方模。
    /// </summary>
    public double MagnitudeSqr
    {
        get { return (x * x + y * y); }
    }

    /// <summary>
    /// 获取此向量的归一化向量。
    /// </summary>
    public Vector2d Normalized
    {
        get
        {
            Vector2d result;
            Normalize(ref this, out result);
            return result;
        }
    }

    /// <summary>
    /// 通过索引获取或设置此向量中的特定分量。
    /// </summary>
    /// <param name="index">分量索引。</param>
    public double this[int index]
    {
        get
        {
            switch (index)
            {
                case 0:
                    return x;
                case 1:
                    return y;
                default:
                    throw new IndexOutOfRangeException("Invalid Vector2d index!");
            }
        }
        set
        {
            switch (index)
            {
                case 0:
                    x = value;
                    break;
                case 1:
                    y = value;
                    break;
                default:
                    throw new IndexOutOfRangeException("Invalid Vector2d index!");
            }
        }
    }

    /// <summary>
    /// 创建一个所有分量值相同的新向量。
    /// </summary>
    /// <param name="value">值。</param>
    public Vector2d(double value)
    {
        this.x = value;
        this.y = value;
    }

    /// <summary>
    /// 创建一个新向量。
    /// </summary>
    /// <param name="x">x 值。</param>
    /// <param name="y">y 值。</param>
    public Vector2d(double x, double y)
    {
        this.x = x;
        this.y = y;
    }

    /// <summary>
    /// 两个向量相加。
    /// </summary>
    /// <param name="a">第一个向量。</param>
    /// <param name="b">第二个向量。</param>
    /// <returns>结果向量。</returns>
    public static Vector2d operator +(Vector2d a, Vector2d b)
    {
        return new Vector2d(a.x + b.x, a.y + b.y);
    }

    /// <summary>
    /// 两个向量相减。
    /// </summary>
    /// <param name="a">第一个向量。</param>
    /// <param name="b">第二个向量。</param>
    /// <returns>结果向量。</returns>
    public static Vector2d operator -(Vector2d a, Vector2d b)
    {
        return new Vector2d(a.x - b.x, a.y - b.y);
    }

    /// <summary>
    /// 均匀缩放向量。
    /// </summary>
    /// <param name="a">向量。</param>
    /// <param name="d">缩放值。</param>
    /// <returns>结果向量。</returns>
    public static Vector2d operator *(Vector2d a, double d)
    {
        return new Vector2d(a.x * d, a.y * d);
    }

    /// <summary>
    /// 均匀缩放向量。
    /// </summary>
    /// <param name="d">缩放值。</param>
    /// <param name="a">向量。</param>
    /// <returns>结果向量。</returns>
    public static Vector2d operator *(double d, Vector2d a)
    {
        return new Vector2d(a.x * d, a.y * d);
    }

    /// <summary>
    /// 用浮点数除向量。
    /// </summary>
    /// <param name="a">向量。</param>
    /// <param name="d">除数浮点值。</param>
    /// <returns>结果向量。</returns>
    public static Vector2d operator /(Vector2d a, double d)
    {
        return new Vector2d(a.x / d, a.y / d);
    }

    /// <summary>
    /// 从零向量中减去该向量。
    /// </summary>
    /// <param name="a">向量。</param>
    /// <returns>结果向量。</returns>
    public static Vector2d operator -(Vector2d a)
    {
        return new Vector2d(-a.x, -a.y);
    }

    /// <summary>
    /// 返回两个向量是否相等。
    /// </summary>
    /// <param name="lhs">左侧向量。</param>
    /// <param name="rhs">右侧向量。</param>
    /// <returns>If equals.</returns>
    public static bool operator ==(Vector2d lhs, Vector2d rhs)
    {
        return (lhs - rhs).MagnitudeSqr < Epsilon;
    }

    /// <summary>
    /// 返回两个向量是否不相等。
    /// </summary>
    /// <param name="lhs">左侧向量。</param>
    /// <param name="rhs">右侧向量。</param>
    /// <returns>If not equals.</returns>
    public static bool operator !=(Vector2d lhs, Vector2d rhs)
    {
        return (lhs - rhs).MagnitudeSqr >= Epsilon;
    }

    /// <summary>
    /// 隐式将单精度向量转换为双精度向量。
    /// </summary>
    /// <param name="v">单精度向量。</param>
    public static implicit operator Vector2d(Vector2 v)
    {
        return new Vector2d(v.x, v.y);
    }

    /// <summary>
    /// 隐式将整数向量转换为双精度向量。
    /// </summary>
    /// <param name="v">整数向量。</param>
    public static implicit operator Vector2d(Vector2i v)
    {
        return new Vector2d(v.x, v.y);
    }

    /// <summary>
    /// 设置现有向量的 x 和 y 分量。
    /// </summary>
    /// <param name="x">x 值。</param>
    /// <param name="y">y 值。</param>
    public void Set(double x, double y)
    {
        this.x = x;
        this.y = y;
    }

    /// <summary>
    /// 与另一个向量按分量相乘。
    /// </summary>
    /// <param name="scale">要相乘的向量。</param>
    public void Scale(ref Vector2d scale)
    {
        x *= scale.x;
        y *= scale.y;
    }

    /// <summary>
    /// 归一化此向量。
    /// </summary>
    public void Normalize()
    {
        double mag = this.Magnitude;
        if (mag > Epsilon)
        {
            x /= mag;
            y /= mag;
        }
        else
        {
            x = y = 0;
        }
    }

    /// <summary>
    /// 将此向量限制在特定范围内。
    /// </summary>
    /// <param name="min">最小分量值。</param>
    /// <param name="max">最大分量值。</param>
    public void Clamp(double min, double max)
    {
        if (x < min) x = min;
        else if (x > max) x = max;

        if (y < min) y = min;
        else if (y > max) y = max;
    }

    /// <summary>
    /// 返回此向量的哈希码。
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        return x.GetHashCode() ^ y.GetHashCode() << 2;
    }

    /// <summary>
    /// 返回此向量是否等于另一个向量。
    /// </summary>
    /// <param name="other">要比较的另一个向量。</param>
    /// <returns>If equals.</returns>
    public override bool Equals(object? other)
    {
        if (!(other is Vector2d))
        {
            return false;
        }
        Vector2d vector = (Vector2d)other;
        return (x == vector.x && y == vector.y);
    }

    /// <summary>
    /// 返回此向量是否等于另一个向量。
    /// </summary>
    /// <param name="other">要比较的另一个向量。</param>
    /// <returns>If equals.</returns>
    public bool Equals(Vector2d other)
    {
        return (x == other.x && y == other.y);
    }

    /// <summary>
    /// 返回此向量的格式化字符串。
    /// </summary>
    /// <returns>The string.</returns>
    public override string ToString()
    {
        return string.Format("({0}, {1})",
            x.ToString("F1", CultureInfo.InvariantCulture),
            y.ToString("F1", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 返回此向量的格式化字符串。
    /// </summary>
    /// <param name="format">浮点格式。</param>
    /// <returns>The string.</returns>
    public string ToString(string format)
    {
        return string.Format("({0}, {1})",
            x.ToString(format, CultureInfo.InvariantCulture),
            y.ToString(format, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 两个向量的点积。
    /// </summary>
    /// <param name="lhs">左侧向量。</param>
    /// <param name="rhs">右侧向量。</param>
    public static double Dot(ref Vector2d lhs, ref Vector2d rhs)
    {
        return lhs.x * rhs.x + lhs.y * rhs.y;
    }

    /// <summary>
    /// 在两个向量之间执行线性插值。
    /// </summary>
    /// <param name="a">起始插值向量。</param>
    /// <param name="b">目标插值向量。</param>
    /// <param name="t">时间分数。</param>
    /// <param name="result">结果向量。</param>
    public static void Lerp(ref Vector2d a, ref Vector2d b, double t, out Vector2d result)
    {
        result = new Vector2d(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
    }

    /// <summary>
    /// 两个向量按分量相乘。
    /// </summary>
    /// <param name="a">第一个向量。</param>
    /// <param name="b">第二个向量。</param>
    /// <param name="result">结果向量。</param>
    public static void Scale(ref Vector2d a, ref Vector2d b, out Vector2d result)
    {
        result = new Vector2d(a.x * b.x, a.y * b.y);
    }

    /// <summary>
    /// 归一化向量。
    /// </summary>
    /// <param name="value">要归一化的向量。</param>
    /// <param name="result">结果归一化向量。</param>
    public static void Normalize(ref Vector2d value, out Vector2d result)
    {
        double mag = value.Magnitude;
        if (mag > Epsilon)
        {
            result = new Vector2d(value.x / mag, value.y / mag);
        }
        else
        {
            result = Vector2d.zero;
        }
    }
}