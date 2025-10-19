using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;
using RealScene3D.Infrastructure.MinIO;
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
    private readonly ILogger<UsersController> _logger;

    private const long MaxAvatarSize = 5 * 1024 * 1024; // 5MB

    /// <summary>
    /// 构造函数 - 依赖注入用户服务和日志记录器
    /// </summary>
    /// <param name="userService">用户应用服务接口，提供业务逻辑处理</param>
    /// <param name="storageService">MinIO存储服务</param>
    /// <param name="logger">日志记录器，用于记录操作日志和安全事件</param>
    public UsersController(
        IUserService userService,
        IMinioStorageService storageService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _storageService = storageService;
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
            _logger.LogWarning(ex, "Login failed for {Email}", request.Email);
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { message = "An error occurred during login" });
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
            _logger.LogWarning(ex, "Registration failed for {Email}", request.Email);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return StatusCode(500, new { message = "An error occurred during registration" });
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
            _logger.LogError(ex, "Error getting user {UserId}", id);
            return StatusCode(500, new { message = "An error occurred" });
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
            _logger.LogError(ex, "Error getting all users");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// 上传用户头像
    /// </summary>
    [HttpPost("avatar")]
    [Authorize]
    [RequestSizeLimit(MaxAvatarSize)]
    [ProducesResponseType(typeof(AvatarUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AvatarUploadResponse>> UploadAvatar([FromForm] IFormFile avatar)
    {
        try
        {
            // 验证文件
            if (avatar == null || avatar.Length == 0)
            {
                return BadRequest(new { message = "头像文件不能为空" });
            }

            if (avatar.Length > MaxAvatarSize)
            {
                return BadRequest(new { message = $"头像大小不能超过 {MaxAvatarSize / (1024 * 1024)} MB" });
            }

            // 验证文件类型
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(avatar.ContentType.ToLower()))
            {
                return BadRequest(new { message = "只支持 JPEG、PNG、WebP 格式的图片" });
            }

            // 获取当前用户ID
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "用户未授权" });
            }

            // 生成唯一文件名
            var extension = Path.GetExtension(avatar.FileName);
            var fileName = $"avatar_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";

            // 上传到MinIO
            using var stream = avatar.OpenReadStream();
            var filePath = await _storageService.UploadFileAsync(
                MinioBuckets.THUMBNAILS,
                fileName,
                stream,
                avatar.ContentType);

            // 生成预签名URL (有效期30天)
            var avatarUrl = await _storageService.GetPresignedUrlAsync(
                MinioBuckets.THUMBNAILS,
                fileName,
                30 * 24 * 3600); // 30天转换为秒

            // TODO: 更新用户头像URL到数据库
            // await _userService.UpdateUserAvatarAsync(userId, avatarUrl);

            // 记录操作日志
            await _userService.LogUserActionAsync(
                userId,
                "UpdateAvatar",
                $"User uploaded new avatar: {fileName}",
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            );

            _logger.LogInformation("用户 {UserId} 上传头像成功: {FileName}", userId, fileName);

            return Ok(new AvatarUploadResponse
            {
                Success = true,
                AvatarUrl = avatarUrl,
                FileName = fileName,
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
