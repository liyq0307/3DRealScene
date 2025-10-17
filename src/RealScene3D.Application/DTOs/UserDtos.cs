namespace RealScene3D.Application.DTOs;

/// <summary>
/// 用户管理数据传输对象集合
/// 包含用户认证、注册、登录等相关DTO
/// 用于处理用户管理业务的数据传输
/// </summary>
public class UserDtos
{
    /// <summary>
    /// 用户登录请求DTO
    /// 用于接收用户登录的凭证信息
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// 用户邮箱地址，必填项
        /// 用于用户身份识别和登录验证
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 用户密码，必填项
        /// 以加密形式传输，确保安全性
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// 用户注册请求DTO
    /// 用于接收新用户注册的必要信息
    /// </summary>
    public class RegisterRequest
    {
        /// <summary>
        /// 用户名，必填项
        /// 用于显示和唯一标识用户，支持字母、数字和下划线
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 用户邮箱地址，必填项，唯一
        /// 用于登录验证、密码找回和系统通知
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 用户密码，必填项
        /// 长度至少8位，建议包含字母、数字和特殊字符
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// 用户响应DTO
    /// 用于向前端返回用户的完整信息
    /// 不包含敏感信息如密码等
    /// </summary>
    public class UserDto
    {
        /// <summary>
        /// 用户唯一标识符
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 用户名，唯一标识
        /// 用于显示和用户提及
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 用户邮箱地址，唯一
        /// 用于登录和联系
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 用户角色，如：管理员、普通用户等
        /// 用于权限控制和功能访问
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// 用户是否激活
        /// 只有激活的用户才能登录系统
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// 用户注册时间
        /// 记录用户的创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// 登录响应DTO
    /// 用于返回用户登录成功后的认证信息
    /// </summary>
    public class LoginResponse
    {
        /// <summary>
        /// JWT认证令牌，必填项
        /// 用于后续API请求的身份验证，有效期通常为几小时
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// 用户详细信息
        /// 登录成功后返回用户的完整信息
        /// </summary>
        public UserDto User { get; set; } = null!;
    }
}
