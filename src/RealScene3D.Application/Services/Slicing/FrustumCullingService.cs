using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// 视锥剔除服务 - 负责视锥剔除和预测加载优化
/// 基于视锥体的可见性判断算法，提升渲染性能
/// 算法参考： https://www.scratchapixel.com/lessons/3d-basic-rendering/visibility-determination/frustum-culling
/// 预测加载基于运动矢量，提前加载可能需要的数据以减少等待时间
/// 
/// 注意：该服务为通用服务，可应用于不同类型的三维数据（点云、网格等）
/// </summary>
public class FrustumCullingService
{
    private readonly ILogger<FrustumCullingService> _logger;

    public FrustumCullingService(ILogger<FrustumCullingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 执行视锥剔除 - 基于视锥体的可见性判断算法
    /// 算法：使用六个平面（上下左右远近）定义视锥体，判断物体包围盒是否与视锥体相交
    /// 优化：提前剔除完全在视锥外的物体，减少GPU渲染负担
    /// </summary>
    /// <typeparam name="T">项目类型（支持泛型以适应不同数据类型）</typeparam>
    /// <param name="viewport">视口信息</param>
    /// <param name="items">待剔除的项目集合</param>
    /// <param name="getBoundingBox">获取包围盒的委托函数</param>
    /// <returns>可见的项目集合</returns>
    public Task<IEnumerable<T>> PerformFrustumCullingAsync<T>(
        ViewportInfo viewport,
        IEnumerable<T> items,
        Func<T, BoundingBox3D> getBoundingBox) where T : class
    {
        try
        {
            // 1. 构建视锥平面
            var frustumPlanes = BuildFrustumPlanes(viewport);

            // 2. 遍历所有项目，判断可见性
            var visibleItems = items.Where(item =>
            {
                var boundingBox = getBoundingBox(item);
                return IsVisible(boundingBox, frustumPlanes);
            }).ToList();

            _logger.LogDebug("视锥剔除完成：输入 {InputCount} 个项目，可见 {VisibleCount} 个项目",
                items.Count(), visibleItems.Count);

            return Task.FromResult<IEnumerable<T>>(visibleItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "视锥剔除过程中发生错误");
            // 出错时返回所有项目，避免数据丢失
            return Task.FromResult(items);
        }
    }

    /// <summary>
    /// 预测加载 - 基于运动矢量的加载预测算法
    /// 算法：根据相机移动方向预测接下来可能需要的数据，提前加载以减少等待时间
    /// 策略：
    /// 1. 计算扩展视锥（当前视锥 + 运动方向）
    /// 2. 判断物体是否在扩展视锥内
    /// 3. 按距离排序，优先加载最近的物体
    /// </summary>
    /// <typeparam name="T">项目类型</typeparam>
    /// <param name="currentViewport">当前视口信息</param>
    /// <param name="movementVector">运动矢量（表示相机移动方向和速度）</param>
    /// <param name="items">待加载的项目集合</param>
    /// <param name="getBoundingBox">获取包围盒的委托函数</param>
    /// <returns>预测需要加载的项目集合（按优先级排序）</returns>
    public Task<IEnumerable<T>> PredictLoadingAsync<T>(
        ViewportInfo currentViewport,
        Vector3D movementVector,
        IEnumerable<T> items,
        Func<T, BoundingBox3D> getBoundingBox) where T : class
    {
        try
        {
            // 1. 创建预测视口（当前位置 + 运动矢量）
            var predictedViewport = new ViewportInfo
            {
                CameraPosition = new Vector3D
                {
                    X = currentViewport.CameraPosition.X + movementVector.X,
                    Y = currentViewport.CameraPosition.Y + movementVector.Y,
                    Z = currentViewport.CameraPosition.Z + movementVector.Z
                },
                CameraDirection = currentViewport.CameraDirection,
                FieldOfView = currentViewport.FieldOfView,
                NearPlane = currentViewport.NearPlane,
                FarPlane = currentViewport.FarPlane,
                AspectRatio = currentViewport.AspectRatio
            };

            // 2. 构建预测视锥平面
            var predictedFrustumPlanes = BuildFrustumPlanes(predictedViewport);

            // 3. 筛选预测视锥内的项目
            var predictedItems = items.Where(item =>
            {
                var boundingBox = getBoundingBox(item);
                return IsVisible(boundingBox, predictedFrustumPlanes);
            }).ToList();

            // 4. 按距离排序（优先加载距离相机较近的物体）
            var sortedItems = predictedItems.OrderBy(item =>
            {
                var bbox = getBoundingBox(item);
                var centerX = (bbox.MinX + bbox.MaxX) / 2.0;
                var centerY = (bbox.MinY + bbox.MaxY) / 2.0;
                var centerZ = (bbox.MinZ + bbox.MaxZ) / 2.0;

                var dx = centerX - predictedViewport.CameraPosition.X;
                var dy = centerY - predictedViewport.CameraPosition.Y;
                var dz = centerZ - predictedViewport.CameraPosition.Z;

                return Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }).ToList();

            _logger.LogDebug("预测加载完成：预测需要加载 {PredictedCount} 个项目",
                sortedItems.Count);

            return Task.FromResult<IEnumerable<T>>(sortedItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "预测加载过程中发生错误");
            return Task.FromResult(Enumerable.Empty<T>());
        }
    }

    /// <summary>
    /// 判断包围盒是否在视锥内可见
    /// </summary>
    private bool IsVisible(BoundingBox3D boundingBox, FrustumPlane[] frustumPlanes)
    {
        // 对每个视锥平面进行测试
        foreach (var plane in frustumPlanes)
        {
            // 找到包围盒相对于平面的"最正向"顶点（P-vertex）
            var px = plane.A > 0 ? boundingBox.MaxX : boundingBox.MinX;
            var py = plane.B > 0 ? boundingBox.MaxY : boundingBox.MinY;
            var pz = plane.C > 0 ? boundingBox.MaxZ : boundingBox.MinZ;

            // 如果P-vertex在平面外侧，则整个包围盒都在平面外侧
            if (plane.DistanceToPoint(px, py, pz) < 0)
            {
                return false; // 包围盒完全在该平面外侧，不可见
            }
        }

        return true; // 通过所有平面测试，可见
    }

    /// <summary>
    /// 构建视锥平面 - 六平面视锥体构建算法
    /// 返回6个平面：左、右、上、下、近、远
    /// 算法：基于投影矩阵或相机参数计算平面方程
    /// </summary>
    private FrustumPlane[] BuildFrustumPlanes(ViewportInfo viewport)
    {
        var planes = new FrustumPlane[6];

        // 简化实现：使用相机参数直接计算平面
        // 在实际应用中，可能需要从投影矩阵提取平面

        // 相机方向向量
        var forward = NormalizeVector(viewport.CameraDirection);

        // 计算up向量：假设世界上方向为Z轴
        var worldUp = new Vector3D { X = 0, Y = 0, Z = 1 };
        var right = CrossProduct(forward, worldUp);
        right = NormalizeVector(right);
        var up = CrossProduct(right, forward);
        up = NormalizeVector(up);

        // 计算视锥参数
        var nearHeight = 2.0 * Math.Tan(viewport.FieldOfView / 2.0) * viewport.NearPlane;
        var nearWidth = nearHeight * viewport.AspectRatio;
        var farHeight = 2.0 * Math.Tan(viewport.FieldOfView / 2.0) * viewport.FarPlane;
        var farWidth = farHeight * viewport.AspectRatio;

        // 近平面中心
        var nearCenter = new
        {
            X = viewport.CameraPosition.X + forward.X * viewport.NearPlane,
            Y = viewport.CameraPosition.Y + forward.Y * viewport.NearPlane,
            Z = viewport.CameraPosition.Z + forward.Z * viewport.NearPlane
        };

        // 远平面中心
        var farCenter = new
        {
            X = viewport.CameraPosition.X + forward.X * viewport.FarPlane,
            Y = viewport.CameraPosition.Y + forward.Y * viewport.FarPlane,
            Z = viewport.CameraPosition.Z + forward.Z * viewport.FarPlane
        };

        // 近平面（Near Plane）
        planes[0] = CreatePlane(
            forward.X, forward.Y, forward.Z,
            nearCenter.X, nearCenter.Y, nearCenter.Z);

        // 远平面（Far Plane）
        planes[1] = CreatePlane(
            -forward.X, -forward.Y, -forward.Z,
            farCenter.X, farCenter.Y, farCenter.Z);

        // 左平面（Left Plane）
        var leftNormal = CrossProduct(up, new Vector3D
        {
            X = farCenter.X - nearWidth / 2.0 * right.X - viewport.CameraPosition.X,
            Y = farCenter.Y - nearWidth / 2.0 * right.Y - viewport.CameraPosition.Y,
            Z = farCenter.Z - nearWidth / 2.0 * right.Z - viewport.CameraPosition.Z
        });
        planes[2] = CreatePlane(
            leftNormal.X, leftNormal.Y, leftNormal.Z,
            viewport.CameraPosition.X, viewport.CameraPosition.Y, viewport.CameraPosition.Z);

        // 右平面（Right Plane）
        var rightNormal = CrossProduct(new Vector3D
        {
            X = farCenter.X + nearWidth / 2.0 * right.X - viewport.CameraPosition.X,
            Y = farCenter.Y + nearWidth / 2.0 * right.Y - viewport.CameraPosition.Y,
            Z = farCenter.Z + nearWidth / 2.0 * right.Z - viewport.CameraPosition.Z
        }, up);
        planes[3] = CreatePlane(
            rightNormal.X, rightNormal.Y, rightNormal.Z,
            viewport.CameraPosition.X, viewport.CameraPosition.Y, viewport.CameraPosition.Z);

        // 上平面（Top Plane）
        var topNormal = CrossProduct(right, new Vector3D
        {
            X = farCenter.X + nearHeight / 2.0 * up.X - viewport.CameraPosition.X,
            Y = farCenter.Y + nearHeight / 2.0 * up.Y - viewport.CameraPosition.Y,
            Z = farCenter.Z + nearHeight / 2.0 * up.Z - viewport.CameraPosition.Z
        });
        planes[4] = CreatePlane(
            topNormal.X, topNormal.Y, topNormal.Z,
            viewport.CameraPosition.X, viewport.CameraPosition.Y, viewport.CameraPosition.Z);

        // 下平面（Bottom Plane）
        var bottomNormal = CrossProduct(new Vector3D
        {
            X = farCenter.X - nearHeight / 2.0 * up.X - viewport.CameraPosition.X,
            Y = farCenter.Y - nearHeight / 2.0 * up.Y - viewport.CameraPosition.Y,
            Z = farCenter.Z - nearHeight / 2.0 * up.Z - viewport.CameraPosition.Z
        }, right);
        planes[5] = CreatePlane(
            bottomNormal.X, bottomNormal.Y, bottomNormal.Z,
            viewport.CameraPosition.X, viewport.CameraPosition.Y, viewport.CameraPosition.Z);

        return planes;
    }

    /// <summary>
    /// 创建平面
    /// </summary>
    private FrustumPlane CreatePlane(double nx, double ny, double nz, double px, double py, double pz)
    {
        return FrustumPlane.FromComponents(nx, ny, nz, px, py, pz);
    }

    /// <summary>
    /// 向量归一化
    /// </summary>
    private Vector3D NormalizeVector(Vector3D v)
    {
        var length = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        if (length < 1e-10) length = 1.0;

        return new Vector3D
        {
            X = v.X / length,
            Y = v.Y / length,
            Z = v.Z / length
        };
    }

    /// <summary>
    /// 向量叉积
    /// </summary>
    private Vector3D CrossProduct(Vector3D a, Vector3D b)
    {
        return new Vector3D
        {
            X = a.Y * b.Z - a.Z * b.Y,
            Y = a.Z * b.X - a.X * b.Z,
            Z = a.X * b.Y - a.Y * b.X
        };
    }
}
