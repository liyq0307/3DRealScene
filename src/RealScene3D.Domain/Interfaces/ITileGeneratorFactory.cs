namespace RealScene3D.Domain.Interfaces;
using RealScene3D.Domain.Enums;

/// <summary>
/// 瓦片生成器工厂接口 - 根据TileFormat创建对应的生成器实例
/// 设计模式：抽象工厂模式
/// 职责：解耦切片策略与具体瓦片生成器，支持运行时动态选择格式
/// </summary>
public interface ITileGeneratorFactory
{
    /// <summary>
    /// 根据瓦片格式创建对应的生成器实例
    /// </summary>
    /// <param name="format">瓦片格式枚举</param>
    /// <returns>对应格式的瓦片生成器实例</returns>
    /// <exception cref="NotSupportedException">当请求的格式不支持时抛出</exception>
    object CreateGenerator(TileFormat format);

    /// <summary>
    /// 检查是否支持指定的瓦片格式
    /// </summary>
    /// <param name="format">瓦片格式</param>
    /// <returns>如果支持返回true，否则返回false</returns>
    bool SupportsFormat(TileFormat format);

    /// <summary>
    /// 获取所有支持的瓦片格式列表
    /// </summary>
    IEnumerable<TileFormat> SupportedFormats { get; }
}
