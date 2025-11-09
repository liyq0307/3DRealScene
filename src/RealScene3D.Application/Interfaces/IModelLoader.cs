using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Application.Interfaces;

/// <summary>
/// 模型加载器接口 - 用于加载和解析3D模型文件
/// 支持多种3D模型格式的加载,提取几何数据用于切片处理
/// </summary>
public interface IModelLoader
{
    /// <summary>
    /// 加载3D模型文件并提取三角形网格数据、材质信息
    /// </summary>
    /// <param name="modelPath">模型文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>三角形列表、模型包围盒和材质字典（材质名称->材质对象）</returns>
    Task<(List<Triangle> Triangles, BoundingBox3D BoundingBox, Dictionary<string, Material> Materials)> LoadModelAsync(
        string modelPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否支持指定的文件格式
    /// </summary>
    /// <param name="extension">文件扩展名(如".obj",".glb")</param>
    /// <returns>是否支持该格式</returns>
    bool SupportsFormat(string extension);

    /// <summary>
    /// 获取支持的所有文件格式
    /// </summary>
    /// <returns>支持的文件扩展名列表</returns>
    IEnumerable<string> GetSupportedFormats();
}
