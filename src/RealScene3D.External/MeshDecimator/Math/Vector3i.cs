using System.Globalization;

namespace MeshDecimator.Math
{
    /// <summary>
    /// A 3D integer vector.
    /// </summary>
    public struct Vector3i : IEquatable<Vector3i>
    {
        #region 静态 Read-Only
        /// <summary>
        /// 零向量。
        /// </summary>
        public static readonly Vector3i zero = new Vector3i(0, 0, 0);
        #endregion

        #region 字段
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
        #endregion

        #region 属性
        /// <summary>
        /// 获取此向量的模。
        /// </summary>
        public int Magnitude
        {
            get { return (int)System.Math.Sqrt(x * x + y * y + z * z); }
        }

        /// <summary>
        /// 获取此向量的平方模。
        /// </summary>
        public int MagnitudeSqr
        {
            get { return (x * x + y * y + z * z); }
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
                    default:
                        throw new IndexOutOfRangeException("Invalid Vector3i index!");
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
                        throw new IndexOutOfRangeException("Invalid Vector3i index!");
                }
            }
        }
        #endregion

        #region 构造函数
        /// <summary>
        /// 创建一个所有分量值相同的新向量。
        /// </summary>
        /// <param name="value">值。</param>
        public Vector3i(int value)
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
        public Vector3i(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        #endregion

        #region 运算符
        /// <summary>
        /// 两个向量相加。
        /// </summary>
        /// <param name="a">第一个向量。</param>
        /// <param name="b">第二个向量。</param>
        /// <returns>结果向量。</returns>
        public static Vector3i operator +(Vector3i a, Vector3i b)
        {
            return new Vector3i(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        /// <summary>
        /// 两个向量相减。
        /// </summary>
        /// <param name="a">第一个向量。</param>
        /// <param name="b">第二个向量。</param>
        /// <returns>结果向量。</returns>
        public static Vector3i operator -(Vector3i a, Vector3i b)
        {
            return new Vector3i(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        /// <summary>
        /// 均匀缩放向量。
        /// </summary>
        /// <param name="a">向量。</param>
        /// <param name="d">缩放值。</param>
        /// <returns>结果向量。</returns>
        public static Vector3i operator *(Vector3i a, int d)
        {
            return new Vector3i(a.x * d, a.y * d, a.z * d);
        }

        /// <summary>
        /// 均匀缩放向量。
        /// </summary>
        /// <param name="d">缩放值。</param>
        /// <param name="a">向量。</param>
        /// <returns>结果向量。</returns>
        public static Vector3i operator *(int d, Vector3i a)
        {
            return new Vector3i(a.x * d, a.y * d, a.z * d);
        }

        /// <summary>
        /// 用浮点数除向量。
        /// </summary>
        /// <param name="a">向量。</param>
        /// <param name="d">除数浮点值。</param>
        /// <returns>结果向量。</returns>
        public static Vector3i operator /(Vector3i a, int d)
        {
            return new Vector3i(a.x / d, a.y / d, a.z / d);
        }

        /// <summary>
        /// 从零向量中减去该向量。
        /// </summary>
        /// <param name="a">向量。</param>
        /// <returns>结果向量。</returns>
        public static Vector3i operator -(Vector3i a)
        {
            return new Vector3i(-a.x, -a.y, -a.z);
        }

        /// <summary>
        /// 返回两个向量是否相等。
        /// </summary>
        /// <param name="lhs">左侧向量。</param>
        /// <param name="rhs">右侧向量。</param>
        /// <returns>If equals.</returns>
        public static bool operator ==(Vector3i lhs, Vector3i rhs)
        {
            return (lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z);
        }

        /// <summary>
        /// 返回两个向量是否不相等。
        /// </summary>
        /// <param name="lhs">左侧向量。</param>
        /// <param name="rhs">右侧向量。</param>
        /// <returns>If not equals.</returns>
        public static bool operator !=(Vector3i lhs, Vector3i rhs)
        {
            return (lhs.x != rhs.x || lhs.y != rhs.y || lhs.z != rhs.z);
        }

        /// <summary>
        /// 显式将单精度向量转换为整数向量。
        /// </summary>
        /// <param name="v">单精度向量。</param>
        public static implicit operator Vector3i(Vector3 v)
        {
            return new Vector3i((int)v.x, (int)v.y, (int)v.z);
        }

        /// <summary>
        /// 显式将双精度向量转换为整数向量。
        /// </summary>
        /// <param name="v">双精度向量。</param>
        public static explicit operator Vector3i(Vector3d v)
        {
            return new Vector3i((int)v.x, (int)v.y, (int)v.z);
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
        public void Set(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>
        /// 与另一个向量按分量相乘。
        /// </summary>
        /// <param name="scale">要相乘的向量。</param>
        public void Scale(ref Vector3i scale)
        {
            x *= scale.x;
            y *= scale.y;
            z *= scale.z;
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
            if (!(other is Vector3i))
            {
                return false;
            }
            Vector3i vector = (Vector3i)other;
            return (x == vector.x && y == vector.y && z == vector.z);
        }

        /// <summary>
        /// 返回此向量是否等于另一个向量。
        /// </summary>
        /// <param name="other">要比较的另一个向量。</param>
        /// <returns>If equals.</returns>
        public bool Equals(Vector3i other)
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
                x.ToString(CultureInfo.InvariantCulture),
                y.ToString(CultureInfo.InvariantCulture),
                z.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 返回此向量的格式化字符串。
        /// </summary>
        /// <param name="format">整数格式。</param>
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
        /// 两个向量按分量相乘。
        /// </summary>
        /// <param name="a">第一个向量。</param>
        /// <param name="b">第二个向量。</param>
        /// <param name="result">结果向量。</param>
        public static void Scale(ref Vector3i a, ref Vector3i b, out Vector3i result)
        {
            result = new Vector3i(a.x * b.x, a.y * b.y, a.z * b.z);
        }
        #endregion
        #endregion
    }
}