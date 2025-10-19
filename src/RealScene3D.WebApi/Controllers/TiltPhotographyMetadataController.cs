using Microsoft.AspNetCore.Mvc;
using RealScene3D.Infrastructure.MongoDB.Entities;
using RealScene3D.Infrastructure.MongoDB.Repositories;

namespace RealScene3D.WebApi.Controllers;

/// <summary>
/// 倾斜摄影元数据管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TiltPhotographyMetadataController : ControllerBase
{
    private readonly ITiltPhotographyMetadataRepository _repository;
    private readonly ILogger<TiltPhotographyMetadataController> _logger;

    public TiltPhotographyMetadataController(
        ITiltPhotographyMetadataRepository repository,
        ILogger<TiltPhotographyMetadataController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 统计倾斜摄影数据数量
    /// </summary>
    [HttpGet("count")]
    public async Task<ActionResult<long>> Count([FromQuery] Guid? sceneId, CancellationToken cancellationToken)
    {
        try
        {
            var count = await _repository.CountAsync(
                sceneId.HasValue ? t => t.SceneId == sceneId.Value : null,
                cancellationToken);
            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "统计倾斜摄影数据数量失败");
            return StatusCode(500, new { error = "统计倾斜摄影数据数量失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据场景 ID 获取倾斜摄影列表
    /// </summary>
    [HttpGet("scene/{sceneId}")]
    public async Task<ActionResult<IEnumerable<TiltPhotographyMetadata>>> GetBySceneId(Guid sceneId, CancellationToken cancellationToken)
    {
        try
        {
            var data = await _repository.GetAllBySceneIdAsync(sceneId, cancellationToken);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取场景倾斜摄影数据失败: {SceneId}", sceneId);
            return StatusCode(500, new { error = "获取场景倾斜摄影数据失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据项目名称搜索倾斜摄影
    /// </summary>
    [HttpGet("search/{projectName}")]
    public async Task<ActionResult<IEnumerable<TiltPhotographyMetadata>>> SearchByProjectName(string projectName, CancellationToken cancellationToken)
    {
        try
        {
            var data = await _repository.SearchByProjectNameAsync(projectName, cancellationToken);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索倾斜摄影数据失败: {ProjectName}", projectName);
            return StatusCode(500, new { error = "搜索倾斜摄影数据失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据采集日期范围查询倾斜摄影
    /// </summary>
    [HttpGet("date-range")]
    public async Task<ActionResult<IEnumerable<TiltPhotographyMetadata>>> GetByDateRange(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
    {
        try
        {
            var data = await _repository.GetByCaptureDateRangeAsync(startDate, endDate, cancellationToken);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据日期范围查询倾斜摄影失败");
            return StatusCode(500, new { error = "根据日期范围查询倾斜摄影失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据覆盖面积查询倾斜摄影
    /// </summary>
    [HttpGet("coverage-area")]
    public async Task<ActionResult<IEnumerable<TiltPhotographyMetadata>>> GetByCoverageArea(
        [FromQuery] double minAreaKm2,
        CancellationToken cancellationToken)
    {
        try
        {
            var data = await _repository.GetByCoverageAreaAsync(minAreaKm2, cancellationToken);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据覆盖面积查询倾斜摄影失败");
            return StatusCode(500, new { error = "根据覆盖面积查询倾斜摄影失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据输出格式查询倾斜摄影
    /// </summary>
    [HttpGet("format/{format}")]
    public async Task<ActionResult<IEnumerable<TiltPhotographyMetadata>>> GetByOutputFormat(string format, CancellationToken cancellationToken)
    {
        try
        {
            var data = await _repository.GetByOutputFormatAsync(format, cancellationToken);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据格式查询倾斜摄影失败: {Format}", format);
            return StatusCode(500, new { error = "根据格式查询倾斜摄影失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据地理边界查询倾斜摄影(空间查询)
    /// </summary>
    [HttpGet("bounds")]
    public async Task<ActionResult<IEnumerable<TiltPhotographyMetadata>>> GetByBounds(
        [FromQuery] double minLon,
        [FromQuery] double minLat,
        [FromQuery] double maxLon,
        [FromQuery] double maxLat,
        CancellationToken cancellationToken)
    {
        try
        {
            var data = await _repository.GetByBoundsAsync(minLon, minLat, maxLon, maxLat, cancellationToken);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据地理边界查询倾斜摄影失败");
            return StatusCode(500, new { error = "根据地理边界查询倾斜摄影失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 获取所有倾斜摄影元数据
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<TiltPhotographyMetadata>>> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var data = await _repository.GetAllAsync(cancellationToken);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取倾斜摄影数据列表失败");
            return StatusCode(500, new { error = "获取倾斜摄影数据列表失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据 ID 获取倾斜摄影元数据
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TiltPhotographyMetadata>> GetById(string id, CancellationToken cancellationToken)
    {
        try
        {
            var data = await _repository.GetByIdAsync(id, cancellationToken);
            if (data == null)
            {
                return NotFound($"未找到 ID 为 {id} 的倾斜摄影数据");
            }
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取倾斜摄影数据失败: {Id}", id);
            return StatusCode(500, new { error = "获取倾斜摄影数据失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 创建倾斜摄影元数据
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TiltPhotographyMetadata>> Create([FromBody] TiltPhotographyMetadata data, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _repository.AddAsync(data, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建倾斜摄影元数据失败");
            return StatusCode(500, new { error = "创建倾斜摄影元数据失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 更新倾斜摄影元数据
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, [FromBody] TiltPhotographyMetadata data, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _repository.UpdateAsync(id, data, cancellationToken);
            if (!success)
            {
                return NotFound($"未找到 ID 为 {id} 的倾斜摄影数据");
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新倾斜摄影元数据失败: {Id}", id);
            return StatusCode(500, new { error = "更新倾斜摄影元数据失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 删除倾斜摄影元数据
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _repository.DeleteAsync(id, cancellationToken);
            if (!success)
            {
                return NotFound($"未找到 ID 为 {id} 的倾斜摄影数据");
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除倾斜摄影元数据失败: {Id}", id);
            return StatusCode(500, new { error = "删除倾斜摄影元数据失败", details = ex.Message });
        }
    }
}
