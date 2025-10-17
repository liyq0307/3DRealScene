using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RealScene3D.Domain.Entities;

namespace RealScene3D.Infrastructure.Authentication;

/// <summary>
/// JWT令牌服务实现
/// 负责生成和管理JWT访问令牌
/// 使用对称加密算法(HMAC-SHA256)签名令牌以确保安全性
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _jwtSettings;

    /// <summary>
    /// 构造函数 - 通过依赖注入获取JWT配置
    /// </summary>
    /// <param name="jwtSettings">JWT配置选项,包含密钥、颁发者等信息</param>
    public JwtTokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    /// <summary>
    /// 为指定用户生成JWT访问令牌
    /// 令牌包含用户的身份信息(ID、邮箱、角色等)作为Claims
    /// </summary>
    /// <param name="user">用户实体,包含用户ID、邮箱、角色等信息</param>
    /// <returns>JWT访问令牌字符串,可用于后续API请求的身份验证</returns>
    public string GenerateAccessToken(User user)
    {
        // 创建签名密钥
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // 定义令牌中包含的声明(Claims)
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        // 创建JWT令牌
        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials
        );

        // 序列化令牌为字符串
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
