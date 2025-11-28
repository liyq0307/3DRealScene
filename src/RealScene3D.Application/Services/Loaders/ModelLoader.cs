using RealScene3D.Domain.Geometry;
using RealScene3D.Domain.Materials;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// 模型加载器抽象基类 - 用于加载和解析3D模型文件
/// 所有具体的模型加载器都应继承此基类
/// 支持多种3D模型格式的加载,直接构建索引网格（MeshT）用于切片处理
/// </summary>
public abstract class ModelLoader
{
    /// <summary>
    /// 加载3D模型文件并构建索引网格（MeshT）
    /// </summary>
    /// <param name="modelPath">模型文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>MeshT 对象（包含顶点、UV、面和材质）和模型包围盒</returns>
    public abstract Task<(MeshT Mesh, Box3 BoundingBox)> LoadModelAsync(
        string modelPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否支持指定的文件格式
    /// </summary>
    /// <param name="extension">文件扩展名(如".obj",".glb")</param>
    /// <returns>是否支持该格式</returns>
    public abstract bool SupportsFormat(string extension);

    /// <summary>
    /// 获取支持的所有文件格式
    /// </summary>
    /// <returns>支持的文件扩展名列表</returns>
    public abstract IEnumerable<string> GetSupportedFormats();

    /// <summary>
    /// 验证文件是否存在
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
    protected void ValidateFileExists(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"模型文件不存在: {filePath}", filePath);
        }
    }

    /// <summary>
    /// 验证文件扩展名是否支持
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <exception cref="NotSupportedException">不支持的格式时抛出</exception>
    protected void ValidateFileExtension(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension))
        {
            throw new ArgumentException($"无法确定文件格式: {filePath}");
        }

        if (!SupportsFormat(extension))
        {
            throw new NotSupportedException(
                $"不支持的文件格式: {extension}. 支持的格式: {string.Join(", ", GetSupportedFormats())}");
        }
    }

    /// <summary>
    /// 创建默认材质
    /// 当模型文件没有材质信息时使用
    /// </summary>
    /// <param name="materialName">材质名称</param>
    /// <returns>默认材质对象</returns>
    protected Material CreateDefaultMaterial(string materialName = "default")
    {
        return new Material(
            materialName,
            null,
            null,
            new RGB(0.7, 0.7, 0.7), // 环境光
            new RGB(0.7, 0.7, 0.7), // 漫反射 - 灰色
            new RGB(0.2, 0.2, 0.2), // 镜面反射
            32.0,                    // 高光指数
            1.0);                    // 不透明
    }
}
