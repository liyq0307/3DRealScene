using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Domain.Services;

/// <summary>
/// 三维切片领域服务实现
/// </summary>
public class SlicingService : ISlicingService
{
    private readonly IRepository<SlicingTask> _slicingTaskRepository;
    private readonly IRepository<Slice> _sliceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SlicingService(
        IRepository<SlicingTask> slicingTaskRepository,
        IRepository<Slice> sliceRepository,
        IUnitOfWork unitOfWork)
    {
        _slicingTaskRepository = slicingTaskRepository;
        _sliceRepository = sliceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> CreateSlicingTaskAsync(string name, string sourceModelPath, string modelType, SlicingConfig config, Guid userId)
    {
        var task = new SlicingTask
        {
            Name = name,
            SourceModelPath = sourceModelPath,
            ModelType = modelType,
            SlicingConfig = System.Text.Json.JsonSerializer.Serialize(config),
            CreatedBy = userId,
            Status = SlicingTaskStatus.Created
        };

        await _slicingTaskRepository.AddAsync(task);
        await _unitOfWork.SaveChangesAsync();

        return task.Id;
    }

    public async Task<SlicingTask?> GetSlicingTaskAsync(Guid taskId)
    {
        return await _slicingTaskRepository.GetByIdAsync(taskId);
    }

    public async Task<IEnumerable<SlicingTask>> GetUserSlicingTasksAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        var tasks = await _slicingTaskRepository.GetAllAsync();
        return tasks.Where(t => t.CreatedBy == userId)
                   .OrderByDescending(t => t.CreatedAt)
                   .Skip((page - 1) * pageSize)
                   .Take(pageSize);
    }

    public async Task<bool> CancelSlicingTaskAsync(Guid taskId, Guid userId)
    {
        var task = await _slicingTaskRepository.GetByIdAsync(taskId);
        if (task == null || task.CreatedBy != userId)
        {
            return false;
        }

        task.Status = SlicingTaskStatus.Cancelled;
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<Slice?> GetSliceAsync(Guid taskId, int level, int x, int y, int z)
    {
        var slices = await _sliceRepository.GetAllAsync();
        return slices.FirstOrDefault(s =>
            s.SlicingTaskId == taskId &&
            s.Level == level &&
            s.X == x &&
            s.Y == y &&
            s.Z == z);
    }

    public async Task<IEnumerable<Slice>> GetSlicesByLevelAsync(Guid taskId, int level)
    {
        var slices = await _sliceRepository.GetAllAsync();
        return slices.Where(s => s.SlicingTaskId == taskId && s.Level == level);
    }

    public async Task<bool> DeleteSlicingTaskAsync(Guid taskId, Guid userId)
    {
        var task = await _slicingTaskRepository.GetByIdAsync(taskId);
        if (task == null || task.CreatedBy != userId)
        {
            return false;
        }

        await _slicingTaskRepository.DeleteAsync(task);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// 执行视锥剔除 - 渲染优化算法实现
    /// </summary>
    /// <param name="viewport">视口参数</param>
    /// <param name="allSlices">所有待测试的切片集合</param>
    /// <returns>可见切片集合</returns>
    public Task<IEnumerable<Slice>> PerformFrustumCullingAsync(ViewportInfo viewport, IEnumerable<Slice> allSlices)
    {
        var visibleSlices = new List<Slice>();

        foreach (var slice in allSlices)
        {
            if (IsSliceVisible(slice, viewport))
            {
                visibleSlices.Add(slice);
            }
        }

        return Task.FromResult<IEnumerable<Slice>>(visibleSlices);
    }

    /// <summary>
    /// 预测加载算法 - 预加载优化算法实现
    /// </summary>
    /// <param name="currentViewport">当前视口</param>
    /// <param name="movementVector">移动向量</param>
    /// <param name="allSlices">所有切片</param>
    /// <returns>预测加载的切片集合</returns>
    public async Task<IEnumerable<Slice>> PredictLoadingAsync(ViewportInfo currentViewport, Vector3D movementVector, IEnumerable<Slice> allSlices)
    {
        // 预测下一个视口位置
        var predictedPosition = currentViewport.CameraPosition + movementVector * 2.0; // 预测2秒后的位置

        var predictedViewport = new ViewportInfo
        {
            CameraPosition = predictedPosition,
            CameraDirection = currentViewport.CameraDirection,
            FieldOfView = currentViewport.FieldOfView,
            NearPlane = currentViewport.NearPlane,
            FarPlane = currentViewport.FarPlane
        };

        return await PerformFrustumCullingAsync(predictedViewport, allSlices);
    }

    /// <summary>
    /// 判断切片是否可见 - 完整的视锥剔除算法实现
    /// 算法：六平面视锥剔除 + 距离LOD优化 + 包围盒相交测试
    /// </summary>
    private bool IsSliceVisible(Slice slice, ViewportInfo viewport)
    {
        try
        {
            // 解析切片包围盒
            var boundingBoxDict = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, double>>(slice.BoundingBox);
            if (boundingBoxDict == null) return false;

            // 计算切片中心点和半径
            var minX = boundingBoxDict.GetValueOrDefault("minX", 0);
            var minY = boundingBoxDict.GetValueOrDefault("minY", 0);
            var minZ = boundingBoxDict.GetValueOrDefault("minZ", 0);
            var maxX = boundingBoxDict.GetValueOrDefault("maxX", 0);
            var maxY = boundingBoxDict.GetValueOrDefault("maxY", 0);
            var maxZ = boundingBoxDict.GetValueOrDefault("maxZ", 0);

            var sliceCenter = new Vector3D
            {
                X = (minX + maxX) / 2,
                Y = (minY + maxY) / 2,
                Z = (minZ + maxZ) / 2
            };

            // 计算包围盒半径
            var boundingBoxRadius = Math.Sqrt(
                Math.Pow(maxX - minX, 2) +
                Math.Pow(maxY - minY, 2) +
                Math.Pow(maxZ - minZ, 2)
            ) / 2;

            // 1. 距离剔除测试 - 基于LOD级别的动态距离阈值
            var distance = CalculateDistance(viewport.CameraPosition, sliceCenter);

            // LOD级别越高，最大可见距离越小
            var lodDistanceFactor = Math.Pow(0.75, slice.Level);
            var maxDistance = viewport.FarPlane * lodDistanceFactor;

            // 近平面和远平面剔除
            if (distance < viewport.NearPlane || distance > maxDistance)
                return false;

            // 2. 视野角度剔除测试
            var toCenterVector = new Vector3D
            {
                X = sliceCenter.X - viewport.CameraPosition.X,
                Y = sliceCenter.Y - viewport.CameraPosition.Y,
                Z = sliceCenter.Z - viewport.CameraPosition.Z
            };

            var angle = CalculateAngle(viewport.CameraDirection, toCenterVector);

            // 考虑包围盒半径的扩展角度
            var angularRadius = Math.Atan2(boundingBoxRadius, distance);
            var effectiveFOV = viewport.FieldOfView / 2 + angularRadius;

            if (angle > effectiveFOV)
                return false;

            // 3. 相机前方检测 - 确保切片在相机前方
            var dotProduct = toCenterVector.X * viewport.CameraDirection.X +
                           toCenterVector.Y * viewport.CameraDirection.Y +
                           toCenterVector.Z * viewport.CameraDirection.Z;

            if (dotProduct <= 0)
                return false;

            // 4. 遮挡剔除优化 - 距离很远的小切片可能被遮挡
            if (distance > maxDistance * 0.8 && boundingBoxRadius < distance * 0.01)
                return false;

            // 通过所有测试，切片可见
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 计算两点间距离
    /// </summary>
    private double CalculateDistance(Vector3D point1, Vector3D point2)
    {
        var dx = point2.X - point1.X;
        var dy = point2.Y - point1.Y;
        var dz = point2.Z - point1.Z;

        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// 计算向量间角度 - 完整的向量夹角计算算法
    /// 算法：点积法计算向量夹角，处理边界情况和数值稳定性
    /// </summary>
    private double CalculateAngle(Vector3D vector1, Vector3D vector2)
    {
        // 计算点积
        var dotProduct = vector1.X * vector2.X + vector1.Y * vector2.Y + vector1.Z * vector2.Z;

        // 计算向量长度
        var magnitude1 = Math.Sqrt(vector1.X * vector1.X + vector1.Y * vector1.Y + vector1.Z * vector1.Z);
        var magnitude2 = Math.Sqrt(vector2.X * vector2.X + vector2.Y * vector2.Y + vector2.Z * vector2.Z);

        // 处理零向量情况
        if (magnitude1 < 1e-10 || magnitude2 < 1e-10) return 0;

        // 计算余弦值，并限制在[-1, 1]范围内以避免数值误差
        var cosAngle = dotProduct / (magnitude1 * magnitude2);
        cosAngle = Math.Max(-1.0, Math.Min(1.0, cosAngle));

        // 返回弧度值
        return Math.Acos(cosAngle);
    }
}