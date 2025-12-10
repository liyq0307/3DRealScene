using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Text;
using RealScene3D.Application.Interfaces;
using RealScene3D.Application.Services.Generators;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Geometry;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Domain.Materials;
using RealScene3D.Infrastructure.MinIO;


namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// 切片数据服务 - 负责模型加载、切片文件生成等数据处理工作
///
/// 职责范围：
/// 1. 模型数据加载：从各种来源（本地、MinIO）加载模型，直接构建 MeshT
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
    /// 从源模型文件加载模型数据
    /// 支持本地文件和MinIO对象存储
    /// </summary>
    /// <param name="modelPath">模型文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>MeshT 网格和包围盒</returns>
    public async Task<(IMesh Mesh, Box3 BoundingBox)>
    LoadMeshFromModelAsync(string modelPath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始加载模型：{ModelPath}", modelPath);

        try
        {
            // 如果是本地路径且文件存在，直接使用文件路径加载（无需创建临时文件）
            if (IsLocalFilePath(modelPath) && File.Exists(modelPath))
            {
                _logger.LogDebug("从本地文件系统直接加载：{LocalPath}", modelPath);
                return await LoadMeshFromLocalFileAsync(modelPath, cancellationToken);
            }
            else
            {
                // 从MinIO加载到临时文件
                try
                {
                    string? bucket = null;
                    string? objectName = null;

                    var segments = modelPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 2)
                    {
                        bucket = segments[0];
                        objectName = string.Join("/", segments.Skip(1));
                    }
                    else if (!string.IsNullOrEmpty(modelPath))
                    {
                        // 仅包含文件名（如直接上传场景时，modelPath可能只包含文件名，没有bucket前缀）
                        // 默认使用3D模型存储桶
                        bucket = MinioBuckets.MODELS_3D;
                        objectName = modelPath.TrimStart('/').TrimStart('\\');
                    }

                    if (!string.IsNullOrEmpty(bucket) && !string.IsNullOrEmpty(objectName))
                    {
                        _logger.LogDebug("从MinIO加载：bucket={Bucket}, object={ObjectName}", bucket, objectName);

                        return await LoadMeshFromMinIOAsync(bucket, objectName, modelPath, cancellationToken);
                    }
                }
                catch (Exception ex)
                {

                    // 如果MinIO加载失败，再尝试本地路径作为备用
                    _logger.LogWarning(ex, "从MinIO加载模型失败：{ModelPath}", modelPath);
                    if (File.Exists(modelPath))
                    {
                        _logger.LogDebug("MinIO加载失败，尝试本地文件系统：{LocalPath}", modelPath);
                        return await LoadMeshFromLocalFileAsync(modelPath, cancellationToken);
                    }
                }

                // 如果两种方式都失败，返回空 Mesh
                _logger.LogWarning("无法从任何数据源加载模型文件：{ModelPath}", modelPath);
                return (new MeshT([], [], [], []), new Box3(0, 0, 0, 0, 0, 0));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载模型失败：{ModelPath}", modelPath);
            return (new MeshT([], [], [], []), new Box3(0, 0, 0, 0, 0, 0));
        }
    }

    /// <summary>
    /// 从本地文件直接加载网格数据（无需临时文件）
    /// </summary>
    private async Task<(IMesh Mesh, Box3 BoundingBox)>
    LoadMeshFromLocalFileAsync(string filePath, CancellationToken cancellationToken)
    {
        // 使用 ModelLoaderFactory 根据文件扩展名创建加载器
        var loader = _modelLoaderFactory.CreateLoaderFromPath(filePath);
        _logger.LogDebug("使用加载器 {LoaderType} 直接加载本地文件", loader.GetType().Name);

        // 直接从本地文件路径加载
        var result = await loader.LoadModelAsync(filePath, cancellationToken);

        _logger.LogInformation("从本地文件加载完成：{VertexCount} 个顶点, {FaceCount} 个面",
            result.Mesh.Vertices.Count, result.Mesh.Faces.Count);

        return result;
    }

    /// <summary>
    /// 从MinIO下载到临时文件再加载网格数据
    /// </summary>
    private async Task<(IMesh Mesh, Box3 BoundingBox)>
    LoadMeshFromMinIOAsync(
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

            _logger.LogInformation("从MinIO加载完成：{VertexCount} 个顶点, {FaceCount} 个面",
                result.Mesh.Vertices.Count, result.Mesh.Faces.Count);

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
    /// 支持有纹理（MeshT）和无纹理（Mesh）两种情况
    /// </summary>
    public async Task<bool> GenerateTileAsync(
        Slice slice,
        SlicingConfig config,
        IMesh mesh,
        CancellationToken cancellationToken)
    {
        // 如果没有面数据，跳过生成
        if (mesh == null || mesh.FacesCount == 0)
        {
            _logger.LogDebug("切片({Level},{X},{Y},{Z})没有找到相交面，跳过生成",
                slice.Level, slice.X, slice.Y, slice.Z);
            return false;
        }

        // 根据输出格式生成文件内容
        byte[]? fileContent = config.OutputFormat.ToLower() switch
        {
            "b3dm" => await GenerateB3DMAsync(slice, mesh),
            "gltf" => await GenerateGLTFAsync(slice, mesh),
            "i3dm" => await GenerateI3DMAsync(slice, mesh),
            "pnts" => await GeneratePNTSAsync(slice, mesh),
            "cmpt" => await GenerateCmptAsync(slice, mesh),
            _ => await GenerateB3DMAsync(slice, mesh)
        };

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
            // MinIO要求使用正斜杠作为路径分隔符，需要将Windows的反斜杠转换为正斜杠
            var minioPath = slice.FilePath.Replace('\\', '/');

            _logger.LogInformation("准备上传切片到MinIO: bucket=slices, path={FilePath}, size={Size}",
                minioPath, fileContent.Length);

            using (var stream = new MemoryStream(fileContent, false))
            {
                var contentType = GetContentType(config.OutputFormat);
                await _minioService.UploadFileAsync("slices", minioPath, stream, contentType, cancellationToken);
            }

            // 更新切片的FilePath为MinIO兼容的路径（正斜杠），
            // 这样保存到数据库后，后续的下载、删除操作都能直接使用，无需再次转换
            slice.FilePath = minioPath;

            _logger.LogInformation("切片文件已上传到MinIO：{FilePath}, 大小：{FileSize}",
                minioPath, fileContent.Length);

            fileContent = null; // 释放引用，帮助GC回收
        }

        return true;
    }


    /// <summary>
    /// 生成B3DM格式切片内容
    /// </summary>
    private async Task<byte[]> GenerateB3DMAsync(Slice slice, IMesh mesh)
    {
        var generator = _tileGeneratorFactory.CreateB3dmGenerator();
        _logger.LogDebug("生成B3DM：切片{SliceId}, 面数={Count}, 材质数={MaterialCount}",
            slice.Id, mesh.Faces.Count, mesh.Materials?.Count);

        var b3dmData = generator.GenerateTile(mesh);

        _logger.LogDebug("B3DM文件生成完成：切片{SliceId}, 大小{Size}字节", slice.Id, b3dmData.Length);

        return await Task.FromResult(b3dmData);
    }

    /// <summary>
    /// 生成GLTF格式切片内容
    /// </summary>
    private async Task<byte[]> GenerateGLTFAsync(Slice slice, IMesh mesh)
    {
        var generator = _tileGeneratorFactory.CreateGltfGenerator();
        _logger.LogDebug("生成GLB：切片{SliceId}, 面数={Count}, 材质数={MaterialCount}",
            slice.Id, mesh.Faces.Count, mesh.Materials?.Count);

        var glbData = generator.GenerateTile(mesh);

        _logger.LogDebug("GLB文件生成完成：切片{SliceId}, 大小{Size}字节", slice.Id, glbData.Length);

        return await Task.FromResult(glbData);
    }

    /// <summary>
    /// 生成I3DM格式切片内容
    /// </summary>
    private async Task<byte[]> GenerateI3DMAsync(Slice slice, IMesh mesh)
    {
        var generator = _tileGeneratorFactory.CreateI3dmGenerator();
        _logger.LogDebug("生成I3DM：切片{SliceId}, 面数={Count}, 材质数={MaterialCount}",
            slice.Id, mesh.Faces.Count, mesh.Materials?.Count);

        var i3dmData = generator.GenerateTile(mesh);

        _logger.LogDebug("I3DM文件生成完成：切片{SliceId}, 大小{Size}字节", slice.Id, i3dmData.Length);

        return await Task.FromResult(i3dmData);
    }

    /// <summary>
    /// 生成PNTS格式切片内容
    /// </summary>
    private async Task<byte[]> GeneratePNTSAsync(Slice slice, IMesh mesh)
    {
        var generator = _tileGeneratorFactory.CreatePntsGenerator();
        _logger.LogDebug("生成PNTS：切片{SliceId}, 面数={Count}", slice.Id, mesh.Faces.Count);

        var pntsData = generator.GenerateTile(mesh);

        _logger.LogDebug("PNTS文件生成完成：切片{SliceId}, 大小{Size}字节", slice.Id, pntsData.Length);

        return await Task.FromResult(pntsData);
    }

    /// <summary>
    /// 生成CMPT格式切片内容
    /// </summary>
    private async Task<byte[]> GenerateCmptAsync(Slice slice, IMesh mesh)
    {
        var generator = _tileGeneratorFactory.CreateCmptGenerator();
        _logger.LogDebug("生成CMPT：切片{SliceId}, 面数={Count}", slice.Id, mesh.Faces.Count);

        var cmptData = generator.GenerateTile(mesh);

        _logger.LogDebug("CMPT文件生成完成：切片{SliceId}, 大小{Size}字节", slice.Id, cmptData.Length);

        return await Task.FromResult(cmptData);
    }

    #endregion

    #region Tileset 生成

    /// <summary>
    /// 生成 tileset.json 文件
    /// </summary>
    public async Task<bool> GenerateTilesetAsync(
        List<Slice> taskSlices,
        SlicingConfig config,
        Box3 modelBounds,
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
            await tilesetGenerator.GenerateTilesetAsync(taskSlices, config, modelBounds, tilesetPath);

            // 如果是 MinIO 存储，上传 tileset.json
            if (storageLocation == StorageLocationType.MinIO)
            {
                var tilesetContent = await File.ReadAllBytesAsync(tilesetPath, cancellationToken);
                using var tilesetStream = new MemoryStream(tilesetContent);

                // MinIO上传时需要包含完整的路径前缀，并使用正斜杠
                var minioTilesetPath = string.IsNullOrEmpty(outputPath)
                    ? "tileset.json"
                    : $"{outputPath.Replace('\\', '/')}/tileset.json";

                await _minioService.UploadFileAsync(
                    "slices", minioTilesetPath, tilesetStream, "application/json", cancellationToken);

                _logger.LogInformation("tileset.json已上传到MinIO：{Path}", minioTilesetPath);

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
