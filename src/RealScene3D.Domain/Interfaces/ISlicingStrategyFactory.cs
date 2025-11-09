namespace RealScene3D.Domain.Interfaces;

/// <summary>
/// 切片策略工厂接口 - 根据SlicingStrategy创建对应的策略实例
/// 设计模式：抽象工厂模式
/// 职责：解耦切片处理器与具体切片策略，支持运行时动态选择策略
/// </summary>
public interface ISlicingStrategyFactory
{
    /// <summary>
    /// 根据切片策略类型创建对应的策略实例
    /// </summary>
    /// <param name="strategy">切片策略枚举</param>
    /// <returns>对应的切片策略实例</returns>
    /// <exception cref="NotSupportedException">当请求的策略不支持时抛出</exception>
    ISlicingStrategy CreateStrategy(SlicingStrategy strategy);

    /// <summary>
    /// 检查是否支持指定的切片策略
    /// </summary>
    /// <param name="strategy">切片策略</param>
    /// <returns>如果支持返回true，否则返回false</returns>
    bool SupportsStrategy(SlicingStrategy strategy);

    /// <summary>
    /// 获取所有支持的切片策略列表
    /// </summary>
    IEnumerable<SlicingStrategy> SupportedStrategies { get; }
}
