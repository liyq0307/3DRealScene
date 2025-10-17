using RealScene3D.Application.DTOs;

namespace RealScene3D.Application.Interfaces;

/// <summary>
/// 用户管理应用服务接口
/// 提供用户认证、注册、信息管理等核心功能
/// 支持用户行为日志记录和安全审计
/// </summary>
public interface IUserService
{
    /// <summary>
    /// 用户登录认证
    /// </summary>
    /// <param name="request">登录请求，包含邮箱和密码</param>
    /// <returns>登录响应，包含JWT令牌和用户信息</returns>
    Task<UserDtos.LoginResponse> LoginAsync(UserDtos.LoginRequest request);

    /// <summary>
    /// 用户注册
    /// </summary>
    /// <param name="request">注册请求，包含用户名、邮箱和密码</param>
    /// <returns>注册成功后的登录响应，包含令牌和用户信息</returns>
    Task<UserDtos.LoginResponse> RegisterAsync(UserDtos.RegisterRequest request);

    /// <summary>
    /// 根据ID获取用户信息
    /// </summary>
    /// <param name="id">用户唯一标识符</param>
    /// <returns>用户信息，如果不存在则返回null</returns>
    Task<UserDtos.UserDto?> GetUserByIdAsync(Guid id);

    /// <summary>
    /// 获取所有用户列表
    /// </summary>
    /// <returns>系统中所有用户的列表</returns>
    Task<IEnumerable<UserDtos.UserDto>> GetAllUsersAsync();

    /// <summary>
    /// 记录用户行为日志
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="action">行为动作，如：登录、登出、创建场景等</param>
    /// <param name="details">行为详情描述</param>
    /// <param name="ipAddress">用户IP地址，用于安全审计</param>
    /// <returns>日志记录是否成功</returns>
    Task<bool> LogUserActionAsync(Guid userId, string action, string details, string ipAddress);
}
