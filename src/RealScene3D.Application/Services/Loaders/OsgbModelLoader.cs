using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Geometry;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// OSGB模型加载器 - 加载和解析OSGB格式的倾斜摄影数据
/// TODO: 建议预处理为3D Tiles或GLTF格式后加载
/// </summary>
public class OsgbModelLoader : ModelLoader
{
    private readonly ILogger<OsgbModelLoader> _logger;
    private static readonly string[] SupportedFormats = { ".osgb", ".osg" };

    public OsgbModelLoader(ILogger<OsgbModelLoader> logger)
    {
        _logger = logger;
    }

    public override async Task<(MeshT Mesh, Box3 BoundingBox)> LoadModelAsync(
        string modelPath,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        _logger.LogWarning("OSGB加载器建议预处理为GLTF或3D Tiles格式");
        throw new NotImplementedException("OSGB格式加载需要集成OpenSceneGraph或转换为GLTF");
    }

    public override bool SupportsFormat(string extension)
    {
        return SupportedFormats.Contains(extension.ToLowerInvariant());
    }

    public override IEnumerable<string> GetSupportedFormats()
    {
        return SupportedFormats;
    }
}
