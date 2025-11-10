using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Enums;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.MinIO;

namespace RealScene3D.Application.Services.Strategys;

/// <summary>
/// 切片策略工厂实现 - 创建各种切片策略实例
/// 设计模式：工厂方法模式 + 依赖注入
/// 职责：集中管理切片策略的创建逻辑，解耦切片处理器与具体策略
/// </summary>
public class SlicingStrategyFactory : ISlicingStrategyFactory
{
    private readonly ILogger<SlicingStrategyFactory> _logger;
    private readonly ITileGeneratorFactory _tileGeneratorFactory;
    private readonly IModelLoader _modelLoader;
    private readonly IMinioStorageService _minioService;
    private readonly MeshDecimationService? _meshDecimationService;

    /// <summary>
    /// 构造函数 - 注入所有策略可能需要的依赖
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="tileGeneratorFactory">瓦片生成器工厂</param>
    /// <param name="modelLoader">模型加载器</param>
    /// <param name="minioService">MinIO存储服务</param>
    /// <param name="meshDecimationService">网格简化服务（可选）</param>
    public SlicingStrategyFactory(
        ILogger<SlicingStrategyFactory> logger,
        ITileGeneratorFactory tileGeneratorFactory,
        IModelLoader modelLoader,
        IMinioStorageService minioService,
        MeshDecimationService? meshDecimationService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tileGeneratorFactory = tileGeneratorFactory ?? throw new ArgumentNullException(nameof(tileGeneratorFactory));
        _modelLoader = modelLoader ?? throw new ArgumentNullException(nameof(modelLoader));
        _minioService = minioService ?? throw new ArgumentNullException(nameof(minioService));
        _meshDecimationService = meshDecimationService;
    }

    /// <summary>
    /// 创建切片策略实例
    /// </summary>
    /// <param name="strategy">切片策略枚举</param>
    /// <returns>切片策略实例</returns>
    /// <exception cref="NotSupportedException">当请求的策略不支持时抛出</exception>
    public ISlicingStrategy CreateStrategy(SlicingStrategy strategy)
    {
        _logger.LogDebug("创建切片策略: {Strategy}", strategy);

        return strategy switch
        {
            SlicingStrategy.Grid => new GridSlicingStrategy(
                CreateGenericLogger<GridSlicingStrategy>(),
                _tileGeneratorFactory,
                _modelLoader,
                _meshDecimationService),

            SlicingStrategy.Octree => new OctreeSlicingStrategy(
                CreateGenericLogger<OctreeSlicingStrategy>(),
                _tileGeneratorFactory,
                _modelLoader,
                _meshDecimationService),

            SlicingStrategy.KdTree => new KdTreeSlicingStrategy(
                CreateGenericLogger<KdTreeSlicingStrategy>(),
                _tileGeneratorFactory,
                _modelLoader,
                _meshDecimationService),

            SlicingStrategy.Adaptive => new AdaptiveSlicingStrategy(
                CreateGenericLogger<AdaptiveSlicingStrategy>(),
                _tileGeneratorFactory,
                _minioService),

            SlicingStrategy.Recursive => new RecursiveSubdivisionStrategy(
                CreateTypedLogger<RecursiveSubdivisionStrategy>(),
                _tileGeneratorFactory,
                _modelLoader,
                _meshDecimationService),

            _ => throw new NotSupportedException($"不支持的切片策略: {strategy}")
        };
    }

    /// <summary>
    /// 检查是否支持指定的切片策略
    /// </summary>
    /// <param name="strategy">切片策略</param>
    /// <returns>如果支持返回true，否则返回false</returns>
    public bool SupportsStrategy(SlicingStrategy strategy)
    {
        return strategy switch
        {
            SlicingStrategy.Grid => true,
            SlicingStrategy.Octree => true,
            SlicingStrategy.KdTree => true,
            SlicingStrategy.Adaptive => true,
            SlicingStrategy.Recursive => true,
            _ => false
        };
    }

    /// <summary>
    /// 获取所有支持的切片策略列表
    /// </summary>
    public IEnumerable<SlicingStrategy> SupportedStrategies => new[]
    {
        SlicingStrategy.Grid,
        SlicingStrategy.Octree,
        SlicingStrategy.KdTree,
        SlicingStrategy.Adaptive,
        SlicingStrategy.Recursive
    };

    /// <summary>
    /// 创建通用ILogger实例（用于非泛型ILogger<T>的策略）
    /// </summary>
    private ILogger CreateGenericLogger<T>() where T : class
    {
        // 这里使用工厂logger的同一个实例作为通用logger
        // 实际使用中，如果需要更精确的日志分类，可以通过ILoggerFactory创建
        return _logger as ILogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    /// <summary>
    /// 创建类型化ILogger<T>实例
    /// </summary>
    private ILogger<T> CreateTypedLogger<T>()
    {
        // 如果原logger恰好是ILogger<T>类型，尝试转换
        // 否则返回NullLogger
        if (_logger is ILogger<T> typedLogger)
            return typedLogger;

        return Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance;
    }
}
