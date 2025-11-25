namespace RealScene3D.Domain.Enums;

/// <summary>
/// 切片任务状态枚举
/// </summary>
public enum SlicingTaskStatus
{
    Created = 0,
    Queued = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

/// <summary>
/// 纹理处理策略枚举
/// 控制切片时纹理的处理方式
/// </summary>
public enum TextureStrategy
{
    /// <summary>
    /// 重新打包纹理（默认，推荐）
    /// 为每个切片生成专属的纹理图集，只包含该切片实际使用的纹理区域
    /// 优点：大幅减少纹理文件大小，优化加载性能
    /// 缺点：处理时间稍长
    /// </summary>
    Repack = 0,

    /// <summary>
    /// 保留原始纹理（不推荐）
    /// 直接复制原始纹理文件，不进行重打包
    /// 优点：处理速度快
    /// 缺点：纹理文件较大，可能包含大量未使用的纹理区域
    /// </summary>
    KeepOriginal = 1,

    /// <summary>
    /// 重新打包并压缩纹理
    /// 在重打包基础上对纹理进行压缩（JPEG质量75）
    /// 优点：文件体积最小
    /// 缺点：有一定质量损失
    /// </summary>
    RepackCompressed = 2
}
