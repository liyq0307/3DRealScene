using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Geometry;
using RealScene3D.Domain.Utils;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// OBJ模型加载器 - 加载和解析Wavefront OBJ格式的3D模型
/// 完整支持顶点(v)、法线(vn)、纹理坐标(vt)和面片(f)数据的解析
/// 支持MTL材质文件加载和材质关联
/// 直接构建索引化的MeshT网格，避免中间转换开销
/// </summary>
public class ObjModelLoader : ModelLoader
{
    private readonly ILogger<ObjModelLoader> _logger;
    private static readonly string[] SupportedFormats = { ".obj" };

    public ObjModelLoader(ILogger<ObjModelLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 加载OBJ模型文件并构建索引网格（IMesh）
    /// 算法:逐行解析OBJ文件,提取v(顶点)、vn(法线)、vt(纹理坐标)和f(面片)数据
    /// 根据是否有纹理坐标返回 Mesh 或 MeshT
    /// </summary>
    public override async Task<(IMesh Mesh, Box3 BoundingBox)> LoadModelAsync(
        string modelPath,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("开始加载OBJ模型: {Path}", modelPath);

        try
        {
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"模型文件不存在: {modelPath}");
            }

            var (mesh, _, normals, boundingBox) = await MeshUtils.LoadMesh(modelPath, cancellationToken);

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "OBJ模型加载完成: 类型={MeshType}, 顶点={VertexCount}, 法线={NormalCount}, 纹理坐标={TexCoordCount}, " +
                "面片={FaceCount}, 三角形={TriangleCount}, 材质={MaterialCount}, " +
                "包围盒=[{MinX:F3},{MinY:F3},{MinZ:F3}]-[{MaxX:F3},{MaxY:F3},{MaxZ:F3}], 耗时={Elapsed:F2}秒",
                mesh.HasTexture ? "MeshT" : "Mesh", mesh.VertexCount, normals.Count, mesh.TextureVertices?.Count,
                mesh.FacesCount, mesh.FacesCount, mesh.Materials?.Count, boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Min.Z,
                boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Max.Z, elapsed.TotalSeconds);

            return (mesh, boundingBox);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载OBJ模型失败: {Path}", modelPath);
            throw;
        }
    }

    /// <summary>
    /// 检查是否支持指定的文件格式
    /// </summary>
    public override bool SupportsFormat(string extension)
    {
        return SupportedFormats.Contains(extension.ToLowerInvariant());
    }

    /// <summary>
    /// 获取支持的所有文件格式
    /// </summary>
    public override IEnumerable<string> GetSupportedFormats()
    {
        return SupportedFormats;
    }
}
