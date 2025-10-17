namespace RealScene3D.Domain.Entities;

/// <summary>
/// 用户日志实体
/// </summary>
public class UserLog : BaseEntity
{
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;

    // 导航属性
    public User User { get; set; } = null!;
}
