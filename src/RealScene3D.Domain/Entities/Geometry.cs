/// <summary>
/// 几何基础类型定义
/// 提供3D渲染和切片处理所需的几何数学基础结构
/// 包括向量、视口、包围盒等核心几何概念
/// </summary>
namespace RealScene3D.Domain.Entities;

/// <summary>
/// 三维向量 - 空间几何计算基础结构
/// 提供三维空间中的点坐标和向量运算功能
/// 支持向量运算符重载和基本几何计算
/// </summary>
public class Vector3D
{
    /// <summary>
    /// X坐标分量 - 三维空间X轴坐标值
    /// 表示向量或点在X轴方向上的投影
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y坐标分量 - 三维空间Y轴坐标值
    /// 表示向量或点在Y轴方向上的投影
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Z坐标分量 - 三维空间Z轴坐标值
    /// 表示向量或点在Z轴方向上的投影
    /// </summary>
    public double Z { get; set; }

    /// <summary>
    /// 默认构造函数 - 创建零向量
    /// 初始化X、Y、Z坐标均为0的向量
    /// </summary>
    public Vector3D() { }

    /// <summary>
    /// 带参数构造函数 - 创建指定坐标的向量
    /// </summary>
    /// <param name="x">X坐标值</param>
    /// <param name="y">Y坐标值</param>
    /// <param name="z">Z坐标值</param>
    public Vector3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// 向量减法运算符重载 - 计算两个向量的差值
    /// 用于计算从一个点到另一个点的方向向量
    /// </summary>
    /// <param name="left">左操作数向量</param>
    /// <param name="right">右操作数向量</param>
    /// <returns>两个向量相减的结果向量</returns>
    public static Vector3D operator -(Vector3D left, Vector3D right)
    {
        return new Vector3D
        {
            X = left.X - right.X,
            Y = left.Y - right.Y,
            Z = left.Z - right.Z
        };
    }

    /// <summary>
    /// 向量加法运算符重载 - 计算两个向量的和
    /// 用于向量合成和位置平移计算
    /// </summary>
    /// <param name="left">左操作数向量</param>
    /// <param name="right">右操作数向量</param>
    /// <returns>两个向量相加的结果向量</returns>
    public static Vector3D operator +(Vector3D left, Vector3D right)
    {
        return new Vector3D
        {
            X = left.X + right.X,
            Y = left.Y + right.Y,
            Z = left.Z + right.Z
        };
    }

    /// <summary>
    /// 向量标量乘法运算符重载 - 计算向量与标量的乘积
    /// 用于向量缩放、单位化等操作
    /// </summary>
    /// <param name="vector">向量操作数</param>
    /// <param name="scalar">标量操作数</param>
    /// <returns>向量与标量相乘的结果向量</returns>
    public static Vector3D operator *(Vector3D vector, double scalar)
    {
        return new Vector3D
        {
            X = vector.X * scalar,
            Y = vector.Y * scalar,
            Z = vector.Z * scalar
        };
    }

    /// <summary>
    /// 向量点积计算 - 计算两个向量的点积
    /// 用于计算夹角余弦、投影长度等几何计算
    /// </summary>
    /// <param name="other">另一个向量</param>
    /// <returns>两个向量的点积结果</returns>
    public double Dot(Vector3D other)
    {
        return X * other.X + Y * other.Y + Z * other.Z;
    }

    /// <summary>
    /// 向量叉积计算 - 计算两个向量的叉积
    /// 用于计算垂直向量、法向量等几何计算
    /// </summary>
    /// <param name="other">另一个向量</param>
    /// <returns>两个向量的叉积结果向量</returns>
    public Vector3D Cross(Vector3D other)
    {
        return new Vector3D
        {
            X = Y * other.Z - Z * other.Y,
            Y = Z * other.X - X * other.Z,
            Z = X * other.Y - Y * other.X
        };
    }

    /// <summary>
    /// 向量长度计算 - 计算向量的欧几里得范数
    /// 用于距离计算、单位化等操作
    /// </summary>
    /// <returns>向量的长度值</returns>
    public double Length()
    {
        return Math.Sqrt(X * X + Y * Y + Z * Z);
    }

    /// <summary>
    /// 向量单位化 - 将向量标准化为单位向量
    /// 用于方向计算、光照计算等操作
    /// </summary>
    /// <returns>单位化后的向量</returns>
    public Vector3D Normalize()
    {
        var length = Length();
        if (length < 1e-10) return new Vector3D(0, 0, 1); // 避免除以零，返回默认向上方向
        return new Vector3D(X / length, Y / length, Z / length);
    }

    /// <summary>
    /// 计算两点间的距离 - 欧几里得距离计算
    /// </summary>
    /// <param name="point">目标点</param>
    /// <returns>两点间的欧几里得距离</returns>
    public double DistanceTo(Vector3D point)
    {
        var dx = point.X - X;
        var dy = point.Y - Y;
        var dz = point.Z - Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// 计算与另一向量的夹角 - 返回弧度值
    /// </summary>
    /// <param name="other">另一个向量</param>
    /// <returns>夹角（弧度）</returns>
    public double AngleTo(Vector3D other)
    {
        var dotProduct = Dot(other);
        var magnitude1 = Length();
        var magnitude2 = other.Length();

        if (magnitude1 == 0 || magnitude2 == 0) return 0;

        var cosAngle = dotProduct / (magnitude1 * magnitude2);
        // 限制在 [-1, 1] 范围内，避免浮点误差导致的 Acos 参数越界
        return Math.Acos(Math.Max(-1.0, Math.Min(1.0, cosAngle)));
    }
}

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
    public Vector3D CameraPosition { get; set; } = new Vector3D();

    /// <summary>
    /// 相机朝向 - 相机的观察方向向量
    /// 表示相机指向的方向，用于计算视野中心线
    /// 在视锥剔除算法中用于判断物体是否在视野范围内
    /// </summary>
    public Vector3D CameraDirection { get; set; } = new Vector3D();

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
    public ViewportInfo(Vector3D cameraPosition, Vector3D cameraDirection, double fieldOfView = Math.PI / 3, double nearPlane = 1.0, double farPlane = 10000.0)
    {
        CameraPosition = cameraPosition;
        CameraDirection = cameraDirection;
        FieldOfView = fieldOfView;
        NearPlane = nearPlane;
        FarPlane = farPlane;
    }
}

/// <summary>
/// 三维包围盒类 - 轴对齐包围盒（AABB）
/// 用于定义三维空间中的矩形区域，支持空间查询和碰撞检测
/// 提供高效的包围体表示和相交测试算法
/// </summary>
public class BoundingBox3D
{
    /// <summary>
    /// 最小X坐标 - 包围盒在X轴的最小值
    /// </summary>
    public double MinX { get; set; }

    /// <summary>
    /// 最小Y坐标 - 包围盒在Y轴的最小值
    /// </summary>
    public double MinY { get; set; }

    /// <summary>
    /// 最小Z坐标 - 包围盒在Z轴的最小值
    /// </summary>
    public double MinZ { get; set; }

    /// <summary>
    /// 最大X坐标 - 包围盒在X轴的最大值
    /// </summary>
    public double MaxX { get; set; }

    /// <summary>
    /// 最大Y坐标 - 包围盒在Y轴的最大值
    /// </summary>
    public double MaxY { get; set; }

    /// <summary>
    /// 最大Z坐标 - 包围盒在Z轴的最大值
    /// </summary>
    public double MaxZ { get; set; }

    /// <summary>
    /// 默认构造函数
    /// </summary>
    public BoundingBox3D() { }

    /// <summary>
    /// 带参数构造函数
    /// </summary>
    public BoundingBox3D(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        MinX = minX;
        MinY = minY;
        MinZ = minZ;
        MaxX = maxX;
        MaxY = maxY;
        MaxZ = maxZ;
    }

    /// <summary>
    /// 计算包围盒的中心点
    /// </summary>
    public Vector3D GetCenter()
    {
        return new Vector3D
        {
            X = (MinX + MaxX) / 2,
            Y = (MinY + MaxY) / 2,
            Z = (MinZ + MaxZ) / 2
        };
    }

    /// <summary>
    /// 计算包围盒的尺寸
    /// </summary>
    public (double Width, double Height, double Depth) GetSize()
    {
        return (MaxX - MinX, MaxY - MinY, MaxZ - MinZ);
    }

    /// <summary>
    /// 判断点是否在包围盒内
    /// </summary>
    public bool Contains(Vector3D point)
    {
        return point.X >= MinX && point.X <= MaxX &&
               point.Y >= MinY && point.Y <= MaxY &&
               point.Z >= MinZ && point.Z <= MaxZ;
    }

    /// <summary>
    /// 判断两个包围盒是否相交
    /// </summary>
    public bool Intersects(BoundingBox3D other)
    {
        return !(MaxX < other.MinX || MinX > other.MaxX ||
                 MaxY < other.MinY || MinY > other.MaxY ||
                 MaxZ < other.MinZ || MinZ > other.MaxZ);
    }

    /// <summary>
    /// 距离计算 - 计算点到包围盒的最短距离
    /// 用于LOD选择、优先级排序等算法
    /// </summary>
    /// <param name="point">目标点</param>
    /// <returns>点到包围盒的最短距离</returns>
    public double DistanceTo(Vector3D point)
    {
        var closest = new Vector3D
        {
            X = Math.Max(MinX, Math.Min(MaxX, point.X)),
            Y = Math.Max(MinY, Math.Min(MaxY, point.Y)),
            Z = Math.Max(MinZ, Math.Min(MaxZ, point.Z))
        };

        var dx = point.X - closest.X;
        var dy = point.Y - closest.Y;
        var dz = point.Z - closest.Z;

        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// 计算包围盒的体积
    /// </summary>
    public double GetVolume()
    {
        var size = GetSize();
        return size.Width * size.Height * size.Depth;
    }

    /// <summary>
    /// 验证包围盒的有效性
    /// 要求包围盒至少在一个维度上有非零尺寸
    /// </summary>
    public bool IsValid()
    {
        // 检查坐标顺序是否正确
        if (!(MinX <= MaxX && MinY <= MaxY && MinZ <= MaxZ))
            return false;

        // 检查是否至少有一个维度的尺寸大于0（避免退化为点或线）
        // 对于有效的3D包围盒，应该在所有维度上都有非零尺寸
        var sizeX = MaxX - MinX;
        var sizeY = MaxY - MinY;
        var sizeZ = MaxZ - MinZ;

        // 至少需要在一个维度上有尺寸（避免全零包围盒）
        return sizeX > 0 || sizeY > 0 || sizeZ > 0;
    }

    /// <summary>
    /// 扩展包围盒以包含指定点
    /// </summary>
    public void Expand(Vector3D point)
    {
        MinX = Math.Min(MinX, point.X);
        MinY = Math.Min(MinY, point.Y);
        MinZ = Math.Min(MinZ, point.Z);
        MaxX = Math.Max(MaxX, point.X);
        MaxY = Math.Max(MaxY, point.Y);
        MaxZ = Math.Max(MaxZ, point.Z);
    }

    /// <summary>
    /// 扩展包围盒以包含另一个包围盒
    /// </summary>
    public void Expand(BoundingBox3D other)
    {
        MinX = Math.Min(MinX, other.MinX);
        MinY = Math.Min(MinY, other.MinY);
        MinZ = Math.Min(MinZ, other.MinZ);
        MaxX = Math.Max(MaxX, other.MaxX);
        MaxY = Math.Max(MaxY, other.MaxY);
        MaxZ = Math.Max(MaxZ, other.MaxZ);
    }
}

/// <summary>
/// 2D向量 - 用于UV纹理坐标等二维数据
/// </summary>
public class Vector2D
{
    /// <summary>
    /// U坐标（X轴）
    /// </summary>
    public double U { get; set; }

    /// <summary>
    /// V坐标（Y轴）
    /// </summary>
    public double V { get; set; }

    /// <summary>
    /// 默认构造函数
    /// </summary>
    public Vector2D()
    {
        U = 0;
        V = 0;
    }

    /// <summary>
    /// 带参数构造函数
    /// </summary>
    public Vector2D(double u, double v)
    {
        U = u;
        V = v;
    }

    /// <summary>
    /// 向量长度
    /// </summary>
    public double Length() => Math.Sqrt(U * U + V * V);

    /// <summary>
    /// 克隆2D向量
    /// </summary>
    public Vector2D Clone() => new Vector2D(U, V);

    /// <summary>
    /// 字符串表示
    /// </summary>
    public override string ToString() => $"({U:F6}, {V:F6})";
}

/// <summary>
/// 三角形 - 3D几何三角形单元
/// 用于网格简化、切片生成等几何计算
/// 支持法向量计算、面积计算、质心计算等基础几何操作
/// 现已扩展支持纹理坐标和顶点法线
/// </summary>
public class Triangle
{
    /// <summary>
    /// 顶点1 - 三角形的第一个顶点
    /// </summary>
    public Vector3D V1 { get; set; }

    /// <summary>
    /// 顶点2 - 三角形的第二个顶点
    /// </summary>
    public Vector3D V2 { get; set; }

    /// <summary>
    /// 顶点3 - 三角形的第三个顶点
    /// </summary>
    public Vector3D V3 { get; set; }

    /// <summary>
    /// 法向量 - 三角形的单位法向量（可选，用于平面法线）
    /// </summary>
    public Vector3D? Normal { get; set; }

    // ========== 顶点法线（每个顶点一个法线，用于平滑着色） ==========

    /// <summary>
    /// 顶点1的法线
    /// </summary>
    public Vector3D? Normal1 { get; set; }

    /// <summary>
    /// 顶点2的法线
    /// </summary>
    public Vector3D? Normal2 { get; set; }

    /// <summary>
    /// 顶点3的法线
    /// </summary>
    public Vector3D? Normal3 { get; set; }

    // ========== UV纹理坐标 ==========

    /// <summary>
    /// 顶点1的UV纹理坐标
    /// </summary>
    public Vector2D? UV1 { get; set; }

    /// <summary>
    /// 顶点2的UV纹理坐标
    /// </summary>
    public Vector2D? UV2 { get; set; }

    /// <summary>
    /// 顶点3的UV纹理坐标
    /// </summary>
    public Vector2D? UV3 { get; set; }

    // ========== 材质信息 ==========

    /// <summary>
    /// 材质名称 - 关联到Material对象的名称
    /// 用于纹理图集中的材质查找
    /// </summary>
    public string? MaterialName { get; set; }

    /// <summary>
    /// 顶点数组 - 向后兼容属性
    /// </summary>
    public Vector3D[] Vertices
    {
        get => new[] { V1, V2, V3 };
        set
        {
            if (value != null && value.Length >= 3)
            {
                V1 = value[0];
                V2 = value[1];
                V3 = value[2];
            }
        }
    }

    /// <summary>
    /// 默认构造函数
    /// </summary>
    public Triangle()
    {
        V1 = new Vector3D();
        V2 = new Vector3D();
        V3 = new Vector3D();
    }

    /// <summary>
    /// 带参数构造函数 - 创建指定顶点的三角形
    /// </summary>
    /// <param name="v1">顶点1</param>
    /// <param name="v2">顶点2</param>
    /// <param name="v3">顶点3</param>
    public Triangle(Vector3D v1, Vector3D v2, Vector3D v3)
    {
        V1 = v1;
        V2 = v2;
        V3 = v3;
    }

    /// <summary>
    /// 计算法向量 - 使用右手法则计算三角形法向量
    /// 公式: normal = (v2 - v1) × (v3 - v1)
    /// </summary>
    /// <returns>单位化的法向量</returns>
    public Vector3D ComputeNormal()
    {
        var edge1 = V2 - V1;
        var edge2 = V3 - V1;
        var normal = edge1.Cross(edge2);
        return normal.Normalize();
    }

    /// <summary>
    /// 计算面积 - 使用海伦公式计算三角形面积
    /// 公式: area = 0.5 * ||(v2 - v1) × (v3 - v1)||
    /// </summary>
    /// <returns>三角形面积</returns>
    public double ComputeArea()
    {
        var edge1 = V2 - V1;
        var edge2 = V3 - V1;
        var crossProduct = edge1.Cross(edge2);
        return crossProduct.Length() * 0.5;
    }

    /// <summary>
    /// 计算质心 - 计算三角形的几何中心
    /// 公式: center = (v1 + v2 + v3) / 3
    /// </summary>
    /// <returns>质心坐标</returns>
    public Vector3D ComputeCenter()
    {
        return new Vector3D
        {
            X = (V1.X + V2.X + V3.X) / 3.0,
            Y = (V1.Y + V2.Y + V3.Y) / 3.0,
            Z = (V1.Z + V2.Z + V3.Z) / 3.0
        };
    }

    /// <summary>
    /// 计算包围盒 - 计算三角形的轴对齐包围盒
    /// </summary>
    /// <returns>三角形的包围盒</returns>
    public BoundingBox3D ComputeBoundingBox()
    {
        return new BoundingBox3D
        {
            MinX = Math.Min(V1.X, Math.Min(V2.X, V3.X)),
            MinY = Math.Min(V1.Y, Math.Min(V2.Y, V3.Y)),
            MinZ = Math.Min(V1.Z, Math.Min(V2.Z, V3.Z)),
            MaxX = Math.Max(V1.X, Math.Max(V2.X, V3.X)),
            MaxY = Math.Max(V1.Y, Math.Max(V2.Y, V3.Y)),
            MaxZ = Math.Max(V1.Z, Math.Max(V2.Z, V3.Z))
        };
    }

    /// <summary>
    /// 判断点是否在三角形内 - 使用重心坐标法
    /// </summary>
    /// <param name="point">待测试的点</param>
    /// <returns>点是否在三角形内</returns>
    public bool Contains(Vector3D point)
    {
        // 使用重心坐标判断
        var v0 = V3 - V1;
        var v1 = V2 - V1;
        var v2 = point - V1;

        var dot00 = v0.Dot(v0);
        var dot01 = v0.Dot(v1);
        var dot02 = v0.Dot(v2);
        var dot11 = v1.Dot(v1);
        var dot12 = v1.Dot(v2);

        var invDenom = 1.0 / (dot00 * dot11 - dot01 * dot01);
        var u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        var v = (dot00 * dot12 - dot01 * dot02) * invDenom;

        return (u >= 0) && (v >= 0) && (u + v <= 1);
    }

    /// <summary>
    /// 克隆三角形 - 创建三角形的深拷贝
    /// </summary>
    /// <returns>克隆的三角形实例</returns>
    public Triangle Clone()
    {
        return new Triangle(
            new Vector3D(V1.X, V1.Y, V1.Z),
            new Vector3D(V2.X, V2.Y, V2.Z),
            new Vector3D(V3.X, V3.Y, V3.Z)
        )
        {
            Normal = Normal != null ? new Vector3D(Normal.X, Normal.Y, Normal.Z) : null,
            Normal1 = Normal1 != null ? new Vector3D(Normal1.X, Normal1.Y, Normal1.Z) : null,
            Normal2 = Normal2 != null ? new Vector3D(Normal2.X, Normal2.Y, Normal2.Z) : null,
            Normal3 = Normal3 != null ? new Vector3D(Normal3.X, Normal3.Y, Normal3.Z) : null,
            UV1 = UV1?.Clone(),
            UV2 = UV2?.Clone(),
            UV3 = UV3?.Clone()
        };
    }

    /// <summary>
    /// 判断三角形是否有UV纹理坐标
    /// </summary>
    public bool HasUVCoordinates() => UV1 != null && UV2 != null && UV3 != null;

    /// <summary>
    /// 判断三角形是否有顶点法线
    /// </summary>
    public bool HasVertexNormals() => Normal1 != null && Normal2 != null && Normal3 != null;
}