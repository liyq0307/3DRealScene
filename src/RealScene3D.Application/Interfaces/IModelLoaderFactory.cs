using RealScene3D.Application.Services.Loaders;
using RealScene3D.Domain.Enums;

namespace RealScene3D.Application.Interfaces;

/// <summary>
/// 模型加载器工厂接口 - 抽象工厂模式
/// 负责根据模型格式创建对应的加载器实例
/// </summary>
public interface IModelLoaderFactory
{
    /// <summary>
    /// 根据模型格式创建对应的加载器
    /// </summary>
    /// <param name="format">模型格式枚举</param>
    /// <returns>模型加载器实例</returns>
    ModelLoader CreateLoader(ModelFormat format);

    /// <summary>
    /// 根据文件路径创建对应的加载器
    /// </summary>
    /// <param name="modelPath">模型文件路径</param>
    /// <returns>模型加载器实例</returns>
    ModelLoader CreateLoaderFromPath(string modelPath);

    /// <summary>
    /// 根据文件扩展名获取模型格式
    /// </summary>
    /// <param name="extension">文件扩展名（如".obj"或"obj"）</param>
    /// <returns>模型格式枚举，如果不支持则返回 ModelFormat.Unknown</returns>
    ModelFormat GetModelFormatFromExtension(string extension);

    /// <summary>
    /// 根据文件路径获取模型格式
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>模型格式枚举</returns>
    ModelFormat GetModelFormatFromPath(string filePath);

    /// <summary>
    /// 获取模型格式对应的文件扩展名列表
    /// </summary>
    /// <param name="format">模型格式</param>
    /// <returns>扩展名列表</returns>
    IEnumerable<string> GetExtensionsForFormat(ModelFormat format);

    /// <summary>
    /// 检查是否支持指定的文件格式
    /// </summary>
    /// <param name="extension">文件扩展名</param>
    /// <returns>是否支持</returns>
    bool SupportsFormat(string extension);

    /// <summary>
    /// 检查是否支持指定的模型格式枚举
    /// </summary>
    /// <param name="format">模型格式枚举</param>
    /// <returns>是否支持</returns>
    bool SupportsFormat(ModelFormat format);

    /// <summary>
    /// 获取支持的所有文件扩展名
    /// </summary>
    /// <returns>扩展名集合</returns>
    IEnumerable<string> GetSupportedExtensions();

    /// <summary>
    /// 获取支持的所有模型格式枚举
    /// </summary>
    /// <returns>格式枚举集合</returns>
    IEnumerable<ModelFormat> GetSupportedFormats();

    /// <summary>
    /// 获取模型格式的详细描述
    /// </summary>
    /// <param name="format">模型格式</param>
    /// <returns>格式描述</returns>
    string GetFormatDescription(ModelFormat format);
}
