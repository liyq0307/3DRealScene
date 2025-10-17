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
        if (length == 0) return new Vector3D(0, 0, 0);
        return new Vector3D(X / length, Y / length, Z / length);
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
/// 包围盒 - 轴对齐包围盒（AABB）结构
/// 用于空间查询、碰撞检测、视锥剔除等几何计算
/// 提供高效的包围体表示和相交测试算法
/// </summary>
public class BoundingBox
{
    /// <summary>
    /// 最小X坐标 - 包围盒在X轴的最小值
    /// 定义了包围盒在X轴方向上的最小边界
    /// </summary>
    public double MinX { get; set; }

    /// <summary>
    /// 最小Y坐标 - 包围盒在Y轴的最小值
    /// 定义了包围盒在Y轴方向上的最小边界
    /// </summary>
    public double MinY { get; set; }

    /// <summary>
    /// 最小Z坐标 - 包围盒在Z轴的最小值
    /// 定义了包围盒在Z轴方向上的最小边界
    /// </summary>
    public double MinZ { get; set; }

    /// <summary>
    /// 最大X坐标 - 包围盒在X轴的最大值
    /// 定义了包围盒在X轴方向上的最大边界
    /// </summary>
    public double MaxX { get; set; }

    /// <summary>
    /// 最大Y坐标 - 包围盒在Y轴的最大值
    /// 定义了包围盒在Y轴方向上的最大边界
    /// </summary>
    public double MaxY { get; set; }

    /// <summary>
    /// 最大Z坐标 - 包围盒在Z轴的最大值
    /// 定义了包围盒在Z轴方向上的最大边界
    /// </summary>
    public double MaxZ { get; set; }

    /// <summary>
    /// 中心点计算 - 计算包围盒的几何中心
    /// 用于距离计算、包围球近似等操作
    /// </summary>
    /// <returns>包围盒中心点的坐标</returns>
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
    /// 尺寸计算 - 计算包围盒在三个轴向的长度
    /// 用于体积计算、大小比较等操作
    /// </summary>
    /// <returns>包含三个轴向尺寸的元组</returns>
    public (double Width, double Height, double Depth) GetSize()
    {
        return (MaxX - MinX, MaxY - MinY, MaxZ - MinZ);
    }

    /// <summary>
    /// 点包容性测试 - 判断点是否在包围盒内
    /// 用于快速剔除和空间查询优化
    /// </summary>
    /// <param name="point">待测试的点</param>
    /// <returns>点是否在包围盒内</returns>
    public bool Contains(Vector3D point)
    {
        return point.X >= MinX && point.X <= MaxX &&
               point.Y >= MinY && point.Y <= MaxY &&
               point.Z >= MinZ && point.Z <= MaxZ;
    }

    /// <summary>
    /// 包围盒相交测试 - 判断两个包围盒是否相交
    /// 使用分离轴定理进行快速相交测试
    /// </summary>
    /// <param name="other">另一个包围盒</param>
    /// <returns>两个包围盒是否相交</returns>
    public bool Intersects(BoundingBox other)
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
}