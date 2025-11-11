using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// 模型加载器抽象基类 - 用于加载和解析3D模型文件
/// 所有具体的模型加载器都应继承此基类
/// 支持多种3D模型格式的加载,提取几何数据用于切片处理
/// </summary>
public abstract class ModelLoader
{
    /// <summary>
    /// 加载3D模型文件并提取三角形网格数据、材质信息
    /// </summary>
    /// <param name="modelPath">模型文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>三角形列表、模型包围盒和材质字典（材质名称->材质对象）</returns>
    public abstract Task<(List<Triangle> Triangles, BoundingBox3D BoundingBox, Dictionary<string, Material> Materials)> LoadModelAsync(
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
        return new Material
        {
            Name = materialName,
            DiffuseColor = new Color3D { R = 0.7, G = 0.7, B = 0.7 }, // 灰色
            SpecularColor = new Color3D { R = 0.2, G = 0.2, B = 0.2 },
            Shininess = 32.0
        };
    }

    /// <summary>
    /// 计算三角形列表的包围盒
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <returns>包围盒</returns>
    protected BoundingBox3D CalculateBoundingBox(List<Triangle> triangles)
    {
        if (triangles == null || triangles.Count == 0)
        {
            return new BoundingBox3D
            {
                MinX = 0, MinY = 0, MinZ = 0,
                MaxX = 0, MaxY = 0, MaxZ = 0
            };
        }

        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

        foreach (var triangle in triangles)
        {
            foreach (var vertex in triangle.Vertices)
            {
                minX = Math.Min(minX, vertex.X);
                minY = Math.Min(minY, vertex.Y);
                minZ = Math.Min(minZ, vertex.Z);

                maxX = Math.Max(maxX, vertex.X);
                maxY = Math.Max(maxY, vertex.Y);
                maxZ = Math.Max(maxZ, vertex.Z);
            }
        }

        return new BoundingBox3D
        {
            MinX = minX, MinY = minY, MinZ = minZ,
            MaxX = maxX, MaxY = maxY, MaxZ = maxZ
        };
    }

    /// <summary>
    /// 计算三角形的法线向量
    /// 使用右手定则计算
    /// </summary>
    /// <param name="v0">第一个顶点</param>
    /// <param name="v1">第二个顶点</param>
    /// <param name="v2">第三个顶点</param>
    /// <returns>法线向量</returns>
    protected Vector3D CalculateNormal(Vector3D v0, Vector3D v1, Vector3D v2)
    {
        // 计算两条边向量
        var edge1 = new Vector3D
        {
            X = v1.X - v0.X,
            Y = v1.Y - v0.Y,
            Z = v1.Z - v0.Z
        };

        var edge2 = new Vector3D
        {
            X = v2.X - v0.X,
            Y = v2.Y - v0.Y,
            Z = v2.Z - v0.Z
        };

        // 计算叉积
        var normal = new Vector3D
        {
            X = edge1.Y * edge2.Z - edge1.Z * edge2.Y,
            Y = edge1.Z * edge2.X - edge1.X * edge2.Z,
            Z = edge1.X * edge2.Y - edge1.Y * edge2.X
        };

        // 归一化
        var length = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);
        if (length > 0)
        {
            normal.X /= length;
            normal.Y /= length;
            normal.Z /= length;
        }

        return normal;
    }
}
