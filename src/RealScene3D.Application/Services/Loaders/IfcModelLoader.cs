using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Geometry;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// IFC模型加载器 - 加载和解析IFC格式的BIM建筑信息模型
/// TODO: 需要集成 xBIM Toolkit 实现完整功能
/// </summary>
public class IfcModelLoader : ModelLoader
{
    private readonly ILogger<IfcModelLoader> _logger;
    private static readonly string[] SupportedFormats = { ".ifc", ".ifcxml", ".ifczip" };

    public IfcModelLoader(ILogger<IfcModelLoader> logger)
    {
        _logger = logger;
    }

    public override async Task<(MeshT Mesh, Box3 BoundingBox)> LoadModelAsync(
        string modelPath,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        _logger.LogWarning("IFC加载器需要集成xBIM Toolkit才能完整实现");
        throw new NotImplementedException("IFC格式加载需要集成 xBIM Toolkit");
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
