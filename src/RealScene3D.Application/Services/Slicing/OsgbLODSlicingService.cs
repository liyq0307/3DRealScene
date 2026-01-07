using Microsoft.Extensions.Logging;
using RealScene3D.Application.Services.Generators;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Geometry;
using RealScene3D.Lib.OSGB.Interop;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// OSGB PagedLOD 分层切片服务
///
/// 核心功能：将 OSGB 的 PagedLOD 层次结构映射到 3DTiles 切片
/// - 使用最新的 OsgbReader SWIG API 实现
/// - 一站式生成切片文件和 tileset.json
/// - 保持 OSGB 原有的 LOD 层级关系
/// </summary>
public class OsgbLODSlicingService
{
    private readonly ILogger<OsgbLODSlicingService> _logger;
    private readonly B3dmGenerator _b3dmGenerator;

    public OsgbLODSlicingService(
        ILogger<OsgbLODSlicingService> logger,
        B3dmGenerator b3dmGenerator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _b3dmGenerator = b3dmGenerator ?? throw new ArgumentNullException(nameof(b3dmGenerator));
    }

    /// <summary>
    /// 为 OSGB PagedLOD 层次生成 3DTiles 切片
    /// 使用最新的 OsgbReader C API 实现
    /// </summary>
    public async Task<List<Slice>> GenerateLODTilesAsync(
        string osgbPath,
        string outputDir,
        SlicingConfig config,
        GpsCoords? gpsCoords = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("========== 开始 OSGB PagedLOD 分层切片 ==========");
        _logger.LogInformation("源文件: {OsgbPath}", osgbPath);
        _logger.LogInformation("输出目录: {OutputDir}", outputDir);

        var slices = new List<Slice>();

        try
        {
            // 准备输出目录
            Directory.CreateDirectory(outputDir);

            // 准备GPS坐标参数
            double offsetX = 0.0;
            double offsetY = 0.0;

            if (gpsCoords != null)
            {
                offsetX = gpsCoords.Longitude;
                offsetY = gpsCoords.Latitude;
                _logger.LogInformation("使用GPS坐标: 经度={Lon}, 纬度={Lat}, 高度={Alt}",
                    gpsCoords.Longitude, gpsCoords.Latitude, gpsCoords.Altitude);
            }

            // 调用原生OsgbReader进行切片生成
            using var reader = new OsgbReaderHelper();

            _logger.LogInformation("调用 OsgbReader::To3dTile 生成切片");

            string? tilesetJson = await Task.Run(() =>
                reader.ConvertTo3dTiles(
                    osgbPath,
                    outputDir,
                    offsetX,
                    offsetY,
                    maxLevel: 0,  // 0表示不限制层级
                    enableTextureCompression: false,
                    enableMeshOptimization: false,
                    enableDracoCompression: false
                ), cancellationToken);

            if (string.IsNullOrEmpty(tilesetJson))
            {
                throw new InvalidOperationException($"OSGB切片生成失败: 未知错误");
            }

            _logger.LogInformation("切片生成完成，tileset.json 已生成");

            // 扫描生成的切片文件并创建Slice记录
            string tilesetPath = Path.Combine(outputDir, "tileset.json");
            if (!File.Exists(tilesetPath))
            {
                // 保存tileset.json
                await File.WriteAllTextAsync(tilesetPath, tilesetJson, System.Text.Encoding.UTF8);
            }

            // 扫描输出目录中的所有.b3dm文件
            var b3dmFiles = Directory.GetFiles(outputDir, "*.b3dm", SearchOption.AllDirectories);
            _logger.LogInformation("扫描到 {Count} 个B3DM切片文件", b3dmFiles.Length);

            foreach (var b3dmFile in b3dmFiles)
            {
                var fileInfo = new FileInfo(b3dmFile);
                var slice = new Slice
                {
                    Id = Guid.NewGuid(),
                    Level = ExtractLevelFromPath(b3dmFile),
                    FilePath = b3dmFile,
                    BoundingBox = "{}",  // 从tileset.json中提取
                    FileSize = fileInfo.Length,
                    CreatedAt = DateTime.UtcNow
                };

                slices.Add(slice);
            }

            _logger.LogInformation("========== OSGB PagedLOD 分层切片完成 ==========");
            _logger.LogInformation("总切片数: {Count}", slices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OSGB PagedLOD 分层切片失败");
            throw;
        }

        return slices;
    }

    /// <summary>
    /// 从文件路径中提取LOD层级
    /// </summary>
    private int ExtractLevelFromPath(string filePath)
    {
        try
        {
            // 假设路径格式类似：outputDir/LOD-0/xxx.b3dm 或 outputDir/xxx_L0.b3dm
            var dirName = Path.GetDirectoryName(filePath);
            var folderName = Path.GetFileName(dirName);

            if (folderName?.StartsWith("LOD-") == true)
            {
                if (int.TryParse(folderName.Substring(4), out int level))
                {
                    return level;
                }
            }

            // 尝试从文件名中提取
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (fileName.Contains("_L") || fileName.Contains("_l"))
            {
                var parts = fileName.Split('_');
                foreach (var part in parts)
                {
                    if ((part.StartsWith("L") || part.StartsWith("l")) &&
                        int.TryParse(part.Substring(1), out int level))
                    {
                        return level;
                    }
                }
            }

            return 0;  // 默认层级
        }
        catch
        {
            return 0;
        }
    }
}
