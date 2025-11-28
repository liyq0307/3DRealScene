using System.Globalization;

namespace RealScene3D.Domain.Geometry;

/// <summary>
/// 四维整数向量
/// </summary>
public struct Vector4i : IEquatable<Vector4i>
{
    /// <summary>
    /// 零向量。
    /// </summary>
    public static readonly Vector4i zero = new Vector4i(0, 0, 0, 0);

    /// <summary>
    /// x 分量。
    /// </summary>
    public int x;
    /// <summary>
    /// y 分量。
    /// </summary>
    public int y;
    /// <summary>
    /// z 分量。
    /// </summary>
    public int z;
    /// <summary>
    /// w 分量。
    /// </summary>
    public int w;

    /// <summary>
    /// 获取此向量的模。
    /// </summary>
    public int Magnitude
    {
        get { return (int)System.Math.Sqrt(x * x + y * y + z * z + w * w); }
    }

    /// <summary>
    /// 获取此向量的平方模。
    /// </summary>
    public int MagnitudeSqr
    {
        get { return (x * x + y * y + z * z + w * w); }
    }

    /// <summary>
    /// 通过索引获取或设置此向量中的特定分量。
    /// </summary>
    /// <param name="index">分量索引。</param>
    public int this[int index]
    {
        get
        {
            switch (index)
            {
                case 0:
                    return x;
                case 1:
                    return y;
                case 2:
                    return z;
                case 3:
                    return w;
                default:
                    throw new IndexOutOfRangeException("Invalid Vector4i index!");
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
                case 2:
                    z = value;
                    break;
                case 3:
                    w = value;
                    break;
                default:
                    throw new IndexOutOfRangeException("Invalid Vector4i index!");
            }
        }
    }

    /// <summary>
    /// 创建一个所有分量值相同的新向量。
    /// </summary>
    /// <param name="value">值。</param>
    public Vector4i(int value)
    {
        this.x = value;
        this.y = value;
        this.z = value;
        this.w = value;
    }

    /// <summary>
    /// 创建一个新向量。
    /// </summary>
    /// <param name="x">x 值。</param>
    /// <param name="y">y 值。</param>
    /// <param name="z">z 值。</param>
    /// <param name="w">w 值。</param>
    public Vector4i(int x, int y, int z, int w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    /// <summary>
    /// 两个向量相加。
    /// </summary>
    /// <param name="a">第一个向量。</param>
    /// <param name="b">第二个向量。</param>
    /// <returns>结果向量。</returns>
    public static Vector4i operator +(Vector4i a, Vector4i b)
    {
        return new Vector4i(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
    }

    /// <summary>
    /// 两个向量相减。
    /// </summary>
    /// <param name="a">第一个向量。</param>
    /// <param name="b">第二个向量。</param>
    /// <returns>结果向量。</returns>
    public static Vector4i operator -(Vector4i a, Vector4i b)
    {
        return new Vector4i(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
    }

    /// <summary>
    /// 均匀缩放向量。
    /// </summary>
    /// <param name="a">向量。</param>
    /// <param name="d">缩放值。</param>
    /// <returns>结果向量。</returns>
    public static Vector4i operator *(Vector4i a, int d)
    {
        return new Vector4i(a.x * d, a.y * d, a.z * d, a.w * d);
    }

    /// <summary>
    /// 均匀缩放向量。
    /// </summary>
    /// <param name="d">缩放值。</param>
    /// <param name="a">向量。</param>
    /// <returns>结果向量。</returns>
    public static Vector4i operator *(int d, Vector4i a)
    {
        return new Vector4i(a.x * d, a.y * d, a.z * d, a.w * d);
    }

    /// <summary>
    /// 用浮点数除向量。
    /// </summary>
    /// <param name="a">向量。</param>
    /// <param name="d">除数浮点值。</param>
    /// <returns>结果向量。</returns>
    public static Vector4i operator /(Vector4i a, int d)
    {
        return new Vector4i(a.x / d, a.y / d, a.z / d, a.w / d);
    }

    /// <summary>
    /// 从零向量中减去该向量。
    /// </summary>
    /// <param name="a">向量。</param>
    /// <returns>结果向量。</returns>
    public static Vector4i operator -(Vector4i a)
    {
        return new Vector4i(-a.x, -a.y, -a.z, -a.w);
    }

    /// <summary>
    /// 返回两个向量是否相等。
    /// </summary>
    /// <param name="lhs">左侧向量。</param>
    /// <param name="rhs">右侧向量。</param>
    /// <returns>If equals.</returns>
    public static bool operator ==(Vector4i lhs, Vector4i rhs)
    {
        return (lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z && lhs.w == rhs.w);
    }

    /// <summary>
    /// 返回两个向量是否不相等。
    /// </summary>
    /// <param name="lhs">左侧向量。</param>
    /// <param name="rhs">右侧向量。</param>
    /// <returns>If not equals.</returns>
    public static bool operator !=(Vector4i lhs, Vector4i rhs)
    {
        return (lhs.x != rhs.x || lhs.y != rhs.y || lhs.z != rhs.z || lhs.w != rhs.w);
    }

    /// <summary>
    /// 显式将单精度向量转换为整数向量。
    /// </summary>
    /// <param name="v">单精度向量。</param>
    public static explicit operator Vector4i(Vector4 v)
    {
        return new Vector4i((int)v.x, (int)v.y, (int)v.z, (int)v.w);
    }

    /// <summary>
    /// 显式将双精度向量转换为整数向量。
    /// </summary>
    /// <param name="v">双精度向量。</param>
    public static explicit operator Vector4i(Vector4d v)
    {
        return new Vector4i((int)v.x, (int)v.y, (int)v.z, (int)v.w);
    }

    /// <summary>
    /// 设置现有向量的 x、y 和 z 分量。
    /// </summary>
    /// <param name="x">x 值。</param>
    /// <param name="y">y 值。</param>
    /// <param name="z">z 值。</param>
    /// <param name="w">w 值。</param>
    public void Set(int x, int y, int z, int w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    /// <summary>
    /// 与另一个向量按分量相乘。
    /// </summary>
    /// <param name="scale">要相乘的向量。</param>
    public void Scale(ref Vector4i scale)
    {
        x *= scale.x;
        y *= scale.y;
        z *= scale.z;
        w *= scale.w;
    }

    /// <summary>
    /// 将此向量限制在特定范围内。
    /// </summary>
    /// <param name="min">最小分量值。</param>
    /// <param name="max">最大分量值。</param>
    public void Clamp(int min, int max)
    {
        if (x < min) x = min;
        else if (x > max) x = max;

        if (y < min) y = min;
        else if (y > max) y = max;

        if (z < min) z = min;
        else if (z > max) z = max;

        if (w < min) w = min;
        else if (w > max) w = max;
    }

    /// <summary>
    /// 返回此向量的哈希码。
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        return x.GetHashCode() ^ y.GetHashCode() << 2 ^ z.GetHashCode() >> 2 ^ w.GetHashCode() >> 1;
    }

    /// <summary>
    /// 返回此向量是否等于另一个向量。
    /// </summary>
    /// <param name="other">要比较的另一个向量。</param>
    /// <returns>If equals.</returns>
    public override bool Equals(object? other)
    {
        if (!(other is Vector4i))
        {
            return false;
        }
        Vector4i vector = (Vector4i)other;
        return (x == vector.x && y == vector.y && z == vector.z && w == vector.w);
    }

    /// <summary>
    /// 返回此向量是否等于另一个向量。
    /// </summary>
    /// <param name="other">要比较的另一个向量。</param>
    /// <returns>If equals.</returns>
    public bool Equals(Vector4i other)
    {
        return (x == other.x && y == other.y && z == other.z && w == other.w);
    }

    /// <summary>
    /// 返回此向量的格式化字符串。
    /// </summary>
    /// <returns>The string.</returns>
    public override string ToString()
    {
        return string.Format("({0}, {1}, {2}, {3})",
            x.ToString(CultureInfo.InvariantCulture),
            y.ToString(CultureInfo.InvariantCulture),
            z.ToString(CultureInfo.InvariantCulture),
            w.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 返回此向量的格式化字符串。
    /// </summary>
    /// <param name="format">整数格式。</param>
    /// <returns>The string.</returns>
    public string ToString(string format)
    {
        return string.Format("({0}, {1}, {2}, {3})",
            x.ToString(format, CultureInfo.InvariantCulture),
            y.ToString(format, CultureInfo.InvariantCulture),
            z.ToString(format, CultureInfo.InvariantCulture),
            w.ToString(format, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 两个向量按分量相乘。
    /// </summary>
    /// <param name="a">第一个向量。</param>
    /// <param name="b">第二个向量。</param>
    /// <param name="result">结果向量。</param>
    public static void Scale(ref Vector4i a, ref Vector4i b, out Vector4i result)
    {
        result = new Vector4i(a.x * b.x, a.y * b.y, a.z * b.z, a.w * b.w);
    }

}