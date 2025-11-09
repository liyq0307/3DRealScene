using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// 组合模型加载器 - 支持多种3D模型格式的统一加载接口
/// 自动根据文件扩展名选择合适的加载器
///
/// 支持的格式：
/// - 通用格式: OBJ, GLB/GLTF, STL, PLY
/// - 专业格式: FBX (游戏/影视), IFC (BIM建筑), OSGB (倾斜摄影)
///
/// 注意：FBX、IFC、OSGB格式需要集成第三方库才能完整实现
/// </summary>
public class CompositeModelLoader : IModelLoader
{
    private readonly ILogger<CompositeModelLoader> _logger;
    private readonly Dictionary<string, IModelLoader> _loaders;
    private readonly HashSet<string> _supportedFormats;

    /// <summary>
    /// 构造函数 - 注入所有可用的模型加载器
    /// </summary>
    public CompositeModelLoader(
        ILogger<CompositeModelLoader> logger,
        ObjModelLoader objLoader,
        GltfModelLoader gltfLoader,
        StlModelLoader stlLoader,
        PlyModelLoader plyLoader,
        FbxModelLoader fbxLoader,
        IfcModelLoader ifcLoader,
        OsgbModelLoader osgbLoader)
    {
        _logger = logger;
        _loaders = new Dictionary<string, IModelLoader>(StringComparer.OrdinalIgnoreCase);
        _supportedFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 注册通用格式加载器
        RegisterLoader(objLoader);
        RegisterLoader(gltfLoader);
        RegisterLoader(stlLoader);
        RegisterLoader(plyLoader);

        // 注册专业格式加载器
        RegisterLoader(fbxLoader);
        RegisterLoader(ifcLoader);
        RegisterLoader(osgbLoader);

        _logger.LogInformation("组合模型加载器初始化完成，支持格式: {Formats}",
            string.Join(", ", _supportedFormats));
    }

    /// <summary>
    /// 注册一个加载器
    /// </summary>
    private void RegisterLoader(IModelLoader loader)
    {
        foreach (var format in loader.GetSupportedFormats())
        {
            _loaders[format] = loader;
            _supportedFormats.Add(format);
            _logger.LogDebug("注册加载器: {Format} -> {LoaderType}", format, loader.GetType().Name);
        }
    }

    /// <summary>
    /// 加载3D模型文件
    /// 自动根据文件扩展名选择合适的加载器
    /// </summary>
    public async Task<(List<Triangle> Triangles, BoundingBox3D BoundingBox, Dictionary<string, Material> Materials)> LoadModelAsync(
        string modelPath,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(modelPath);
        if (string.IsNullOrEmpty(extension))
        {
            throw new ArgumentException($"无法确定文件格式: {modelPath}");
        }

        extension = extension.ToLowerInvariant();

        if (!_loaders.TryGetValue(extension, out var loader))
        {
            throw new NotSupportedException(
                $"不支持的文件格式: {extension}. 支持的格式: {string.Join(", ", _supportedFormats)}");
        }

        _logger.LogInformation("使用 {LoaderType} 加载模型: {Path}",
            loader.GetType().Name, modelPath);

        return await loader.LoadModelAsync(modelPath, cancellationToken);
    }

    /// <summary>
    /// 检查是否支持指定的文件格式
    /// </summary>
    public bool SupportsFormat(string extension)
    {
        return _supportedFormats.Contains(extension.ToLowerInvariant());
    }

    /// <summary>
    /// 获取支持的所有文件格式
    /// </summary>
    public IEnumerable<string> GetSupportedFormats()
    {
        return _supportedFormats;
    }

    /// <summary>
    /// 获取指定格式的加载器
    /// </summary>
    public IModelLoader? GetLoader(string extension)
    {
        _loaders.TryGetValue(extension.ToLowerInvariant(), out var loader);
        return loader;
    }
}
