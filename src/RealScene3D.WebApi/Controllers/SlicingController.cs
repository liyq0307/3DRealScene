using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;

namespace RealScene3D.WebApi.Controllers;

/// <summary>
/// 3D模型切片管理API控制器
/// 提供3D模型切片任务管理的完整RESTful API接口
/// 支持切片任务的创建、进度监控、数据获取、批量处理等功能
/// 集成实时进度更新和异步任务处理机制
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SlicingController : ControllerBase
{
    private readonly ISlicingAppService _slicingService;
    private readonly ILogger<SlicingController> _logger;

    /// <summary>
    /// 构造函数 - 依赖注入切片应用服务和日志记录器
    /// </summary>
    /// <param name="slicingService">切片应用服务接口，提供切片业务逻辑处理</param>
    /// <param name="logger">日志记录器，用于记录切片操作和性能指标</param>
    public SlicingController(ISlicingAppService slicingService, ILogger<SlicingController> logger)
    {
        _slicingService = slicingService;
        _logger = logger;
    }

    /// <summary>
    /// 创建切片任务
    /// </summary>
    [HttpPost("tasks")]
    /// <summary>
    /// 创建3D模型切片任务
    /// </summary>
    /// <param name="request">切片任务创建请求，包含模型路径和切片配置</param>
    /// <param name="userId">创建用户ID</param>
    /// <returns>创建成功的切片任务信息</returns>
    public async Task<ActionResult<SlicingDtos.SlicingTaskDto>> CreateSlicingTask([FromBody] SlicingDtos.CreateSlicingTaskRequest request, [FromQuery] Guid userId)
    {
        try
        {
            var task = await _slicingService.CreateSlicingTaskAsync(request, userId);
            return CreatedAtAction(nameof(GetSlicingTask), new { id = task.Id }, task);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create slicing task");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating slicing task");
            return StatusCode(500, new { message = "创建切片任务时发生错误" });
        }
    }

    /// <summary>
    /// 获取切片任务详情
    /// </summary>
    [HttpGet("tasks/{id}")]
    /// <summary>
    /// 根据ID获取切片任务详情
    /// </summary>
    /// <param name="id">切片任务唯一标识符</param>
    /// <returns>切片任务详情，如果不存在则返回404</returns>
    public async Task<ActionResult<SlicingDtos.SlicingTaskDto>> GetSlicingTask(Guid id)
    {
        try
        {
            var task = await _slicingService.GetSlicingTaskAsync(id);
            if (task == null)
            {
                return NotFound(new { message = "切片任务未找到" });
            }
            return Ok(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting slicing task {TaskId}", id);
            return StatusCode(500, new { message = "获取切片任务时发生错误" });
        }
    }

    /// <summary>
    /// 获取指定用户的切片任务列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="page">页码，从1开始</param>
    /// <param name="pageSize">每页大小，默认20</param>
    /// <returns>用户的切片任务列表，按创建时间倒序排列</returns>
    [HttpGet("tasks/user/{userId}")]
    public async Task<ActionResult<IEnumerable<SlicingDtos.SlicingTaskDto>>> GetUserSlicingTasks(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var tasks = await _slicingService.GetUserSlicingTasksAsync(userId, page, pageSize);
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting slicing tasks for user {UserId}", userId);
            return StatusCode(500, new { message = "获取切片任务列表时发生错误" });
        }
    }

    /// <summary>
    /// 获取切片任务实时进度信息
    /// </summary>
    /// <param name="id">切片任务ID</param>
    /// <returns>切片任务进度详情，包括当前阶段和预计剩余时间</returns>
    [HttpGet("tasks/{id}/progress")]
    public async Task<ActionResult<SlicingDtos.SlicingProgressDto>> GetSlicingProgress(Guid id)
    {
        try
        {
            var progress = await _slicingService.GetSlicingProgressAsync(id);
            if (progress == null)
            {
                return NotFound(new { message = "切片任务未找到" });
            }
            return Ok(progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting slicing progress for task {TaskId}", id);
            return StatusCode(500, new { message = "获取切片进度时发生错误" });
        }
    }

    /// <summary>
    /// 取消切片任务
    /// </summary>
    [HttpPost("tasks/{id}/cancel")]
    public async Task<IActionResult> CancelSlicingTask(Guid id, [FromQuery] Guid userId)
    {
        try
        {
            var result = await _slicingService.CancelSlicingTaskAsync(id, userId);
            if (!result)
            {
                return NotFound(new { message = "切片任务未找到或无权限" });
            }
            return Ok(new { message = "切片任务已取消" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling slicing task {TaskId}", id);
            return StatusCode(500, new { message = "取消切片任务时发生错误" });
        }
    }

    /// <summary>
    /// 删除切片任务
    /// </summary>
    [HttpDelete("tasks/{id}")]
    public async Task<IActionResult> DeleteSlicingTask(Guid id, [FromQuery] Guid userId)
    {
        try
        {
            var result = await _slicingService.DeleteSlicingTaskAsync(id, userId);
            if (!result)
            {
                return NotFound(new { message = "切片任务未找到或无权限" });
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting slicing task {TaskId}", id);
            return StatusCode(500, new { message = "删除切片任务时发生错误" });
        }
    }

    /// <summary>
    /// 获取切片数据
    /// </summary>
    [HttpGet("tasks/{taskId}/slices/{level}/{x}/{y}/{z}")]
    /// <summary>
    /// 根据坐标获取特定切片数据
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="level">LOD层级</param>
    /// <param name="x">切片X坐标</param>
    /// <param name="y">切片Y坐标</param>
    /// <param name="z">切片Z坐标</param>
    /// <returns>切片数据，如果不存在则返回404</returns>
    public async Task<ActionResult<SlicingDtos.SliceDto>> GetSlice(Guid taskId, int level, int x, int y, int z)
    {
        try
        {
            var slice = await _slicingService.GetSliceAsync(taskId, level, x, y, z);
            if (slice == null)
            {
                return NotFound(new { message = "切片未找到" });
            }
            return Ok(slice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting slice for task {TaskId}, level {Level}, coordinates ({X}, {Y}, {Z})", taskId, level, x, y, z);
            return StatusCode(500, new { message = "获取切片数据时发生错误" });
        }
    }

    /// <summary>
    /// 获取指定层级的所有切片元数据
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="level">LOD层级</param>
    /// <returns>该层级所有切片的元数据集合</returns>
    [HttpGet("tasks/{taskId}/slices/{level}/metadata")]
    public async Task<ActionResult<IEnumerable<SlicingDtos.SliceMetadataDto>>> GetSliceMetadata(Guid taskId, int level)
    {
        try
        {
            var metadata = await _slicingService.GetSliceMetadataAsync(taskId, level);
            return Ok(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting slice metadata for task {TaskId}, level {Level}", taskId, level);
            return StatusCode(500, new { message = "获取切片元数据时发生错误" });
        }
    }

    /// <summary>
    /// 下载切片文件的二进制数据
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="level">LOD层级</param>
    /// <param name="x">切片X坐标</param>
    /// <param name="y">切片Y坐标</param>
    /// <param name="z">切片Z坐标</param>
    /// <returns>切片文件流，浏览器触发下载</returns>
    [HttpGet("tasks/{taskId}/slices/{level}/{x}/{y}/{z}/download")]
    public async Task<IActionResult> DownloadSlice(Guid taskId, int level, int x, int y, int z)
    {
        try
        {
            var sliceData = await _slicingService.DownloadSliceAsync(taskId, level, x, y, z);

            // 根据切片数据确定内容类型
            var contentType = GetContentType(taskId, level, x, y, z);

            return File(sliceData, contentType, $"slice_{level}_{x}_{y}_{z}.b3dm");
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { message = "切片文件未找到" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading slice for task {TaskId}, level {Level}, coordinates ({X}, {Y}, {Z})", taskId, level, x, y, z);
            return StatusCode(500, new { message = "下载切片文件时发生错误" });
        }
    }

    /// <summary>
    /// 批量获取切片
    /// </summary>
    [HttpPost("tasks/{taskId}/slices/{level}/batch")]
    /// <summary>
    /// 批量获取指定坐标的切片数据
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="level">LOD层级</param>
    /// <param name="coordinates">切片坐标集合，支持批量获取以提高效率</param>
    /// <returns>切片数据集合，只返回存在的切片</returns>
    public async Task<ActionResult<IEnumerable<SlicingDtos.SliceDto>>> GetSlicesBatch(Guid taskId, int level, [FromBody] List<(int x, int y, int z)> coordinates)
    {
        try
        {
            var slices = await _slicingService.GetSlicesBatchAsync(taskId, level, coordinates);
            return Ok(slices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting slices batch for task {TaskId}, level {Level}", taskId, level);
            return StatusCode(500, new { message = "批量获取切片时发生错误" });
        }
    }

    /// <summary>
    /// 执行视锥剔除 - 渲染优化API
    /// </summary>
    [HttpPost("tasks/{taskId}/frustum-culling")]
    /// <summary>
    /// 基于视口参数执行视锥剔除，返回可见切片集合
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="viewport">视口参数，包含相机位置、视角等信息</param>
    /// <param name="level">LOD层级，可选，不指定则返回所有层级可见切片</param>
    /// <returns>可见切片元数据集合</returns>
    public async Task<ActionResult<IEnumerable<SlicingDtos.SliceMetadataDto>>> PerformFrustumCulling(
        Guid taskId,
        [FromBody] ViewportRequestDto viewport,
        [FromQuery] int? level = null)
    {
        try
        {
            // 获取所有切片
            var allSlices = await _slicingService.GetSliceMetadataAsync(taskId, level ?? 0);

            // 转换视口信息
            var viewportInfo = new ViewportInfo
            {
                CameraPosition = new Vector3D
                {
                    X = viewport.CameraPosition.X,
                    Y = viewport.CameraPosition.Y,
                    Z = viewport.CameraPosition.Z
                },
                CameraDirection = new Vector3D
                {
                    X = viewport.CameraDirection.X,
                    Y = viewport.CameraDirection.Y,
                    Z = viewport.CameraDirection.Z
                },
                FieldOfView = viewport.FieldOfView,
                NearPlane = viewport.NearPlane,
                FarPlane = viewport.FarPlane
            };

            // 执行视锥剔除
            var visibleSlices = await _slicingService.PerformFrustumCullingAsync(new ViewportInfo
            {
                CameraPosition = new Vector3D
                {
                    X = viewport.CameraPosition.X,
                    Y = viewport.CameraPosition.Y,
                    Z = viewport.CameraPosition.Z
                },
                CameraDirection = new Vector3D
                {
                    X = viewport.CameraDirection.X,
                    Y = viewport.CameraDirection.Y,
                    Z = viewport.CameraDirection.Z
                },
                FieldOfView = viewport.FieldOfView,
                NearPlane = viewport.NearPlane,
                FarPlane = viewport.FarPlane
            }, allSlices);

            return Ok(visibleSlices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing frustum culling for task {TaskId}", taskId);
            return StatusCode(500, new { message = "执行视锥剔除时发生错误" });
        }
    }

    /// <summary>
    /// 预测加载切片 - 预加载优化API
    /// </summary>
    [HttpPost("tasks/{taskId}/predict-loading")]
    /// <summary>
    /// 基于用户移动趋势预测需要加载的切片
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="request">预测加载请求，包含当前视口和移动向量</param>
    /// <param name="predictionTime">预测时间（秒），默认2秒</param>
    /// <param name="level">LOD层级，可选</param>
    /// <returns>预测需要加载的切片集合</returns>
    public async Task<ActionResult<IEnumerable<SlicingDtos.SliceMetadataDto>>> PredictLoading(
        Guid taskId,
        [FromBody] PredictLoadingRequestDto request,
        [FromQuery] double predictionTime = 2.0,
        [FromQuery] int? level = null)
    {
        try
        {
            // 获取所有切片
            var allSlices = await _slicingService.GetSliceMetadataAsync(taskId, level ?? 0);

            // 转换视口信息
            var viewportInfo = new ViewportInfo
            {
                CameraPosition = new Vector3D
                {
                    X = request.CurrentViewport.CameraPosition.X,
                    Y = request.CurrentViewport.CameraPosition.Y,
                    Z = request.CurrentViewport.CameraPosition.Z
                },
                CameraDirection = new Vector3D
                {
                    X = request.CurrentViewport.CameraDirection.X,
                    Y = request.CurrentViewport.CameraDirection.Y,
                    Z = request.CurrentViewport.CameraDirection.Z
                },
                FieldOfView = request.CurrentViewport.FieldOfView,
                NearPlane = request.CurrentViewport.NearPlane,
                FarPlane = request.CurrentViewport.FarPlane
            };

            // 转换移动向量
            var movementVec = new Vector3D
            {
                X = request.MovementVector.X,
                Y = request.MovementVector.Y,
                Z = request.MovementVector.Z
            };

            // 执行预测加载
            var predictedSlices = await _slicingService.PredictLoadingAsync(viewportInfo, movementVec, allSlices);

            return Ok(predictedSlices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error predicting loading for task {TaskId}", taskId);
            return StatusCode(500, new { message = "执行预测加载时发生错误" });
        }
    }

    /// <summary>
    /// 获取切片策略信息 - 切片算法查询API
    /// </summary>
    [HttpGet("strategies")]
    /// <summary>
    /// 获取所有支持的切片策略信息
    /// </summary>
    /// <returns>切片策略枚举和描述</returns>
    public IActionResult GetSlicingStrategies()
    {
        try
        {
            var strategies = new[]
            {
                new { Id = 0, Name = "Grid", Description = "规则网格切片算法，适用于规则地形和均匀分布的数据" },
                new { Id = 1, Name = "Octree", Description = "八叉树切片算法，适用于不规则模型，自适应精度" },
                new { Id = 2, Name = "KdTree", Description = "KD树切片算法，自适应空间剖分，适用于复杂场景" },
                new { Id = 3, Name = "Adaptive", Description = "密度自适应切片算法，基于几何密度自动调整切片大小" }
            };

            return Ok(strategies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting slicing strategies");
            return StatusCode(500, new { message = "获取切片策略信息时发生错误" });
        }
    }

    /// <summary>
    /// 获取增量更新索引 - 增量更新查询API
    /// </summary>
    [HttpGet("tasks/{taskId}/incremental-index")]
    /// <summary>
    /// 获取切片任务的增量更新索引信息
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <returns>增量更新索引详情</returns>
    public async Task<ActionResult<SlicingDtos.IncrementalUpdateIndexDto>> GetIncrementalUpdateIndex(Guid taskId)
    {
        try
        {
            var indexData = await _slicingService.GetIncrementalUpdateIndexAsync(taskId);

            if (indexData == null)
            {
                return NotFound(new { message = "增量更新索引未找到或该任务未启用增量更新" });
            }

            return Ok(indexData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting incremental update index for task {TaskId}", taskId);
            return StatusCode(500, new { message = "获取增量更新索引时发生错误" });
        }
    }

    private string GetContentType(Guid taskId, int level, int x, int y, int z)
    {
        // 这里应该根据实际的切片格式确定内容类型
        // 目前假设都是B3DM格式
        return "application/octet-stream";
    }

    /// <summary>
    /// 视口请求DTO - 前端视口参数传输对象
    /// </summary>
    public class ViewportRequestDto
    {
        public Vector3DRequestDto CameraPosition { get; set; } = new();
        public Vector3DRequestDto CameraDirection { get; set; } = new();
        public double FieldOfView { get; set; } = Math.PI / 3; // 默认60度视野
        public double NearPlane { get; set; } = 1.0;
        public double FarPlane { get; set; } = 10000.0;
    }

    /// <summary>
    /// 三维向量请求DTO - 前端向量参数传输对象
    /// </summary>
    public class Vector3DRequestDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    /// <summary>
    /// 预测加载请求DTO - 结合视口和移动向量的请求对象
    /// </summary>
    public class PredictLoadingRequestDto
    {
        public ViewportRequestDto CurrentViewport { get; set; } = new();
        public Vector3DRequestDto MovementVector { get; set; } = new();
    }
}