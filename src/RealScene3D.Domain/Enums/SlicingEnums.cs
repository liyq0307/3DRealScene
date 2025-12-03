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
/// 纹理处理策略
/// </summary>
public enum TexturesStrategy
{
    /// <summary>
    /// 保持原样，不进行任何处理。
    /// </summary>
    KeepOriginal,

    /// <summary>
    /// 压缩纹理以减小文件大小，但保持其原始分辨率。
    /// </summary>
    Compress,

    /// <summary>
    /// 重新打包纹理以优化空间利用率，但保持其原始分辨率。
    /// </summary>
    Repack,

    /// <summary>
    /// 重新打包并压缩纹理以优化空间利用率和文件大小。
    /// </summary>
    RepackCompressed
}

/// <summary>
/// 自定义拆分策略
/// </summary>
public enum SplitPointStrategy
{
    /// <summary>
    /// 绝对中心
    /// 使用网格边界框的中心点作为拆分点
    /// </summary>
    AbsoluteCenter,

    /// <summary>
    /// 顶点重心
    /// 使用网格顶点的重心作为拆分点
    /// </summary>
    VertexBaricenter
}
