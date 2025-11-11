using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.Data;
using RealScene3D.Infrastructure.Authentication;

namespace RealScene3D.Application.Services;

/// <summary>
/// 用户服务实现
/// </summary>
public class UserService : IUserService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<UserLog> _logRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _context;
    private readonly IJwtTokenService _jwtTokenService;

    public UserService(
        IRepository<User> userRepository,
        IRepository<UserLog> logRepository,
        IUnitOfWork unitOfWork,
        ApplicationDbContext context,
        IJwtTokenService jwtTokenService)
    {
        _userRepository = userRepository;
        _logRepository = logRepository;
        _unitOfWork = unitOfWork;
        _context = context;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// 用户登录认证
    /// </summary>
    /// <param name="request">登录请求，包含邮箱和密码</param>
    /// <returns>登录响应，包含JWT令牌和用户信息</returns>
    public async Task<UserDtos.LoginResponse> LoginAsync(UserDtos.LoginRequest request)
    {
        var passwordHash = HashPassword(request.Password);
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.PasswordHash == passwordHash);

        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid credentials or inactive account");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        var token = GenerateToken(user);

        return new UserDtos.LoginResponse
        {
            Token = token,
            User = MapToDto(user)
        };
    }

    /// <summary>
    /// 用户注册
    /// </summary>
    /// <param name="request">注册请求，包含用户名、邮箱和密码</param>
    /// <returns>注册成功后的登录响应，包含令牌和用户信息</returns>
    public async Task<UserDtos.LoginResponse> RegisterAsync(UserDtos.RegisterRequest request)
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email || u.Username == request.Username);

        if (existingUser != null)
        {
            throw new InvalidOperationException("User already exists");
        }

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = HashPassword(request.Password),
            IsActive = true
        };

        await _userRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // 注册成功后自动生成令牌
        var token = GenerateToken(user);

        return new UserDtos.LoginResponse
        {
            Token = token,
            User = MapToDto(user)
        };
    }

    /// <summary>
    /// 根据ID获取用户信息
    /// </summary>
    /// <param name="id">用户唯一标识符</param>
    /// <returns>用户信息，如果不存在则返回null</returns>
    public async Task<UserDtos.UserDto?> GetUserByIdAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user != null ? MapToDto(user) : null;
    }

    /// <summary>
    /// 获取所有用户列表
    /// </summary>
    /// <returns>系统中所有用户的列表</returns>
    public async Task<IEnumerable<UserDtos.UserDto>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();
        return users.Select(MapToDto);
    }

    /// <summary>
    /// 根据ID获取用户实体
    /// </summary>
    /// <param name="id">用户唯一标识符</param>
    /// <returns>用户实体，如果不存在则返回null</returns>
    public async Task<User?> GetUserEntityAsync(Guid id)
    {
        return await _userRepository.GetByIdAsync(id);
    }

    /// <summary>
    /// 更新用户头像URL
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="avatarUrl">头像URL</param>
    /// <param name="oldAvatarUrl">旧的头像URL（用于清理）</param>
    public async Task<bool> UpdateUserAvatarAsync(Guid userId, string avatarUrl, string? oldAvatarUrl = null)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return false;
        }

        // 保存旧的头像URL用于返回
        oldAvatarUrl ??= user.AvatarUrl;

        user.AvatarUrl = avatarUrl;
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> LogUserActionAsync(Guid userId, string action, string details, string ipAddress)
    {
        var log = new UserLog
        {
            UserId = userId,
            Action = action,
            Details = details,
            IpAddress = ipAddress
        };

        await _logRepository.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    private string GenerateToken(User user)
    {
        // 使用JWT服务生成标准的JWT令牌
        return _jwtTokenService.GenerateAccessToken(user);
    }

    /// <summary>
    /// 将领域对象映射为DTO对象
    /// </summary>
    /// <param name="user">用户领域实体</param>
    /// <returns>用户DTO，包含前端所需的所有信息</returns>
    private static UserDtos.UserDto MapToDto(User user)
    {
        return new UserDtos.UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            AvatarUrl = user.AvatarUrl,
            CreatedAt = user.CreatedAt
        };
    }
}
