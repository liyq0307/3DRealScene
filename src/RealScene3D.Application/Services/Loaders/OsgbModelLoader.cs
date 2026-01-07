using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using RealScene3D.Domain.Geometry;
using RealScene3D.Lib.OSGB.Interop;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// OSGB模型加载器 - 使用最新的 RealScene3D.Lib.OSGB SWIG API
///
/// 功能：将OSGB转换为GLB，然后使用GLB加载器加载
/// 依赖：RealScene3D.Lib.OSGB 原生库
/// 优势：简洁的实现，复用现有的GLB加载逻辑
/// </summary>
public class OsgbModelLoader : ModelLoader
{
    private readonly ILogger<OsgbModelLoader> _logger;
    private readonly IServiceProvider _serviceProvider;

    private static readonly string[] SupportedFormats = { ".osgb", ".osg" };

    public OsgbModelLoader(
        ILogger<OsgbModelLoader> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 加载 OSGB 文件：先转换为GLB，然后使用GLB加载器加载
    /// </summary>
    public override async Task<(IMesh Mesh, Box3 BoundingBox)> LoadModelAsync(
        string modelPath,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("开始加载 OSGB 模型: {Path}", modelPath);

        try
        {
            // 验证文件
            ValidateFileExists(modelPath);
            ValidateFileExtension(modelPath);

            // 创建临时GLB文件
            string tempGlbPath = Path.Combine(
                Path.GetTempPath(),
                $"osgb_temp_{Guid.NewGuid():N}.glb"
            );

            try
            {
                // 1. 使用OsgbReader SWIG API转换为GLB
                _logger.LogInformation("转换OSGB到GLB: {TempPath}", tempGlbPath);

                using (var reader = new OsgbReaderHelper())
                {
                    bool success = await Task.Run(() =>
                        reader.ConvertToGlb(
                            modelPath,
                            tempGlbPath,
                            enableTextureCompression: false,
                            enableMeshOptimization: false,
                            enableDracoCompression: false
                        ), cancellationToken);

                    if (!success)
                    {
                        throw new InvalidOperationException($"OSGB转GLB失败: 未知错误");
                    }
                }

                // 2. 使用GltfModelLoader加载GLB
                _logger.LogInformation("从GLB加载模型");
                var gltfLoader = ActivatorUtilities.CreateInstance<GltfModelLoader>(_serviceProvider);
                var result = await gltfLoader.LoadModelAsync(tempGlbPath, cancellationToken);

                var elapsed = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "OSGB 模型加载完成: 类型={MeshType}, 顶点={VertexCount}, 面={FaceCount}, 耗时={Elapsed:F2}秒",
                    result.Mesh.HasTexture ? "MeshT" : "Mesh",
                    result.Mesh.VertexCount,
                    result.Mesh.FacesCount,
                    elapsed.TotalSeconds);

                return result;
            }
            finally
            {
                // 清理临时文件
                if (File.Exists(tempGlbPath))
                {
                    try
                    {
                        File.Delete(tempGlbPath);
                        _logger.LogDebug("临时GLB文件已清理: {Path}", tempGlbPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "清理临时文件失败: {Path}", tempGlbPath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 OSGB 模型失败: {Path}", modelPath);
            throw;
        }
    }

    public override bool SupportsFormat(string extension)
    {
        return SupportedFormats.Contains(extension.ToLowerInvariant());
    }

    public override IEnumerable<string> GetSupportedFormats()
    {
        return SupportedFormats;
    }
}