using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealScene3D.Infrastructure.MinIO;
using System.Security.Claims;

namespace RealScene3D.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IMinioStorageService _storageService;
    private readonly ILogger<FilesController> _logger;

    // 最大文件大小限制 (100MB)
    private const long MaxFileSize = 100 * 1024 * 1024;

    public FilesController(
        IMinioStorageService storageService,
        ILogger<FilesController> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// 通用文件上传接口
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSize)]
    [ProducesResponseType(typeof(FileUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FileUploadResponse>> Upload(
        [FromForm] IFormFile file,
        [FromForm] string? bucketName = null)
    {
        try
        {
            // 验证文件
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "文件不能为空" });
            }

            if (file.Length > MaxFileSize)
            {
                return BadRequest(new { message = $"文件大小不能超过 {MaxFileSize / (1024 * 1024)} MB" });
            }

            // 获取用户ID
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // 自动选择存储桶
            var targetBucket = DetermineBucket(bucketName, file.FileName);

            // 生成唯一文件名
            var fileName = GenerateUniqueFileName(file.FileName);

            // 上传文件
            using var stream = file.OpenReadStream();
            var filePath = await _storageService.UploadFileAsync(
                targetBucket,
                fileName,
                stream,
                file.ContentType);

            // 生成预签名URL (有效期7天)
            var downloadUrl = await _storageService.GetPresignedUrlAsync(
                targetBucket,
                fileName,
                7 * 24 * 3600); // 7天转换为秒

            _logger.LogInformation(
                "文件上传成功: {FileName} -> {FilePath}, 用户: {UserId}",
                file.FileName,
                filePath,
                userId);

            return Ok(new FileUploadResponse
            {
                Success = true,
                FilePath = filePath,
                FileName = fileName,
                OriginalFileName = file.FileName,
                Bucket = targetBucket,
                Size = file.Length,
                ContentType = file.ContentType,
                DownloadUrl = downloadUrl,
                UploadedAt = DateTime.UtcNow,
                UploadedBy = userId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文件上传失败: {FileName}", file?.FileName);
            return StatusCode(500, new { message = "文件上传失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 批量文件上传
    /// </summary>
    [HttpPost("upload/batch")]
    [RequestSizeLimit(MaxFileSize * 10)] // 最多10个文件
    [ProducesResponseType(typeof(BatchFileUploadResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BatchFileUploadResponse>> UploadBatch(
        [FromForm] List<IFormFile> files,
        [FromForm] string? bucketName = null)
    {
        var results = new List<FileUploadResponse>();
        var errors = new List<string>();

        if (files == null || files.Count == 0)
        {
            return BadRequest(new { message = "未选择文件" });
        }

        if (files.Count > 10)
        {
            return BadRequest(new { message = "一次最多上传10个文件" });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        foreach (var file in files)
        {
            try
            {
                if (file.Length > MaxFileSize)
                {
                    errors.Add($"{file.FileName}: 文件大小超过限制");
                    continue;
                }

                var targetBucket = DetermineBucket(bucketName, file.FileName);
                var fileName = GenerateUniqueFileName(file.FileName);

                using var stream = file.OpenReadStream();
                var filePath = await _storageService.UploadFileAsync(
                    targetBucket,
                    fileName,
                    stream,
                    file.ContentType);

                var downloadUrl = await _storageService.GetPresignedUrlAsync(
                    targetBucket,
                    fileName,
                    7 * 24 * 3600); // 7天转换为秒

                results.Add(new FileUploadResponse
                {
                    Success = true,
                    FilePath = filePath,
                    FileName = fileName,
                    OriginalFileName = file.FileName,
                    Bucket = targetBucket,
                    Size = file.Length,
                    ContentType = file.ContentType,
                    DownloadUrl = downloadUrl,
                    UploadedAt = DateTime.UtcNow,
                    UploadedBy = userId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量上传文件失败: {FileName}", file.FileName);
                errors.Add($"{file.FileName}: {ex.Message}");
            }
        }

        return Ok(new BatchFileUploadResponse
        {
            Success = errors.Count == 0,
            TotalFiles = files.Count,
            SuccessCount = results.Count,
            FailedCount = errors.Count,
            Results = results,
            Errors = errors
        });
    }

    /// <summary>
    /// 删除文件
    /// </summary>
    [HttpDelete("{bucket}/{*objectName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string bucket, string objectName)
    {
        try
        {
            var exists = await _storageService.FileExistsAsync(bucket, objectName);
            if (!exists)
            {
                return NotFound(new { message = "文件不存在" });
            }

            await _storageService.DeleteFileAsync(bucket, objectName);

            _logger.LogInformation("文件删除成功: {Bucket}/{ObjectName}", bucket, objectName);

            return Ok(new { message = "文件删除成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文件删除失败: {Bucket}/{ObjectName}", bucket, objectName);
            return StatusCode(500, new { message = "文件删除失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 获取文件下载链接
    /// </summary>
    [HttpGet("download-url/{bucket}/{*objectName}")]
    [ProducesResponseType(typeof(DownloadUrlResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DownloadUrlResponse>> GetDownloadUrl(
        string bucket,
        string objectName,
        [FromQuery] int expiryHours = 24)
    {
        try
        {
            var exists = await _storageService.FileExistsAsync(bucket, objectName);
            if (!exists)
            {
                return NotFound(new { message = "文件不存在" });
            }

            var url = await _storageService.GetPresignedUrlAsync(
                bucket,
                objectName,
                expiryHours * 3600); // 小时转换为秒

            return Ok(new DownloadUrlResponse
            {
                Url = url,
                ExpiresAt = DateTime.UtcNow.AddHours(expiryHours)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取下载链接失败: {Bucket}/{ObjectName}", bucket, objectName);
            return StatusCode(500, new { message = "获取下载链接失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 列出存储桶中的文件
    /// </summary>
    [HttpGet("list/{bucket}")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<string>>> ListFiles(
        string bucket,
        [FromQuery] string? prefix = null)
    {
        try
        {
            var files = await _storageService.ListFilesAsync(bucket, prefix ?? string.Empty);
            return Ok(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "列出文件失败: {Bucket}", bucket);
            return StatusCode(500, new { message = "列出文件失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 代理获取文件（用于避免CORS问题）
    /// </summary>
    [HttpGet("proxy/{bucket}/{*objectName}")]
    [AllowAnonymous] // 允许匿名访问，因为是公开资源
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProxyFile(string bucket, string objectName)
    {
        try
        {
            var exists = await _storageService.FileExistsAsync(bucket, objectName);
            if (!exists)
            {
                return NotFound(new { message = "文件不存在" });
            }

            // 从MinIO获取文件流
            var stream = await _storageService.DownloadFileAsync(bucket, objectName);

            // 根据文件扩展名确定Content-Type
            var contentType = GetContentType(objectName);

            // 设置响应头以启用缓存
            Response.Headers.CacheControl = "public, max-age=604800"; // 7天缓存

            return File(stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "代理文件失败: {Bucket}/{ObjectName}", bucket, objectName);
            return StatusCode(500, new { message = "获取文件失败", error = ex.Message });
        }
    }

    private string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".json" => "application/json",
            ".xml" => "application/xml",
            _ => "application/octet-stream"
        };
    }

    private string DetermineBucket(string? requestedBucket, string fileName)
    {
        // 如果指定了存储桶，直接使用
        if (!string.IsNullOrEmpty(requestedBucket))
        {
            return requestedBucket;
        }

        // 根据文件扩展名自动选择存储桶
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            // 3D模型
            ".gltf" or ".glb" or ".obj" or ".fbx" or ".dae" or ".3ds" => MinioBuckets.MODELS_3D,

            // BIM模型
            ".ifc" or ".rvt" or ".rfa" => MinioBuckets.BIM_MODELS,

            // 视频
            ".mp4" or ".avi" or ".mov" or ".wmv" or ".flv" or ".mkv" => MinioBuckets.VIDEOS,

            // 纹理和图像
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".tga" or ".dds" => MinioBuckets.TEXTURES,

            // 文档
            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" => MinioBuckets.DOCUMENTS,

            // 倾斜摄影 (OSGB等)
            ".osgb" or ".xml" => MinioBuckets.TILT_PHOTOGRAPHY,

            // 默认使用临时存储桶
            _ => MinioBuckets.TEMP
        };
    }

    private string GenerateUniqueFileName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        var fileName = Path.GetFileNameWithoutExtension(originalFileName);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var guid = Guid.NewGuid().ToString("N")[..8]; // 取前8位

        return $"{fileName}_{timestamp}_{guid}{extension}";
    }
}

public class FileUploadResponse
{
    public bool Success { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public long Size { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string? UploadedBy { get; set; }
}

public class BatchFileUploadResponse
{
    public bool Success { get; set; }
    public int TotalFiles { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<FileUploadResponse> Results { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class DownloadUrlResponse
{
    public string Url { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
