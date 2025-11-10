namespace RealScene3D.Domain.Enums;

/// <summary>
/// 告警级别枚举
/// </summary>
public enum AlertLevel
{
    Info = 0,
    Warning = 1,
    Critical = 2,
    Error = 3
}

/// <summary>
/// 告警状态枚举
/// </summary>
public enum AlertStatus
{
    Firing = 0,
    Resolved = 1,
    Acknowledged = 2,
    Suppressed = 3
}

/// <summary>
/// 聚合函数枚举
/// </summary>
public enum AggregationFunction
{
    Avg,
    Max,
    Min,
    Sum,
    Count,
    Last
}

/// <summary>
/// 比较操作符枚举
/// </summary>
public enum ComparisonOperator
{
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Equal,
    NotEqual
}
