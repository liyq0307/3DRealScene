using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;

namespace RealScene3D.WebApi.Controllers;

/// <summary>
/// 3D场景对象管理API控制器
/// 提供场景中3D对象的完整生命周期管理RESTful API接口
/// 支持对象的创建、查询、删除操作,集成3D变换和空间定位功能
/// 遵循ASP.NET Core最佳实践,包含完善的错误处理和性能监控
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SceneObjectsController : ControllerBase
{
    private readonly ISceneObjectService _objectService;
    private readonly ILogger<SceneObjectsController> _logger;

    /// <summary>
    /// 构造函数 - 依赖注入场景对象服务和日志记录器
    /// </summary>
    /// <param name="objectService">场景对象应用服务接口，提供业务逻辑处理</param>
    /// <param name="logger">日志记录器，用于记录对象操作和性能指标</param>
    public SceneObjectsController(ISceneObjectService objectService, ILogger<SceneObjectsController> logger)
    {
        _objectService = objectService;
        _logger = logger;
    }

    /// <summary>
    /// 创建场景对象
    /// </summary>
    [HttpPost]
    /// <summary>
    /// 在场景中创建新的3D对象
    /// </summary>
    /// <param name="request">对象创建请求，包含位置、旋转、缩放、模型路径等信息</param>
    /// <returns>创建成功的对象信息，包含完整的对象属性</returns>
    public async Task<ActionResult<SceneDtos.SceneObjectDto>> CreateObject([FromBody] SceneDtos.CreateSceneObjectRequest request)
    {
        try
        {
            var obj = await _objectService.CreateObjectAsync(request);
            return CreatedAtAction(nameof(GetObject), new { id = obj.Id }, obj);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建场景对象时发生错误");
            return StatusCode(500, new { message = "创建场景对象时发生错误" });
        }
    }

    /// <summary>
    /// 获取场景对象详情
    /// </summary>
    [HttpGet("{id}")]
    /// <summary>
    /// 根据ID获取场景对象详情
    /// </summary>
    /// <param name="id">场景对象唯一标识符</param>
    /// <returns>场景对象详情，如果不存在则返回404</returns>
    public async Task<ActionResult<SceneDtos.SceneObjectDto>> GetObject(Guid id)
    {
        try
        {
            var obj = await _objectService.GetObjectByIdAsync(id);
            if (obj == null)
            {
                return NotFound(new { message = "对象未找到" });
            }
            return Ok(obj);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取场景对象 {ObjectId} 时发生错误", id);
            return StatusCode(500, new { message = "获取场景对象时发生错误" });
        }
    }

    /// <summary>
    /// 获取指定场景中的所有3D对象列表
    /// </summary>
    /// <param name="sceneId">场景ID</param>
    /// <returns>该场景下的所有对象列表，按创建时间倒序排列</returns>
    [HttpGet("scene/{sceneId}")]
    public async Task<ActionResult<IEnumerable<SceneDtos.SceneObjectDto>>> GetSceneObjects(Guid sceneId)
    {
        try
        {
            var objects = await _objectService.GetSceneObjectsAsync(sceneId);
            return Ok(objects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取场景 {SceneId} 的对象列表时发生错误", sceneId);
            return StatusCode(500, new { message = "获取场景对象列表时发生错误" });
        }
    }

    /// <summary>
    /// 更新场景对象
    /// </summary>
    [HttpPut("{id}")]
    /// <summary>
    /// 更新现有场景对象的属性
    /// </summary>
    /// <param name="id">场景对象唯一标识符</param>
    /// <param name="request">对象更新请求，包含新的位置、旋转、缩放、模型路径等信息</param>
    /// <returns>更新成功的对象信息</returns>
    public async Task<ActionResult<SceneDtos.SceneObjectDto>> UpdateObject(Guid id, [FromBody] SceneDtos.UpdateSceneObjectRequest request)
    {
        try
        {
            var obj = await _objectService.UpdateObjectAsync(id, request);
            if (obj == null)
            {
                return NotFound(new { message = "对象未找到" });
            }
            return Ok(obj);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新场景对象 {ObjectId} 时发生错误", id);
            return StatusCode(500, new { message = "更新场景对象时发生错误" });
        }
    }

    /// <summary>
    /// 删除场景对象
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteObject(Guid id)
    {
        try
        {
            var result = await _objectService.DeleteObjectAsync(id);
            if (!result)
            {
                return NotFound(new { message = "对象未找到" });
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除场景对象 {ObjectId} 时发生错误", id);
            return StatusCode(500, new { message = "删除场景对象时发生错误" });
        }
    }
}
