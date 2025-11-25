using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Application.Services.Generators;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Application.Services;

/// <summary>
/// 从OBJ到3D Tiles的端到端转换流程
/// 实现完整的工作流程：OBJ加载 -> LOD生成 -> B3DM -> Tileset.json
/// </summary>
public class Obj2TilesService
{
    private readonly ILogger<Obj2TilesService> _logger;
    private readonly IModelLoaderFactory _modelLoaderFactory;
    private readonly ITileGeneratorFactory _tileGeneratorFactory;
    private readonly MeshDecimationService _meshDecimationService;
    private readonly ISpatialSplitterService _spatialSplitterService; // 新增

    public Obj2TilesService(
        ILogger<Obj2TilesService> logger,
        IModelLoaderFactory modelLoaderFactory,
        ITileGeneratorFactory tileGeneratorFactory,
        MeshDecimationService meshDecimationService,
        ISpatialSplitterService spatialSplitterService) // 新增
    {
        _logger = logger;
        _modelLoaderFactory = modelLoaderFactory;
        _tileGeneratorFactory = tileGeneratorFactory;
        _meshDecimationService = meshDecimationService;
        _spatialSplitterService = spatialSplitterService; // 新增
    }

    /// <summary>
    /// 将OBJ模型转换为带LOD的3D Tiles
    /// </summary>
    /// <param name="objFilePath">输入OBJ文件路径</param>
    /// <param name="outputDirectory">切片输出目录</param>
    /// <param name="lodLevels">要生成的LOD级别数量</param>
    /// <param name="atlasOptions">纹理图集配置选项（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ConversionResult> ConvertObjTo3DTilesAsync(
        string objFilePath,
        string outputDirectory,
        int lodLevels = 3,
        TextureAtlasOptions? atlasOptions = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("开始OBJ到3D Tiles转换: {Path}", objFilePath);

        // 使用默认配置（如果未提供）
        atlasOptions ??= new TextureAtlasOptions();

        try
        {
            // 步骤 1: 加载OBJ模型
            _logger.LogInformation("步骤 1/6: 加载OBJ模型...");
            var modelLoader = _modelLoaderFactory.CreateLoaderFromPath(objFilePath);
            var (triangles, modelBounds, materials) = await modelLoader.LoadModelAsync(objFilePath, cancellationToken);
            _logger.LogInformation("已加载 {Count} 个三角形, 包围盒: [{MinX:F2},{MinY:F2},{MinZ:F2}]-[{MaxX:F2},{MaxY:F2},{MaxZ:F2}]",
                triangles.Count, modelBounds.MinX, modelBounds.MinY, modelBounds.MinZ,
                modelBounds.MaxX, modelBounds.MaxY, modelBounds.MaxZ);

            // 步骤 2: 生成纹理图集（如果有材质且启用）
            TextureAtlasGenerator.AtlasResult? atlasResult = null;
            if (materials != null && materials.Count > 0 && atlasOptions.Enabled)
            {
                _logger.LogInformation("步骤 2/6: 生成纹理图集 ({MaterialCount} 个材质, 最大尺寸: {MaxSize}x{MaxSize}, 填充: {Padding}px)...",
                    materials.Count, atlasOptions.MaxAtlasSize, atlasOptions.MaxAtlasSize, atlasOptions.Padding);

                // 从工厂创建GLTF生成器
                var gltfGenerator = _tileGeneratorFactory.CreateGltfGenerator();

                // 生成纹理图集并更新材质的UV映射
                var atlasOutputPath = Path.Combine(outputDirectory, atlasOptions.AtlasFileName);
                (materials, atlasResult) = await gltfGenerator.GenerateTextureAtlasAsync(materials, atlasOutputPath);

                if (atlasResult != null)
                {
                    _logger.LogInformation("纹理图集生成成功: {Width}x{Height}, {TextureCount} 张纹理, 大小: {Size} KB",
                        atlasResult.Width, atlasResult.Height, atlasResult.TextureRegions.Count, atlasResult.ImageData.Length / 1024);

                    if (atlasOptions.VerboseLogging)
                    {
                        _logger.LogDebug("纹理图集详细信息:");
                        foreach (var (texturePath, region) in atlasResult.TextureRegions)
                        {
                            _logger.LogDebug("  {Texture}: UV=[{MinU:F4},{MinV:F4}]-[{MaxU:F4},{MaxV:F4}]",
                                Path.GetFileName(texturePath),
                                region.UVMin.U, region.UVMin.V,
                                region.UVMax.U, region.UVMax.V);
                        }
                    }
                }
            }
            else if (materials == null || materials.Count == 0)
            {
                _logger.LogInformation("步骤 2/6: 跳过纹理图集生成（无材质数据）");
            }
            else
            {
                _logger.LogInformation("步骤 2/6: 跳过纹理图集生成（已禁用）");
            }

            // 步骤 3: 生成LOD级别
            _logger.LogInformation("步骤 3/6: 生成 {Levels} 个LOD级别...", lodLevels);
            var lodDecimatedMeshes = _meshDecimationService.GenerateLODs(triangles, lodLevels);

            // 用于存储所有LOD级别和分割后的子网格生成的Slice
            var allGeneratedSlices = new List<Slice>();
            var allLODInfos = new List<LODInfo>();

            // 步骤 4: 对每个LOD级别进行空间分割，并为每个子网格生成B3DM文件
            _logger.LogInformation("步骤 4/6: 对每个LOD级别进行空间分割并并行生成B3DM文件...");

            // 默认最大分割深度和最小三角形数量 (可配置)
            const int maxSplitDepth = 4; // 例如：四叉树/八叉树深度
            const int minTrianglesPerSplit = 100; // 每个子网格至少包含的三角形数量

            // 针对每个LOD级别进行处理
            foreach (var lodDecimatedMesh in lodDecimatedMeshes)
            {
                var currentLODLevel = lodDecimatedMeshes.IndexOf(lodDecimatedMesh);
                _logger.LogInformation("  LOD级别 {Level}: 开始空间分割 {Count} 个三角形...",
                    currentLODLevel, lodDecimatedMesh.SimplifiedTriangleCount);

                // 执行空间分割
                // 确保将更新后的材质（包含图集UV）传递给分割服务
                var splitMeshes = await _spatialSplitterService.SplitMeshRecursivelyAsync(
                    lodDecimatedMesh.Triangles,
                    materials ?? new Dictionary<string, Material>(), // 传递更新后的材质或空字典
                    maxSplitDepth,
                    minTrianglesPerSplit);

                _logger.LogInformation("  LOD级别 {Level}: 分割成 {Count} 个子网格...",
                    currentLODLevel, splitMeshes.Count);

                // 为每个分割后的子网格生成B3DM
                var b3dmGenerator = _tileGeneratorFactory.CreateB3dmGenerator();
                var splitSliceGenerationTasks = splitMeshes.Select(async (splitMesh) =>
                {
                    // 生成子网格的唯一路径
                    var tileOutputDirectory = Path.Combine(outputDirectory, $"LOD{currentLODLevel}");
                    Directory.CreateDirectory(tileOutputDirectory); // 确保目录存在
                    var outputPath = Path.Combine(tileOutputDirectory, $"{splitMesh.Id}.b3dm");

                    // 保存B3DM文件
                    await b3dmGenerator.SaveB3DMFileAsync(splitMesh.Triangles, splitMesh.BoundingBox, outputPath, splitMesh.Materials);

                    // 创建切片记录
                    var slice = new Slice
                    {
                        Level = currentLODLevel,
                        X = splitMesh.Coordinates.X, // 使用空间分割后的X坐标
                        Y = splitMesh.Coordinates.Y, // 使用空间分割后的Y坐标
                        Z = splitMesh.Coordinates.Z, // 使用空间分割后的Z坐标
                        FilePath = outputPath,
                        BoundingBox = SerializeBoundingBox(splitMesh.BoundingBox),
                        FileSize = new FileInfo(outputPath).Length,
                        CreatedAt = DateTime.UtcNow,
                    };

                    _logger.LogInformation("    已生成LOD {Level} 子网格 {TileId}: {Triangles} 个三角形, 文件大小: {Size} 字节",
                        currentLODLevel, splitMesh.Id, splitMesh.Triangles.Count, slice.FileSize);

                    return slice;
                }).ToList(); // .ToList() 强制立即创建所有任务

                allGeneratedSlices.AddRange(await Task.WhenAll(splitSliceGenerationTasks));

                allLODInfos.Add(new LODInfo
                {
                    Level = currentLODLevel,
                    TriangleCount = lodDecimatedMesh.SimplifiedTriangleCount,
                    ReductionRatio = lodDecimatedMesh.ReductionRatio
                });
            }

            _logger.LogInformation("所有LOD级别和子网格的B3DM文件生成完成（总计 {Count} 个B3DM文件）", allGeneratedSlices.Count);

            // 步骤 5: 生成tileset.json
            _logger.LogInformation("步骤 5/6: 生成tileset.json...");
            var config = new SlicingConfig
            {
                Divisions = lodLevels - 1,
                OutputFormat = "b3dm"
            };

            // 从工厂创建Tileset生成器
            var tilesetGenerator = _tileGeneratorFactory.CreateTilesetGenerator();

            var tilesetPath = Path.Combine(outputDirectory, "tileset.json");
            // Tileset生成器现在需要接收所有生成的子网格slice
            await tilesetGenerator.GenerateTilesetJsonAsync(allGeneratedSlices, config, modelBounds, tilesetPath);

            // 步骤 6: 汇总
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("步骤 6/6: 转换成功完成！");

            var result = new ConversionResult
            {
                Success = true,
                InputFile = objFilePath,
                OutputDirectory = outputDirectory,
                OriginalTriangleCount = triangles.Count,
                LODLevels = allLODInfos, // 使用收集到的LOD信息
                TilesetPath = tilesetPath,
                TotalFiles = allGeneratedSlices.Count + 1, // B3DM文件数 + tileset.json
                TotalSize = allGeneratedSlices.Sum(s => s.FileSize),
                ProcessingTime = elapsed
            };

            _logger.LogInformation(
                "转换汇总:\n" +
                "  原始三角形数: {Original:N0}\n" +
                "  LOD级别数: {Levels}\n" +
                "  总文件数: {Files}\n" +
                "  总文件大小: {Size:N0} 字节\n" +
                (atlasResult != null ? "  纹理图集: {AtlasWidth}x{AtlasHeight}, {TextureCount} 张纹理\n" : "") +
                "  处理耗时: {Time:F2}秒",
                result.OriginalTriangleCount,
                result.LODLevels.Count,
                result.TotalFiles,
                result.TotalSize,
                atlasResult?.Width ?? 0,
                atlasResult?.Height ?? 0,
                atlasResult?.TextureRegions.Count ?? 0,
                result.ProcessingTime.TotalSeconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "转换失败: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 序列化包围盒为JSON字符串
    /// </summary>
    private string SerializeBoundingBox(BoundingBox3D bounds)
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            bounds.MinX,
            bounds.MinY,
            bounds.MinZ,
            bounds.MaxX,
            bounds.MaxY,
            bounds.MaxZ
        });
    }
}

/// <summary>
/// 纹理图集生成配置选项
/// 控制纹理图集的生成行为和参数
/// </summary>
public class TextureAtlasOptions
{
    /// <summary>
    /// 是否启用纹理图集生成
    /// 默认值：true（当有材质时自动生成）
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 图集最大尺寸（宽度和高度）
    /// 默认值：4096（支持大多数现代GPU）
    /// 范围：512-8192
    /// </summary>
    public int MaxAtlasSize { get; set; } = 4096;

    /// <summary>
    /// 纹理之间的填充间距（像素）
    /// 用于防止纹理渗色（texture bleeding）
    /// 默认值：2像素
    /// </summary>
    public int Padding { get; set; } = 2;

    /// <summary>
    /// 图集输出文件名
    /// 默认值：atlas.jpg（使用JPEG压缩以减小文件大小）
    /// 支持格式：.png, .jpg, .bmp
    /// </summary>
    public string AtlasFileName { get; set; } = "atlas.jpg";

    /// <summary>
    /// 是否在日志中显示详细的图集信息
    /// 默认值：false
    /// </summary>
    public bool VerboseLogging { get; set; } = false;
}

/// <summary>
/// OBJ到3D Tiles转换结果
/// </summary>
public class ConversionResult
{
    /// <summary>转换是否成功</summary>
    public bool Success { get; set; }

    /// <summary>输入文件路径</summary>
    public required string InputFile { get; set; }

    /// <summary>输出目录路径</summary>
    public required string OutputDirectory { get; set; }

    /// <summary>原始三角形数量</summary>
    public int OriginalTriangleCount { get; set; }

    /// <summary>LOD级别信息列表</summary>
    public required List<LODInfo> LODLevels { get; set; }

    /// <summary>Tileset文件路径</summary>
    public required string TilesetPath { get; set; }

    /// <summary>总文件数</summary>
    public int TotalFiles { get; set; }

    /// <summary>总文件大小(字节)</summary>
    public long TotalSize { get; set; }

    /// <summary>处理耗时</summary>
    public TimeSpan ProcessingTime { get; set; }
}

/// <summary>
/// LOD级别信息
/// </summary>
public class LODInfo
{
    /// <summary>LOD级别</summary>
    public int Level { get; set; }

    /// <summary>三角形数量</summary>
    public int TriangleCount { get; set; }

    /// <summary>简化比例</summary>
    public double ReductionRatio { get; set; }
}
