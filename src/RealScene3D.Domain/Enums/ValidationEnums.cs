namespace RealScene3D.Domain.Enums;

/// <summary>
/// 验证问题类型
/// </summary>
public enum ValidationIssueType
{
    MissingFile,
    IncorrectPath,
    MissingMetadata,
    StructureError,
    FormatError
}

/// <summary>
/// 验证严重程度
/// </summary>
public enum ValidationSeverity
{
    Error,
    Warning,
    Info
}

/// <summary>
/// 修复状态
/// </summary>
public enum RepairStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}
