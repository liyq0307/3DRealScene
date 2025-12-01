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