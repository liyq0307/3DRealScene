using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Geometry;
using RealScene3D.Domain.Materials;
using SharpGLTF.Schema2;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Numerics;
using DomainMaterial = RealScene3D.Domain.Materials.Material;

namespace RealScene3D.Application.Services.Generators;

/// <summary>
/// GLTF生成器 - 基于SharpGLTF库重构
/// 生成标准glTF 2.0格式文件（GLB二进制格式）
/// 完整支持顶点位置、法线、纹理坐标和PBR材质
/// </summary>
public class GltfGenerator : TileGenerator
{
    private readonly TextureAtlasGenerator? _textureAtlasGenerator;

    /// <summary>
    /// 纹理优化配置
    /// </summary>
    public class TextureOptimizationOptions
    {
        /// <summary>
        /// 是否启用纹理压缩（默认启用）
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// JPEG质量（1-100，默认75）
        /// </summary>
        public int JpegQuality { get; set; } = 75;

        /// <summary>
        /// 最大纹理尺寸（默认2048）
        /// </summary>
        public int MaxTextureSize { get; set; } = 2048;

        /// <summary>
        /// 是否启用降采样（默认启用）
        /// </summary>
        public bool EnableDownsampling { get; set; } = true;
    }

    /// <summary>
    /// 纹理优化配置（可由外部设置）
    /// </summary>
    public TextureOptimizationOptions TextureOptions { get; set; } = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    public GltfGenerator(ILogger<GltfGenerator> logger, TextureAtlasGenerator? textureAtlasGenerator = null)
        : base(logger)
    {
        _textureAtlasGenerator = textureAtlasGenerator;
    }

    /// <summary>
    /// 生成瓦片文件数据 - 实现抽象方法
    /// 使用SharpGLTF生成GLB格式
    /// </summary>
    public override byte[] GenerateTile(MeshT mesh)
    {
        ValidateInput(mesh);
        _logger.LogDebug("开始生成GLB: 三角形数={Count}", mesh.Faces.Count);

        try
        {
            // 创建glTF模型
            var model = ModelRoot.CreateModel();
            var scene = model.UseScene("default");
            var node = scene.CreateNode("mesh");

            // 按材质分组面
            var facesByMaterial = GroupFacesByMaterial(mesh);
            _logger.LogDebug("按材质分组: {GroupCount}个组", facesByMaterial.Count);

            // 为每个材质组创建primitive
            var meshBuilder = new MeshBuilder<VertexPositionNormalTexture1>("mesh");

            foreach (var (materialIndex, groupFaces) in facesByMaterial)
            {
                // 获取或创建材质
                MaterialBuilder materialBuilder;
                if (materialIndex >= 0 && materialIndex < mesh.Materials.Count)
                {
                    var meshMaterial = mesh.Materials[materialIndex];
                    materialBuilder = CreateMaterial(model, meshMaterial);
                }
                else
                {
                    materialBuilder = CreateDefaultMaterial(model);
                }

                // 创建primitive
                var primitiveBuilder = meshBuilder.UsePrimitive(materialBuilder);

                // 添加三角形
                foreach (var face in groupFaces)
                {
                    var v0 = CreateVertex(mesh, face.IndexA, face.TextureIndexA);
                    var v1 = CreateVertex(mesh, face.IndexB, face.TextureIndexB);
                    var v2 = CreateVertex(mesh, face.IndexC, face.TextureIndexC);

                    primitiveBuilder.AddTriangle(v0, v1, v2);
                }

                _logger.LogDebug("材质组: 材质索引={Index}, {FaceCount}个面",
                    materialIndex, groupFaces.Count);
            }

            // 添加到场景
            node.Mesh = model.CreateMesh(meshBuilder);

            // 写入GLB
            using var ms = new MemoryStream();
            var writeSettings = new WriteSettings
            {
                JsonIndented = false,
                ImageWriting = ResourceWriteMode.Embedded,
                MergeBuffers = true
            };
            model.SaveGLB(ms, writeSettings);

            var result = ms.ToArray();
            _logger.LogInformation("GLB生成完成: 总大小={Size}字节", result.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成GLB失败");
            throw;
        }
    }

    /// <summary>
    /// 按材质分组面
    /// </summary>
    private Dictionary<int, List<FaceT>> GroupFacesByMaterial(MeshT mesh)
    {
        var groups = new Dictionary<int, List<FaceT>>();
        const int NoMaterialIndex = -1;

        foreach (var face in mesh.Faces)
        {
            var materialIndex = face.MaterialIndex;

            // 检查材质索引是否有效
            if (materialIndex < 0 || materialIndex >= mesh.Materials.Count)
            {
                materialIndex = NoMaterialIndex;
            }

            if (!groups.ContainsKey(materialIndex))
            {
                groups[materialIndex] = new List<FaceT>();
            }

            groups[materialIndex].Add(face);
        }

        return groups;
    }

    /// <summary>
    /// 创建顶点（包含位置、法线、纹理坐标）
    /// </summary>
    private VertexPositionNormalTexture1 CreateVertex(MeshT mesh, int vertexIndex, int texCoordIndex)
    {
        // 位置
        var pos = mesh.Vertices[vertexIndex];
        var position = new Vector3((float)pos.X, (float)pos.Y, (float)pos.Z);

        // 法线（暂时使用默认值，可以从mesh中计算）
        var normal = new Vector3(0, 0, 1);

        // 纹理坐标
        Vector2 texCoord = Vector2.Zero;
        if (texCoordIndex >= 0 && texCoordIndex < mesh.TextureVertices.Count)
        {
            var uv = mesh.TextureVertices[texCoordIndex];
            texCoord = new Vector2((float)uv.U, (float)uv.V);
        }

        return new VertexPositionNormalTexture1(position, normal, texCoord);
    }

    /// <summary>
    /// 创建材质
    /// </summary>
    private MaterialBuilder CreateMaterial(ModelRoot model, DomainMaterial meshMaterial)
    {
        var materialBuilder = new MaterialBuilder(meshMaterial.Name ?? "Material")
            .WithMetallicRoughnessShader();

        // 设置基础颜色
        if (meshMaterial.DiffuseColor != null)
        {
            var color = new Vector4(
                (float)meshMaterial.DiffuseColor.R,
                (float)meshMaterial.DiffuseColor.G,
                (float)meshMaterial.DiffuseColor.B,
                (float)meshMaterial.Opacity
            );
            materialBuilder.WithChannelParam("BaseColor", color);
        }

        // 设置金属度和粗糙度
        materialBuilder.WithMetallicFactor(0.0f);
        materialBuilder.WithRoughnessFactor(1.0f);

        // 漫反射纹理
        if (meshMaterial.DiffuseTexture != null && !string.IsNullOrEmpty(meshMaterial.DiffuseTexture.FilePath))
        {
            var texturePath = meshMaterial.DiffuseTexture.FilePath;
            if (File.Exists(texturePath))
            {
                try
                {
                    if (TextureOptions.EnableCompression)
                    {
                        // 使用压缩纹理
                        var (imageData, _) = CompressAndResizeTexture(
                            texturePath,
                            TextureOptions.MaxTextureSize,
                            TextureOptions.JpegQuality);

                        var imageBuilder = ImageBuilder.From(imageData, texturePath);
                        materialBuilder.WithChannelImage("BaseColor", imageBuilder);
                    }
                    else
                    {
                        // 直接加载
                        var imageBuilder = ImageBuilder.From(texturePath);
                        materialBuilder.WithChannelImage("BaseColor", imageBuilder);
                    }

                    _logger.LogDebug("材质 {Name} 添加漫反射纹理: {Path}",
                        meshMaterial.Name, texturePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "加载纹理失败: {Path}", texturePath);
                }
            }
        }

        // 法线纹理
        if (meshMaterial.NormalTexture != null && !string.IsNullOrEmpty(meshMaterial.NormalTexture.FilePath))
        {
            var texturePath = meshMaterial.NormalTexture.FilePath;
            if (File.Exists(texturePath))
            {
                try
                {
                    var imageBuilder = ImageBuilder.From(texturePath);
                    materialBuilder.WithChannelImage("Normal", imageBuilder);
                    _logger.LogDebug("材质 {Name} 添加法线纹理: {Path}",
                        meshMaterial.Name, texturePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "加载法线纹理失败: {Path}", texturePath);
                }
            }
        }

        // 透明度
        if (meshMaterial.Opacity < 1.0)
        {
            materialBuilder.WithAlpha(meshMaterial.Opacity < 0.5 ? AlphaMode.MASK : AlphaMode.BLEND);
        }

        return materialBuilder;
    }

    /// <summary>
    /// 创建默认材质
    /// </summary>
    private MaterialBuilder CreateDefaultMaterial(ModelRoot model)
    {
        return new MaterialBuilder("default")
            .WithMetallicRoughnessShader()
            .WithMetallicFactor(0.0f)
            .WithRoughnessFactor(1.0f)
            .WithChannelParam("BaseColor", Vector4.One);
    }

    /// <summary>
    /// 压缩和调整纹理大小
    /// </summary>
    private (byte[] data, string mimeType) CompressAndResizeTexture(
        string texturePath,
        int maxSize,
        int jpegQuality)
    {
        try
        {
            using var image = Image.Load<Rgba32>(texturePath);
            var originalSize = new FileInfo(texturePath).Length;

            // 检查是否需要降采样
            bool needsResize = TextureOptions.EnableDownsampling &&
                              (image.Width > maxSize || image.Height > maxSize);

            if (needsResize)
            {
                double scale = Math.Min((double)maxSize / image.Width, (double)maxSize / image.Height);
                int newWidth = (int)(image.Width * scale);
                int newHeight = (int)(image.Height * scale);

                image.Mutate(x => x.Resize(newWidth, newHeight));

                _logger.LogDebug("纹理降采样: {Path} 从 {OldW}x{OldH} 到 {NewW}x{NewH}",
                    Path.GetFileName(texturePath), image.Width, image.Height, newWidth, newHeight);
            }

            // 检查是否有透明通道
            bool hasAlpha = HasTransparency(image);

            using var ms = new MemoryStream();
            string mimeType;

            if (hasAlpha)
            {
                image.SaveAsPng(ms);
                mimeType = "image/png";
            }
            else
            {
                var encoder = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
                {
                    Quality = jpegQuality
                };
                image.SaveAsJpeg(ms, encoder);
                mimeType = "image/jpeg";
            }

            var compressedData = ms.ToArray();
            var compressionRatio = (1.0 - (double)compressedData.Length / originalSize) * 100;

            _logger.LogDebug("纹理压缩: {Path} {OriginalSize}KB -> {CompressedSize}KB (减少{Ratio:F1}%, 格式={Format})",
                Path.GetFileName(texturePath),
                originalSize / 1024,
                compressedData.Length / 1024,
                compressionRatio,
                hasAlpha ? "PNG" : "JPEG");

            return (compressedData, mimeType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "纹理压缩失败，使用原始文件: {Path}", texturePath);
            var data = File.ReadAllBytes(texturePath);
            var ext = Path.GetExtension(texturePath).ToLowerInvariant();
            var mimeType = ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => "image/png"
            };
            return (data, mimeType);
        }
    }

    /// <summary>
    /// 检查图像是否有透明像素
    /// </summary>
    private bool HasTransparency(Image<Rgba32> image)
    {
        int step = Math.Max(1, Math.Min(image.Width, image.Height) / 50);

        for (int y = 0; y < image.Height; y += step)
        {
            for (int x = 0; x < image.Width; x += step)
            {
                if (image[x, y].A < 255)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 获取瓦片格式名称
    /// </summary>
    protected override string GetFormatName() => "GLTF";

    /// <summary>
    /// 保存瓦片文件到磁盘
    /// </summary>
    public override async Task SaveTileAsync(MeshT mesh, string outputPath)
    {
        await SaveGLBFileAsync(mesh, outputPath);
    }

    /// <summary>
    /// 保存GLB文件到磁盘
    /// </summary>
    public async Task SaveGLBFileAsync(MeshT mesh, string outputPath)
    {
        _logger.LogInformation("保存GLB文件: {Path}", outputPath);

        try
        {
            var glbData = GenerateTile(mesh);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(outputPath, glbData);

            _logger.LogInformation("GLB文件保存成功: {Path}, 大小={Size}字节", outputPath, glbData.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存GLB文件失败: {Path}", outputPath);
            throw;
        }
    }

    /// <summary>
    /// 生成纹理图集并更新材质的UV映射
    /// </summary>
    public async Task<(List<DomainMaterial> UpdatedMaterials, TextureAtlasGenerator.AtlasResult? AtlasResult)>
        GenerateTextureAtlasAsync(
            IReadOnlyList<DomainMaterial> materials,
            string? atlasOutputPath = null,
            double downsampleFactor = 1.0,
            bool useJpegCompression = true)
    {
        if (_textureAtlasGenerator == null)
        {
            _logger.LogWarning("TextureAtlasGenerator未注入，跳过纹理图集生成");
            return (new List<DomainMaterial>(materials), null);
        }

        if (materials == null || materials.Count == 0)
        {
            return (new List<DomainMaterial>(), null);
        }

        // 收集所有纹理路径
        var texturePaths = new HashSet<string>();
        foreach (var material in materials)
        {
            foreach (var texture in material.GetAllTextures())
            {
                if (!string.IsNullOrEmpty(texture.FilePath) && File.Exists(texture.FilePath))
                {
                    texturePaths.Add(texture.FilePath);
                }
            }
        }

        if (texturePaths.Count == 0)
        {
            _logger.LogInformation("没有找到有效的纹理文件，跳过图集生成");
            return (new List<Material>(materials), null);
        }

        _logger.LogInformation("开始生成纹理图集: {Count}张纹理", texturePaths.Count);

        // 生成纹理图集
        var atlasResult = await _textureAtlasGenerator.GenerateAtlasAsync(
            texturePaths,
            maxAtlasSize: 4096,
            padding: 2,
            downsampleFactor: downsampleFactor,
            useJpegCompression: useJpegCompression);

        // 保存图集文件
        if (!string.IsNullOrEmpty(atlasOutputPath))
        {
            var directory = Path.GetDirectoryName(atlasOutputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(atlasOutputPath, atlasResult.ImageData);
            _logger.LogInformation("纹理图集已保存: {Path}", atlasOutputPath);
        }

        // 更新材质UV映射
        var updatedMaterials = materials.Select(m => CloneMaterialWithAtlasInfo(m, atlasResult)).ToList();

        return (updatedMaterials, atlasResult);
    }

    /// <summary>
    /// 克隆材质并更新图集信息
    /// </summary>
    private DomainMaterial CloneMaterialWithAtlasInfo(DomainMaterial source, TextureAtlasGenerator.AtlasResult atlasResult)
    {
        var cloned = new DomainMaterial
        {
            Name = source.Name,
            AmbientColor = source.AmbientColor,
            DiffuseColor = source.DiffuseColor,
            SpecularColor = source.SpecularColor,
            EmissiveColor = source.EmissiveColor,
            Shininess = source.Shininess,
            Opacity = source.Opacity,
            RefractiveIndex = source.RefractiveIndex,
            DiffuseTexture = CloneTextureWithAtlas(source.DiffuseTexture, atlasResult),
            NormalTexture = CloneTextureWithAtlas(source.NormalTexture, atlasResult),
            SpecularTexture = CloneTextureWithAtlas(source.SpecularTexture, atlasResult),
            EmissiveTexture = CloneTextureWithAtlas(source.EmissiveTexture, atlasResult),
            OpacityTexture = CloneTextureWithAtlas(source.OpacityTexture, atlasResult),
            MetallicTexture = CloneTextureWithAtlas(source.MetallicTexture, atlasResult),
            RoughnessTexture = CloneTextureWithAtlas(source.RoughnessTexture, atlasResult)
        };

        return cloned;
    }

    /// <summary>
    /// 克隆纹理并设置图集信息
    /// </summary>
    private TextureInfo? CloneTextureWithAtlas(TextureInfo? source, TextureAtlasGenerator.AtlasResult atlasResult)
    {
        if (source == null) return null;

        var cloned = new TextureInfo
        {
            Type = source.Type,
            FilePath = source.FilePath,
            WrapU = source.WrapU,
            WrapV = source.WrapV,
            FilterMode = source.FilterMode
        };

        // 更新图集UV信息
        if (!string.IsNullOrEmpty(source.FilePath) &&
            atlasResult.TextureRegions.TryGetValue(source.FilePath, out var region))
        {
            cloned.AtlasOffset = region.UVMin;
            cloned.AtlasScale = new Vector2D(
                region.UVMax.U - region.UVMin.U,
                region.UVMax.V - region.UVMin.V
            );
        }

        return cloned;
    }
}
