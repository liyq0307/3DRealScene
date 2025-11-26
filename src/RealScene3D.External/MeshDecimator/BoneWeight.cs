using MeshDecimator.Math;

namespace MeshDecimator
{
    /// <summary>
    /// 骨骼权重。
    /// </summary>
    public struct BoneWeight : IEquatable<BoneWeight>
    {
        #region 字段
        /// <summary>
        /// 第一个骨骼索引。
        /// </summary>
        public int boneIndex0;
        /// <summary>
        /// 第二个骨骼索引。
        /// </summary>
        public int boneIndex1;
        /// <summary>
        /// 第三个骨骼索引。
        /// </summary>
        public int boneIndex2;
        /// <summary>
        /// 第四个骨骼索引。
        /// </summary>
        public int boneIndex3;

        /// <summary>
        /// 第一个骨骼权重。
        /// </summary>
        public float boneWeight0;
        /// <summary>
        /// 第二个骨骼权重。
        /// </summary>
        public float boneWeight1;
        /// <summary>
        /// 第三个骨骼权重。
        /// </summary>
        public float boneWeight2;
        /// <summary>
        /// 第四个骨骼权重。
        /// </summary>
        public float boneWeight3;
        #endregion

        #region 构造函数
        /// <summary>
        /// 创建一个新的骨骼权重。
        /// </summary>
        /// <param name="boneIndex0">第一个骨骼索引。</param>
        /// <param name="boneIndex1">第二个骨骼索引。</param>
        /// <param name="boneIndex2">第三个骨骼索引。</param>
        /// <param name="boneIndex3">第四个骨骼索引。</param>
        /// <param name="boneWeight0">第一个骨骼权重。</param>
        /// <param name="boneWeight1">第二个骨骼权重。</param>
        /// <param name="boneWeight2">第三个骨骼权重。</param>
        /// <param name="boneWeight3">第四个骨骼权重。</param>
        public BoneWeight(int boneIndex0, int boneIndex1, int boneIndex2, int boneIndex3, float boneWeight0, float boneWeight1, float boneWeight2, float boneWeight3)
        {
            this.boneIndex0 = boneIndex0;
            this.boneIndex1 = boneIndex1;
            this.boneIndex2 = boneIndex2;
            this.boneIndex3 = boneIndex3;

            this.boneWeight0 = boneWeight0;
            this.boneWeight1 = boneWeight1;
            this.boneWeight2 = boneWeight2;
            this.boneWeight3 = boneWeight3;
        }
        #endregion

        #region 运算符
        /// <summary>
        /// 返回两个骨骼权重是否相等。
        /// </summary>
        /// <param name="lhs">左侧骨骼权重。</param>
        /// <param name="rhs">右侧骨骼权重。</param>
        /// <returns>是否相等。</returns>
        public static bool operator ==(BoneWeight lhs, BoneWeight rhs)
        {
            return (lhs.boneIndex0 == rhs.boneIndex0 && lhs.boneIndex1 == rhs.boneIndex1 && lhs.boneIndex2 == rhs.boneIndex2 && lhs.boneIndex3 == rhs.boneIndex3 &&
                new Vector4(lhs.boneWeight0, lhs.boneWeight1, lhs.boneWeight2, lhs.boneWeight3) == new Vector4(rhs.boneWeight0, rhs.boneWeight1, rhs.boneWeight2, rhs.boneWeight3));
        }

        /// <summary>
        /// 返回两个骨骼权重是否不相等。
        /// </summary>
        /// <param name="lhs">左侧骨骼权重。</param>
        /// <param name="rhs">右侧骨骼权重。</param>
        /// <returns>是否不相等。</returns>
        public static bool operator !=(BoneWeight lhs, BoneWeight rhs)
        {
            return !(lhs == rhs);
        }
        #endregion

        #region 私有方法
        private void MergeBoneWeight(int boneIndex, float weight)
        {
            if (boneIndex == boneIndex0)
            {
                boneWeight0 = (boneWeight0 + weight) * 0.5f;
            }
            else if (boneIndex == boneIndex1)
            {
                boneWeight1 = (boneWeight1 + weight) * 0.5f;
            }
            else if (boneIndex == boneIndex2)
            {
                boneWeight2 = (boneWeight2 + weight) * 0.5f;
            }
            else if (boneIndex == boneIndex3)
            {
                boneWeight3 = (boneWeight3 + weight) * 0.5f;
            }
            else if(boneWeight0 == 0f)
            {
                boneIndex0 = boneIndex;
                boneWeight0 = weight;
            }
            else if (boneWeight1 == 0f)
            {
                boneIndex1 = boneIndex;
                boneWeight1 = weight;
            }
            else if (boneWeight2 == 0f)
            {
                boneIndex2 = boneIndex;
                boneWeight2 = weight;
            }
            else if (boneWeight3 == 0f)
            {
                boneIndex3 = boneIndex;
                boneWeight3 = weight;
            }
            Normalize();
        }

        private void Normalize()
        {
            float mag = (float)System.Math.Sqrt(boneWeight0 * boneWeight0 + boneWeight1 * boneWeight1 + boneWeight2 * boneWeight2 + boneWeight3 * boneWeight3);
            if (mag > float.Epsilon)
            {
                boneWeight0 /= mag;
                boneWeight1 /= mag;
                boneWeight2 /= mag;
                boneWeight3 /= mag;
            }
            else
            {
                boneWeight0 = boneWeight1 = boneWeight2 = boneWeight3 = 0f;
            }
        }
        #endregion

        #region 公共方法
        #region 对象
        /// <summary>
        /// 返回此向量的哈希码。
        /// </summary>
        /// <returns>哈希码。</returns>
        public override int GetHashCode()
        {
            return boneIndex0.GetHashCode() ^ boneIndex1.GetHashCode() << 2 ^ boneIndex2.GetHashCode() >> 2 ^ boneIndex3.GetHashCode() >>
                1 ^ boneWeight0.GetHashCode() << 5 ^ boneWeight1.GetHashCode() << 4 ^ boneWeight2.GetHashCode() >> 4 ^ boneWeight3.GetHashCode() >> 3;
        }

        /// <summary>
        /// 返回此骨骼权重是否等于另一个对象。
        /// </summary>
        /// <param name="obj">要比较的另一个对象。</param>
        /// <returns>是否相等。</returns>
        public override bool Equals(object? obj)
        {
            if (!(obj is BoneWeight))
            {
                return false;
            }
            BoneWeight other = (BoneWeight)obj;
            return (boneIndex0 == other.boneIndex0 && boneIndex1 == other.boneIndex1 && boneIndex2 == other.boneIndex2 && boneIndex3 == other.boneIndex3 &&
                boneWeight0 == other.boneWeight0 && boneWeight1 == other.boneWeight1 && boneWeight2 == other.boneWeight2 && boneWeight3 == other.boneWeight3);
        }

        /// <summary>
        /// 返回此骨骼权重是否等于另一个骨骼权重。
        /// </summary>
        /// <param name="other">要比较的另一个骨骼权重。</param>
        /// <returns>是否相等。</returns>
        public bool Equals(BoneWeight other)
        {
            return (boneIndex0 == other.boneIndex0 && boneIndex1 == other.boneIndex1 && boneIndex2 == other.boneIndex2 && boneIndex3 == other.boneIndex3 &&
                boneWeight0 == other.boneWeight0 && boneWeight1 == other.boneWeight1 && boneWeight2 == other.boneWeight2 && boneWeight3 == other.boneWeight3);
        }

        /// <summary>
        /// 返回此骨骼权重的格式化字符串。
        /// </summary>
        /// <returns>字符串。</returns>
        public override string ToString()
        {
            return string.Format("({0}:{4:F1}, {1}:{5:F1}, {2}:{6:F1}, {3}:{7:F1})",
                boneIndex0, boneIndex1, boneIndex2, boneIndex3, boneWeight0, boneWeight1, boneWeight2, boneWeight3);
        }
        #endregion

        #region 静态
        /// <summary>
        /// 合并两个骨骼权重并将合并结果存储在第一个参数中。
        /// </summary>
        /// <param name="a">第一个骨骼权重，也存储结果。</param>
        /// <param name="b">第二个骨骼权重。</param>
        public static void Merge(ref BoneWeight a, ref BoneWeight b)
        {
            if (b.boneWeight0 > 0f) a.MergeBoneWeight(b.boneIndex0, b.boneWeight0);
            if (b.boneWeight1 > 0f) a.MergeBoneWeight(b.boneIndex1, b.boneWeight1);
            if (b.boneWeight2 > 0f) a.MergeBoneWeight(b.boneIndex2, b.boneWeight2);
            if (b.boneWeight3 > 0f) a.MergeBoneWeight(b.boneIndex3, b.boneWeight3);
        }
        #endregion
        #endregion
    }
}