using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;

namespace RealScene3D.WebApi.Controllers;

/// <summary>
/// 认证管理API控制器
/// 提供用户注册、登录等认证功能的RESTful API接口
/// 支持基于JWT的身份验证机制
/// </summary>
[ApiController]
[Route("api/auth")]  // 明确指定小写路由
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// 构造函数 - 依赖注入用户服务和日志记录器
    /// </summary>
    /// <param name="userService">用户应用服务接口</param>
    /// <param name="logger">日志记录器,用于记录认证操作和安全事件</param>
    public AuthController(IUserService userService, ILogger<AuthController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="request">登录请求，包含邮箱和密码</param>
    /// <returns>登录响应，包含JWT令牌和用户信息</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<UserDtos.LoginResponse>> Login([FromBody] UserDtos.LoginRequest request)
    {
        try
        {
            var response = await _userService.LoginAsync(request);

            // 记录成功登录日志
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            await _userService.LogUserActionAsync(response.User.Id, "Login", "User logged in successfully", ipAddress);

            _logger.LogInformation("用户 {Email} 登录成功", request.Email);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("登录失败: {Email} - {Message}", request.Email, ex.Message);
            return Unauthorized(new { message = "邮箱或密码错误" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录时发生错误: {Email}", request.Email);
            return StatusCode(500, new { message = "登录时发生错误" });
        }
    }

    /// <summary>
    /// 用户注册
    /// </summary>
    /// <param name="request">注册请求，包含用户名、邮箱和密码</param>
    /// <returns>注册成功后的登录响应，包含令牌和用户信息</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<UserDtos.LoginResponse>> Register([FromBody] UserDtos.RegisterRequest request)
    {
        try
        {
            var response = await _userService.RegisterAsync(request);

            // 记录注册日志
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            await _userService.LogUserActionAsync(response.User.Id, "Register", "User registered successfully", ipAddress);

            _logger.LogInformation("新用户注册成功: {Username} ({Email})", request.Username, request.Email);
            return CreatedAtAction(nameof(Login), new { email = request.Email }, response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("注册失败: {Email} - {Message}", request.Email, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "注册时发生错误: {Email}", request.Email);
            return StatusCode(500, new { message = "注册时发生错误" });
        }
    }

    /// <summary>
    /// 获取当前登录用户信息
    /// 需要认证后才能访问
    /// </summary>
    /// <returns>当前用户的详细信息</returns>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDtos.UserDto>> GetCurrentUser()
    {
        try
        {
            // 从JWT令牌中获取用户ID
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                           ?? User.FindFirst("sub");

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { message = "无效的令牌" });
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "用户未找到" });
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取当前用户信息时发生错误");
            return StatusCode(500, new { message = "获取用户信息时发生错误" });
        }
    }
}
