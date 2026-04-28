using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Enums;
using RealScene3D.Lib.OSGB.Interop;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// OSGB 倾斜摄影数据集批量切片服务
///
/// 功能：调用 C++ ToB3DMBatch 一站式处理完整的倾斜摄影数据集
///
/// 数据结构要求：
/// - 根目录/Data/ （瓦片目录）
/// - 根目录/metadata.xml （坐标系信息，可选）
/// - Data/Tile_xxx/Tile_xxx.osgb （瓦片文件）
///
/// C++ 自动生成：
/// - 根 tileset.json （包含变换矩阵和子瓦片引用）
/// - Data/Tile_xxx/tileset.json （每个瓦片的 tileset）
/// - Data/Tile_xxx/*.b3dm （切片文件）
/// </summary>
public class OsgbTiledDatasetSlicingService
{
    private readonly ILogger<OsgbTiledDatasetSlicingService> _logger;

    public OsgbTiledDatasetSlicingService(
        ILogger<OsgbTiledDatasetSlicingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 处理完整的倾斜摄影数据集
    /// </summary>
    /// <param name="datasetRootPath">数据集根目录（包含 Data 目录和 metadata.xml）</param>
    /// <param name="outputDir">输出目录或MinIO路径（如 "bucket/path/to/tiles"）</param>
    /// <param name="config">切片配置（包含MinIO配置）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理是否成功</returns>
    public async Task<bool> ProcessDatasetAsync(
        string datasetRootPath,
        string outputDir,
        SlicingConfig config,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("========== 开始处理 OSGB 倾斜摄影数据集 ==========");
        _logger.LogInformation("数据集根目录: {RootPath}", datasetRootPath);
        _logger.LogInformation("输出路径: {OutputDir}", outputDir);
        _logger.LogInformation("存储位置: {StorageLocation}", config.StorageLocation);

        // 1. 验证数据集结构
        ValidateDatasetStructure(datasetRootPath);

        // 2. 检查并读取 metadata.xml
        string metadataPath = Path.Combine(datasetRootPath, "metadata.xml");
        if (File.Exists(metadataPath))
        {
            _logger.LogInformation("找到 metadata.xml，读取坐标系信息");
            ReadMetadata(metadataPath, config);
        }
        else
        {
            _logger.LogWarning("未找到 metadata.xml，将使用默认坐标系");
        }

        // 3. 调用 C++ 处理
        using var reader = new OSGB23dTiles.Helper();
        bool success;

        if (config.StorageLocation == StorageLocationType.MinIO)
        {
            // MinIO 模式：直接写入 MinIO
            _logger.LogInformation("调用 OSGB23dTiles.Helper::ConvertToB3DMBatchToMinIO 直接写入MinIO");

            if (string.IsNullOrEmpty(config.MinioEndpoint) ||
                string.IsNullOrEmpty(config.MinioAccessKey) ||
                string.IsNullOrEmpty(config.MinioSecretKey))
            {
                throw new InvalidOperationException("MinIO 配置不完整，请提供 Endpoint、AccessKey 和 SecretKey");
            }

            success = await Task.Run(() =>
                reader.ConvertToB3DMBatchToMinIO(
                    datasetRootPath,
                    outputDir,
                    config.MinioEndpoint,
                    config.MinioAccessKey,
                    config.MinioSecretKey,
                    config.MinioUseSSL,
                    centerX: config.CenterX ?? 0.0,
                    centerY: config.CenterY ?? 0.0,
                    maxLevel: -1, // -1 表示不限制层级，处理所有 LOD
                    enableTextureCompression: config.EnableTextureCompression,
                    enableMeshOptimization: config.EnableMeshOptimization,
                    enableDracoCompression: config.EnableDracoCompression
                ), cancellationToken);
        }
        else
        {
            // 本地文件系统模式
            _logger.LogInformation("调用 OSGB23dTiles.Helper::ConvertToB3DMBatch 写入本地文件系统");

            // 确保输出目录存在
            Directory.CreateDirectory(outputDir);

            success = await Task.Run(() =>
                reader.ConvertToB3DMBatch(
                    datasetRootPath,
                    outputDir,
                    centerX: config.CenterX ?? 0.0,
                    centerY: config.CenterY ?? 0.0,
                    maxLevel: -1, // -1 表示不限制层级，处理所有 LOD
                    enableTextureCompression: config.EnableTextureCompression,
                    enableMeshOptimization: config.EnableMeshOptimization,
                    enableDracoCompression: config.EnableDracoCompression
                ), cancellationToken);
        }

        if (!success)
        {
            _logger.LogError("OSGB 倾斜摄影数据集处理失败");
            return false;
        }

        _logger.LogInformation("========== OSGB 倾斜摄影数据集处理完成 ==========");
        
        if (config.StorageLocation == StorageLocationType.LocalFileSystem)
        {
            _logger.LogInformation("根 tileset.json 已生成: {Path}", Path.Combine(outputDir, "tileset.json"));
        }
        else
        {
            _logger.LogInformation("切片已写入 MinIO: {Path}", outputDir);
        }

        return true;
    }

    /// <summary>
    /// 读取metadata.xml文件，提取坐标系信息
    /// </summary>
    private void ReadMetadata(string metadataPath, SlicingConfig config)
    {
        try
        {
            var doc = new System.Xml.XmlDocument();
            doc.Load(metadataPath);

            // 尝试读取SRS（空间参考系统）
            var srsNode = doc.SelectSingleNode("//SRS") ?? doc.SelectSingleNode("//CoordinateSystem");
            if (srsNode != null && !string.IsNullOrEmpty(srsNode.InnerText))
            {
                config.SpatialReference = srsNode.InnerText;
                _logger.LogInformation("空间参考系统: {SRS}", config.SpatialReference);
            }

            // 尝试读取中心点坐标
            var centerXNode = doc.SelectSingleNode("//CenterX") ?? doc.SelectSingleNode("//X");
            var centerYNode = doc.SelectSingleNode("//CenterY") ?? doc.SelectSingleNode("//Y");
            var centerZNode = doc.SelectSingleNode("//CenterZ") ?? doc.SelectSingleNode("//Z");

            if (centerXNode != null && double.TryParse(centerXNode.InnerText, out double centerX))
            {
                config.CenterX = centerX;
            }

            if (centerYNode != null && double.TryParse(centerYNode.InnerText, out double centerY))
            {
                config.CenterY = centerY;
            }

            if (centerZNode != null && double.TryParse(centerZNode.InnerText, out double centerZ))
            {
                config.CenterZ = centerZ;
            }

            if (config.CenterX.HasValue || config.CenterY.HasValue || config.CenterZ.HasValue)
            {
                _logger.LogInformation("中心点坐标: ({X}, {Y}, {Z})",
                    config.CenterX ?? 0, config.CenterY ?? 0, config.CenterZ ?? 0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取metadata.xml失败，将使用默认坐标");
        }
    }

    /// <summary>
    /// 验证数据集结构
    /// </summary>
    private void ValidateDatasetStructure(string datasetRootPath)
    {
        if (!Directory.Exists(datasetRootPath))
        {
            throw new DirectoryNotFoundException($"数据集根目录不存在: {datasetRootPath}");
        }

        string dataDir = Path.Combine(datasetRootPath, "Data");
        if (!Directory.Exists(dataDir))
        {
            throw new DirectoryNotFoundException(
                $"未找到 Data 目录。倾斜摄影数据必须包含 Data 目录。\n" +
                $"正确的目录结构：\n" +
                $"  {datasetRootPath}/\n" +
                $"    ├ metadata.xml\n" +
                $"    └ Data/\n" +
                $"        └ Tile_xxx/\n" +
                $"            └ Tile_xxx.osgb");
        }
    }
}
