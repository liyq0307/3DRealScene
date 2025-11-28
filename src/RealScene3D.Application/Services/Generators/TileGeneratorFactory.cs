using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Application.Services.Generators;

/// <summary>
/// 瓦片生成器工厂实现 - 基于依赖注入的工厂模式
/// 优点：
/// 1. 解耦切片策略与具体生成器
/// 2. 支持运行时动态选择格式
/// 3. 自动创建生成器实例，无需预先注册
/// 4. 易于扩展新的瓦片格式
/// 5. 支持递归创建生成器依赖（如 B3dmGenerator 依赖 GltfGenerator）
/// </summary>
public class TileGeneratorFactory : ITileGeneratorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TileGeneratorFactory> _logger;
    private readonly Dictionary<TileFormat, Type> _generatorTypes;

    /// <summary>
    /// 构造函数 - 注入服务提供者和日志记录器
    /// </summary>
    public TileGeneratorFactory(
        IServiceProvider serviceProvider,
        ILogger<TileGeneratorFactory> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 初始化生成器类型映射
        _generatorTypes = new Dictionary<TileFormat, Type>
        {
            { TileFormat.B3DM, typeof(B3dmGenerator) },
            { TileFormat.GLTF, typeof(GltfGenerator) },
            { TileFormat.I3DM, typeof(I3dmGenerator) },
            { TileFormat.PNTS, typeof(PntsGenerator) },
            { TileFormat.CMPT, typeof(CmptGenerator) }
        };
    }

    /// <summary>
    /// 根据瓦片格式创建对应的生成器实例
    /// 使用反射自动创建，从 DI 容器解析构造函数依赖
    /// </summary>
    public object CreateGenerator(TileFormat format)
    {
        if (!_generatorTypes.TryGetValue(format, out var generatorType))
        {
            throw new NotSupportedException($"不支持的瓦片格式: {format}");
        }

        // 使用反射自动创建生成器实例
        var generator = CreateGeneratorInstance(generatorType);
        if (generator == null)
        {
            throw new InvalidOperationException($"无法创建生成器实例: {generatorType.Name}");
        }

        _logger.LogDebug("创建瓦片生成器: {GeneratorType} 用于格式: {Format}",
            generatorType.Name, format);

        return generator;
    }

    /// <summary>
    /// 创建生成器实例
    /// 使用反射自动创建，从 DI 容器解析构造函数依赖
    /// 支持递归创建生成器依赖（如 B3dmGenerator 依赖 GltfGenerator）
    /// </summary>
    internal object? CreateGeneratorInstance(Type generatorType)
    {
        var constructors = generatorType.GetConstructors();
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
            // 如果是生成器类型（继承自 TileGenerator），递归创建
            else if (IsGeneratorType(paramType))
            {
                _logger.LogDebug("递归创建生成器依赖: {GeneratorType}", paramType.Name);
                args[i] = CreateGeneratorInstance(paramType);
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

        return Activator.CreateInstance(generatorType, args);
    }

    /// <summary>
    /// 判断是否为生成器类型
    /// 检查类型是否继承自 TileGenerator 或在 _generatorTypes 映射中
    /// </summary>
    private bool IsGeneratorType(Type type)
    {
        // 检查是否在已知生成器类型映射中
        if (_generatorTypes.ContainsValue(type))
        {
            return true;
        }

        // 检查是否继承自 TileGenerator 基类
        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.Name == "TileGenerator")
            {
                return true;
            }
            baseType = baseType.BaseType;
        }

        return false;
    }

    /// <summary>
    /// 检查是否支持指定的瓦片格式
    /// </summary>
    public bool SupportsFormat(TileFormat format)
    {
        return _generatorTypes.ContainsKey(format);
    }

    /// <summary>
    /// 获取所有支持的瓦片格式列表
    /// </summary>
    public IEnumerable<TileFormat> SupportedFormats => _generatorTypes.Keys;

    /// <summary>
    /// 获取IServiceProvider以供扩展方法使用
    /// </summary>
    internal IServiceProvider ServiceProvider => _serviceProvider;
}

/// <summary>
/// 瓦片生成器工厂扩展方法 - 提供类型安全的生成器创建
/// 这些扩展方法提供强类型接口，避免手动类型转换和枚举传递
/// </summary>
public static class TileGeneratorFactoryExtensions
{
    /// <summary>
    /// 创建B3DM瓦片生成器
    /// </summary>
    public static B3dmGenerator CreateB3dmGenerator(this ITileGeneratorFactory factory)
    {
        return (B3dmGenerator)factory.CreateGenerator(TileFormat.B3DM);
    }

    /// <summary>
    /// 创建GLTF/GLB生成器
    /// </summary>
    public static GltfGenerator CreateGltfGenerator(this ITileGeneratorFactory factory)
    {
        return (GltfGenerator)factory.CreateGenerator(TileFormat.GLTF);
    }

    /// <summary>
    /// 创建I3DM实例瓦片生成器
    /// </summary>
    public static I3dmGenerator CreateI3dmGenerator(this ITileGeneratorFactory factory)
    {
        return (I3dmGenerator)factory.CreateGenerator(TileFormat.I3DM);
    }

    /// <summary>
    /// 创建PNTS点云瓦片生成器
    /// </summary>
    public static PntsGenerator CreatePntsGenerator(this ITileGeneratorFactory factory)
    {
        return (PntsGenerator)factory.CreateGenerator(TileFormat.PNTS);
    }

    /// <summary>
    /// 创建CMPT复合瓦片生成器
    /// </summary>
    public static CmptGenerator CreateCmptGenerator(this ITileGeneratorFactory factory)
    {
        return (CmptGenerator)factory.CreateGenerator(TileFormat.CMPT);
    }

    /// <summary>
    /// 创建Tileset.json生成器
    /// 注意：TilesetGenerator不通过TileFormat枚举创建，直接使用反射创建实例
    /// </summary>
    public static TilesetGenerator CreateTilesetGenerator(this ITileGeneratorFactory factory)
    {
        if (factory is TileGeneratorFactory concreteFactory)
        {
            // 使用反射自动创建实例
            return concreteFactory.CreateGeneratorInstance(typeof(TilesetGenerator)) as TilesetGenerator
                ?? throw new InvalidOperationException("无法创建TilesetGenerator实例");
        }

        throw new InvalidOperationException("无法创建TilesetGenerator，工厂类型不匹配");
    }
}
