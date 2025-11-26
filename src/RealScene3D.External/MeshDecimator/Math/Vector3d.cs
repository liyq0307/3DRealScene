using System.Globalization;

namespace MeshDecimator.Math
{
    /// <summary>
    /// A double precision 3D vector.
    /// </summary>
    public struct Vector3d : IEquatable<Vector3d>
    {
        #region 静态 Read-Only
        /// <summary>
        /// 零向量。
        /// </summary>
        public static readonly Vector3d zero = new Vector3d(0, 0, 0);
        #endregion

        #region 常量
        /// <summary>
        /// 向量 epsilon 值。
        /// </summary>
        public const double Epsilon = double.Epsilon;
        #endregion

        #region 字段
        /// <summary>
        /// x 分量。
        /// </summary>
        public double x;
        /// <summary>
        /// y 分量。
        /// </summary>
        public double y;
        /// <summary>
        /// z 分量。
        /// </summary>
        public double z;
        #endregion

        #region 属性
        /// <summary>
        /// 获取此向量的模。
        /// </summary>
        public double Magnitude
        {
            get { return System.Math.Sqrt(x * x + y * y + z * z); }
        }

        /// <summary>
        /// 获取此向量的平方模。
        /// </summary>
        public double MagnitudeSqr
        {
            get { return (x * x + y * y + z * z); }
        }

        /// <summary>
        /// 获取此向量的归一化向量。
        /// </summary>
        public Vector3d Normalized
        {
            get
            {
                Vector3d result;
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
                    case 2:
                        return z;
                    default:
                        throw new IndexOutOfRangeException("Invalid Vector3d index!");
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
                        throw new IndexOutOfRangeException("Invalid Vector3d index!");
                }
            }
        }
        #endregion

        #region 构造函数
        /// <summary>
        /// 创建一个所有分量值相同的新向量。
        /// </summary>
        /// <param name="value">值。</param>
        public Vector3d(double value)
        {
            this.x = value;
            this.y = value;
            this.z = value;
        }

        /// <summary>
        /// 创建一个新向量。
        /// </summary>
        /// <param name="x">x 值。</param>
        /// <param name="y">y 值。</param>
        /// <param name="z">z 值。</param>
        public Vector3d(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>
        /// 从单精度向量创建新向量。
        /// </summary>
        /// <param name="vector">单精度向量。</param>
        public Vector3d(Vector3 vector)
        {
            this.x = vector.x;
            this.y = vector.y;
            this.z = vector.z;
        }
        #endregion

        #region 运算符
        /// <summary>
        /// 两个向量相加。
        /// </summary>
        /// <param name="a">第一个向量。</param>
        /// <param name="b">第二个向量。</param>
        /// <returns>结果向量。</returns>
        public static Vector3d operator +(Vector3d a, Vector3d b)
        {
            return new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        /// <summary>
        /// 两个向量相减。
        /// </summary>
        /// <param name="a">第一个向量。</param>
        /// <param name="b">第二个向量。</param>
        /// <returns>结果向量。</returns>
        public static Vector3d operator -(Vector3d a, Vector3d b)
        {
            return new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        /// <summary>
        /// 均匀缩放向量。
        /// </summary>
        /// <param name="a">向量。</param>
        /// <param name="d">缩放值。</param>
        /// <returns>结果向量。</returns>
        public static Vector3d operator *(Vector3d a, double d)
        {
            return new Vector3d(a.x * d, a.y * d, a.z * d);
        }

        /// <summary>
        /// 均匀缩放向量。
        /// </summary>
        /// <param name="d">缩放值。</param>
        /// <param name="a">向量。</param>
        /// <returns>结果向量。</returns>
        public static Vector3d operator *(double d, Vector3d a)
        {
            return new Vector3d(a.x * d, a.y * d, a.z * d);
        }

        /// <summary>
        /// 用浮点数除向量。
        /// </summary>
        /// <param name="a">向量。</param>
        /// <param name="d">除数浮点值。</param>
        /// <returns>结果向量。</returns>
        public static Vector3d operator /(Vector3d a, double d)
        {
            return new Vector3d(a.x / d, a.y / d, a.z / d);
        }

        /// <summary>
        /// 从零向量中减去该向量。
        /// </summary>
        /// <param name="a">向量。</param>
        /// <returns>结果向量。</returns>
        public static Vector3d operator -(Vector3d a)
        {
            return new Vector3d(-a.x, -a.y, -a.z);
        }

        /// <summary>
        /// 返回两个向量是否相等。
        /// </summary>
        /// <param name="lhs">左侧向量。</param>
        /// <param name="rhs">右侧向量。</param>
        /// <returns>If equals.</returns>
        public static bool operator ==(Vector3d lhs, Vector3d rhs)
        {
            return (lhs - rhs).MagnitudeSqr < Epsilon;
        }

        /// <summary>
        /// 返回两个向量是否不相等。
        /// </summary>
        /// <param name="lhs">左侧向量。</param>
        /// <param name="rhs">右侧向量。</param>
        /// <returns>If not equals.</returns>
        public static bool operator !=(Vector3d lhs, Vector3d rhs)
        {
            return (lhs - rhs).MagnitudeSqr >= Epsilon;
        }

        /// <summary>
        /// 隐式将单精度向量转换为双精度向量。
        /// </summary>
        /// <param name="v">单精度向量。</param>
        public static implicit operator Vector3d(Vector3 v)
        {
            return new Vector3d(v.x, v.y, v.z);
        }

        /// <summary>
        /// 隐式将整数向量转换为双精度向量。
        /// </summary>
        /// <param name="v">整数向量。</param>
        public static implicit operator Vector3d(Vector3i v)
        {
            return new Vector3d(v.x, v.y, v.z);
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
        public void Set(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>
        /// 与另一个向量按分量相乘。
        /// </summary>
        /// <param name="scale">要相乘的向量。</param>
        public void Scale(ref Vector3d scale)
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
            double mag = this.Magnitude;
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
        public void Clamp(double min, double max)
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
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode() << 2 ^ z.GetHashCode() >> 2;
        }

        /// <summary>
        /// 返回此向量是否等于另一个向量。
        /// </summary>
        /// <param name="other">要比较的另一个向量。</param>
        /// <returns>If equals.</returns>
        public override bool Equals(object? other)
        {
            if (!(other is Vector3d))
            {
                return false;
            }
            Vector3d vector = (Vector3d)other;
            return (x == vector.x && y == vector.y && z == vector.z);
        }

        /// <summary>
        /// 返回此向量是否等于另一个向量。
        /// </summary>
        /// <param name="other">要比较的另一个向量。</param>
        /// <returns>If equals.</returns>
        public bool Equals(Vector3d other)
        {
            return (x == other.x && y == other.y && z == other.z);
        }

        /// <summary>
        /// 返回此向量的格式化字符串。
        /// </summary>
        /// <returns>The string.</returns>
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
        /// <param name="format">浮点格式。</param>
        /// <returns>The string.</returns>
        public string ToString(string format)
        {
            return string.Format("({0}, {1}, {2})",
                x.ToString(format, CultureInfo.InvariantCulture),
                y.ToString(format, CultureInfo.InvariantCulture),
                z.ToString(format, CultureInfo.InvariantCulture));
        }
        #endregion

        #region 静态
        /// <summary>
        /// 两个向量的点积。
        /// </summary>
        /// <param name="lhs">左侧向量。</param>
        /// <param name="rhs">右侧向量。</param>
        public static double Dot(ref Vector3d lhs, ref Vector3d rhs)
        {
            return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z;
        }

        /// <summary>
        /// 两个向量的叉积。
        /// </summary>
        /// <param name="lhs">左侧向量。</param>
        /// <param name="rhs">右侧向量。</param>
        /// <param name="result">结果向量。</param>
        public static void Cross(ref Vector3d lhs, ref Vector3d rhs, out Vector3d result)
        {
            result = new Vector3d(lhs.y * rhs.z - lhs.z * rhs.y, lhs.z * rhs.x - lhs.x * rhs.z, lhs.x * rhs.y - lhs.y * rhs.x);
        }

        /// <summary>
        /// 计算两个向量之间的夹角。
        /// </summary>
        /// <param name="from">起始向量。</param>
        /// <param name="to">目标向量。</param>
        /// <returns>The angle.</returns>
        public static double Angle(ref Vector3d from, ref Vector3d to)
        {
            Vector3d fromNormalized = from.Normalized;
            Vector3d toNormalized = to.Normalized;
            return System.Math.Acos(MathHelper.Clamp(Vector3d.Dot(ref fromNormalized, ref toNormalized), -1.0, 1.0)) * MathHelper.Rad2Degd;
        }

        /// <summary>
        /// 在两个向量之间执行线性插值。
        /// </summary>
        /// <param name="a">起始插值向量。</param>
        /// <param name="b">目标插值向量。</param>
        /// <param name="t">时间分数。</param>
        /// <param name="result">结果向量。</param>
        public static void Lerp(ref Vector3d a, ref Vector3d b, double t, out Vector3d result)
        {
            result = new Vector3d(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }

        /// <summary>
        /// 两个向量按分量相乘。
        /// </summary>
        /// <param name="a">第一个向量。</param>
        /// <param name="b">第二个向量。</param>
        /// <param name="result">结果向量。</param>
        public static void Scale(ref Vector3d a, ref Vector3d b, out Vector3d result)
        {
            result = new Vector3d(a.x * b.x, a.y * b.y, a.z * b.z);
        }

        /// <summary>
        /// 归一化向量。
        /// </summary>
        /// <param name="value">要归一化的向量。</param>
        /// <param name="result">结果归一化向量。</param>
        public static void Normalize(ref Vector3d value, out Vector3d result)
        {
            double mag = value.Magnitude;
            if (mag > Epsilon)
            {
                result = new Vector3d(value.x / mag, value.y / mag, value.z / mag);
            }
            else
            {
                result = Vector3d.zero;
            }
        }
        #endregion
        #endregion
    }
}