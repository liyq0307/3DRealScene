using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealScene3D.Infrastructure.MinIO;
using RealScene3D.Infrastructure.Utilities;
using System.Security.Claims;

namespace RealScene3D.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IMinioStorageService _storageService;
    private readonly ILogger<FilesController> _logger;

    // 最大文件大小限制 (500MB) - 匹配前端限制
    private const long MaxFileSize = 500 * 1024 * 1024;

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
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(FileUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FileUploadResponse>> Upload(
        IFormFile file,
        string? bucketName = null,
        string? folderName = null,
        bool keepOriginalName = true)
    {
        try
        {
            // 1. 验证文件大小
            var sizeValidation = FileValidator.ValidateFileSize(file.Length, MaxFileSize);
            if (!sizeValidation.IsValid)
            {
                return BadRequest(new { message = sizeValidation.ErrorMessage });
            }

            // 2. 获取用户ID
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            _logger.LogInformation("开始处理文件上传: {FileName}, 大小: {Size}, 用户: {UserId}",
                file.FileName, FileValidator.GetFileSizeString(file.Length), userId ?? "Anonymous");

            // 3. 自动选择存储桶
            var targetBucket = DetermineBucket(bucketName, file.FileName);

            // 4. 确定文件名（支持保持原始文件名或生成唯一文件名）
            string objectName;
            if (keepOriginalName)
            {
                // 保持原始文件名
                if (!string.IsNullOrEmpty(folderName))
                {
                    // 如果指定了文件夹，放在文件夹中
                    var sanitizedFolder = SanitizeFolderName(folderName);
                    objectName = $"{sanitizedFolder}/{file.FileName}";
                }
                else
                {
                    // 直接使用原始文件名（可能会覆盖同名文件）
                    objectName = file.FileName;
                }
            }
            else
            {
                // 生成唯一文件名（添加时间戳和GUID）
                objectName = GenerateUniqueFileName(file.FileName);
            }

            _logger.LogInformation("目标对象名称: {ObjectName}, 保持原始文件名: {KeepOriginalName}",
                objectName, keepOriginalName);

            // 5. 上传文件（自动重试）
            using var stream = file.OpenReadStream();
            var filePath = await _storageService.UploadFileAsync(
                targetBucket,
                objectName,
                stream,
                file.ContentType);

            // 6. 验证文件上传成功
            var fileExists = await _storageService.FileExistsAsync(targetBucket, objectName);
            if (!fileExists)
            {
                _logger.LogError("文件上传后验证失败: Bucket={Bucket}, ObjectName={ObjectName}",
                    targetBucket, objectName);
                return StatusCode(500, new { message = "文件上传验证失败，请稍后重试" });
            }

            _logger.LogInformation("文件上传成功并已验证: Bucket={Bucket}, ObjectName={ObjectName}, FilePath={FilePath}",
                targetBucket, objectName, filePath);

            // 7. 生成预签名URL (有效期7天)
            var downloadUrl = await _storageService.GetPresignedUrlAsync(
                targetBucket,
                objectName,
                7 * 24 * 3600); // 7天转换为秒

            return Ok(new FileUploadResponse
            {
                Success = true,
                FilePath = filePath,
                FileName = objectName,  // 完整对象名称（可能包含文件夹路径）
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
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BatchFileUploadResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BatchFileUploadResponse>> UploadBatch(
        List<IFormFile> files,
        string? bucketName = null,
        string? folderName = null)  // 新增：文件夹名称参数
    {
        var results = new List<FileUploadResponse>();
        var errors = new List<string>();

        // 1. 验证文件列表
        if (files == null || files.Count == 0)
        {
            return BadRequest(new { message = "未选择文件" });
        }

        if (files.Count > 10)
        {
            return BadRequest(new { message = "一次最多上传10个文件" });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var totalSize = files.Sum(f => f.Length);

        _logger.LogInformation("开始批量上传: {Count} 个文件, 总大小: {TotalSize}, 用户: {UserId}",
            files.Count, FileValidator.GetFileSizeString(totalSize), userId ?? "Anonymous");

        // 2. 确定上传目录
        // 优先使用用户指定的文件夹名，否则使用第一个模型文件的文件名（不含扩展名）
        var targetBucket = string.IsNullOrEmpty(bucketName) ? MinioBuckets.MODELS_3D : bucketName;
        string uploadDirectory;

        if (!string.IsNullOrEmpty(folderName))
        {
            // 使用用户指定的文件夹名
            uploadDirectory = SanitizeFolderName(folderName);
        }
        else
        {
            // 从文件列表中找到第一个模型文件，使用其文件名（不含扩展名）作为文件夹名
            var modelFile = files.FirstOrDefault(f =>
                f.FileName.EndsWith(".obj", StringComparison.OrdinalIgnoreCase) ||
                f.FileName.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase) ||
                f.FileName.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ||
                f.FileName.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase));

            if (modelFile != null)
            {
                uploadDirectory = Path.GetFileNameWithoutExtension(modelFile.FileName);
            }
            else
            {
                // 如果没有找到模型文件，使用时间戳
                uploadDirectory = $"upload_{DateTime.UtcNow:yyyyMMddHHmmss}";
            }
        }

        _logger.LogInformation("批量上传目录: {Bucket}/{Directory}", targetBucket, uploadDirectory);

        // 3. 逐个处理文件，保持原始文件名
        foreach (var file in files)
        {
            try
            {
                // 验证单个文件大小
                var sizeValidation = FileValidator.ValidateFileSize(file.Length, MaxFileSize);
                if (!sizeValidation.IsValid)
                {
                    errors.Add($"{file.FileName}: {sizeValidation.ErrorMessage}");
                    continue;
                }

                // 保持原始文件名，放在统一的子目录下
                var objectName = $"{uploadDirectory}/{file.FileName}";

                // 上传文件（自动重试）
                using var stream = file.OpenReadStream();
                var filePath = await _storageService.UploadFileAsync(
                    targetBucket,
                    objectName,
                    stream,
                    file.ContentType);

                // 验证上传成功
                var fileExists = await _storageService.FileExistsAsync(targetBucket, objectName);
                if (!fileExists)
                {
                    _logger.LogWarning("批量上传中文件验证失败: {FileName}", objectName);
                    errors.Add($"{file.FileName}: 上传验证失败");
                    continue;
                }

                var downloadUrl = await _storageService.GetPresignedUrlAsync(
                    targetBucket,
                    objectName,
                    7 * 24 * 3600); // 7天转换为秒

                results.Add(new FileUploadResponse
                {
                    Success = true,
                    FilePath = filePath,
                    FileName = objectName,  // 包含子目录的完整路径
                    OriginalFileName = file.FileName,
                    Bucket = targetBucket,
                    Size = file.Length,
                    ContentType = file.ContentType,
                    DownloadUrl = downloadUrl,
                    UploadedAt = DateTime.UtcNow,
                    UploadedBy = userId
                });

                _logger.LogInformation("批量上传成功: {OriginalFileName} -> {ObjectName}",
                    file.FileName, objectName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量上传文件失败: {FileName}", file.FileName);
                errors.Add($"{file.FileName}: {ex.Message}");
            }
        }

        _logger.LogInformation("批量上传完成: 成功 {SuccessCount}/{TotalCount}, 失败 {FailedCount}",
            results.Count, files.Count, errors.Count);

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
    /// 代理访问MinIO文件（用于3D Tiles等需要目录结构的场景）
    /// </summary>
    [HttpGet("proxy/{bucket}/{*objectPath}")]
    [AllowAnonymous] // 允许匿名访问，因为Cesium无法传递认证token
    public async Task<IActionResult> ProxyFile(string bucket, string objectPath)
    {
        try
        {
            // 验证bucket是否在允许列表中
            var allowedBuckets = new[] { MinioBuckets.MODELS_3D, MinioBuckets.TEXTURES, MinioBuckets.THUMBNAILS, MinioBuckets.SLICES };
            if (!allowedBuckets.Contains(bucket))
            {
                return BadRequest(new { message = "不支持的存储桶" });
            }

            // 使用原始路径，保持文件名完整性
            var decodedObjectPath = objectPath;

            // 检查文件是否存在
            var exists = await _storageService.FileExistsAsync(bucket, decodedObjectPath);
            if (!exists)
            {
                _logger.LogWarning("代理访问文件不存在: {Bucket}/{ObjectPath} (原始路径: {RawPath})", bucket, decodedObjectPath, objectPath);
                return NotFound();
            }

            // 获取文件流
            Stream? stream = null;
            var fileFound = false;

            // 首先尝试从MinIO获取
            try
            {
                var minioExists = await _storageService.FileExistsAsync(bucket, decodedObjectPath);
                if (minioExists)
                {
                    stream = await _storageService.DownloadFileAsync(bucket, decodedObjectPath);
                    fileFound = true;
                    _logger.LogDebug("从MinIO获取文件成功：{Bucket}/{ObjectPath}", bucket, decodedObjectPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "从MinIO获取文件失败，尝试从本地文件系统读取：{Bucket}/{ObjectPath}", bucket, decodedObjectPath);
            }

            // 如果MinIO中没有找到文件，尝试从本地文件系统读取
            if (!fileFound)
            {
                var localPath = decodedObjectPath;
                // 如果路径是Windows绝对路径，尝试直接读取
                if (System.IO.Path.IsPathRooted(localPath) && System.IO.File.Exists(localPath))
                {
                    stream = System.IO.File.OpenRead(localPath);
                    fileFound = true;
                    _logger.LogInformation("从本地文件系统加载文件成功：{LocalPath}", localPath);
                }
                else
                {
                    // 尝试在slices目录下查找
                    var slicesDir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "slices");
                    var relativePath = decodedObjectPath.TrimStart('/');
                    var fullLocalPath = System.IO.Path.Combine(slicesDir, relativePath);
                    if (System.IO.File.Exists(fullLocalPath))
                    {
                        stream = System.IO.File.OpenRead(fullLocalPath);
                        fileFound = true;
                        _logger.LogInformation("从slices目录加载文件成功：{LocalPath}", fullLocalPath);
                    }
                }
            }

            if (!fileFound || stream == null)
            {
                _logger.LogWarning("文件不存在：MinIO {Bucket}/{ObjectPath}", bucket, decodedObjectPath);
                return NotFound();
            }

            // 根据文件扩展名确定Content-Type
            var contentType = GetContentTypeFromPath(decodedObjectPath);

            // 检查是否是b3dm文件且已gzip压缩
            var extension = Path.GetExtension(decodedObjectPath).ToLowerInvariant();
            if (extension == ".b3dm" || extension == ".pnts" || extension == ".i3dm" || extension == ".cmpt")
            {
                // 读取文件头部，检查是否是gzip压缩
                var header = new byte[2];
                var bytesRead = await stream.ReadAsync(header, 0, 2);

                // 检查gzip魔数 (0x1f 0x8b)
                if (bytesRead == 2 && header[0] == 0x1f && header[1] == 0x8b)
                {
                    _logger.LogInformation("检测到gzip压缩的3D Tiles文件，进行解压: {Bucket}/{ObjectPath}", bucket, decodedObjectPath);

                    // 重置流位置
                    stream.Position = 0;

                    // 解压
                    using (var gzipStream = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress, leaveOpen: false))
                    {
                        var decompressedData = new MemoryStream();
                        await gzipStream.CopyToAsync(decompressedData);
                        decompressedData.Position = 0;

                        _logger.LogInformation("成功解压3D Tiles文件: {Bucket}/{ObjectPath}, 解压后大小: {DecompressedSize}",
                            bucket, decodedObjectPath, decompressedData.Length);

                        stream = decompressedData;
                    }
                }
                else
                {
                    // 重置流位置
                    stream.Position = 0;
                }
            }

            // 修复tileset.json的schema字段问题
            if (decodedObjectPath.EndsWith("tileset.json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var reader = new StreamReader(stream);
                    var jsonContent = await reader.ReadToEndAsync();

                    // 解析JSON并移除字符串类型的schema字段
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
                    var memoryStream = new MemoryStream();
                    using (var writer = new System.Text.Json.Utf8JsonWriter(memoryStream, new System.Text.Json.JsonWriterOptions { SkipValidation = false }))
                    {
                        writer.WriteStartObject();

                        foreach (var property in jsonDoc.RootElement.EnumerateObject())
                        {
                            // 跳过字符串类型的schema字段
                            if (property.Name.Equals("schema", StringComparison.OrdinalIgnoreCase) &&
                                property.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                _logger.LogDebug("移除tileset.json中的字符串schema字段: {Path}", decodedObjectPath);
                                continue;
                            }

                            // 复制其他属性
                            property.WriteTo(writer);
                        }

                        writer.WriteEndObject();
                        writer.Flush();
                    }

                    memoryStream.Position = 0;
                    stream = memoryStream;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "处理tileset.json时出错，返回原始文件: {Path}", decodedObjectPath);
                    // 如果处理失败，重新获取原始流
                    stream = await _storageService.DownloadFileAsync(bucket, decodedObjectPath);
                }
            }

            _logger.LogInformation("代理访问文件成功: {Bucket}/{ObjectPath}", bucket, decodedObjectPath);

            // 设置CORS头，允许Cesium跨域访问
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            Response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
            Response.Headers["Access-Control-Allow-Headers"] = "*";
            // 添加缓存控制，避免重复请求
            Response.Headers["Cache-Control"] = "public, max-age=3600"; // 1小时缓存

            // 确保Content-Type正确设置，特别是对于JSON文件
            if (decodedObjectPath.EndsWith("tileset.json", StringComparison.OrdinalIgnoreCase))
            {
                Response.ContentType = "application/json";
            }

            return File(stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "代理访问文件失败: {Bucket}/{ObjectPath}", bucket, objectPath);
            return StatusCode(500, new { message = "访问文件失败" });
        }
    }

    /// <summary>
    /// 访问本地文件系统中的文件（用于开发和测试环境）
    /// </summary>
    [HttpGet("local/{*filePath}")]
    [AllowAnonymous] // 允许匿名访问，因为Cesium无法传递认证token
    public IActionResult GetLocalFile(string filePath)
    {
        try
        {
            // 构建完整的本地文件路径
            // 基础路径: F:/Data/3D/
            var basePath = "F:\\Data\\3D\\";
            var fullPath = Path.Combine(basePath, filePath.Replace('/', '\\'));

            _logger.LogInformation("访问本地文件: {FilePath}", fullPath);

            if (!System.IO.File.Exists(fullPath))
            {
                _logger.LogWarning("本地文件不存在: {FilePath}", fullPath);
                return NotFound(new { message = "文件不存在", path = fullPath });
            }

            var contentType = GetContentTypeFromPath(filePath);

            // 检查是否是b3dm文件且已gzip压缩
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension == ".b3dm" || extension == ".pnts" || extension == ".i3dm" || extension == ".cmpt")
            {
                // 读取文件头部，检查是否是gzip压缩
                using (var fs = System.IO.File.OpenRead(fullPath))
                {
                    var header = new byte[2];
                    fs.Read(header, 0, 2);

                    // 检查gzip魔数 (0x1f 0x8b)
                    if (header[0] == 0x1f && header[1] == 0x8b)
                    {
                        _logger.LogInformation("检测到gzip压缩的b3dm文件，进行解压: {FilePath}", fullPath);

                        // 重新打开文件并解压
                        using (var compressedStream = System.IO.File.OpenRead(fullPath))
                        using (var gzipStream = new System.IO.Compression.GZipStream(compressedStream, System.IO.Compression.CompressionMode.Decompress))
                        {
                            var decompressedData = new MemoryStream();
                            gzipStream.CopyTo(decompressedData);
                            decompressedData.Position = 0;

                            // 设置为inline，避免浏览器下载
                            Response.Headers["Content-Disposition"] = "inline";
                            Response.Headers["Cache-Control"] = "public, max-age=3600";

                            _logger.LogInformation("成功解压并返回本地文件: {FilePath}, 原始大小: {CompressedSize}, 解压后大小: {DecompressedSize}",
                                fullPath, new FileInfo(fullPath).Length, decompressedData.Length);

                            return File(decompressedData.ToArray(), contentType);
                        }
                    }
                }
            }

            // 读取文件（未压缩或非3D Tiles文件）
            var fileStream = System.IO.File.OpenRead(fullPath);

            // 设置为inline，避免浏览器下载
            Response.Headers["Content-Disposition"] = "inline";

            // 添加缓存控制
            Response.Headers["Cache-Control"] = "public, max-age=3600";

            _logger.LogInformation("成功返回本地文件: {FilePath}, ContentType: {ContentType}", fullPath, contentType);
            return File(fileStream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "访问本地文件失败：{FilePath}", filePath);
            return StatusCode(500, new { message = "访问文件失败", error = ex.Message });
        }
    }

    private string GetContentTypeFromPath(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".json" => "application/json",
            ".b3dm" => "application/octet-stream",
            ".pnts" => "application/octet-stream",
            ".i3dm" => "application/octet-stream",
            ".cmpt" => "application/octet-stream",
            ".glb" => "model/gltf-binary",
            ".gltf" => "model/gltf+json",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".obj" => "text/plain",
            _ => "application/octet-stream"
        };
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

    /// <summary>
    /// 清理文件夹名称，移除不安全的字符
    /// </summary>
    private string SanitizeFolderName(string folderName)
    {
        // 移除路径分隔符和其他不安全字符
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", folderName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

        // 限制长度
        if (sanitized.Length > 100)
        {
            sanitized = sanitized.Substring(0, 100);
        }

        return sanitized;
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
