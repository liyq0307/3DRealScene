namespace RealScene3D.Infrastructure.Authentication;

/// <summary>
/// JWT认证配置类
/// 包含JWT令牌生成和验证所需的所有配置参数
/// 从配置文件读取,支持灵活配置以适应不同环境
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// 配置节名称
    /// 用于从appsettings.json中读取JWT配置
    /// </summary>
    public const string SectionName = "JwtSettings";

    /// <summary>
    /// JWT密钥,用于签名和验证令牌
    /// 必须足够长(建议至少32字符)以确保安全性
    /// 生产环境应使用环境变量或密钥管理服务存储
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// JWT颁发者
    /// 标识令牌的颁发方,通常是应用程序的域名或标识
    /// 用于令牌验证时确保令牌来自可信来源
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// JWT接收者
    /// 标识令牌的预期接收方,通常是前端应用的域名
    /// 用于令牌验证时确保令牌被正确的接收方使用
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// JWT过期时间(分钟)
    /// 令牌在此时间后将失效,需要重新登录或刷新
    /// 默认值为60分钟,可根据安全需求调整
    /// </summary>
    public int ExpirationMinutes { get; set; } = 60;
}
