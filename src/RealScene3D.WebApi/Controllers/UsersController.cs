using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;
using RealScene3D.Infrastructure.MinIO;
using RealScene3D.Infrastructure.Services;
using RealScene3D.Infrastructure.Utilities;
using System.Security.Claims;

namespace RealScene3D.WebApi.Controllers;

/// <summary>
/// 用户管理API控制器
/// 提供用户认证、注册、信息管理等RESTful API接口
/// 支持用户登录注册、安全审计和行为追踪
/// 遵循ASP.NET Core安全最佳实践，包含身份验证和授权机制
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IMinioStorageService _storageService;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly IFileCleanupService _fileCleanupService;
    private readonly ILogger<UsersController> _logger;

    private const long MaxAvatarSize = 5 * 1024 * 1024; // 5MB
    private const int ThumbnailSize = 200; // 缩略图尺寸
    private const int ThumbnailQuality = 85; // 缩略图质量

    /// <summary>
    /// 构造函数 - 依赖注入用户服务和日志记录器
    /// </summary>
    /// <param name="userService">用户应用服务接口，提供业务逻辑处理</param>
    /// <param name="storageService">MinIO存储服务</param>
    /// <param name="imageProcessingService">图片处理服务</param>
    /// <param name="fileCleanupService">文件清理服务</param>
    /// <param name="logger">日志记录器，用于记录操作日志和安全事件</param>
    public UsersController(
        IUserService userService,
        IMinioStorageService storageService,
        IImageProcessingService imageProcessingService,
        IFileCleanupService fileCleanupService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _storageService = storageService;
        _imageProcessingService = imageProcessingService;
        _fileCleanupService = fileCleanupService;
        _logger = logger;
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    [HttpPost("login")]
    /// <summary>
    /// 用户登录认证
    /// </summary>
    /// <param name="request">登录请求，包含邮箱和密码</param>
    /// <returns>登录响应，包含JWT令牌和用户信息</returns>
    public async Task<ActionResult<UserDtos.LoginResponse>> Login([FromBody] UserDtos.LoginRequest request)
    {
        try
        {
            var response = await _userService.LoginAsync(request);

            // 记录用户登录
            await _userService.LogUserActionAsync(
                response.User.Id,
                "Login",
                "User logged in successfully",
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            );

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "登录失败: {Email} - {Message}", request.Email, ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录时发生错误");
            return StatusCode(500, new { message = "登录时发生错误" });
        }
    }

    /// <summary>
    /// 用户注册
    /// </summary>
    [HttpPost("register")]
    /// <summary>
    /// 用户注册
    /// </summary>
    /// <param name="request">注册请求，包含用户名、邮箱和密码</param>
    /// <returns>注册成功的用户信息和令牌</returns>
    public async Task<ActionResult<UserDtos.LoginResponse>> Register([FromBody] UserDtos.RegisterRequest request)
    {
        try
        {
            var response = await _userService.RegisterAsync(request);

            // 记录用户注册
            await _userService.LogUserActionAsync(
                response.User.Id,
                "Register",
                "User registered successfully",
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            );

            return CreatedAtAction(nameof(GetUser), new { id = response.User.Id }, response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "注册失败: {Email} - {Message}", request.Email, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "注册时发生错误");
            return StatusCode(500, new { message = "注册时发生错误" });
        }
    }

    /// <summary>
    /// 根据ID获取用户信息
    /// </summary>
    /// <param name="id">用户唯一标识符</param>
    /// <returns>用户信息，如果不存在则返回404</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDtos.UserDto>> GetUser(Guid id)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户 {UserId} 时发生错误", id);
            return StatusCode(500, new { message = "获取用户信息时发生错误" });
        }
    }

    /// <summary>
    /// 获取所有用户列表
    /// </summary>
    /// <returns>系统中所有用户的列表</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDtos.UserDto>>> GetAllUsers()
    {
        try
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有用户时发生错误");
            return StatusCode(500, new { message = "获取用户列表时发生错误" });
        }
    }

    /// <summary>
    /// 上传用户头像
    /// </summary>
    [HttpPost("avatar")]
    [Authorize]
    [RequestSizeLimit(MaxAvatarSize)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(AvatarUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AvatarUploadResponse>> UploadAvatar(IFormFile avatar)
    {
        try
        {
            // 1. 验证文件是否存在
            if (avatar == null || avatar.Length == 0)
            {
                return BadRequest(new { message = "请选择要上传的头像文件" });
            }

            // 2. 获取当前用户ID
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "用户未授权" });
            }

            // 3. 验证文件大小
            var sizeValidation = FileValidator.ValidateFileSize(avatar.Length, MaxAvatarSize);
            if (!sizeValidation.IsValid)
            {
                return BadRequest(new { message = sizeValidation.ErrorMessage });
            }

            // 4. 验证文件格式并生成缩略图
            Stream? thumbnailStream = null;
            long thumbnailSize = 0;
            string filePath;
            string thumbnailFileName;

            try
            {
                using var avatarStream = avatar.OpenReadStream();
                var formatValidation = FileValidator.ValidateImage(avatarStream, avatar.FileName, avatar.ContentType);
                if (!formatValidation.IsValid)
                {
                    return BadRequest(new { message = formatValidation.ErrorMessage });
                }

                _logger.LogInformation("开始处理用户 {UserId} 的头像上传: {FileName}, 大小: {Size}",
                    userId, avatar.FileName, FileValidator.GetFileSizeString(avatar.Length));

                // 5. 生成唯一文件名
                var extension = Path.GetExtension(avatar.FileName);
                var originalFileName = $"avatar_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}_original{extension}";
                thumbnailFileName = $"avatar_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}_thumb.jpg";

                // 6. 生成缩略图
                avatarStream.Position = 0;
                try
                {
                    thumbnailStream = await _imageProcessingService.GenerateThumbnailAsync(
                        avatarStream,
                        ThumbnailSize,
                        ThumbnailQuality);

                    thumbnailSize = thumbnailStream.Length;
                    _logger.LogInformation("缩略图生成成功: 原始大小 {OriginalSize}, 缩略图大小 {ThumbnailSize}",
                        FileValidator.GetFileSizeString(avatar.Length),
                        FileValidator.GetFileSizeString(thumbnailSize));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "生成缩略图失败，将使用原始图片");
                    // 如果缩略图生成失败，复制原始图片到新的流
                    avatarStream.Position = 0;
                    var ms = new MemoryStream();
                    await avatarStream.CopyToAsync(ms);
                    ms.Position = 0;
                    thumbnailStream = ms;
                    thumbnailSize = ms.Length;
                }

                // 7. 上传缩略图到MinIO
                filePath = await _storageService.UploadFileAsync(
                    MinioBuckets.THUMBNAILS,
                    thumbnailFileName,
                    thumbnailStream,
                    "image/jpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理头像上传时发生错误");
                // 清理可能创建的流
                if (thumbnailStream != null)
                {
                    await thumbnailStream.DisposeAsync();
                }
                throw;
            }

            // 8. 验证文件是否真的上传成功
            var fileExists = await _storageService.FileExistsAsync(MinioBuckets.THUMBNAILS, thumbnailFileName);
            if (!fileExists)
            {
                _logger.LogError("文件上传后验证失败: Bucket={Bucket}, FileName={FileName}",
                    MinioBuckets.THUMBNAILS, thumbnailFileName);
                return StatusCode(500, new { message = "文件上传验证失败，请稍后重试" });
            }

            _logger.LogInformation("文件上传成功并已验证: Bucket={Bucket}, FileName={FileName}, FilePath={FilePath}",
                MinioBuckets.THUMBNAILS, thumbnailFileName, filePath);

            // 9. 生成代理URL（通过后端API访问，避免CORS问题）
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var avatarUrl = $"{baseUrl}/api/files/proxy/{MinioBuckets.THUMBNAILS}/{thumbnailFileName}";

            _logger.LogInformation("生成的头像URL: {AvatarUrl}", avatarUrl);

            // 10. 获取用户当前的头像URL（用于清理）
            var user = await _userService.GetUserEntityAsync(userId);
            var oldAvatarUrl = user?.AvatarUrl;

            // 11. 更新用户头像URL到数据库
            var updateSuccess = await _userService.UpdateUserAvatarAsync(userId, avatarUrl, oldAvatarUrl);
            if (!updateSuccess)
            {
                _logger.LogError("更新用户头像URL失败: UserId={UserId}", userId);
                // 清理已上传的文件
                await _storageService.DeleteFileAsync(MinioBuckets.THUMBNAILS, thumbnailFileName);
                return StatusCode(500, new { message = "更新用户信息失败" });
            }

            // 12. 清理旧头像（异步执行，不阻塞响应）
            if (!string.IsNullOrEmpty(oldAvatarUrl))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var deleted = await _fileCleanupService.DeleteFileFromUrlAsync(oldAvatarUrl);
                        if (deleted)
                        {
                            _logger.LogInformation("旧头像已清理: {OldAvatarUrl}", oldAvatarUrl);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "清理旧头像失败（不影响主流程）: {OldAvatarUrl}", oldAvatarUrl);
                    }
                });
            }

            // 13. 记录操作日志
            await _userService.LogUserActionAsync(
                userId,
                "UpdateAvatar",
                $"User uploaded new avatar: {thumbnailFileName}",
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            );

            _logger.LogInformation("用户 {UserId} 上传头像成功: {FileName}", userId, thumbnailFileName);

            // 14. 清理临时流
            if (thumbnailStream != null)
            {
                await thumbnailStream.DisposeAsync();
            }

            return Ok(new AvatarUploadResponse
            {
                Success = true,
                AvatarUrl = avatarUrl,
                FileName = thumbnailFileName,
                Bucket = MinioBuckets.THUMBNAILS,
                FilePath = filePath,
                FileSize = thumbnailSize,
                Message = "头像上传成功"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "头像上传失败");
            return StatusCode(500, new { message = "头像上传失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 更新用户信息
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(UserDtos.UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDtos.UserDto>> UpdateUser(
        Guid id,
        [FromBody] UpdateUserRequest request)
    {
        try
        {
            // 验证用户是否有权限更新
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var currentUserId))
            {
                return Unauthorized(new { message = "用户未授权" });
            }

            if (currentUserId != id)
            {
                return Forbid(); // 用户只能更新自己的信息
            }

            // TODO: 实现用户信息更新逻辑
            // var updatedUser = await _userService.UpdateUserAsync(id, request);

            await _userService.LogUserActionAsync(
                id,
                "UpdateProfile",
                "User updated profile information",
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            );

            // 临时返回，需要实现完整的更新逻辑
            return Ok(new { message = "用户信息更新成功（功能待实现）" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新用户信息失败: {UserId}", id);
            return StatusCode(500, new { message = "更新用户信息失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            // 获取当前用户ID
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "用户未授权" });
            }

            // TODO: 实现密码修改逻辑
            // await _userService.ChangePasswordAsync(userId, request.OldPassword, request.NewPassword);

            await _userService.LogUserActionAsync(
                userId,
                "ChangePassword",
                "User changed password",
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            );

            return Ok(new { message = "密码修改成功（功能待实现）" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修改密码失败");
            return StatusCode(500, new { message = "修改密码失败", error = ex.Message });
        }
    }
}

// DTO类定义
public class AvatarUploadResponse
{
    public bool Success { get; set; }
    public string AvatarUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Department { get; set; }
}

public class ChangePasswordRequest
{
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
