namespace RealScene3D.Domain.Utils;

/// <summary>
/// 对称矩阵。
/// </summary>
public struct SymmetricMatrix
{
    /// <summary>
    /// m11 分量。
    /// </summary>
    public double m0;

    /// <summary>
    /// m12 分量。
    /// </summary>
    public double m1;

    /// <summary>
    /// m13 分量。
    /// </summary>
    public double m2;

    /// <summary>
    /// m14 分量。
    /// </summary>
    public double m3;

    /// <summary>
    /// m22 分量。
    /// </summary>
    public double m4;

    /// <summary>
    /// m23 分量。
    /// </summary>
    public double m5;

    /// <summary>
    /// m24 分量。
    /// </summary>
    public double m6;

    /// <summary>
    /// m33 分量。
    /// </summary>
    public double m7;

    /// <summary>
    /// m34 分量。
    /// </summary>
    public double m8;

    /// <summary>
    /// m44 分量。
    /// </summary>
    public double m9;

    /// <summary>
    /// 获取具有特定索引的分量值。
    /// </summary>
    /// <param name="index">分量索引。</param>
    /// <returns>值。</returns>
    public double this[int index]
    {
        get
        {
            switch (index)
            {
                case 0:
                    return m0;
                case 1:
                    return m1;
                case 2:
                    return m2;
                case 3:
                    return m3;
                case 4:
                    return m4;
                case 5:
                    return m5;
                case 6:
                    return m6;
                case 7:
                    return m7;
                case 8:
                    return m8;
                case 9:
                    return m9;
                default:
                    throw new IndexOutOfRangeException();
            }
        }
    }

    /// <summary>
    /// 创建一个每个分量都有值的对称矩阵。
    /// </summary>
    /// <param name="c">分量值。</param>
    public SymmetricMatrix(double c)
    {
        this.m0 = c;
        this.m1 = c;
        this.m2 = c;
        this.m3 = c;
        this.m4 = c;
        this.m5 = c;
        this.m6 = c;
        this.m7 = c;
        this.m8 = c;
        this.m9 = c;
    }

    /// <summary>
    /// 创建一个对称矩阵。
    /// </summary>
    /// <param name="m0">m11 分量。</param>
    /// <param name="m1">m12 分量。</param>
    /// <param name="m2">m13 分量。</param>
    /// <param name="m3">m14 分量。</param>
    /// <param name="m4">m22 分量。</param>
    /// <param name="m5">m23 分量。</param>
    /// <param name="m6">m24 分量。</param>
    /// <param name="m7">m33 分量。</param>
    /// <param name="m8">m34 分量。</param>
    /// <param name="m9">m44 分量。</param>
    public SymmetricMatrix(double m0, double m1, double m2, double m3,
        double m4, double m5, double m6, double m7, double m8, double m9)
    {
        this.m0 = m0;
        this.m1 = m1;
        this.m2 = m2;
        this.m3 = m3;
        this.m4 = m4;
        this.m5 = m5;
        this.m6 = m6;
        this.m7 = m7;
        this.m8 = m8;
        this.m9 = m9;
    }

    /// <summary>
    /// 从平面创建一个对称矩阵。
    /// </summary>
    /// <param name="a">平面 x-分量。</param>
    /// <param name="b">平面 y-分量</param>
    /// <param name="c">平面 z-分量</param>
    /// <param name="d">平面 w-分量</param>
    public SymmetricMatrix(double a, double b, double c, double d)
    {
        this.m0 = a * a;
        this.m1 = a * b;
        this.m2 = a * c;
        this.m3 = a * d;

        this.m4 = b * b;
        this.m5 = b * c;
        this.m6 = b * d;

        this.m7 = c * c;
        this.m8 = c * d;

        this.m9 = d * d;
    }

    /// <summary>
    /// 将两个矩阵相加。
    /// </summary>
    /// <param name="a">左侧。</param>
    /// <param name="b">右侧。</param>
    /// <returns>结果矩阵。</returns>
    public static SymmetricMatrix operator +(SymmetricMatrix a, SymmetricMatrix b)
    {
        return new SymmetricMatrix(
            a.m0 + b.m0, a.m1 + b.m1, a.m2 + b.m2, a.m3 + b.m3,
            a.m4 + b.m4, a.m5 + b.m5, a.m6 + b.m6,
            a.m7 + b.m7, a.m8 + b.m8,
            a.m9 + b.m9
        );
    }

    /// <summary>
    /// 行列式(0, 1, 2, 1, 4, 5, 2, 5, 7)
    /// </summary>
    /// <returns></returns>
    public double Determinant1()
    {
        double det =
            m0 * m4 * m7 +
            m2 * m1 * m5 +
            m1 * m5 * m2 -
            m2 * m4 * m2 -
            m0 * m5 * m5 -
            m1 * m1 * m7;
        return det;
    }

    /// <summary>
    /// 行列式(1, 2, 3, 4, 5, 6, 5, 7, 8)
    /// </summary>
    /// <returns></returns>
    public double Determinant2()
    {
        double det =
            m1 * m5 * m8 +
            m3 * m4 * m7 +
            m2 * m6 * m5 -
            m3 * m5 * m5 -
            m1 * m6 * m7 -
            m2 * m4 * m8;
        return det;
    }

    /// <summary>
    /// 行列式(0, 2, 3, 1, 5, 6, 2, 7, 8)
    /// </summary>
    /// <returns></returns>
    public double Determinant3()
    {
        double det =
            m0 * m5 * m8 +
            m3 * m1 * m7 +
            m2 * m6 * m2 -
            m3 * m5 * m2 -
            m0 * m6 * m7 -
            m2 * m1 * m8;
        return det;
    }

    /// <summary>
    /// 行列式(0, 1, 3, 1, 4, 6, 2, 5, 8)
    /// </summary>
    /// <returns></returns>
    public double Determinant4()
    {
        double det =
            m0 * m4 * m8 +
            m3 * m1 * m5 +
            m1 * m6 * m2 -
            m3 * m4 * m2 -
            m0 * m6 * m5 -
            m1 * m1 * m8;
        return det;
    }

    /// <summary>
    /// 计算此矩阵的行列式。
    /// </summary>
    /// <param name="a11">a11 索引。</param>
    /// <param name="a12">a12 索引。</param>
    /// <param name="a13">a13 索引。</param>
    /// <param name="a21">a21 索引。</param>
    /// <param name="a22">a22 索引。</param>
    /// <param name="a23">a23 索引。</param>
    /// <param name="a31">a31 索引。</param>
    /// <param name="a32">a32 索引。</param>
    /// <param name="a33">a33 索引。</param>
    /// <returns>行列式值。</returns>
    public double Determinant(int a11, int a12, int a13,
        int a21, int a22, int a23,
        int a31, int a32, int a33)
    {
        double det =
            this[a11] * this[a22] * this[a33] +
            this[a13] * this[a21] * this[a32] +
            this[a12] * this[a23] * this[a31] -
            this[a13] * this[a22] * this[a31] -
            this[a11] * this[a23] * this[a32] -
            this[a12] * this[a21] * this[a33];
        return det;
    }
}