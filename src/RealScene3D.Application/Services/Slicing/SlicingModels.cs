using RealScene3D.Domain.Entities;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// 几何图元数据结构
/// 共享类，供AdaptiveSlicingStrategy和GeometricDensityAnalyzer使用
/// </summary>
public class GeometricPrimitive
{
    public Vector3D[] Vertices { get; set; } = new Vector3D[3];
    public required Triangle Triangle { get; set; }
    public required Vector3D Normal { get; set; }
    public double Area { get; set; }
    public Vector3D Center => new Vector3D
    {
        X = (Vertices[0].X + Vertices[1].X + Vertices[2].X) / 3,
        Y = (Vertices[0].Y + Vertices[1].Y + Vertices[2].Y) / 3,
        Z = (Vertices[0].Z + Vertices[1].Z + Vertices[2].Z) / 3
    };
}

/// <summary>
/// 密度分析指标
/// 共享类，供AdaptiveSlicingStrategy和GeometricDensityAnalyzer使用
/// </summary>
public class DensityMetrics
{
    public double VertexDensity { get; set; }
    public double TriangleDensity { get; set; }
    public double CurvatureComplexity { get; set; }
    public double SurfaceArea { get; set; }
    public double Volume { get; set; }
}

/// <summary>
/// 空间索引结构
/// 共享类，供AdaptiveSlicingStrategy和GeometricDensityAnalyzer使用
/// </summary>
public class SpatialIndex
{
    public Dictionary<string, List<GeometricPrimitive>> Grid { get; set; } = new Dictionary<string, List<GeometricPrimitive>>();
    public required BoundingBox3D Bounds { get; set; }
}


/// <summary>
/// 任务进度历史记录 - 用于趋势分析和精确时间估算
/// 线程安全的进度跟踪类，支持并发访问
/// </summary>
internal class TaskProgressHistory
{
    private readonly object _lock = new object();
    private readonly List<ProgressRecord> _progressRecords = new();
    private const int MaxRecordsCount = 100; // 最多保留100条历史记录
    private const int MaxRecordAgeMinutes = 60; // 最多保留60分钟的历史记录

    /// <summary>
    /// 进度记录列表（只读）
    /// </summary>
    public IReadOnlyList<ProgressRecord> ProgressRecords
    {
        get
        {
            lock (_lock)
            {
                return _progressRecords.ToList();
            }
        }
    }

    /// <summary>
    /// 上次估算的剩余时间（秒）
    /// </summary>
    public double? LastEstimatedTime { get; set; }

    /// <summary>
    /// 记录进度数据点
    /// </summary>
    /// <param name="progress">当前进度（0-100）</param>
    /// <param name="timestamp">时间戳</param>
    public void RecordProgress(double progress, DateTime timestamp)
    {
        lock (_lock)
        {
            // 添加新记录
            _progressRecords.Add(new ProgressRecord
            {
                Progress = progress,
                Timestamp = timestamp
            });

            // 清理过期数据
            CleanupOldRecords();

            // 限制记录数量
            while (_progressRecords.Count > MaxRecordsCount)
            {
                _progressRecords.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// 获取指定时间范围内的记录
    /// </summary>
    /// <param name="timeSpan">时间范围</param>
    /// <returns>时间范围内的记录列表</returns>
    public List<ProgressRecord> GetRecentRecords(TimeSpan timeSpan)
    {
        lock (_lock)
        {
            var cutoffTime = DateTime.UtcNow - timeSpan;
            return _progressRecords.Where(r => r.Timestamp >= cutoffTime).ToList();
        }
    }

    /// <summary>
    /// 清理过期记录
    /// </summary>
    private void CleanupOldRecords()
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-MaxRecordAgeMinutes);
        _progressRecords.RemoveAll(r => r.Timestamp < cutoffTime);
    }

    /// <summary>
    /// 进度记录数据点
    /// </summary>
    public class ProgressRecord
    {
        public double Progress { get; set; }
        public DateTime Timestamp { get; set; }
    }
}