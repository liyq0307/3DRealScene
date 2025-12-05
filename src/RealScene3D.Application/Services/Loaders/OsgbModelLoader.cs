using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Microsoft.Extensions.DependencyInjection;
using RealScene3D.Domain.Geometry;
using RealScene3D.Domain.Materials;
using RealScene3D.Managed;

namespace RealScene3D.Application.Services.Loaders;

/// <summary>
/// OSGB模型加载器 - 使用 OpenSceneGraph C++/CLI 封装直接读取 OSGB 文件
///
/// 功能：直接读取 OSGB 二进制文件，无需任何转换工具
/// 依赖：RealScene3D.Lib.OSGB C++/CLI 封装库（封装 OpenSceneGraph）
/// 优势：原生高质量纹理、无需外部工具、性能最优
/// </summary>
public class OsgbModelLoader : ModelLoader
{
    private readonly ILogger<OsgbModelLoader> _logger;
    private readonly IServiceProvider _serviceProvider;

    private static readonly string[] SupportedFormats = { ".osgb", ".osg" };

    public OsgbModelLoader(
        ILogger<OsgbModelLoader> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 直接加载 OSGB 文件
    /// 使用原生 OpenSceneGraph 库，无需转换
    /// </summary>
    public override async Task<(IMesh Mesh, Box3 BoundingBox)> LoadModelAsync(
        string modelPath,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("开始加载 OSGB 模型: {Path}", modelPath);

        try
        {
            // 验证文件
            ValidateFileExists(modelPath);
            ValidateFileExtension(modelPath);

            // 检查原生库是否可用
            if (!OsgbNativeReader.IsAvailable())
            {
                throw new InvalidOperationException(
                    "RealScene3D.Lib.OSGB C++/CLI 封装库不可用。" +
                    "\n请确保：" +
                    "\n1. RealScene3D.Lib.OSGB.dll 已部署到应用程序目录" +
                    "\n2. OpenSceneGraph DLL 文件已部署（osg.dll, osgDB.dll 等）" +
                    "\n3. 设置了正确的环境变量 OSG_ROOT" +
                    "\n\n详细信息请参阅：src/RealScene3D.Lib/OSGB/README.md");
            }

            // 使用原生读取器直接加载
            var nativeReader = ActivatorUtilities.CreateInstance<OsgbNativeReader>(_serviceProvider);
            var result = await nativeReader.LoadModelAsync(modelPath, cancellationToken);

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "OSGB 模型加载完成: 类型={MeshType}, 顶点={VertexCount}, 面={FaceCount}, 耗时={Elapsed:F2}秒",
                result.Mesh.HasTexture ? "MeshT" : "Mesh",
                result.Mesh.VertexCount,
                result.Mesh.FacesCount,
                elapsed.TotalSeconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 OSGB 模型失败: {Path}", modelPath);
            throw;
        }
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


/// <summary>
/// OSGB 原生读取器 - 直接使用 OpenSceneGraph C++/CLI 封装读取 OSGB 文件
/// 完全不需要 osgconv 转换，直接返回 IMesh 格式的网格数据
/// </summary>
public class OsgbNativeReader
{
    private readonly ILogger<OsgbNativeReader> _logger;

    public OsgbNativeReader(ILogger<OsgbNativeReader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 直接加载 OSGB 文件并转换为 IMesh
    /// 无需 osgconv，完全使用原生 OpenSceneGraph 库
    /// </summary>
    public async Task<(IMesh Mesh, Box3 BoundingBox)> LoadModelAsync(
        string osgbPath,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => LoadModel(osgbPath), cancellationToken);
    }

    /// <summary>
    /// 同步加载模型
    /// </summary>
    private (IMesh Mesh, Box3 BoundingBox) LoadModel(string osgbPath)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("开始使用原生 OpenSceneGraph 读取 OSGB: {Path}", osgbPath);

        try
        {
            // 检查文件
            if (!File.Exists(osgbPath))
            {
                throw new FileNotFoundException($"OSGB 文件不存在: {osgbPath}");
            }

            // 创建 C++/CLI 读取器
            using var reader = new OsgbReaderWrapper();

            // 直接读取并转换为网格数据
            var managedMeshData = reader.LoadAndConvertToMesh(osgbPath);

            if (managedMeshData == null || managedMeshData.VertexCount == 0)
            {
                var error = reader.GetLastError();
                throw new InvalidOperationException($"读取 OSGB 失败: {error}");
            }

            _logger.LogInformation(
                "OSG 读取成功: 顶点={VertexCount}, 面={FaceCount}, 纹理={TextureCount}",
                managedMeshData.VertexCount,
                managedMeshData.FaceCount,
                managedMeshData.TextureCount);

            // 转换为 C# IMesh
            var (mesh, boundingBox) = ConvertToIMesh(managedMeshData, osgbPath);

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "OSGB 模型加载完成: 类型={MeshType}, 顶点={VertexCount}, 面={FaceCount}, " +
                "纹理={TextureCount}, 耗时={Elapsed:F2}秒",
                mesh.HasTexture ? "MeshT" : "Mesh",
                mesh.VertexCount,
                mesh.FacesCount,
                managedMeshData.TextureCount,
                elapsed.TotalSeconds);

            return (mesh, boundingBox);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取 OSGB 文件失败: {Path}", osgbPath);
            throw;
        }
    }

    /// <summary>
    /// 将 ManagedMeshData 转换为 C# IMesh
    /// </summary>
    private (IMesh Mesh, Box3 BoundingBox) ConvertToIMesh(ManagedMeshData managedData, string osgbPath)
    {
        var directory = Path.GetDirectoryName(osgbPath) ?? string.Empty;

        // 构建顶点列表
        var vertices = new List<Vertex3>();
        for (int i = 0; i < managedData.Vertices.Length; i += 3)
        {
            vertices.Add(new Vertex3(
                managedData.Vertices[i],
                managedData.Vertices[i + 1],
                managedData.Vertices[i + 2]
            ));
        }

        // 构建法线列表
        var normals = new List<Vertex3>();
        for (int i = 0; i < managedData.Normals.Length; i += 3)
        {
            normals.Add(new Vertex3(
                managedData.Normals[i],
                managedData.Normals[i + 1],
                managedData.Normals[i + 2]
            ));
        }

        // 构建纹理坐标列表
        List<Vertex2>? texCoords = null;
        if (managedData.TexCoords != null && managedData.TexCoords.Length > 0)
        {
            texCoords = new List<Vertex2>();
            for (int i = 0; i < managedData.TexCoords.Length; i += 2)
            {
                texCoords.Add(new Vertex2(
                    managedData.TexCoords[i],
                    managedData.TexCoords[i + 1]
                ));
            }
        }

        // 构建面列表
        var faces = new List<Face>();
        for (int i = 0; i < managedData.Indices.Length; i += 3)
        {
            faces.Add(new Face(
                (int)managedData.Indices[i],
                (int)managedData.Indices[i + 1],
                (int)managedData.Indices[i + 2]
            ));
        }

        // 处理材质和纹理
        var materials = new List<MaterialEx>();
        if (managedData.Textures.Count > 0)
        {
            for (int i = 0; i < managedData.Textures.Count; i++)
            {
                var texture = managedData.Textures[i];

                // 保存纹理到文件
                var texturePath = Path.Combine(directory, $"texture_{i}.jpg");
                SaveTexture(texture, texturePath);

                // 创建材质
                var material = new MaterialEx
                {
                    Name = $"material_{i}",
                    DiffuseTexturePath = texturePath
                };

                // 如果有对应的材质数据，使用它
                if (i < managedData.Materials.Count)
                {
                    var matData = managedData.Materials[i];
                    material.Name = matData.Name;
                    material.Diffuse = new RGB(matData.DiffuseR, matData.DiffuseG, matData.DiffuseB);
                    material.Specular = new RGB(matData.SpecularR, matData.SpecularG, matData.SpecularB);
                    material.Shininess = matData.Shininess;
                }

                materials.Add(material);
            }
        }

        // 创建网格
        IMesh mesh;
        if (texCoords != null && texCoords.Count > 0)
        {
            // 创建 MeshT（带纹理）
            mesh = new MeshT(vertices, normals, texCoords, faces, materials.Count > 0 ? materials : null);
        }
        else
        {
            // 创建 Mesh（无纹理）
            mesh = new Mesh(vertices, normals, faces, materials.Count > 0 ? materials : null);
        }

        // 构建包围盒
        var boundingBox = new Box3(
            new Vertex3(managedData.BBoxMinX, managedData.BBoxMinY, managedData.BBoxMinZ),
            new Vertex3(managedData.BBoxMaxX, managedData.BBoxMaxY, managedData.BBoxMaxZ)
        );

        return (mesh, boundingBox);
    }

    /// <summary>
    /// 保存纹理到文件
    /// </summary>
    private void SaveTexture(ManagedTextureData texture, string outputPath)
    {
        try
        {
            if (texture.Components == 4)
            {
                var image = Image.LoadPixelData<Rgba32>(texture.ImageData, texture.Width, texture.Height);
                image.SaveAsJpeg(outputPath);
                image.Dispose();
            }
            else if (texture.Components == 3)
            {
                var image = Image.LoadPixelData<Rgb24>(texture.ImageData, texture.Width, texture.Height);
                image.SaveAsJpeg(outputPath);
                image.Dispose();
            }

            _logger.LogDebug("纹理已保存: {Path}, 尺寸: {Width}x{Height}",
                outputPath, texture.Width, texture.Height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存纹理失败: {Path}", outputPath);
        }
    }

    /// <summary>
    /// 检查原生库是否可用
    /// </summary>
    public static bool IsAvailable()
    {
        try
        {
            using var testReader = new OsgbReaderWrapper();
            return true;
        }
        catch
        {
            return false;
        }
    }
}