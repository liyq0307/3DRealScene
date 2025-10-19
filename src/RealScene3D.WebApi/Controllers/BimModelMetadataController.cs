using Microsoft.AspNetCore.Mvc;
using RealScene3D.Infrastructure.MongoDB.Entities;
using RealScene3D.Infrastructure.MongoDB.Repositories;

namespace RealScene3D.WebApi.Controllers;

/// <summary>
/// BIM 模型元数据管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BimModelMetadataController : ControllerBase
{
    private readonly IBimModelMetadataRepository _repository;
    private readonly ILogger<BimModelMetadataController> _logger;

    public BimModelMetadataController(
        IBimModelMetadataRepository repository,
        ILogger<BimModelMetadataController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 根据场景 ID 获取 BIM 模型列表
    /// </summary>
    [HttpGet("scene/{sceneId}")]
    public async Task<ActionResult<IEnumerable<BimModelMetadata>>> GetBySceneId(Guid sceneId, CancellationToken cancellationToken)
    {
        try
        {
            var models = await _repository.GetAllBySceneIdAsync(sceneId, cancellationToken);
            return Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取场景 BIM 模型失败: {SceneId}", sceneId);
            return StatusCode(500, new { error = "获取场景 BIM 模型失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据学科类型获取 BIM 模型列表
    /// </summary>
    [HttpGet("discipline/{discipline}")]
    public async Task<ActionResult<IEnumerable<BimModelMetadata>>> GetByDiscipline(string discipline, CancellationToken cancellationToken)
    {
        try
        {
            var models = await _repository.GetByDisciplineAsync(discipline, cancellationToken);
            return Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据学科查询 BIM 模型失败: {Discipline}", discipline);
            return StatusCode(500, new { error = "根据学科查询 BIM 模型失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据格式获取 BIM 模型列表
    /// </summary>
    [HttpGet("format/{format}")]
    public async Task<ActionResult<IEnumerable<BimModelMetadata>>> GetByFormat(string format, CancellationToken cancellationToken)
    {
        try
        {
            var models = await _repository.GetByFormatAsync(format, cancellationToken);
            return Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据格式查询 BIM 模型失败: {Format}", format);
            return StatusCode(500, new { error = "根据格式查询 BIM 模型失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据场景和学科获取 BIM 模型列表
    /// </summary>
    [HttpGet("scene/{sceneId}/discipline/{discipline}")]
    public async Task<ActionResult<IEnumerable<BimModelMetadata>>> GetBySceneAndDiscipline(
        Guid sceneId,
        string discipline,
        CancellationToken cancellationToken)
    {
        try
        {
            var models = await _repository.GetBySceneAndDisciplineAsync(sceneId, discipline, cancellationToken);
            return Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据场景和学科查询 BIM 模型失败");
            return StatusCode(500, new { error = "根据场景和学科查询 BIM 模型失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 获取 BIM 模型构件统计信息
    /// </summary>
    [HttpGet("{id}/elements/stats")]
    public async Task<ActionResult<object>> GetElementStats(string id, CancellationToken cancellationToken)
    {
        try
        {
            var stats = await _repository.GetElementStatsAsync(id, cancellationToken);
            if (stats == null)
            {
                return NotFound($"未找到 ID 为 {id} 的 BIM 模型");
            }
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 BIM 模型构件统计失败: {Id}", id);
            return StatusCode(500, new { error = "获取 BIM 模型构件统计失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 获取所有 BIM 模型元数据
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<BimModelMetadata>>> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var models = await _repository.GetAllAsync(cancellationToken);
            return Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 BIM 模型列表失败");
            return StatusCode(500, new { error = "获取 BIM 模型列表失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据 ID 获取 BIM 模型元数据
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<BimModelMetadata>> GetById(string id, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _repository.GetByIdAsync(id, cancellationToken);
            if (model == null)
            {
                return NotFound($"未找到 ID 为 {id} 的 BIM 模型");
            }
            return Ok(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 BIM 模型失败: {Id}", id);
            return StatusCode(500, new { error = "获取 BIM 模型失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 创建 BIM 模型元数据
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BimModelMetadata>> Create([FromBody] BimModelMetadata model, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _repository.AddAsync(model, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建 BIM 模型元数据失败");
            return StatusCode(500, new { error = "创建 BIM 模型元数据失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 更新 BIM 模型元数据
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, [FromBody] BimModelMetadata model, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _repository.UpdateAsync(id, model, cancellationToken);
            if (!success)
            {
                return NotFound($"未找到 ID 为 {id} 的 BIM 模型");
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新 BIM 模型元数据失败: {Id}", id);
            return StatusCode(500, new { error = "更新 BIM 模型元数据失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 删除 BIM 模型元数据
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _repository.DeleteAsync(id, cancellationToken);
            if (!success)
            {
                return NotFound($"未找到 ID 为 {id} 的 BIM 模型");
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除 BIM 模型元数据失败: {Id}", id);
            return StatusCode(500, new { error = "删除 BIM 模型元数据失败", details = ex.Message });
        }
    }
}
