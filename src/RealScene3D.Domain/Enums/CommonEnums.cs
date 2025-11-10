namespace RealScene3D.Domain.Enums;

/// <summary>
/// 用户角色枚举
/// </summary>
public enum UserRole
{
    Guest = 0,
    User = 1,
    Admin = 2,
    SuperAdmin = 3
}

/// <summary>
/// 排序方向枚举
/// </summary>
public enum SortDirection
{
    Asc,
    Desc
}

/// <summary>
/// 存储位置类型枚举
/// </summary>
public enum StorageLocationType
{
    /// <summary>
    /// MinIO对象存储
    /// </summary>
    MinIO = 0,
    /// <summary>
    /// 本地文件系统
    /// </summary>
    LocalFileSystem = 1
}
