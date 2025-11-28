using System.Globalization;

namespace RealScene3D.Domain.Geometry;

/// <summary>
/// 单精度 4D 向量。
/// </summary>
public struct Vector4 : IEquatable<Vector4>
{
    /// <summary>
    /// 零向量。
    /// </summary>
    public static readonly Vector4 zero = new Vector4(0, 0, 0, 0);

    /// <summary>
    /// 向量 epsilon。
    /// </summary>
    public const float Epsilon = 9.99999944E-11f;

    /// <summary>
    /// x 分量。
    /// </summary>
    public float x;
    /// <summary>
    /// y 分量。
    /// </summary>
    public float y;
    /// <summary>
    /// z 分量。
    /// </summary>
    public float z;
    /// <summary>
    /// w 分量。
    /// </summary>
    public float w;

    /// <summary>
    /// 获取此向量的大小。
    /// </summary>
    public float Magnitude
    {
        get { return (float)System.Math.Sqrt(x * x + y * y + z * z + w * w); }
    }

    /// <summary>
    /// 获取此向量的平方大小。
    /// </summary>
    public float MagnitudeSqr
    {
        get { return (x * x + y * y + z * z + w * w); }
    }

    /// <summary>
    /// 从此向量获取归一化向量。
    /// </summary>
    public Vector4 Normalized
    {
        get
        {
            Vector4 result;
            Normalize(ref this, out result);
            return result;
        }
    }

    /// <summary>
    /// 获取或设置此向量中特定索引处的分量。
    /// </summary>
    /// <param name="index">分量索引。</param>
    public float this[int index]
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
                    throw new IndexOutOfRangeException("Invalid Vector4 index!");
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
                    throw new IndexOutOfRangeException("Invalid Vector4 index!");
            }
        }
    }

    /// <summary>
    /// 创建一个新的向量，所有分量都使用相同的值。
    /// </summary>
    /// <param name="value">值。</param>
    public Vector4(float value)
    {
        this.x = value;
        this.y = value;
        this.z = value;
        this.w = value;
    }

    /// <summary>
    /// 创建一个新的向量。
    /// </summary>
    /// <param name="x">x 值。</param>
    /// <param name="y">y 值。</param>
    /// <param name="z">z 值。</param>
    /// <param name="w">w 值。</param>
    public Vector4(float x, float y, float z, float w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    /// <summary>
    /// 将两个向量相加。
    /// </summary>
    /// <param name="a">第一个向量。</param>
    /// <param name="b">第二个向量。</param>
    /// <returns>结果向量。</returns>
    public static Vector4 operator +(Vector4 a, Vector4 b)
    {
        return new Vector4(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
    }

    /// <summary>
    /// 将两个向量相减。
    /// </summary>
    /// <param name="a">第一个向量。</param>
    /// <param name="b">第二个向量。</param>
    /// <returns>结果向量。</returns>
    public static Vector4 operator -(Vector4 a, Vector4 b)
    {
        return new Vector4(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
    }

    /// <summary>
    /// 均匀缩放向量。
    /// </summary>
    /// <param name="a">向量。</param>
    /// <param name="d">缩放值。</param>
    /// <returns>结果向量。</returns>
    public static Vector4 operator *(Vector4 a, float d)
    {
        return new Vector4(a.x * d, a.y * d, a.z * d, a.w * d);
    }

    /// <summary>
    /// 均匀缩放向量。
    /// </summary>
    /// <param name="d">缩放值。</param>
    /// <param name="a">向量。</param>
    /// <returns>结果向量。</returns>
    public static Vector4 operator *(float d, Vector4 a)
    {
        return new Vector4(a.x * d, a.y * d, a.z * d, a.w * d);
    }

    /// <summary>
    /// 用浮点数除以向量。
    /// </summary>
    /// <param name="a">向量。</param>
    /// <param name="d">用于除法的浮点值。</param>
    /// <returns>结果向量。</returns>
    public static Vector4 operator /(Vector4 a, float d)
    {
        return new Vector4(a.x / d, a.y / d, a.z / d, a.w / d);
    }

    /// <summary>
    /// 从零向量中减去向量。
    /// </summary>
    /// <param name="a">向量。</param>
    /// <returns>结果向量。</returns>
    public static Vector4 operator -(Vector4 a)
    {
        return new Vector4(-a.x, -a.y, -a.z, -a.w);
    }

    /// <summary>
    /// 返回两个向量是否相等。
    /// </summary>
    /// <param name="lhs">左侧向量。</param>
    /// <param name="rhs">右侧向量。</param>
    /// <returns>如果相等则返回true。</returns>
    public static bool operator ==(Vector4 lhs, Vector4 rhs)
    {
        return (lhs - rhs).MagnitudeSqr < Epsilon;
    }

    /// <summary>
    /// 返回两个向量是否不相等。
    /// </summary>
    /// <param name="lhs">左侧向量。</param>
    /// <param name="rhs">右侧向量。</param>
    /// <returns>如果不相等则返回true。</returns>
    public static bool operator !=(Vector4 lhs, Vector4 rhs)
    {
        return (lhs - rhs).MagnitudeSqr >= Epsilon;
    }

    /// <summary>
    /// 显式地将双精度向量转换为单精度向量。
    /// </summary>
    /// <param name="v">双精度向量。</param>
    public static explicit operator Vector4(Vector4d v)
    {
        return new Vector4((float)v.x, (float)v.y, (float)v.z, (float)v.w);
    }

    /// <summary>
    /// 隐式地将整数向量转换为单精度向量。
    /// </summary>
    /// <param name="v">整数向量。</param>
    public static implicit operator Vector4(Vector4i v)
    {
        return new Vector4(v.x, v.y, v.z, v.w);
    }

    /// <summary>
    /// 设置现有向量的 x、y、z 和 w 分量。
    /// </summary>
    /// <param name="x">x 值。</param>
    /// <param name="y">y 值。</param>
    /// <param name="z">z 值。</param>
    /// <param name="w">w 值。</param>
    public void Set(float x, float y, float z, float w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    /// <summary>
    /// 按分量与另一个向量相乘。
    /// </summary>
    /// <param name="scale">要相乘的向量。</param>
    public void Scale(ref Vector4 scale)
    {
        x *= scale.x;
        y *= scale.y;
        z *= scale.z;
        w *= scale.w;
    }

    /// <summary>
    /// 归一化此向量。
    /// </summary>
    public void Normalize()
    {
        float mag = this.Magnitude;
        if (mag > Epsilon)
        {
            x /= mag;
            y /= mag;
            z /= mag;
            w /= mag;
        }
        else
        {
            x = y = z = w = 0;
        }
    }

    /// <summary>
    /// 将此向量限制在特定范围内。
    /// </summary>
    /// <param name="min">最小分量值。</param>
    /// <param name="max">最大分量值。</param>
    public void Clamp(float min, float max)
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
    /// <returns>哈希码。</returns>
    public override int GetHashCode()
    {
        return x.GetHashCode() ^ y.GetHashCode() << 2 ^ z.GetHashCode() >> 2 ^ w.GetHashCode() >> 1;
    }

    /// <summary>
    /// 返回此向量是否等于另一个向量。
    /// </summary>
    /// <param name="other">要比较的另一个向量。</param>
    /// <returns>如果相等则返回true。</returns>
    public override bool Equals(object? other)
    {
        if (!(other is Vector4))
        {
            return false;
        }
        Vector4 vector = (Vector4)other;
        return (x == vector.x && y == vector.y && z == vector.z && w == vector.w);
    }

    /// <summary>
    /// 返回此向量是否等于另一个向量。
    /// </summary>
    /// <param name="other">要比较的另一个向量。</param>
    /// <returns>如果相等则返回true。</returns>
    public bool Equals(Vector4 other)
    {
        return (x == other.x && y == other.y && z == other.z && w == other.w);
    }

    /// <summary>
    /// 返回此向量的格式化字符串。
    /// </summary>
    /// <returns>字符串。</returns>
    public override string ToString()
    {
        return string.Format("({0}, {1}, {2}, {3})",
            x.ToString("F1", CultureInfo.InvariantCulture),
            y.ToString("F1", CultureInfo.InvariantCulture),
            z.ToString("F1", CultureInfo.InvariantCulture),
            w.ToString("F1", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 返回此向量的格式化字符串。
    /// </summary>
    /// <param name="format">浮点数格式。</param>
    /// <returns>字符串。</returns>
    public string ToString(string format)
    {
        return string.Format("({0}, {1}, {2}, {3})",
            x.ToString(format, CultureInfo.InvariantCulture),
            y.ToString(format, CultureInfo.InvariantCulture),
            z.ToString(format, CultureInfo.InvariantCulture),
            w.ToString(format, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 两个向量的点积。
    /// </summary>
    /// <param name="lhs">左侧向量。</param>
    /// <param name="rhs">右侧向量。</param>
    public static float Dot(ref Vector4 lhs, ref Vector4 rhs)
    {
        return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z + lhs.w * rhs.w;
    }

    /// <summary>
    /// 在两个向量之间执行线性插值。
    /// </summary>
    /// <param name="a">插值起始向量。</param>
    /// <param name="b">插值目标向量。</param>
    /// <param name="t">时间分数。</param>
    /// <param name="result">结果向量。</param>
    public static void Lerp(ref Vector4 a, ref Vector4 b, float t, out Vector4 result)
    {
        result = new Vector4(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
    }

    /// <summary>
    /// 按分量相乘两个向量。
    /// </summary>
    /// <param name="a">第一个向量。</param>
    /// <param name="b">第二个向量。</param>
    /// <param name="result">结果向量。</param>
    public static void Scale(ref Vector4 a, ref Vector4 b, out Vector4 result)
    {
        result = new Vector4(a.x * b.x, a.y * b.y, a.z * b.z, a.w * b.w);
    }

    /// <summary>
    /// 归一化向量。
    /// </summary>
    /// <param name="value">要归一化的向量。</param>
    /// <param name="result">归一化后的结果向量。</param>
    public static void Normalize(ref Vector4 value, out Vector4 result)
    {
        float mag = value.Magnitude;
        if (mag > Epsilon)
        {
            result = new Vector4(value.x / mag, value.y / mag, value.z / mag, value.w / mag);
        }
        else
        {
            result = Vector4.zero;
        }
    }
}