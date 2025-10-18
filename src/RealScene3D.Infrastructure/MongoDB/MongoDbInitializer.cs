using Microsoft.Extensions.Logging;
using RealScene3D.Infrastructure.MongoDB.Repositories;

namespace RealScene3D.Infrastructure.MongoDB;

/// <summary>
/// MongoDB 数据库初始化器
/// 负责创建索引和初始化集合
/// </summary>
public class MongoDbInitializer
{
    private readonly IVideoMetadataRepository _videoRepository;
    private readonly IBimModelMetadataRepository _bimRepository;
    private readonly ITiltPhotographyMetadataRepository _tiltRepository;
    private readonly ILogger<MongoDbInitializer> _logger;

    public MongoDbInitializer(
        IVideoMetadataRepository videoRepository,
        IBimModelMetadataRepository bimRepository,
        ITiltPhotographyMetadataRepository tiltRepository,
        ILogger<MongoDbInitializer> logger)
    {
        _videoRepository = videoRepository;
        _bimRepository = bimRepository;
        _tiltRepository = tiltRepository;
        _logger = logger;
    }

    /// <summary>
    /// 初始化 MongoDB 数据库(创建所有索引)
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始初始化 MongoDB 索引...");

            // 1. 初始化视频元数据集合索引
            _logger.LogInformation("创建视频元数据索引...");
            await _videoRepository.EnsureIndexesAsync(cancellationToken);
            _logger.LogInformation("✓ 视频元数据索引创建完成");

            // 2. 初始化 BIM 模型元数据集合索引
            _logger.LogInformation("创建 BIM 模型元数据索引...");
            await _bimRepository.EnsureIndexesAsync(cancellationToken);
            _logger.LogInformation("✓ BIM 模型元数据索引创建完成");

            // 3. 初始化倾斜摄影元数据集合索引
            _logger.LogInformation("创建倾斜摄影元数据索引...");
            await _tiltRepository.EnsureIndexesAsync(cancellationToken);
            _logger.LogInformation("✓ 倾斜摄影元数据索引创建完成");

            _logger.LogInformation("✓ MongoDB 数据库初始化完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB 数据库初始化失败: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 验证数据库连接和集合状态
    /// </summary>
    public async Task<bool> ValidateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("验证 MongoDB 数据库状态...");

            // 测试每个集合的连接
            var videoCount = await _videoRepository.CountAsync(cancellationToken: cancellationToken);
            var bimCount = await _bimRepository.CountAsync(cancellationToken: cancellationToken);
            var tiltCount = await _tiltRepository.CountAsync(cancellationToken: cancellationToken);

            _logger.LogInformation("MongoDB 集合状态:");
            _logger.LogInformation("  - 视频元数据: {Count} 条记录", videoCount);
            _logger.LogInformation("  - BIM 模型元数据: {Count} 条记录", bimCount);
            _logger.LogInformation("  - 倾斜摄影元数据: {Count} 条记录", tiltCount);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB 数据库验证失败: {Message}", ex.Message);
            return false;
        }
    }
}
