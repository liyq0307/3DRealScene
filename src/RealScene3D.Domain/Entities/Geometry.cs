/// <summary>
/// 几何基础类型定义
/// 提供3D渲染和切片处理所需的几何数学基础结构
/// 包括视口、三角形等核心几何概念
///
/// 注意：基础的数学类型（向量、包围盒等）已移至：
/// - RealScene3D.Domain.Mathematics - 数学类型
/// - RealScene3D.Domain.Geometry - 几何数据结构
/// </summary>

using RealScene3D.Domain.Geometry;

namespace RealScene3D.Domain.Entities;

/// <summary>
/// 视口信息 - 渲染视口参数集合
/// 定义3D渲染视口的核心参数，包括相机位置、投影参数等
/// 用于视锥剔除、LOD计算、预测加载等渲染优化算法
/// </summary>
public class ViewportInfo
{
    /// <summary>
    /// 相机位置 - 视点在三维空间中的坐标
    /// 表示观察者在世界坐标系中的位置
    /// 是视锥剔除和距离计算的核心参数
    /// </summary>
    public Vector3d CameraPosition { get; set; } = new Vector3d(0, 0, 0);

    /// <summary>
    /// 相机朝向 - 相机的观察方向向量
    /// 表示相机指向的方向，用于计算视野中心线
    /// 在视锥剔除算法中用于判断物体是否在视野范围内
    /// </summary>
    public Vector3d CameraDirection { get; set; } = new Vector3d(0, 0, 1);

    /// <summary>
    /// 视野角度（弧度） - 相机的水平视野角度
    /// 定义了视锥的水平张角，默认60度（π/3弧度）
    /// 影响视锥剔除的视野范围和预测加载的覆盖区域
    /// </summary>
    public double FieldOfView { get; set; } = Math.PI / 3; // 默认60度视野

    /// <summary>
    /// 近裁剪面距离 - 视锥的近端截面距离
    /// 定义了可见范围的最小距离，默认1.0米
    /// 任何距离小于此值的物体将被裁剪掉
    /// </summary>
    public double NearPlane { get; set; } = 1.0;

    /// <summary>
    /// 远裁剪面距离 - 视锥的远端截面距离
    /// 定义了可见范围的最大距离，默认10000米
    /// 任何距离大于此值的物体将被裁剪掉
    /// </summary>
    public double FarPlane { get; set; } = 10000.0;

    /// <summary>
    /// 视口高宽比 - 视口宽度与高度的比率
    /// 影响视锥的水平和垂直视野范围
    /// 典型值为16/9或4/3等
    /// </summary>
    public double AspectRatio { get; set; } = 16.0 / 9.0;

    /// <summary>
    /// 视口高度（像素） - 渲染视口的高度
    /// 用于像素级别的计算和LOD优化
    /// 影响屏幕空间误差的计算
    /// </summary>
    public double ViewportHeight { get; set; } = 1080.0;

    /// <summary>
    /// 默认构造函数 - 创建具有默认参数的视口信息
    /// 使用标准的三维渲染默认值，适合大多数场景
    /// </summary>
    public ViewportInfo() { }

    /// <summary>
    /// 带参数构造函数 - 创建具有指定参数的视口信息
    /// </summary>
    /// <param name="cameraPosition">相机位置</param>
    /// <param name="cameraDirection">相机朝向</param>
    /// <param name="fieldOfView">视野角度（弧度）</param>
    /// <param name="nearPlane">近裁剪面距离</param>
    /// <param name="farPlane">远裁剪面距离</param>
    public ViewportInfo(Vector3d cameraPosition, Vector3d cameraDirection, double fieldOfView = Math.PI / 3, double nearPlane = 1.0, double farPlane = 10000.0)
    {
        CameraPosition = cameraPosition;
        CameraDirection = cameraDirection;
        FieldOfView = fieldOfView;
        NearPlane = nearPlane;
        FarPlane = farPlane;
    }
}

// /// <summary>
// /// 2D向量 - 用于UV纹理坐标等二维数据（向后兼容包装）
// /// 内部使用 RealScene3D.Domain.Mathematics.Vector2d
// /// </summary>
// public class Vector2D
// {
//     /// <summary>
//     /// U坐标（X轴）
//     /// </summary>
//     public double U { get; set; }

//     /// <summary>
//     /// V坐标（Y轴）
//     /// </summary>
//     public double V { get; set; }

//     /// <summary>
//     /// 默认构造函数
//     /// </summary>
//     public Vector2D()
//     {
//         U = 0;
//         V = 0;
//     }

//     /// <summary>
//     /// 带参数构造函数
//     /// </summary>
//     public Vector2D(double u, double v)
//     {
//         U = u;
//         V = v;
//     }

//     /// <summary>
//     /// 向量长度
//     /// </summary>
//     public double Length() => Math.Sqrt(U * U + V * V);

//     /// <summary>
//     /// 克隆2D向量
//     /// </summary>
//     public Vector2D Clone() => new Vector2D(U, V);

//     /// <summary>
//     /// 字符串表示
//     /// </summary>
//     public override string ToString() => $"({U:F6}, {V:F6})";
// }

/// <summary>
/// 三维向量 - 空间几何计算基础结构（向后兼容包装）
/// 提供三维空间中的点坐标和向量运算功能
/// 内部使用 RealScene3D.Domain.Mathematics.Vector3d
/// </summary>
// public class Vector3D
// {
//     /// <summary>
//     /// X坐标分量
//     /// </summary>
//     public double X { get; set; }

//     /// <summary>
//     /// Y坐标分量
//     /// </summary>
//     public double Y { get; set; }

//     /// <summary>
//     /// Z坐标分量
//     /// </summary>
//     public double Z { get; set; }

//     /// <summary>
//     /// 默认构造函数
//     /// </summary>
//     public Vector3D() { }

//     /// <summary>
//     /// 带参数构造函数
//     /// </summary>
//     public Vector3D(double x, double y, double z)
//     {
//         X = x;
//         Y = y;
//         Z = z;
//     }

//     /// <summary>
//     /// 从 Vector3d 转换
//     /// </summary>
//     public static implicit operator Vector3D(Vector3d v) => new Vector3D(v.x, v.y, v.z);

//     /// <summary>
//     /// 转换为 Vector3d
//     /// </summary>
//     public static implicit operator Vector3d(Vector3D v) => new Vector3d(v.X, v.Y, v.Z);

//     /// <summary>
//     /// 向量减法
//     /// </summary>
//     public static Vector3D operator -(Vector3D left, Vector3D right)
//     {
//         return new Vector3D(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
//     }

//     /// <summary>
//     /// 向量加法
//     /// </summary>
//     public static Vector3D operator +(Vector3D left, Vector3D right)
//     {
//         return new Vector3D(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
//     }

//     /// <summary>
//     /// 向量标量乘法
//     /// </summary>
//     public static Vector3D operator *(Vector3D vector, double scalar)
//     {
//         return new Vector3D(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);
//     }

//     /// <summary>
//     /// 点积
//     /// </summary>
//     public double Dot(Vector3D other) => X * other.X + Y * other.Y + Z * other.Z;

//     /// <summary>
//     /// 叉积
//     /// </summary>
//     public Vector3D Cross(Vector3D other)
//     {
//         return new Vector3D(
//             Y * other.Z - Z * other.Y,
//             Z * other.X - X * other.Z,
//             X * other.Y - Y * other.X
//         );
//     }

//     /// <summary>
//     /// 向量长度
//     /// </summary>
//     public double Length() => Math.Sqrt(X * X + Y * Y + Z * Z);

//     /// <summary>
//     /// 单位化
//     /// </summary>
//     public Vector3D Normalize()
//     {
//         var length = Length();
//         if (length < 1e-10) return new Vector3D(0, 0, 1);
//         return new Vector3D(X / length, Y / length, Z / length);
//     }

//     /// <summary>
//     /// 距离计算
//     /// </summary>
//     public double DistanceTo(Vector3D point)
//     {
//         var dx = point.X - X;
//         var dy = point.Y - Y;
//         var dz = point.Z - Z;
//         return Math.Sqrt(dx * dx + dy * dy + dz * dz);
//     }

//     /// <summary>
//     /// 夹角计算
//     /// </summary>
//     public double AngleTo(Vector3D other)
//     {
//         var dotProduct = Dot(other);
//         var magnitude1 = Length();
//         var magnitude2 = other.Length();
//         if (magnitude1 == 0 || magnitude2 == 0) return 0;
//         var cosAngle = dotProduct / (magnitude1 * magnitude2);
//         return Math.Acos(Math.Max(-1.0, Math.Min(1.0, cosAngle)));
//     }
// }

// /// <summary>
// /// 三维包围盒类 - 轴对齐包围盒（AABB）（向后兼容包装）
// /// 内部使用 RealScene3D.Domain.Geometry.Box3
// /// </summary>
// public class BoundingBox3D
// {
//     public double MinX { get; set; }
//     public double MinY { get; set; }
//     public double MinZ { get; set; }
//     public double MaxX { get; set; }
//     public double MaxY { get; set; }
//     public double MaxZ { get; set; }

//     public BoundingBox3D() { }

//     public BoundingBox3D(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
//     {
//         MinX = minX;
//         MinY = minY;
//         MinZ = minZ;
//         MaxX = maxX;
//         MaxY = maxY;
//         MaxZ = maxZ;
//     }

//     public Vector3D GetCenter() => new Vector3D((MinX + MaxX) / 2, (MinY + MaxY) / 2, (MinZ + MaxZ) / 2);

//     public (double Width, double Height, double Depth) GetSize() => (MaxX - MinX, MaxY - MinY, MaxZ - MinZ);

//     public bool Contains(Vector3D point) =>
//         point.X >= MinX && point.X <= MaxX &&
//         point.Y >= MinY && point.Y <= MaxY &&
//         point.Z >= MinZ && point.Z <= MaxZ;

//     public bool Intersects(BoundingBox3D other) =>
//         !(MaxX < other.MinX || MinX > other.MaxX ||
//           MaxY < other.MinY || MinY > other.MaxY ||
//           MaxZ < other.MinZ || MinZ > other.MaxZ);

//     public double DistanceTo(Vector3D point)
//     {
//         var closest = new Vector3D(
//             Math.Max(MinX, Math.Min(MaxX, point.X)),
//             Math.Max(MinY, Math.Min(MaxY, point.Y)),
//             Math.Max(MinZ, Math.Min(MaxZ, point.Z))
//         );
//         return point.DistanceTo(closest);
//     }

//     public double GetVolume()
//     {
//         var size = GetSize();
//         return size.Width * size.Height * size.Depth;
//     }

//     public bool IsValid() =>
//         MinX <= MaxX && MinY <= MaxY && MinZ <= MaxZ &&
//         (MaxX - MinX > 0 || MaxY - MinY > 0 || MaxZ - MinZ > 0);

//     public bool IsEmpty => !IsValid();

//     public void Expand(Vector3D point)
//     {
//         MinX = Math.Min(MinX, point.X);
//         MinY = Math.Min(MinY, point.Y);
//         MinZ = Math.Min(MinZ, point.Z);
//         MaxX = Math.Max(MaxX, point.X);
//         MaxY = Math.Max(MaxY, point.Y);
//         MaxZ = Math.Max(MaxZ, point.Z);
//     }

//     public void Expand(BoundingBox3D other)
//     {
//         MinX = Math.Min(MinX, other.MinX);
//         MinY = Math.Min(MinY, other.MinY);
//         MinZ = Math.Min(MinZ, other.MinZ);
//         MaxX = Math.Max(MaxX, other.MaxX);
//         MaxY = Math.Max(MaxY, other.MaxY);
//         MaxZ = Math.Max(MaxZ, other.MaxZ);
//     }
// }

/// <summary>
/// 三角形 - 3D几何三角形单元
/// 用于网格简化、切片生成等几何计算
/// 支持法向量计算、面积计算、质心计算等基础几何操作
/// 现已扩展支持纹理坐标和顶点法线
/// </summary>
// public class Triangle
// {
//     public Vector3D V1 { get; set; } = new Vector3D();
//     public Vector3D V2 { get; set; } = new Vector3D();
//     public Vector3D V3 { get; set; } = new Vector3D();
//     public Vector3D? Normal { get; set; }
//     public Vector3D? Normal1 { get; set; }
//     public Vector3D? Normal2 { get; set; }
//     public Vector3D? Normal3 { get; set; }
//     public Vector2D? UV1 { get; set; }
//     public Vector2D? UV2 { get; set; }
//     public Vector2D? UV3 { get; set; }
//     public string? MaterialName { get; set; }

//     public Vector3D[] Vertices
//     {
//         get => new[] { V1, V2, V3 };
//         set
//         {
//             if (value != null && value.Length >= 3)
//             {
//                 V1 = value[0];
//                 V2 = value[1];
//                 V3 = value[2];
//             }
//         }
//     }

//     public Triangle() { }

//     public Triangle(Vector3D v1, Vector3D v2, Vector3D v3)
//     {
//         V1 = v1;
//         V2 = v2;
//         V3 = v3;
//     }

//     public Vector3D ComputeNormal() => (V2 - V1).Cross(V3 - V1).Normalize();

//     public double ComputeArea()
//     {
//         var edge1 = V2 - V1;
//         var edge2 = V3 - V1;
//         return edge1.Cross(edge2).Length() * 0.5;
//     }

//     public Vector3D ComputeCenter() => new Vector3D(
//         (V1.X + V2.X + V3.X) / 3.0,
//         (V1.Y + V2.Y + V3.Y) / 3.0,
//         (V1.Z + V2.Z + V3.Z) / 3.0
//     );

//     public BoundingBox3D ComputeBoundingBox() => new BoundingBox3D(
//         Math.Min(V1.X, Math.Min(V2.X, V3.X)),
//         Math.Min(V1.Y, Math.Min(V2.Y, V3.Y)),
//         Math.Min(V1.Z, Math.Min(V2.Z, V3.Z)),
//         Math.Max(V1.X, Math.Max(V2.X, V3.X)),
//         Math.Max(V1.Y, Math.Max(V2.Y, V3.Y)),
//         Math.Max(V1.Z, Math.Max(V2.Z, V3.Z))
//     );

//     public bool Contains(Vector3D point)
//     {
//         var v0 = V3 - V1;
//         var v1 = V2 - V1;
//         var v2 = point - V1;
//         var dot00 = v0.Dot(v0);
//         var dot01 = v0.Dot(v1);
//         var dot02 = v0.Dot(v2);
//         var dot11 = v1.Dot(v1);
//         var dot12 = v1.Dot(v2);
//         var invDenom = 1.0 / (dot00 * dot11 - dot01 * dot01);
//         var u = (dot11 * dot02 - dot01 * dot12) * invDenom;
//         var v = (dot00 * dot12 - dot01 * dot02) * invDenom;
//         return (u >= 0) && (v >= 0) && (u + v <= 1);
//     }

//     public Triangle Clone() => new Triangle(
//         new Vector3D(V1.X, V1.Y, V1.Z),
//         new Vector3D(V2.X, V2.Y, V2.Z),
//         new Vector3D(V3.X, V3.Y, V3.Z)
//     )
//     {
//         Normal = Normal != null ? new Vector3D(Normal.X, Normal.Y, Normal.Z) : null,
//         Normal1 = Normal1 != null ? new Vector3D(Normal1.X, Normal1.Y, Normal1.Z) : null,
//         Normal2 = Normal2 != null ? new Vector3D(Normal2.X, Normal2.Y, Normal2.Z) : null,
//         Normal3 = Normal3 != null ? new Vector3D(Normal3.X, Normal3.Y, Normal3.Z) : null,
//         UV1 = UV1?.Clone(),
//         UV2 = UV2?.Clone(),
//         UV3 = UV3?.Clone(),
//         MaterialName = MaterialName
//     };

//     public bool HasUVCoordinates() => UV1 != null && UV2 != null && UV3 != null;
//     public bool HasVertexNormals() => Normal1 != null && Normal2 != null && Normal3 != null;
// }
