using Microsoft.Extensions.DependencyInjection;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Application.Services.Generators;

/// <summary>
/// 瓦片生成器工厂实现 - 基于依赖注入的工厂模式
/// 优点：
/// 1. 解耦切片策略与具体生成器
/// 2. 支持运行时动态选择格式
/// 3. 利用DI容器管理生成器生命周期
/// 4. 易于扩展新的瓦片格式
/// </summary>
public class TileGeneratorFactory : ITileGeneratorFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 构造函数 - 注入服务提供者
    /// </summary>
    /// <param name="serviceProvider">DI容器服务提供者</param>
    public TileGeneratorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// 根据瓦片格式创建对应的生成器实例
    /// 使用模式匹配和DI解析
    /// </summary>
    /// <param name="format">瓦片格式</param>
    /// <returns>对应的瓦片生成器实例</returns>
    /// <exception cref="NotSupportedException">当格式不支持时抛出</exception>
    public object CreateGenerator(TileFormat format)
    {
        return format switch
        {
            TileFormat.B3DM => _serviceProvider.GetRequiredService<B3dmGenerator>(),
            TileFormat.GLTF => _serviceProvider.GetRequiredService<GltfGenerator>(),
            TileFormat.I3DM => _serviceProvider.GetRequiredService<I3dmGenerator>(),
            TileFormat.PNTS => _serviceProvider.GetRequiredService<PntsGenerator>(),
            TileFormat.CMPT => _serviceProvider.GetRequiredService<CmptGenerator>(),
            _ => throw new NotSupportedException($"不支持的瓦片格式: {format}")
        };
    }

    /// <summary>
    /// 检查是否支持指定的瓦片格式
    /// </summary>
    /// <param name="format">瓦片格式</param>
    /// <returns>如果支持返回true</returns>
    public bool SupportsFormat(TileFormat format)
    {
        return format switch
        {
            TileFormat.B3DM => true,
            TileFormat.GLTF => true,
            TileFormat.I3DM => true,
            TileFormat.PNTS => true,
            TileFormat.CMPT => true,
            _ => false
        };
    }

    /// <summary>
    /// 获取所有支持的瓦片格式列表
    /// </summary>
    public IEnumerable<TileFormat> SupportedFormats => new[]
    {
        TileFormat.B3DM,
        TileFormat.GLTF,
        TileFormat.I3DM,
        TileFormat.PNTS,
        TileFormat.CMPT
    };
}
