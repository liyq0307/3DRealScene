using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Application.Services.Generators;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.MinIO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// 切片数据服务 - 负责模型加载、切片文件生成等数据处理工作
///
/// 职责范围：
/// 1. 模型数据加载：从各种来源（本地、MinIO）加载模型，提取三角形数据
/// 2. 切片文件生成：生成各种格式的切片文件（B3DM、GLTF、JSON等）
/// 3. 文件存储：将生成的切片文件保存到本地或MinIO
/// 4. 数据转换：提供各种数据格式之间的转换功能
/// 5. 辅助功能：如纹理处理、元数据计算等
/// </summary>
public class SlicingDataService
{
    private readonly ITileGeneratorFactory _tileGeneratorFactory;
    private readonly IModelLoaderFactory _modelLoaderFactory;
    private readonly IMinioStorageService _minioService;
    private readonly ILogger<SlicingDataService> _logger;

    public SlicingDataService(
        ITileGeneratorFactory tileGeneratorFactory,
        IModelLoaderFactory modelLoaderFactory,
        IMinioStorageService minioService,
        ILogger<SlicingDataService> logger)
    {
        _tileGeneratorFactory = tileGeneratorFactory ?? throw new ArgumentNullException(nameof(tileGeneratorFactory));
        _modelLoaderFactory = modelLoaderFactory ?? throw new ArgumentNullException(nameof(modelLoaderFactory));
        _minioService = minioService ?? throw new ArgumentNullException(nameof(minioService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region 模型加载

    /// <summary>
    /// 从源模型文件加载三角形数据
    /// 支持本地文件和MinIO对象存储
    /// </summary>
    /// <param name="modelPath">模型文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>三角形列表、模型包围盒和材质字典</returns>
    public async Task<(List<Triangle> Triangles, BoundingBox3D BoundingBox, Dictionary<string, Material> Materials)> 
    LoadTrianglesFromModelAsync(string modelPath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始加载模型三角形：{ModelPath}", modelPath);

        try
        {
            // 如果是本地路径且文件存在，直接使用文件路径加载（无需创建临时文件）
            if (IsLocalFilePath(modelPath) && File.Exists(modelPath))
            {
                _logger.LogDebug("从本地文件系统直接加载：{LocalPath}", modelPath);
                return await LoadTrianglesFromLocalFileAsync(modelPath, cancellationToken);
            }
            else
            {
                // 从MinIO加载到临时文件
                try
                {
                    var segments = modelPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 2)
                    {
                        var bucket = segments[0];
                        var objectName = string.Join("/", segments.Skip(1));
                        _logger.LogDebug("从MinIO加载：bucket={Bucket}, object={ObjectName}", bucket, objectName);

                        return await LoadTrianglesFromMinIOAsync(bucket, objectName, modelPath, cancellationToken);
                    }
                }
                catch (Exception ex)
                {

                    // 如果MinIO加载失败，再尝试本地路径作为备用
                    _logger.LogWarning(ex, "从MinIO加载模型失败：{ModelPath}", modelPath);
                    if (File.Exists(modelPath))
                    {
                        _logger.LogDebug("MinIO加载失败，尝试本地文件系统：{LocalPath}", modelPath);
                        return await LoadTrianglesFromLocalFileAsync(modelPath, cancellationToken);
                    }
                }

                // 如果两种方式都失败，记录错误
                _logger.LogWarning("无法从任何数据源加载模型文件：{ModelPath}", modelPath);
                return (new List<Triangle>(), new BoundingBox3D(), new Dictionary<string, Material>());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载模型失败：{ModelPath}", modelPath);
            return (new List<Triangle>(), new BoundingBox3D(), new Dictionary<string, Material>());
        }
    }

    /// <summary>
    /// 从本地文件直接加载三角形数据（无需临时文件）
    /// </summary>
    private async Task<(List<Triangle> Triangles, BoundingBox3D BoundingBox, Dictionary<string, Material> Materials)> 
    LoadTrianglesFromLocalFileAsync(string filePath, CancellationToken cancellationToken)
    {
        // 使用 ModelLoaderFactory 根据文件扩展名创建加载器
        var loader = _modelLoaderFactory.CreateLoaderFromPath(filePath);
        _logger.LogDebug("使用加载器 {LoaderType} 直接加载本地文件", loader.GetType().Name);

        // 直接从本地文件路径加载
        var result = await loader.LoadModelAsync(filePath, cancellationToken);

        _logger.LogInformation("从本地文件加载完成：{TriangleCount} 个三角形", result.Triangles.Count);
        return result;
    }

    /// <summary>
    /// 从MinIO下载到临时文件再加载三角形数据
    /// </summary>
    private async Task<(List<Triangle> Triangles, BoundingBox3D BoundingBox, Dictionary<string, Material> Materials)> 
    LoadTrianglesFromMinIOAsync(
        string bucket,
        string objectName,
        string originalPath,
        CancellationToken cancellationToken)
    {
        // 从MinIO下载文件流
        using var modelStream = await _minioService.DownloadFileAsync(bucket, objectName);

        // 创建临时文件
        var tempFilePath = Path.Combine(
            Path.GetTempPath(),
            $"temp_model_{Guid.NewGuid()}{Path.GetExtension(originalPath)}");

        try
        {
            // 将MinIO流写入临时文件
            _logger.LogDebug("将MinIO文件写入临时文件：{TempPath}", tempFilePath);
            using (var fileStream = File.Create(tempFilePath))
            {
                await modelStream.CopyToAsync(fileStream, cancellationToken);
            }

            // 使用临时文件路径加载模型
            var loader = _modelLoaderFactory.CreateLoaderFromPath(originalPath);
            _logger.LogDebug("使用加载器 {LoaderType} 从临时文件加载", loader.GetType().Name);

            var result = await loader.LoadModelAsync(tempFilePath, cancellationToken);

            _logger.LogInformation("从MinIO加载完成：{TriangleCount} 个三角形", result.Triangles.Count);
            return result;
        }
        finally
        {
            // 清理临时文件
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                    _logger.LogDebug("已清理临时文件：{TempPath}", tempFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "清理临时文件失败：{TempPath}", tempFilePath);
                }
            }
        }
    }

    /// <summary>
    /// 判断是否为本地文件路径
    /// Windows: C:\ or C:/ or \\server\share
    /// Unix: /path/to/file
    /// </summary>
    private bool IsLocalFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return Path.IsPathRooted(path) ||
               path.StartsWith("\\\\") ||
               (path.Length >= 2 && path[1] == ':');
    }

    #endregion

    #region 切片文件生成

    /// <summary>
    /// 生成切片文件 - 多格式切片文件生成算法
    /// 支持纹理重打包策略
    /// </summary>
    public async Task<bool> GenerateSliceFileAsync(
        Slice slice,
        SlicingConfig config,
        List<Triangle> triangles,
        Dictionary<string, Material>? materials,
        CancellationToken cancellationToken)
    {
        // 如果没有三角形数据，跳过生成
        if (triangles == null || triangles.Count == 0)
        {
            _logger.LogDebug("切片({Level},{X},{Y},{Z})没有找到相交三角形，跳过生成",
                slice.Level, slice.X, slice.Y, slice.Z);
            return false;
        }

        // 根据纹理策略处理材质
        var processedMaterials = await ProcessTextureStrategyAsync(
            triangles, materials, config, slice, cancellationToken);

        // 根据输出格式生成文件内容
        byte[]? fileContent = config.OutputFormat.ToLower() switch
        {
            "b3dm" => await GenerateB3DMAsync(slice, triangles, processedMaterials),
            "gltf" => await GenerateGLTFAsync(slice, triangles, processedMaterials),
            "i3dm" => await GenerateI3DMAsync(slice, triangles, processedMaterials),
            "pnts" => await GeneratePNTSAsync(slice, triangles),
            "cmpt" => await GenerateCmptAsync(slice, triangles),
            _ => await GenerateB3DMAsync(slice, triangles, processedMaterials)
        };

        // 注意：不要对 3D Tiles 格式文件进行 Gzip 压缩
        // 3D Tiles 格式（B3DM、GLTF、I3DM、PNTS、CMPT）已经包含了压缩的几何数据
        // HTTP 服务器应该在传输时处理压缩（Content-Encoding: gzip），而不是文件存储时
        // 如果对文件本身进行 Gzip 压缩，会导致 3DTilesViewer 无法识别文件格式

        // 根据存储位置类型保存文件
        if (config.StorageLocation == StorageLocationType.LocalFileSystem)
        {
            var directory = Path.GetDirectoryName(slice.FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllBytesAsync(slice.FilePath, fileContent, cancellationToken);
            _logger.LogDebug("切片文件已保存到本地：{FilePath}, 大小：{FileSize}",
                slice.FilePath, fileContent.Length);
        }
        else // MinIO
        {
            _logger.LogInformation("准备上传切片到MinIO: bucket=slices, path={FilePath}, size={Size}",
                slice.FilePath, fileContent.Length);

            using (var stream = new MemoryStream(fileContent, false))
            {
                var contentType = GetContentType(config.OutputFormat);
                await _minioService.UploadFileAsync("slices", slice.FilePath, stream, contentType, cancellationToken);
            }
            _logger.LogInformation("切片文件已上传到MinIO：{FilePath}, 大小：{FileSize}",
                slice.FilePath, fileContent.Length);

            fileContent = null; // 释放引用，帮助GC回收
        }

        // 根据纹理策略处理纹理文件
        if (config.TextureStrategy == TextureStrategy.KeepOriginal)
        {
            // 保留原始纹理：复制材质纹理文件到输出目录/MinIO
            await CopyMaterialTexturesToOutputAsync(materials, slice.FilePath, config.StorageLocation, cancellationToken);
        }
        // Repack 和 RepackCompressed 策略的纹理已经嵌入到切片文件中，不需要额外复制

        return true;
    }

    /// <summary>
    /// 根据纹理策略处理材质
    /// Repack: 为切片生成专属纹理图集，重映射UV坐标
    /// KeepOriginal: 保留原始材质不变
    /// RepackCompressed: 生成压缩的纹理图集
    /// </summary>
    private async Task<Dictionary<string, Material>?> ProcessTextureStrategyAsync(
        List<Triangle> triangles,
        Dictionary<string, Material>? materials,
        SlicingConfig config,
        Slice slice,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("ProcessTextureStrategyAsync 被调用：LOD={Level}, 材质数={MaterialCount}, TextureStrategy={Strategy}",
            slice.Level, materials?.Count ?? 0, config.TextureStrategy);

        // 如果没有材质或使用 KeepOriginal 策略，直接返回原始材质
        if (materials == null || materials.Count == 0)
        {
            _logger.LogWarning("切片({Level},{X},{Y},{Z})没有材质数据，跳过纹理处理",
                slice.Level, slice.X, slice.Y, slice.Z);
            return materials;
        }

        // KeepOriginal 策略：跳过纹理处理
        if (config.TextureStrategy == TextureStrategy.KeepOriginal)
        {
            _logger.LogDebug("切片({Level},{X},{Y},{Z})使用 KeepOriginal 策略，跳过纹理处理",
                slice.Level, slice.X, slice.Y, slice.Z);
            return materials;
        }

        try
        {
            // 收集切片中实际使用的材质名称
            var usedMaterialNames = triangles
                .Where(t => !string.IsNullOrEmpty(t.MaterialName))
                .Select(t => t.MaterialName!)
                .Distinct()
                .ToHashSet();

            if (usedMaterialNames.Count == 0)
            {
                _logger.LogDebug("切片({Level},{X},{Y},{Z})没有使用材质，跳过纹理重打包",
                    slice.Level, slice.X, slice.Y, slice.Z);
                return materials;
            }

            // 获取切片使用的材质
            var usedMaterials = materials
                .Where(m => usedMaterialNames.Contains(m.Key))
                .ToDictionary(m => m.Key, m => m.Value);

            // 收集需要重打包的纹理路径
            var texturePaths = new HashSet<string>();
            foreach (var material in usedMaterials.Values)
            {
                foreach (var texture in material.GetAllTextures())
                {
                    if (!string.IsNullOrEmpty(texture.FilePath) && File.Exists(texture.FilePath))
                    {
                        texturePaths.Add(texture.FilePath);
                    }
                }
            }

            if (texturePaths.Count == 0)
            {
                _logger.LogDebug("切片({Level},{X},{Y},{Z})没有有效纹理文件，跳过纹理重打包",
                    slice.Level, slice.X, slice.Y, slice.Z);
                return materials;
            }

            _logger.LogDebug("切片({Level},{X},{Y},{Z})开始纹理重打包：{Count}个纹理",
                slice.Level, slice.X, slice.Y, slice.Z, texturePaths.Count);

            // 计算基于LOD级别的降采样因子
            // LOD-0: 1.0 (原始大小)
            // LOD-1: 0.5 (1/2大小)
            // LOD-2: 0.25 (1/4大小)
            // LOD-N: 1 / (2^N)
            double downsampleFactor = slice.Level > 0 ? 1.0 / Math.Pow(2, slice.Level) : 1.0;

            // 确定是否使用JPEG压缩
            bool useJpegCompression = config.TextureStrategy == TextureStrategy.RepackCompressed;

            _logger.LogDebug("切片({Level},{X},{Y},{Z})纹理策略={Strategy}, 降采样={Factor:F2}, 压缩={Compression}",
                slice.Level, slice.X, slice.Y, slice.Z,
                config.TextureStrategy, downsampleFactor, useJpegCompression ? "JPEG" : "PNG");

            // 使用 GltfGenerator 的纹理图集功能（应用降采样和压缩）
            var gltfGenerator = _tileGeneratorFactory.CreateGltfGenerator();
            var (updatedMaterials, atlasResult) = await gltfGenerator.GenerateTextureAtlasAsync(
                usedMaterials,
                atlasOutputPath: null, // 切片不保存单独的图集文件，直接嵌入
                downsampleFactor: downsampleFactor,
                useJpegCompression: useJpegCompression);

            if (atlasResult != null)
            {
                _logger.LogInformation("切片({Level},{X},{Y},{Z})纹理图集生成完成：{Width}x{Height}, 利用率={Utilization:P2}, 大小={Size}KB, 降采样={Factor:F2}x",
                    slice.Level, slice.X, slice.Y, slice.Z,
                    atlasResult.Width, atlasResult.Height, atlasResult.Utilization,
                    atlasResult.ImageData.Length / 1024, downsampleFactor);

                // 纹理压缩策略已在 TextureAtlasGenerator 中实现（JPEG质量75）
                if (config.TextureStrategy == TextureStrategy.RepackCompressed)
                {
                    _logger.LogDebug("切片({Level},{X},{Y},{Z})使用JPEG压缩策略（质量75）",
                        slice.Level, slice.X, slice.Y, slice.Z);
                }
            }

            return updatedMaterials;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "切片({Level},{X},{Y},{Z})纹理重打包失败，使用原始材质",
                slice.Level, slice.X, slice.Y, slice.Z);
            return materials;
        }
    }


    /// <summary>
    /// 生成B3DM格式切片内容
    /// </summary>
    private async Task<byte[]> GenerateB3DMAsync(Slice slice, List<Triangle>? triangles, Dictionary<string, Material>? materials = null)
    {
        var boundingBox = JsonSerializer.Deserialize<BoundingBox3D>(slice.BoundingBox);
        if (boundingBox == null)
        {
            throw new InvalidOperationException("无法解析切片包围盒");
        }

        var generator = _tileGeneratorFactory.CreateB3dmGenerator();
        _logger.LogDebug("生成B3DM：切片{SliceId}, 三角形数={Count}, 材质数={MaterialCount}",
            slice.Id, triangles?.Count ?? 0, materials?.Count ?? 0);

        var b3dmData = generator.GenerateTile(triangles ?? new List<Triangle>(), boundingBox, materials);

        _logger.LogDebug("B3DM文件生成完成：切片{SliceId}, 大小{Size}字节", slice.Id, b3dmData.Length);

        return await Task.FromResult(b3dmData);
    }

    /// <summary>
    /// 生成GLTF格式切片内容
    /// </summary>
    private async Task<byte[]> GenerateGLTFAsync(Slice slice, List<Triangle>? triangles, Dictionary<string, Material>? materials = null)
    {
        var boundingBox = JsonSerializer.Deserialize<BoundingBox3D>(slice.BoundingBox);
        if (boundingBox == null)
        {
            throw new InvalidOperationException("无法解析切片包围盒");
        }

        var generator = _tileGeneratorFactory.CreateGltfGenerator();
        _logger.LogDebug("生成GLB：切片{SliceId}, 三角形数={Count}, 材质数={MaterialCount}",
            slice.Id, triangles?.Count ?? 0, materials?.Count ?? 0);

        var glbData = generator.GenerateGLB(triangles ?? new List<Triangle>(), boundingBox, materials);

        _logger.LogDebug("GLB文件生成完成：切片{SliceId}, 大小{Size}字节", slice.Id, glbData.Length);

        return await Task.FromResult(glbData);
    }

    /// <summary>
    /// 生成I3DM格式切片内容
    /// </summary>
    private async Task<byte[]> GenerateI3DMAsync(Slice slice, List<Triangle>? triangles, Dictionary<string, Material>? materials = null)
    {
        var boundingBox = JsonSerializer.Deserialize<BoundingBox3D>(slice.BoundingBox);
        if (boundingBox == null)
        {
            throw new InvalidOperationException("无法解析切片包围盒");
        }

        var generator = _tileGeneratorFactory.CreateI3dmGenerator();
        _logger.LogDebug("生成I3DM：切片{SliceId}, 三角形数={Count}, 材质数={MaterialCount}",
            slice.Id, triangles?.Count ?? 0, materials?.Count ?? 0);

        var i3dmData = generator.GenerateTile(triangles ?? new List<Triangle>(), boundingBox, materials);

        _logger.LogDebug("I3DM文件生成完成：切片{SliceId}, 大小{Size}字节", slice.Id, i3dmData.Length);

        return await Task.FromResult(i3dmData);
    }

    /// <summary>
    /// 生成PNTS格式切片内容 
    /// </summary>
    private async Task<byte[]> GeneratePNTSAsync(Slice slice, List<Triangle>? triangles)
    {
        var boundingBox = JsonSerializer.Deserialize<BoundingBox3D>(slice.BoundingBox);
        if (boundingBox == null)
        {
            throw new InvalidOperationException("无法解析切片包围盒");
        }

        var generator = _tileGeneratorFactory.CreatePntsGenerator();
        _logger.LogDebug("生成PNTS：切片{SliceId}, 三角形数={Count}", slice.Id, triangles?.Count ?? 0);

        var pntsData = generator.GenerateTile(triangles ?? new List<Triangle>(), boundingBox, materials: null);

        _logger.LogDebug("PNTS文件生成完成：切片{SliceId}, 大小{Size}字节", slice.Id, pntsData.Length);

        return await Task.FromResult(pntsData);
    }

    /// <summary>
    /// 生成CMPT格式切片内容
    /// </summary>
    private async Task<byte[]> GenerateCmptAsync(Slice slice, List<Triangle>? triangles)
    {
        var boundingBox = JsonSerializer.Deserialize<BoundingBox3D>(slice.BoundingBox);
        if (boundingBox == null)
        {
            throw new InvalidOperationException("无法解析切片包围盒");
        }

        var generator = _tileGeneratorFactory.CreateCmptGenerator();
        _logger.LogDebug("生成CMPT：切片{SliceId}, 三角形数={Count}", slice.Id, triangles?.Count ?? 0);

        var cmptData = generator.GenerateTile(triangles ?? new List<Triangle>(), boundingBox, materials: null);

        _logger.LogDebug("CMPT文件生成完成：切片{SliceId}, 大小{Size}字节", slice.Id, cmptData.Length);

        return await Task.FromResult(cmptData);
    }

    #endregion

    #region Tileset 生成

    /// <summary>
    /// 生成 tileset.json 文件
    /// </summary>
    public async Task<bool> GenerateTilesetJsonAsync(
        List<Slice> taskSlices,
        SlicingConfig config,
        BoundingBox3D modelBounds,
        string outputPath,
        StorageLocationType storageLocation,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("开始生成 tileset.json 文件");

            if (taskSlices == null || taskSlices.Count == 0)
            {
                _logger.LogWarning("无法生成 tileset.json：没有切片数据");
                return false;
            }

            // 确定输出路径
            string outputDirectory;
            if (storageLocation == StorageLocationType.LocalFileSystem)
            {
                outputDirectory = outputPath ?? Directory.GetCurrentDirectory();
            }
            else
            {
                // MinIO 存储，使用临时目录
                outputDirectory = Path.Combine(Path.GetTempPath(), $"tileset_{Guid.NewGuid()}");
                Directory.CreateDirectory(outputDirectory);
            }

            // 生成 tileset.json - 使用工厂模式获取TilesetGenerator
            var tilesetPath = Path.Combine(outputDirectory, "tileset.json");
            var tilesetGenerator = _tileGeneratorFactory.CreateTilesetGenerator();
            await tilesetGenerator.GenerateTilesetJsonAsync(taskSlices, config, modelBounds, tilesetPath);

            // 如果是 MinIO 存储，上传 tileset.json
            if (storageLocation == StorageLocationType.MinIO)
            {
                var tilesetContent = await File.ReadAllBytesAsync(tilesetPath, cancellationToken);
                using var tilesetStream = new MemoryStream(tilesetContent);
                await _minioService.UploadFileAsync(
                    "slices", "tileset.json", tilesetStream, "application/json", cancellationToken);

                // 清理临时文件
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, true);
                }
            }

            _logger.LogInformation(
                "tileset.json 生成成功，包含{SliceCount}个切片, LOD级别{MaxLevel}", taskSlices.Count, config.Divisions);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成 tileset.json 失败");
            return false;
        }
    }

    #endregion

    #region 纹理文件处理

    /// <summary>
    /// 复制或上传材质纹理文件到输出目录/MinIO
    /// 确保外部纹理引用能够正确解析
    /// </summary>
    private async Task CopyMaterialTexturesToOutputAsync(
        Dictionary<string, Material>? materials,
        string sliceFilePath,
        StorageLocationType storageLocation,
        CancellationToken cancellationToken)
    {
        if (materials == null || materials.Count == 0)
        {
            return;
        }

        // 收集所有纹理文件路径
        var textureFiles = new HashSet<string>();
        foreach (var material in materials.Values)
        {
            foreach (var texture in material.GetAllTextures())
            {
                if (!string.IsNullOrEmpty(texture.FilePath) && File.Exists(texture.FilePath))
                {
                    textureFiles.Add(texture.FilePath);
                }
            }
        }

        if (textureFiles.Count == 0)
        {
            return;
        }

        _logger.LogDebug("开始复制 {Count} 个纹理文件", textureFiles.Count);

        // 根据存储位置处理纹理文件
        if (storageLocation == StorageLocationType.LocalFileSystem)
        {
            var outputDirectory = Path.GetDirectoryName(sliceFilePath);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                return;
            }

            // 复制纹理文件到输出目录
            foreach (var textureFile in textureFiles)
            {
                try
                {
                    var fileName = Path.GetFileName(textureFile);
                    var destinationPath = Path.Combine(outputDirectory, fileName);

                    // 如果目标文件不存在，复制纹理文件
                    if (!File.Exists(destinationPath))
                    {
                        File.Copy(textureFile, destinationPath, overwrite: false);
                        _logger.LogDebug("纹理文件已复制: {FileName} -> {Destination}", fileName, destinationPath);
                    }
                    else
                    {
                        _logger.LogDebug("纹理文件已存在，跳过复制: {FileName}", fileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "复制纹理文件失败: {TextureFile}", textureFile);
                }
            }
        }
        else // MinIO
        {
            // 上传纹理文件到MinIO
            foreach (var textureFile in textureFiles)
            {
                try
                {
                    var fileName = Path.GetFileName(textureFile);
                    var objectName = fileName; // 直接使用文件名作为对象名

                    // TODO: 可以添加MinIO对象存在性检查以避免重复上传
                    // 为了简化，这里直接上传（MinIO会覆盖同名文件）
                    using var fileStream = File.OpenRead(textureFile);
                    var contentType = GetTextureContentType(fileName);
                    await _minioService.UploadFileAsync("slices", objectName, fileStream, contentType, cancellationToken);

                    _logger.LogDebug("纹理文件已上传到MinIO: {FileName}", fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "上传纹理文件到MinIO失败: {TextureFile}", textureFile);
                }
            }
        }
    }

    /// <summary>
    /// 获取纹理文件的Content-Type
    /// </summary>
    private string GetTextureContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".bmp" => "image/bmp",
            ".tga" => "image/tga",
            ".dds" => "image/vnd-ms.dds",
            _ => "application/octet-stream"
        };
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 压缩切片内容 - 使用GZip压缩
    /// </summary>
    private async Task<byte[]> CompressSliceContentAsync(byte[] content, int compressionLevel)
    {
        using (var compressedStream = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(compressedStream, GetCompressionLevel(compressionLevel)))
            {
                await gzipStream.WriteAsync(content, 0, content.Length);
            }
            return compressedStream.ToArray();
        }
    }

    /// <summary>
    /// 计算几何误差
    /// </summary>
    private double CalculateGeometricError(int level, SlicingConfig config)
    {
        var baseError = config.GeometricErrorThreshold;
        var errorFactor = Math.Pow(2.0, config.Divisions - level);
        return baseError * errorFactor;
    }

    /// <summary>
    /// 计算渲染优先级
    /// </summary>
    private int CalculateRenderingPriority(int level, dynamic center)
    {
        var levelPriority = level * 1000;
        var distanceToOrigin = Math.Sqrt(center.x * center.x + center.y * center.y + center.z * center.z);
        var distancePriority = (int)(distanceToOrigin / 10.0);
        return levelPriority + distancePriority;
    }

    /// <summary>
    /// 计算元数据校验和
    /// </summary>
    private string CalculateMetadataChecksum(Slice slice)
    {
        var checksumInput = $"{slice.Id}|{slice.Level}|{slice.X}|{slice.Y}|{slice.Z}|{slice.FileSize}|{slice.BoundingBox}";
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(checksumInput));
            return Convert.ToHexString(hashBytes).ToLower().Substring(0, 16);
        }
    }

    /// <summary>
    /// 获取压缩级别
    /// </summary>
    private CompressionLevel GetCompressionLevel(int level)
    {
        return level switch
        {
            >= 7 => CompressionLevel.SmallestSize,
            >= 4 => CompressionLevel.Optimal,
            _ => CompressionLevel.Fastest
        };
    }

    /// <summary>
    /// 获取Content-Type
    /// </summary>
    private string GetContentType(string outputFormat)
    {
        return outputFormat.ToLower() switch
        {
            "b3dm" => "application/octet-stream",
            "gltf" => "model/gltf-binary",
            "json" => "application/json",
            _ => "application/octet-stream"
        };
    }

    #endregion
}
