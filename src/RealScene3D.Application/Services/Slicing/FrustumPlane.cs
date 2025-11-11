using RealScene3D.Domain.Entities;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// 视锥平面数据结构 - 用于视锥剔除算法
/// 使用平面方程表示：Ax + By + Cz + D = 0
/// 其中 (A, B, C) 是归一化的法向量，D 是到原点的距离
/// </summary>
public struct FrustumPlane
{
    /// <summary>
    /// 平面方程的 A 系数（法向量的 X 分量）
    /// </summary>
    public double A { get; set; }

    /// <summary>
    /// 平面方程的 B 系数（法向量的 Y 分量）
    /// </summary>
    public double B { get; set; }

    /// <summary>
    /// 平面方程的 C 系数（法向量的 Z 分量）
    /// </summary>
    public double C { get; set; }

    /// <summary>
    /// 平面方程的 D 系数（到原点的距离）
    /// </summary>
    public double D { get; set; }

    /// <summary>
    /// 从法向量和平面上的一点创建平面
    /// </summary>
    /// <param name="normal">归一化的法向量</param>
    /// <param name="point">平面上的一点</param>
    /// <returns>平面实例</returns>
    public static FrustumPlane FromNormalAndPoint(Vector3D normal, Vector3D point)
    {
        // 归一化法向量（防止传入未归一化的向量）
        var length = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);
        if (length < 1e-10) length = 1.0;

        var nx = normal.X / length;
        var ny = normal.Y / length;
        var nz = normal.Z / length;

        // 计算 D = -(n · p)
        var d = -(nx * point.X + ny * point.Y + nz * point.Z);

        return new FrustumPlane
        {
            A = nx,
            B = ny,
            C = nz,
            D = d
        };
    }

    /// <summary>
    /// 从法向量分量和平面上的点坐标创建平面
    /// </summary>
    public static FrustumPlane FromComponents(double nx, double ny, double nz, double px, double py, double pz)
    {
        // 归一化法向量
        var length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        if (length < 1e-10) length = 1.0;

        nx /= length;
        ny /= length;
        nz /= length;

        // 计算 D = -(n · p)
        var d = -(nx * px + ny * py + nz * pz);

        return new FrustumPlane
        {
            A = nx,
            B = ny,
            C = nz,
            D = d
        };
    }

    /// <summary>
    /// 计算点到平面的有符号距离
    /// 正值表示在法向量方向，负值表示在法向量反方向
    /// </summary>
    public double DistanceToPoint(Vector3D point)
    {
        return A * point.X + B * point.Y + C * point.Z + D;
    }

    /// <summary>
    /// 计算点到平面的有符号距离（使用坐标分量）
    /// </summary>
    public double DistanceToPoint(double x, double y, double z)
    {
        return A * x + B * y + C * z + D;
    }
}
