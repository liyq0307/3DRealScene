using Microsoft.AspNetCore.Mvc;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;

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
    private readonly ILogger<UsersController> _logger;

    /// <summary>
    /// 构造函数 - 依赖注入用户服务和日志记录器
    /// </summary>
    /// <param name="userService">用户应用服务接口，提供业务逻辑处理</param>
    /// <param name="logger">日志记录器，用于记录操作日志和安全事件</param>
    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
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
}
