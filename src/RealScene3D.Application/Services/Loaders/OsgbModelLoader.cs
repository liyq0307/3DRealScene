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

            // 使用原生读取器直接加载(默认加载所有层级)
            var nativeReader = ActivatorUtilities.CreateInstance<OsgbNativeReader>(_serviceProvider);
            var result = await nativeReader.LoadModelAsync(modelPath, false, 0, cancellationToken);

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
    /// 无需 osgconv,完全使用原生 OpenSceneGraph 库
    /// </summary>
    /// <param name="osgbPath">OSGB文件路径</param>
    /// <param name="loadAllLevels">是否递归加载所有LOD层级(默认true,加载完整模型)</param>
    /// <param name="maxDepth">最大递归深度(0=无限制,默认0)</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<(IMesh Mesh, Box3 BoundingBox)> LoadModelAsync(
        string osgbPath,
        bool loadAllLevels = true,
        int maxDepth = 0,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => LoadModel(osgbPath, loadAllLevels, maxDepth), cancellationToken);
    }

    /// <summary>
    /// 同步加载模型
    /// </summary>
    private (IMesh Mesh, Box3 BoundingBox) LoadModel(string osgbPath, bool loadAllLevels = true, int maxDepth = 0)
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

            // 直接读取并转换为网格数据(支持递归加载所有LOD层级)
            _logger.LogInformation("加载设置: loadAllLevels={LoadAll}, maxDepth={MaxDepth}",
                loadAllLevels, maxDepth);

            var managedMeshData = reader.LoadAndConvertToMesh(osgbPath, loadAllLevels, maxDepth);

            if (managedMeshData == null || managedMeshData.VertexCount == 0)
            {
                var error = reader.GetLastError();
                throw new InvalidOperationException($"读取 OSGB 失败: {error}");
            }

            _logger.LogInformation(
                "OSG 读取成功: 顶点={VertexCount}, 面={FaceCount}, 纹理={TextureCount}, 材质={MaterialCount}",
                managedMeshData.VertexCount,
                managedMeshData.FaceCount,
                managedMeshData.TextureCount,
                managedMeshData.MaterialCount);

            // 输出纹理详细信息
            for (int i = 0; i < managedMeshData.Textures.Count; i++)
            {
                var tex = managedMeshData.Textures[i];
                _logger.LogInformation(
                    "纹理 {Index}: {Name}, 尺寸={Width}x{Height}, 格式={Format}, 压缩={IsCompressed}, 通道={Components}",
                    i, tex.Name, tex.Width, tex.Height, tex.Format, tex.IsCompressed, tex.Components);
            }

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
        bool hasTexCoords = texCoords != null && texCoords.Count > 0;

        // 验证共享索引模式：顶点数量应该等于纹理坐标数量
        if (hasTexCoords && texCoords != null && vertices.Count != texCoords.Count)
        {
            _logger.LogWarning(
                "OSGB 模型纹理坐标数量({TexCount})与顶点数量({VertCount})不匹配，禁用纹理",
                texCoords.Count, vertices.Count);
            hasTexCoords = false;
            texCoords = null;
        }

        for (int i = 0; i < managedData.Indices.Length; i += 3)
        {
            int idxA = (int)managedData.Indices[i];
            int idxB = (int)managedData.Indices[i + 1];
            int idxC = (int)managedData.Indices[i + 2];

            // 获取当前面的材质索引（方案B修复）
            int materialIndex = 0;  // 默认值
            int faceIndex = i / 3;
            if (managedData.FaceMaterialIndices != null &&
                faceIndex < managedData.FaceMaterialIndices.Length)
            {
                int rawMaterialIndex = managedData.FaceMaterialIndices[faceIndex];
                // 确保材质索引有效（-1表示无材质，使用0作为默认值）
                materialIndex = rawMaterialIndex >= 0 ? rawMaterialIndex : 0;
            }

            if (hasTexCoords)
            {
                // OSGB 使用共享索引模式：顶点索引和纹理索引相同
                faces.Add(new Face(
                    idxA, idxB, idxC,
                    idxA, idxB, idxC,
                    materialIndex  // ✅ 使用正确的材质索引
                ));
            }
            else
            {
                // 无纹理时使用简单构造函数
                faces.Add(new Face(idxA, idxB, idxC));
            }
        }

        // 处理材质和纹理
        var materials = new List<Material>();
        if (managedData.Textures.Count > 0)
        {
            for (int i = 0; i < managedData.Textures.Count; i++)
            {
                var texture = managedData.Textures[i];

                // 根据纹理格式选择文件扩展名
                string textureExtension = texture.IsCompressed ? ".dds" : ".jpg";
                var texturePath = Path.Combine(directory, $"texture_{i}{textureExtension}");

                // 保存纹理到文件
                SaveTexture(texture, texturePath);

                // 验证纹理文件是否成功保存
                if (!File.Exists(texturePath))
                {
                    _logger.LogError("纹理文件保存失败: {Path}", texturePath);
                }
                else
                {
                    var fileInfo = new FileInfo(texturePath);
                    _logger.LogInformation("纹理文件已保存: {Path}, 大小={Size} bytes",
                        texturePath, fileInfo.Length);
                }

                // 获取材质数据
                RGB? diffuseColor = null;
                RGB? specularColor = null;
                double? shininess = null;
                string materialName = $"material_{i}";

                if (i < managedData.Materials.Count)
                {
                    var matData = managedData.Materials[i];
                    materialName = matData.Name;
                    diffuseColor = new RGB(matData.DiffuseR, matData.DiffuseG, matData.DiffuseB);
                    specularColor = new RGB(matData.SpecularR, matData.SpecularG, matData.SpecularB);
                    shininess = matData.Shininess;
                }

                // 创建材质（使用构造函数）
                var material = new Material(
                    name: materialName,
                    texture: texturePath,
                    normalMap: null,
                    ambientColor: null,
                    diffuseColor: diffuseColor,
                    specularColor: specularColor,
                    specularExponent: shininess,
                    dissolve: null,
                    illuminationModel: null
                );

                materials.Add(material);
            }
        }

        // 创建网格
        IMesh mesh;
        if (texCoords != null && texCoords.Count > 0 && materials.Count > 0)
        {
            // 创建 MeshT（带纹理）
            mesh = new MeshT(vertices, texCoords, faces, materials);
        }
        else
        {
            // 创建 Mesh（无纹理）
            mesh = new Mesh(vertices, faces);
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
            // 检查是否为压缩纹理格式（DXT1/3/5）
            if (texture.IsCompressed)
            {
                // 压缩纹理需要使用 OSG 原生保存功能
                // 通过 OsgbReaderWrapper.SaveTexture 保存
                using var reader = new OsgbReaderWrapper();
                bool success = reader.SaveTexture(texture, outputPath);

                if (!success)
                {
                    _logger.LogWarning("OSG 保存压缩纹理失败，尝试跳过: {Path} (格式: {Format})",
                        outputPath, texture.Format);
                    return;
                }

                _logger.LogDebug("压缩纹理已保存: {Path}, 格式: {Format}, 尺寸: {Width}x{Height}",
                    outputPath, texture.Format, texture.Width, texture.Height);
            }
            else
            {
                // 未压缩纹理：直接解码像素数据
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
                else if (texture.Components == 1)
                {
                    // Luminance 格式
                    var image = Image.LoadPixelData<L8>(texture.ImageData, texture.Width, texture.Height);
                    image.SaveAsJpeg(outputPath);
                    image.Dispose();
                }

                _logger.LogDebug("未压缩纹理已保存: {Path}, 尺寸: {Width}x{Height}, 通道: {Components}",
                    outputPath, texture.Width, texture.Height, texture.Components);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存纹理失败: {Path}, 格式: {Format}, 压缩: {IsCompressed}",
                outputPath, texture.Format, texture.IsCompressed);
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