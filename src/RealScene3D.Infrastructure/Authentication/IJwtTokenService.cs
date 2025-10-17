using RealScene3D.Domain.Entities;

namespace RealScene3D.Infrastructure.Authentication;

/// <summary>
/// JWT令牌服务接口
/// 定义JWT令牌的生成和管理功能
/// 支持基于用户信息生成安全的认证令牌
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// 为指定用户生成JWT访问令牌
    /// </summary>
    /// <param name="user">用户实体,包含用户ID、邮箱、角色等信息</param>
    /// <returns>JWT访问令牌字符串,可用于后续API请求的身份验证</returns>
    string GenerateAccessToken(User user);
}
