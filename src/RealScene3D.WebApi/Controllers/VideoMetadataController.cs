using Microsoft.AspNetCore.Mvc;
using RealScene3D.Infrastructure.MongoDB.Entities;
using RealScene3D.Infrastructure.MongoDB.Repositories;

namespace RealScene3D.WebApi.Controllers;

/// <summary>
/// 视频元数据管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VideoMetadataController : ControllerBase
{
    private readonly IVideoMetadataRepository _repository;
    private readonly ILogger<VideoMetadataController> _logger;

    public VideoMetadataController(
        IVideoMetadataRepository repository,
        ILogger<VideoMetadataController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 分页查询视频
    /// </summary>
    [HttpGet("paged")]
    public async Task<ActionResult<object>> GetPaged(
        [FromQuery] Guid? sceneId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (items, totalCount) = await _repository.FindPagedAsync(
                v => !sceneId.HasValue || v.SceneId == sceneId.Value,
                pageNumber,
                pageSize,
                cancellationToken);

            return Ok(new
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "分页查询视频失败");
            return StatusCode(500, new { error = "分页查询视频失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 统计视频数量
    /// </summary>
    [HttpGet("count")]
    public async Task<ActionResult<long>> Count([FromQuery] Guid? sceneId, CancellationToken cancellationToken)
    {
        try
        {
            var count = await _repository.CountAsync(
                sceneId.HasValue ? v => v.SceneId == sceneId.Value : null,
                cancellationToken);
            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "统计视频数量失败");
            return StatusCode(500, new { error = "统计视频数量失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据场景 ID 获取视频列表
    /// </summary>
    [HttpGet("scene/{sceneId}")]
    public async Task<ActionResult<IEnumerable<VideoMetadata>>> GetBySceneId(Guid sceneId, CancellationToken cancellationToken)
    {
        try
        {
            var videos = await _repository.GetAllBySceneIdAsync(sceneId, cancellationToken);
            return Ok(videos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取场景视频失败: {SceneId}", sceneId);
            return StatusCode(500, new { error = "获取场景视频失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据文件名搜索视频
    /// </summary>
    [HttpGet("search/{fileName}")]
    public async Task<ActionResult<IEnumerable<VideoMetadata>>> SearchByFileName(string fileName, CancellationToken cancellationToken)
    {
        try
        {
            var videos = await _repository.SearchByFileNameAsync(fileName, cancellationToken);
            return Ok(videos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索视频失败: {FileName}", fileName);
            return StatusCode(500, new { error = "搜索视频失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据标签查询视频
    /// </summary>
    [HttpGet("tags")]
    public async Task<ActionResult<IEnumerable<VideoMetadata>>> GetByTags([FromQuery] string[] tags, CancellationToken cancellationToken)
    {
        try
        {
            var videos = await _repository.FindByTagsAsync(tags, cancellationToken);
            return Ok(videos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据标签查询视频失败");
            return StatusCode(500, new { error = "根据标签查询视频失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 获取所有视频元数据
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<VideoMetadata>>> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var videos = await _repository.GetAllAsync(cancellationToken);
            return Ok(videos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取视频元数据列表失败");
            return StatusCode(500, new { error = "获取视频元数据列表失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据 ID 获取视频元数据
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<VideoMetadata>> GetById(string id, CancellationToken cancellationToken)
    {
        try
        {
            var video = await _repository.GetByIdAsync(id, cancellationToken);
            if (video == null)
            {
                return NotFound($"未找到 ID 为 {id} 的视频元数据");
            }
            return Ok(video);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取视频元数据失败: {Id}", id);
            return StatusCode(500, new { error = "获取视频元数据失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 创建视频元数据
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<VideoMetadata>> Create([FromBody] VideoMetadata video, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _repository.AddAsync(video, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建视频元数据失败");
            return StatusCode(500, new { error = "创建视频元数据失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 更新视频元数据
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, [FromBody] VideoMetadata video, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _repository.UpdateAsync(id, video, cancellationToken);
            if (!success)
            {
                return NotFound($"未找到 ID 为 {id} 的视频元数据");
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新视频元数据失败: {Id}", id);
            return StatusCode(500, new { error = "更新视频元数据失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 删除视频元数据
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _repository.DeleteAsync(id, cancellationToken);
            if (!success)
            {
                return NotFound($"未找到 ID 为 {id} 的视频元数据");
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除视频元数据失败: {Id}", id);
            return StatusCode(500, new { error = "删除视频元数据失败", details = ex.Message });
        }
    }
}
