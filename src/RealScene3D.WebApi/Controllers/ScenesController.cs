using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;

namespace RealScene3D.WebApi.Controllers;

/// <summary>
/// 3D场景管理API控制器
/// 提供场景管理的RESTful API接口，支持场景的增删改查操作
/// 集成地理信息系统（GIS）支持，处理场景的地理边界和空间数据
/// 遵循ASP.NET Core最佳实践，包含完善的错误处理和日志记录
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ScenesController : ControllerBase
{
    private readonly ISceneService _sceneService;
    private readonly ILogger<ScenesController> _logger;

    /// <summary>
    /// 构造函数 - 依赖注入场景服务和日志记录器
    /// </summary>
    /// <param name="sceneService">场景应用服务接口，提供业务逻辑处理</param>
    /// <param name="logger">日志记录器，用于记录操作日志和错误信息</param>
    public ScenesController(ISceneService sceneService, ILogger<ScenesController> logger)
    {
        _sceneService = sceneService;
        _logger = logger;
    }

    /// <summary>
    /// 创建新场景
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SceneDtos.SceneDto>> CreateScene([FromBody] SceneDtos.CreateSceneRequest request, [FromQuery] Guid ownerId)
    {
        try
        {
            var scene = await _sceneService.CreateSceneAsync(request, ownerId);
            return CreatedAtAction(nameof(GetScene), new { id = scene.Id }, scene);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "创建场景失败");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建场景时发生错误");
            return StatusCode(500, new { message = "创建场景时发生错误" });
        }
    }

    /// <summary>
    /// 获取场景详情
    /// </summary>
    [HttpGet("{id}")]
    /// <summary>
    /// 根据ID获取场景详情
    /// </summary>
    /// <param name="id">场景唯一标识符</param>
    /// <returns>场景详情，如果不存在则返回404</returns>
    public async Task<ActionResult<SceneDtos.SceneDto>> GetScene(Guid id)
    {
        try
        {
            var scene = await _sceneService.GetSceneByIdAsync(id);
            if (scene == null)
            {
                return NotFound(new { message = "Scene not found" });
            }
            return Ok(scene);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取场景 {SceneId} 时发生错误", id);
            return StatusCode(500, new { message = "获取场景时发生错误" });
        }
    }

    /// <summary>
    /// 获取指定用户的所有场景列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户拥有的场景列表，按创建时间倒序排列</returns>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<SceneDtos.SceneDto>>> GetUserScenes(Guid userId)
    {
        try
        {
            var scenes = await _sceneService.GetUserScenesAsync(userId);
            return Ok(scenes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户 {UserId} 的场景列表时发生错误", userId);
            return StatusCode(500, new { message = "获取场景列表时发生错误" });
        }
    }

    /// <summary>
    /// 获取所有公开场景列表
    /// </summary>
    /// <returns>所有公开可访问的场景列表</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SceneDtos.SceneDto>>> GetAllScenes()
    {
        try
        {
            var scenes = await _sceneService.GetAllScenesAsync();
            return Ok(scenes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有场景时发生错误");
            return StatusCode(500, new { message = "获取场景列表时发生错误" });
        }
    }

    /// <summary>
    /// 删除场景
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteScene(Guid id, [FromQuery] Guid userId)
    {
        try
        {
            var result = await _sceneService.DeleteSceneAsync(id, userId);
            if (!result)
            {
                return NotFound(new { message = "Scene not found or unauthorized" });
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除场景 {SceneId} 时发生错误", id);
            return StatusCode(500, new { message = "删除场景时发生错误" });
        }
    }

    /// <summary>
    /// 更新场景信息
    /// </summary>
    /// <param name="id">场景ID</param>
    /// <param name="request">更新请求</param>
    /// <param name="userId">用户ID</param>
    /// <returns>更新后的场景信息</returns>
    [HttpPut("{id}")]
    public async Task<ActionResult<SceneDtos.SceneDto>> UpdateScene(
        Guid id,
        [FromBody] SceneDtos.UpdateSceneRequest request,
        [FromQuery] Guid userId)
    {
        try
        {
            var scene = await _sceneService.UpdateSceneAsync(id, request, userId);
            if (scene == null)
            {
                return NotFound(new { message = "Scene not found or unauthorized" });
            }
            return Ok(scene);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "更新场景 {SceneId} 失败", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新场景 {SceneId} 时发生错误", id);
            return StatusCode(500, new { message = "更新场景时发生错误" });
        }
    }
}
