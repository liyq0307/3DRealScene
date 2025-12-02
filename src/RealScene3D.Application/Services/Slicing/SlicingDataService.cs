using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Application.Services.Generators;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Geometry;
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
    public async Task<(MeshT Mesh, Box3 BoundingBox)>
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
                    var segments = modelPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 2)
                    {
                        var bucket = segments[0];
                        var objectName = string.Join("/", segments.Skip(1));
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
    private async Task<(MeshT Mesh, Box3 BoundingBox)>
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
    private async Task<(MeshT Mesh, Box3 BoundingBox)>
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
    /// </summary>
    public async Task<bool> GenerateSliceFileAsync(
        Slice slice,
        SlicingConfig config,
        MeshT mesh,
        CancellationToken cancellationToken)
    {
        // 如果没有面数据，跳过生成
        if (mesh == null || mesh.Faces.Count == 0)
        {
            _logger.LogDebug("切片({Level},{X},{Y},{Z})没有找到相交面，跳过生成",
                slice.Level, slice.X, slice.Y, slice.Z);
            return false;
        }

        // ⭐ 纹理处理 - 根据配置的纹理策略处理网格材质和纹理
        var processedMesh = await ProcessTexturesAsync(mesh, slice, config, cancellationToken);

        // 根据输出格式生成文件内容
        byte[]? fileContent = config.OutputFormat.ToLower() switch
        {
            "b3dm" => await GenerateB3DMAsync(slice, processedMesh),
            "gltf" => await GenerateGLTFAsync(slice, processedMesh),
            "i3dm" => await GenerateI3DMAsync(slice, processedMesh),
            "pnts" => await GeneratePNTSAsync(slice, processedMesh),
            "cmpt" => await GenerateCmptAsync(slice, processedMesh),
            _ => await GenerateB3DMAsync(slice, processedMesh)
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

        return true;
    }


    /// <summary>
    /// 生成B3DM格式切片内容
    /// </summary>
    private async Task<byte[]> GenerateB3DMAsync(Slice slice, MeshT mesh)
    {
        var generator = _tileGeneratorFactory.CreateB3dmGenerator();
        _logger.LogDebug("生成B3DM：切片{SliceId}, 面数={Count}, 材质数={MaterialCount}",
            slice.Id, mesh.Faces.Count, mesh.Materials.Count);

        var b3dmData = generator.GenerateTile(mesh);

        _logger.LogDebug("B3DM文件生成完成：切片{SliceId}, 大小{Size}字节", slice.Id, b3dmData.Length);

        return await Task.FromResult(b3dmData);
    }

    /// <summary>
    /// 生成GLTF格式切片内容
    /// </summary>
    private async Task<byte[]> GenerateGLTFAsync(Slice slice, MeshT mesh)
    {
        var generator = _tileGeneratorFactory.CreateGltfGenerator();
        _logger.LogDebug("生成GLB：切片{SliceId}, 面数={Count}, 材质数={MaterialCount}",
            slice.Id, mesh.Faces.Count, mesh.Materials.Count);

        var glbData = generator.GenerateTile(mesh);

        _logger.LogDebug("GLB文件生成完成：切片{SliceId}, 大小{Size}字节", slice.Id, glbData.Length);

        return await Task.FromResult(glbData);
    }

    /// <summary>
    /// 生成I3DM格式切片内容
    /// </summary>
    private async Task<byte[]> GenerateI3DMAsync(Slice slice, MeshT mesh)
    {
        var generator = _tileGeneratorFactory.CreateI3dmGenerator();
        _logger.LogDebug("生成I3DM：切片{SliceId}, 面数={Count}, 材质数={MaterialCount}",
            slice.Id, mesh.Faces.Count, mesh.Materials.Count);

        var i3dmData = generator.GenerateTile(mesh);

        _logger.LogDebug("I3DM文件生成完成：切片{SliceId}, 大小{Size}字节", slice.Id, i3dmData.Length);

        return await Task.FromResult(i3dmData);
    }

    /// <summary>
    /// 生成PNTS格式切片内容
    /// </summary>
    private async Task<byte[]> GeneratePNTSAsync(Slice slice, MeshT mesh)
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
    private async Task<byte[]> GenerateCmptAsync(Slice slice, MeshT mesh)
    {
        var generator = _tileGeneratorFactory.CreateCmptGenerator();
        _logger.LogDebug("生成CMPT：切片{SliceId}, 面数={Count}", slice.Id, mesh.Faces.Count);

        var cmptData = generator.GenerateTile(mesh);

        _logger.LogDebug("CMPT文件生成完成：切片{SliceId}, 大小{Size}字节", slice.Id, cmptData.Length);

        return await Task.FromResult(cmptData);
    }

    /// <summary>
    /// 纹理处理 - 根据配置的纹理策略处理网格材质和纹理
    /// 策略：
    /// - KeepOriginal：保持原始纹理不变
    /// - Repack：重新打包纹理，只包含切片使用的纹理区域
    /// - RepackCompressed：重新打包并压缩纹理（JPEG质量75）
    ///
    /// 实现：通过调用 MeshT.WriteObj() 触发内置的纹理打包逻辑
    /// </summary>
    private async Task<MeshT> ProcessTexturesAsync(
        MeshT mesh,
        Slice slice,
        SlicingConfig config,
        CancellationToken cancellationToken)
    {
        // KeepOriginal 策略：直接返回原始网格
        if (config.TextureStrategy == TextureStrategy.KeepOriginal)
        {
            _logger.LogDebug("切片({Level},{X},{Y},{Z})使用 KeepOriginal 策略，跳过纹理处理",
                slice.Level, slice.X, slice.Y, slice.Z);
            return mesh;
        }

        // 检查是否有材质和纹理需要处理
        if (mesh.Materials.Count == 0 || mesh.TextureVertices.Count == 0)
        {
            _logger.LogDebug("切片({Level},{X},{Y},{Z})没有材质或纹理坐标，跳过纹理处理",
                slice.Level, slice.X, slice.Y, slice.Z);
            return mesh;
        }

        // 检查是否有实际的纹理文件
        bool hasTextures = mesh.Materials.Any(m =>
            !string.IsNullOrEmpty(m.Texture) && File.Exists(m.Texture));

        if (!hasTextures)
        {
            _logger.LogDebug("切片({Level},{X},{Y},{Z})没有有效的纹理文件，跳过纹理处理",
                slice.Level, slice.X, slice.Y, slice.Z);
            return mesh;
        }

        try
        {
            // 设置网格的纹理处理策略
            // MeshT.WriteObj() 会根据此策略自动调用 TrimTextures()
            mesh.TexturesStrategy = config.TextureStrategy switch
            {
                TextureStrategy.Repack => TexturesStrategy.Repack,
                TextureStrategy.RepackCompressed => TexturesStrategy.RepackCompressed,
                TextureStrategy.KeepOriginal => TexturesStrategy.KeepOriginal,
                _ => TexturesStrategy.Repack
            };

            // 创建临时目录用于保存打包后的纹理
            var tempFolder = Path.Combine(Path.GetTempPath(), "RealScene3D_TexturePacking", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);

            try
            {
                // 调用 WriteObj 触发纹理打包
                // 这会调用 MeshT 内部的 TrimTextures() 方法
                // 生成优化的纹理图集并更新 UV 坐标
                var tempObjPath = Path.Combine(tempFolder, "temp.obj");
                mesh.WriteObj(tempObjPath, removeUnused: false);

                // 重要：将打包后的纹理加载到内存中（而不是保存文件路径）
                // TrimTextures() 将纹理保存到 tempFolder，文件名已更新到 Material.Texture
                foreach (var material in mesh.Materials)
                {
                    if (!string.IsNullOrEmpty(material.Texture))
                    {
                        // 构建完整路径
                        var texturePath = Path.IsPathRooted(material.Texture)
                            ? material.Texture
                            : Path.Combine(tempFolder, material.Texture);

                        if (File.Exists(texturePath))
                        {
                            try
                            {
                                // 加载到内存
                                material.TextureImage = await SixLabors.ImageSharp.Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Rgba32>(texturePath);
                                _logger.LogDebug("材质 {MaterialName} 纹理已加载到内存：{W}x{H}",
                                    material.Name, material.TextureImage.Width, material.TextureImage.Height);

                                // 清空文件路径，强制使用内存数据
                                material.Texture = null;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "加载纹理到内存失败：{Path}", texturePath);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(material.NormalMap))
                    {
                        var normalMapPath = Path.IsPathRooted(material.NormalMap)
                            ? material.NormalMap
                            : Path.Combine(tempFolder, material.NormalMap);

                        if (File.Exists(normalMapPath))
                        {
                            try
                            {
                                material.NormalMapImage = await SixLabors.ImageSharp.Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Rgba32>(normalMapPath);
                                _logger.LogDebug("材质 {MaterialName} 法线贴图已加载到内存：{W}x{H}",
                                    material.Name, material.NormalMapImage.Width, material.NormalMapImage.Height);

                                material.NormalMap = null;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "加载法线贴图到内存失败：{Path}", normalMapPath);
                            }
                        }
                    }
                }

                _logger.LogInformation("切片({Level},{X},{Y},{Z})纹理打包完成：策略={Strategy}, 材质数={MaterialCount}",
                    slice.Level, slice.X, slice.Y, slice.Z, config.TextureStrategy, mesh.Materials.Count);

                // 删除整个临时目录（包括 OBJ/MTL/纹理文件）
                try
                {
                    Directory.Delete(tempFolder, recursive: true);
                    _logger.LogDebug("临时纹理目录已清理：{TempFolder}", tempFolder);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "清理临时目录失败：{TempFolder}", tempFolder);
                }

                // 返回处理后的网格
                return await Task.FromResult(mesh);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "切片({Level},{X},{Y},{Z})纹理打包失败，使用原始材质",
                    slice.Level, slice.X, slice.Y, slice.Z);

                // 清理临时目录
                try
                {
                    if (Directory.Exists(tempFolder))
                    {
                        Directory.Delete(tempFolder, recursive: true);
                    }
                }
                catch { }
            }

            return await Task.FromResult(mesh);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "切片({Level},{X},{Y},{Z})纹理处理失败，使用原始材质",
                slice.Level, slice.X, slice.Y, slice.Z);
            return mesh;
        }
    }

    #endregion

    #region Tileset 生成

    /// <summary>
    /// 生成 tileset.json 文件
    /// </summary>
    public async Task<bool> GenerateTilesetJsonAsync(
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
