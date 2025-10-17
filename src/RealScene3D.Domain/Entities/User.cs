using RealScene3D.Domain.Enums;

namespace RealScene3D.Domain.Entities;

/// <summary>
/// 用户领域实体类
/// 表示系统用户核心业务概念，管理用户身份、权限和状态信息
/// 继承自BaseEntity，具备审计字段和软删除功能
/// </summary>
public class User : BaseEntity
{
    /// <summary>
    /// 用户名，必填项，全局唯一
    /// 用于用户登录和显示，支持字母、数字、下划线组合
    /// 长度通常在3-50字符之间，由业务层验证
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 用户邮箱地址，必填项，全局唯一
    /// 用于登录验证、密码找回和系统通知
    /// 必须符合标准的邮箱格式，由业务层验证
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 密码哈希值，必填项
    /// 存储加密后的密码哈希，不能明文存储
    /// 使用安全的哈希算法（如bcrypt或Argon2）进行加密
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 用户角色，默认普通用户
    /// 使用枚举类型定义角色体系，如：管理员、普通用户、访客等
    /// 用于权限控制和功能访问限制
    /// </summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>
    /// 用户是否激活，默认激活状态
    /// 只有激活的用户才能正常登录和使用系统功能
    /// 支持管理员禁用用户账号的安全管理
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 最后登录时间，可选项
    /// 记录用户最后一次成功登录的时间戳
    /// 用于安全监控、活跃用户统计和会话管理
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 用户操作日志导航属性
    /// 与UserLog实体建立一对多关联关系
    /// 用于记录和追踪用户的操作历史，支持审计和安全分析
    /// </summary>
    public ICollection<UserLog> UserLogs { get; set; } = new List<UserLog>();
}
