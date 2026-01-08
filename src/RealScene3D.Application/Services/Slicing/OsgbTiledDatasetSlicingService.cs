using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
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
    /// <param name="outputDir">输出目录</param>
    /// <param name="config">切片配置</param>
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
        _logger.LogInformation("输出目录: {OutputDir}", outputDir);

        // 1. 验证数据集结构
        ValidateDatasetStructure(datasetRootPath);

        // 2. 检查 metadata.xml（C++ 会自动读取）
        string metadataPath = Path.Combine(datasetRootPath, "metadata.xml");
        if (File.Exists(metadataPath))
        {
            _logger.LogInformation("找到 metadata.xml，C++ 将自动读取坐标系信息");
        }
        else
        {
            _logger.LogWarning("未找到 metadata.xml，将使用默认坐标系");
        }

        // 3. 直接调用 C++ ToB3DMBatch 一站式处理
        using var reader = new OSGB23dTilesHelper();

        _logger.LogInformation("调用 OsgbReader::ToB3DMBatch 批量生成 3D Tiles");

        bool success = await Task.Run(() =>
            reader.ConvertToB3DMBatch(
                datasetRootPath,
                outputDir,
                centerX: 0.0,  // C++ 会从 metadata.xml 自动读取
                centerY: 0.0,
                maxLevel: -1,   // - 表示不限制层级，处理所有 LOD
                enableTextureCompression: false,
                enableMeshOptimization: false,
                enableDracoCompression: false
            ), cancellationToken);

        if (!success)
        {
            _logger.LogError("OSGB 倾斜摄影数据集处理失败");
            return false;
        }

        _logger.LogInformation("========== OSGB 倾斜摄影数据集处理完成 ==========");
        _logger.LogInformation("根 tileset.json 已生成: {Path}", Path.Combine(outputDir, "tileset.json"));

        return true;
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
