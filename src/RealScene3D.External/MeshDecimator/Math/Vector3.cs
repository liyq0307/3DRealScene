using System.Globalization;

namespace MeshDecimator.Math
{
    /// <summary>
    /// 单精度 3D 向量。
    /// </summary>
    public struct Vector3 : IEquatable<Vector3>
    {
        #region 静态只读
        /// <summary>
        /// 零向量。
        /// </summary>
        public static readonly Vector3 zero = new Vector3(0, 0, 0);
        #endregion

        #region 常量
        /// <summary>
        /// 向量 epsilon。
        /// </summary>
        public const float Epsilon = 9.99999944E-11f;
        #endregion

        #region 字段
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
        #endregion

        #region 属性
        /// <summary>
        /// 获取此向量的大小。
        /// </summary>
        public float Magnitude
        {
            get { return (float)System.Math.Sqrt(x * x + y * y + z * z); }
        }

        /// <summary>
        /// 获取此向量的平方大小。
        /// </summary>
        public float MagnitudeSqr
        {
            get { return (x * x + y * y + z * z); }
        }

        /// <summary>
        /// 从此向量获取归一化向量。
        /// </summary>
        public Vector3 Normalized
        {
            get
            {
                Vector3 result;
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
                    default:
                        throw new IndexOutOfRangeException("Invalid Vector3 index!");
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
                    default:
                        throw new IndexOutOfRangeException("Invalid Vector3 index!");
                }
            }
        }
        #endregion

        #region 构造函数
        /// <summary>
        /// 创建一个新的向量，所有分量都使用相同的值。
        /// </summary>
        /// <param name="value">值。</param>
        public Vector3(float value)
        {
            this.x = value;
            this.y = value;
            this.z = value;
        }

        /// <summary>
        /// 创建一个新的向量。
        /// </summary>
        /// <param name="x">x 值。</param>
        /// <param name="y">y 值。</param>
        /// <param name="z">z 值。</param>
        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>
        /// 从双精度向量创建一个新的向量。
        /// </summary>
        /// <param name="vector">双精度向量。</param>
        public Vector3(Vector3d vector)
        {
            this.x = (float)vector.x;
            this.y = (float)vector.y;
            this.z = (float)vector.z;
        }
        #endregion

        #region 运算符
        /// <summary>
        /// 将两个向量相加。
        /// </summary>
        /// <param name="a">第一个向量。</param>
        /// <param name="b">第二个向量。</param>
        /// <returns>结果向量。</returns>
        public static Vector3 operator +(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        /// <summary>
        /// 将两个向量相减。
        /// </summary>
        /// <param name="a">第一个向量。</param>
        /// <param name="b">第二个向量。</param>
        /// <returns>结果向量。</returns>
        public static Vector3 operator -(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        /// <summary>
        /// 均匀缩放向量。
        /// </summary>
        /// <param name="a">向量。</param>
        /// <param name="d">缩放值。</param>
        /// <returns>结果向量。</returns>
        public static Vector3 operator *(Vector3 a, float d)
        {
            return new Vector3(a.x * d, a.y * d, a.z * d);
        }

        /// <summary>
        /// 均匀缩放向量。
        /// </summary>
        /// <param name="d">缩放值。</param>
        /// <param name="a">向量。</param>
        /// <returns>结果向量。</returns>
        public static Vector3 operator *(float d, Vector3 a)
        {
            return new Vector3(a.x * d, a.y * d, a.z * d);
        }

        /// <summary>
        /// 用浮点数除以向量。
        /// </summary>
        /// <param name="a">向量。</param>
        /// <param name="d">用于除法的浮点值。</param>
        /// <returns>结果向量。</returns>
        public static Vector3 operator /(Vector3 a, float d)
        {
            return new Vector3(a.x / d, a.y / d, a.z / d);
        }

        /// <summary>
        /// 从零向量中减去向量。
        /// </summary>
        /// <param name="a">向量。</param>
        /// <returns>结果向量。</returns>
        public static Vector3 operator -(Vector3 a)
        {
            return new Vector3(-a.x, -a.y, -a.z);
        }

        /// <summary>
        /// 返回两个向量是否相等。
        /// </summary>
        /// <param name="lhs">左侧向量。</param>
        /// <param name="rhs">右侧向量。</param>
        /// <returns>如果相等则返回true。</returns>
        public static bool operator ==(Vector3 lhs, Vector3 rhs)
        {
            return (lhs - rhs).MagnitudeSqr < Epsilon;
        }

        /// <summary>
        /// 返回两个向量是否不相等。
        /// </summary>
        /// <param name="lhs">左侧向量。</param>
        /// <param name="rhs">右侧向量。</param>
        /// <returns>如果不相等则返回true。</returns>
        public static bool operator !=(Vector3 lhs, Vector3 rhs)
        {
            return (lhs - rhs).MagnitudeSqr >= Epsilon;
        }

        /// <summary>
        /// 显式地将双精度向量转换为单精度向量。
        /// </summary>
        /// <param name="v">双精度向量。</param>
        public static explicit operator Vector3(Vector3d v)
        {
            return new Vector3((float)v.x, (float)v.y, (float)v.z);
        }

        /// <summary>
        /// 隐式地将整数向量转换为单精度向量。
        /// </summary>
        /// <param name="v">整数向量。</param>
        public static implicit operator Vector3(Vector3i v)
        {
            return new Vector3(v.x, v.y, v.z);
        }
        #endregion

        #region 公共方法
        #region 实例方法
        /// <summary>
        /// 设置现有向量的 x、y 和 z 分量。
        /// </summary>
        /// <param name="x">x 值。</param>
        /// <param name="y">y 值。</param>
        /// <param name="z">z 值。</param>
        public void Set(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>
        /// 按分量与另一个向量相乘。
        /// </summary>
        /// <param name="scale">要相乘的向量。</param>
        public void Scale(ref Vector3 scale)
        {
            x *= scale.x;
            y *= scale.y;
            z *= scale.z;
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
            }
            else
            {
                x = y = z = 0;
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
        }
        #endregion

        #region 对象方法
        /// <summary>
        /// 返回此向量的哈希码。
        /// </summary>
        /// <returns>哈希码。</returns>
        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode() << 2 ^ z.GetHashCode() >> 2;
        }

        /// <summary>
        /// 返回此向量是否等于另一个向量。
        /// </summary>
        /// <param name="other">要比较的另一个向量。</param>
        /// <returns>如果相等则返回true。</returns>
        public override bool Equals(object? other)
        {
            if (!(other is Vector3))
            {
                return false;
            }
            Vector3 vector = (Vector3)other;
            return (x == vector.x && y == vector.y && z == vector.z);
        }

        /// <summary>
        /// 返回此向量是否等于另一个向量。
        /// </summary>
        /// <param name="other">要比较的另一个向量。</param>
        /// <returns>如果相等则返回true。</returns>
        public bool Equals(Vector3 other)
        {
            return (x == other.x && y == other.y && z == other.z);
        }

        /// <summary>
        /// 返回此向量的格式化字符串。
        /// </summary>
        /// <returns>字符串。</returns>
        public override string ToString()
        {
            return string.Format("({0}, {1}, {2})",
                x.ToString("F1", CultureInfo.InvariantCulture),
                y.ToString("F1", CultureInfo.InvariantCulture),
                z.ToString("F1", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 返回此向量的格式化字符串。
        /// </summary>
        /// <param name="format">浮点数格式。</param>
        /// <returns>字符串。</returns>
        public string ToString(string format)
        {
            return string.Format("({0}, {1}, {2})",
                x.ToString(format, CultureInfo.InvariantCulture),
                y.ToString(format, CultureInfo.InvariantCulture),
                z.ToString(format, CultureInfo.InvariantCulture));
        }
        #endregion

        #region 静态方法
        /// <summary>
        /// 两个向量的点积。
        /// </summary>
        /// <param name="lhs">左侧向量。</param>
        /// <param name="rhs">右侧向量。</param>
        public static float Dot(ref Vector3 lhs, ref Vector3 rhs)
        {
            return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z;
        }

        /// <summary>
        /// 两个向量的叉积。
        /// </summary>
        /// <param name="lhs">左侧向量。</param>
        /// <param name="rhs">右侧向量。</param>
        /// <param name="result">结果向量。</param>
        public static void Cross(ref Vector3 lhs, ref Vector3 rhs, out Vector3 result)
        {
            result = new Vector3(lhs.y * rhs.z - lhs.z * rhs.y, lhs.z * rhs.x - lhs.x * rhs.z, lhs.x * rhs.y - lhs.y * rhs.x);
        }

        /// <summary>
        /// 计算两个向量之间的角度。
        /// </summary>
        /// <param name="from">起始向量。</param>
        /// <param name="to">目标向量。</param>
        /// <returns>角度。</returns>
        public static float Angle(ref Vector3 from, ref Vector3 to)
        {
            Vector3 fromNormalized = from.Normalized;
            Vector3 toNormalized = to.Normalized;
            return (float)System.Math.Acos(MathHelper.Clamp(Vector3.Dot(ref fromNormalized, ref toNormalized), -1f, 1f)) * MathHelper.Rad2Deg;
        }

        /// <summary>
        /// 在两个向量之间执行线性插值。
        /// </summary>
        /// <param name="a">插值起始向量。</param>
        /// <param name="b">插值目标向量。</param>
        /// <param name="t">时间分数。</param>
        /// <param name="result">结果向量。</param>
        public static void Lerp(ref Vector3 a, ref Vector3 b, float t, out Vector3 result)
        {
            result = new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }

        /// <summary>
        /// 按分量相乘两个向量。
        /// </summary>
        /// <param name="a">第一个向量。</param>
        /// <param name="b">第二个向量。</param>
        /// <param name="result">结果向量。</param>
        public static void Scale(ref Vector3 a, ref Vector3 b, out Vector3 result)
        {
            result = new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        }

        /// <summary>
        /// 归一化向量。
        /// </summary>
        /// <param name="value">要归一化的向量。</param>
        /// <param name="result">归一化后的结果向量。</param>
        public static void Normalize(ref Vector3 value, out Vector3 result)
        {
            float mag = value.Magnitude;
            if (mag > Epsilon)
            {
                result = new Vector3(value.x / mag, value.y / mag, value.z / mag);
            }
            else
            {
                result = Vector3.zero;
            }
        }

        /// <summary>
        /// 归一化两个向量并使它们彼此正交。
        /// </summary>
        /// <param name="normal">法线向量。</param>
        /// <param name="tangent">切线。</param>
        public static void OrthoNormalize(ref Vector3 normal, ref Vector3 tangent)
        {
            normal.Normalize();
            Vector3 proj = normal * Vector3.Dot(ref tangent, ref normal);
            tangent -= proj;
            tangent.Normalize();
        }
        #endregion
        #endregion
    }
}