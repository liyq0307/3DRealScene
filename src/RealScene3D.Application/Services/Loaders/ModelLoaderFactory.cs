using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Enums;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// 模型加载器工厂 - 基于枚举的抽象工厂模式实现
/// 采用枚举驱动的设计，提供类型安全的加载器创建机制
///
/// 支持的格式：
/// - 通用格式: OBJ, GLB/GLTF, STL, PLY
/// - 专业格式: FBX (游戏/影视), IFC (BIM建筑), OSGB (倾斜摄影)
///
/// 注意：FBX、IFC、OSGB格式需要集成第三方库才能完整实现
/// </summary>
public class ModelLoaderFactory : IModelLoaderFactory
{
    private readonly ILogger<ModelLoaderFactory> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<ModelFormat, Type> _loaderTypes;
    private readonly Dictionary<string, ModelFormat> _extensionToFormat;

    /// <summary>
    /// 构造函数 - 初始化加载器类型映射
    /// </summary>
    public ModelLoaderFactory(
        ILogger<ModelLoaderFactory> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _loaderTypes = new Dictionary<ModelFormat, Type>();
        _extensionToFormat = new Dictionary<string, ModelFormat>(StringComparer.OrdinalIgnoreCase);

        // 初始化加载器映射
        InitializeLoaderMappings();

        _logger.LogInformation("模型加载器工厂初始化完成，支持 {Count} 种格式",
            _extensionToFormat.Count);
    }

    /// <summary>
    /// 初始化加载器类型映射
    /// </summary>
    private void InitializeLoaderMappings()
    {
        // 通用格式加载器
        RegisterLoader(ModelFormat.OBJ, typeof(ObjModelLoader), ".obj");
        RegisterLoader(ModelFormat.GLTF, typeof(GltfModelLoader), ".gltf");
        RegisterLoader(ModelFormat.GLB, typeof(GltfModelLoader), ".glb");
        RegisterLoader(ModelFormat.STL, typeof(StlModelLoader), ".stl");
        RegisterLoader(ModelFormat.PLY, typeof(PlyModelLoader), ".ply");

        // 专业格式加载器
        RegisterLoader(ModelFormat.FBX, typeof(FbxModelLoader), ".fbx");
        RegisterLoader(ModelFormat.IFC, typeof(IfcModelLoader), ".ifc");
        RegisterLoader(ModelFormat.IFCXML, typeof(IfcModelLoader), ".ifcxml");
        RegisterLoader(ModelFormat.IFCZIP, typeof(IfcModelLoader), ".ifczip");
        RegisterLoader(ModelFormat.OSGB, typeof(OsgbModelLoader), ".osgb");
        RegisterLoader(ModelFormat.OSG, typeof(OsgbModelLoader), ".osg");
    }

    /// <summary>
    /// 注册加载器
    /// </summary>
    private void RegisterLoader(ModelFormat format, Type loaderType, string extension)
    {
        _loaderTypes[format] = loaderType;
        _extensionToFormat[extension] = format;

        _logger.LogDebug("注册加载器: {Format} ({Extension}) -> {LoaderType}",
            format, extension, loaderType.Name);
    }

    /// <summary>
    /// 根据文件扩展名获取模型格式
    /// </summary>
    /// <param name="extension">文件扩展名（如".obj"或"obj"）</param>
    /// <returns>模型格式枚举，如果不支持则返回 ModelFormat.Unknown</returns>
    public ModelFormat GetModelFormatFromExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return ModelFormat.Unknown;

        // 确保扩展名以点开头
        if (!extension.StartsWith("."))
            extension = "." + extension;

        return _extensionToFormat.TryGetValue(extension, out var format)
            ? format
            : ModelFormat.Unknown;
    }

    /// <summary>
    /// 根据文件路径获取模型格式
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>模型格式枚举</returns>
    public ModelFormat GetModelFormatFromPath(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return GetModelFormatFromExtension(extension);
    }

    /// <summary>
    /// 获取模型格式对应的文件扩展名列表
    /// </summary>
    /// <param name="format">模型格式</param>
    /// <returns>扩展名列表</returns>
    public IEnumerable<string> GetExtensionsForFormat(ModelFormat format)
    {
        return _extensionToFormat
            .Where(kvp => kvp.Value == format)
            .Select(kvp => kvp.Key);
    }

    /// <summary>
    /// 根据模型格式创建对应的加载器
    /// 使用反射自动创建实例，从 DI 容器解析构造函数依赖
    /// </summary>
    /// <param name="format">模型格式枚举</param>
    /// <returns>模型加载器实例</returns>
    public ModelLoader CreateLoader(ModelFormat format)
    {
        if (format == ModelFormat.Unknown)
        {
            throw new ArgumentException("未知的模型格式", nameof(format));
        }

        if (!_loaderTypes.TryGetValue(format, out var loaderType))
        {
            throw new NotSupportedException($"不支持的模型格式: {format}");
        }

        // 使用反射自动创建实例
        var loader = CreateLoaderInstance(loaderType);
        if (loader == null)
        {
            throw new InvalidOperationException(
                $"无法创建加载器实例: {loaderType.Name}");
        }

        _logger.LogDebug("创建加载器: {LoaderType} 用于格式: {Format}",
            loaderType.Name, format);

        return loader;
    }

    /// <summary>
    /// 创建加载器实例
    /// 使用反射自动创建，从 DI 容器解析构造函数依赖
    /// </summary>
    private ModelLoader? CreateLoaderInstance(Type loaderType)
    {
        var constructors = loaderType.GetConstructors();
        if (constructors.Length == 0)
        {
            return null;
        }

        var constructor = constructors[0];
        var parameters = constructor.GetParameters();
        var args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;

            // 从 DI 容器解析参数（如 ILogger）
            var service = _serviceProvider.GetService(paramType);
            if (service != null)
            {
                args[i] = service;
            }
            else if (parameters[i].HasDefaultValue)
            {
                args[i] = parameters[i].DefaultValue;
            }
            else if (paramType.IsValueType)
            {
                args[i] = Activator.CreateInstance(paramType);
            }
            else
            {
                args[i] = null;
            }
        }

        return Activator.CreateInstance(loaderType, args) as ModelLoader;
    }

    /// <summary>
    /// 根据文件路径创建对应的加载器
    /// </summary>
    /// <param name="modelPath">模型文件路径</param>
    /// <returns>模型加载器实例</returns>
    public ModelLoader CreateLoaderFromPath(string modelPath)
    {
        var format = GetModelFormatFromPath(modelPath);

        if (format == ModelFormat.Unknown)
        {
            var extension = Path.GetExtension(modelPath);
            throw new NotSupportedException(
                $"不支持的文件格式: {extension}. 支持的格式: {string.Join(", ", GetSupportedExtensions())}");
        }

        return CreateLoader(format);
    }

    /// <summary>
    /// 检查是否支持指定的文件格式
    /// </summary>
    public bool SupportsFormat(string extension)
    {
        return GetModelFormatFromExtension(extension) != ModelFormat.Unknown;
    }

    /// <summary>
    /// 检查是否支持指定的模型格式枚举
    /// </summary>
    public bool SupportsFormat(ModelFormat format)
    {
        return format != ModelFormat.Unknown && _loaderTypes.ContainsKey(format);
    }

    /// <summary>
    /// 获取支持的所有文件扩展名
    /// </summary>
    public IEnumerable<string> GetSupportedExtensions()
    {
        return _extensionToFormat.Keys;
    }

    /// <summary>
    /// 获取支持的所有模型格式枚举
    /// </summary>
    public IEnumerable<ModelFormat> GetSupportedFormats()
    {
        return _loaderTypes.Keys;
    }

    /// <summary>
    /// 获取模型格式的详细描述
    /// </summary>
    public string GetFormatDescription(ModelFormat format)
    {
        return format switch
        {
            ModelFormat.OBJ => "Wavefront OBJ - 最广泛使用的3D模型交换格式",
            ModelFormat.GLTF => "glTF - GL Transmission Format，现代3D传输格式",
            ModelFormat.GLB => "GLB - glTF的二进制版本，更高效",
            ModelFormat.STL => "STL - 3D打印行业标准格式",
            ModelFormat.PLY => "PLY - 斯坦福多边形格式，三维扫描常用",
            ModelFormat.FBX => "FBX - Autodesk Filmbox，游戏和影视行业标准",
            ModelFormat.IFC => "IFC - BIM建筑信息模型国际标准",
            ModelFormat.IFCXML => "IFCXML - IFC的XML版本",
            ModelFormat.IFCZIP => "IFCZIP - 压缩的IFC文件",
            ModelFormat.OSGB => "OSGB - OpenSceneGraph Binary，倾斜摄影标准",
            ModelFormat.OSG => "OSG - OpenSceneGraph ASCII格式",
            ModelFormat.Unknown => "未知格式",
            _ => "未定义的格式"
        };
    }
}
