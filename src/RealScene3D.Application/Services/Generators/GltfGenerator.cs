using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Entities;
using System.Text;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RealScene3D.Application.Services.Generators;

/// <summary>
/// GLTF生成器 - 生成标准glTF 2.0格式文件
/// 支持GLB（二进制格式）和GLTF（JSON+外部资源）两种输出方式
/// 完整支持顶点位置、法线和纹理坐标
/// 参考: glTF 2.0 Specification
/// 适用场景：独立3D模型导出、跨平台3D资源交换
/// </summary>
public class GltfGenerator : TileGenerator
{
    private readonly TextureAtlasGenerator? _textureAtlasGenerator;
    // 缓存合并后的metallicRoughness纹理，避免重复生成
    private readonly Dictionary<string, byte[]> _metallicRoughnessCombinedCache = new();

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
    /// <param name="logger">日志记录器</param>
    /// <param name="textureAtlasGenerator">纹理图集生成器（可选）</param>
    public GltfGenerator(ILogger<GltfGenerator> logger, TextureAtlasGenerator? textureAtlasGenerator = null) : base(logger)
    {
        _textureAtlasGenerator = textureAtlasGenerator;
    }

    /// <summary>
    /// 生成瓦片文件数据 - 实现抽象方法
    /// 默认生成GLB格式
    /// </summary>
    /// <param name="triangles">三角形网格数据</param>
    /// <param name="bounds">空间包围盒</param>
    /// <param name="materials">材质字典</param>
    /// <returns>GLB文件的二进制数据</returns>
    public override byte[] GenerateTile(List<Triangle> triangles, BoundingBox3D bounds, Dictionary<string, Material>? materials = null)
    {
        return GenerateGLB(triangles, bounds, materials);
    }

    /// <summary>
    /// 生成GLB格式数据
    /// GLB格式: Header + JSON Chunk + Binary Chunk
    /// 支持POSITION, NORMAL, TEXCOORD_0属性
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="bounds">包围盒</param>
    /// <param name="materials">材质字典</param>
    /// <returns>GLB文件的二进制数据</returns>
    public byte[] GenerateGLB(List<Triangle> triangles, BoundingBox3D bounds, Dictionary<string, Material>? materials = null)
    {
        ValidateInput(triangles, bounds);

        _logger.LogDebug("开始生成GLB: 三角形数={Count}", triangles.Count);

        try
        {
            // 按材质分组三角形
            var trianglesByMaterial = GroupTrianglesByMaterial(triangles, materials);
            _logger.LogDebug("按材质分组: {GroupCount}个组", trianglesByMaterial.Count);

            // 为每个材质组提取顶点数据和构建 primitive
            var allPrimitives = new List<PrimitiveData>();
            var allBufferData = new List<byte>();
            int currentBufferOffset = 0;

            foreach (var (materialName, groupTriangles) in trianglesByMaterial)
            {
                var primitiveData = ExtractPrimitiveData(groupTriangles, materialName, currentBufferOffset);
                allPrimitives.Add(primitiveData);

                // 添加到总缓冲区
                allBufferData.AddRange(primitiveData.BufferData);
                currentBufferOffset += primitiveData.BufferData.Length;

                var displayName = materialName == "__no_material__" ? "(无材质)" : materialName;
                _logger.LogDebug("材质组 '{Material}': {TriCount}个三角形, {VertCount}个顶点",
                    displayName, groupTriangles.Count, primitiveData.VertexCount);
            }

            // 收集并嵌入纹理到缓冲区
            var embeddedImages = new List<EmbeddedImageInfo>();
            if (materials != null)
            {
                foreach (var material in materials.Values)
                {
                    EmbedMaterialTextures(material, allBufferData, embeddedImages, ref currentBufferOffset);
                }
            }

            // 转换为字节数组
            var binaryData = allBufferData.ToArray();

            // 创建 glTF JSON（包含多个 primitive 和嵌入纹理）
            var gltfJson = CreateGltfJsonWithPrimitives(allPrimitives, binaryData.Length, materials, embeddedImages);
            var gltfJsonBytes = Encoding.UTF8.GetBytes(gltfJson);
            var gltfJsonPadded = PadTo4ByteBoundary(gltfJsonBytes);

            // Binary Chunk需要对齐
            var binaryDataPadded = PadTo4ByteBoundary(binaryData);

            // 构建GLB
            int headerLength = 12;
            int jsonChunkLength = 8 + gltfJsonPadded.Length;
            int binaryChunkLength = 8 + binaryDataPadded.Length;
            int totalLength = headerLength + jsonChunkLength + binaryChunkLength;

            using var ms = new MemoryStream(totalLength);
            using var writer = new BinaryWriter(ms);

            // GLB Header (12 bytes)
            writer.Write((uint)0x46546C67);           // magic "glTF" (4 bytes)
            writer.Write((uint)2);                    // version 2 (4 bytes)
            writer.Write((uint)totalLength);          // length (4 bytes)

            // JSON Chunk
            writer.Write((uint)gltfJsonPadded.Length); // chunkLength (4 bytes)
            writer.Write((uint)0x4E4F534A);            // chunkType "JSON" (4 bytes)
            writer.Write(gltfJsonPadded);              // chunkData

            // Binary Chunk
            writer.Write((uint)binaryDataPadded.Length); // chunkLength (4 bytes)
            writer.Write((uint)0x004E4942);              // chunkType "BIN\0" (4 bytes)
            writer.Write(binaryDataPadded);              // chunkData

            var result = ms.ToArray();
            _logger.LogInformation("GLB生成完成: {PrimitiveCount}个Primitive, {ImageCount}个嵌入纹理, 总大小={Size}字节",
                allPrimitives.Count, embeddedImages.Count, result.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成GLB失败");
            throw;
        }
    }

    /// <summary>
    /// 按材质分组三角形
    /// </summary>
    private Dictionary<string, List<Triangle>> GroupTrianglesByMaterial(
        List<Triangle> triangles,
        Dictionary<string, Material>? materials)
    {
        var groups = new Dictionary<string, List<Triangle>>();
        const string NoMaterialKey = "__no_material__";

        foreach (var triangle in triangles)
        {
            var materialName = triangle.MaterialName;

            // 如果材质名为空或材质不存在于材质字典中，归为无材质组
            if (string.IsNullOrEmpty(materialName))
            {
                materialName = NoMaterialKey;
            }
            else if (materials != null && !materials.ContainsKey(materialName))
            {
                materialName = NoMaterialKey;
            }

            if (!groups.TryGetValue(materialName, out var group))
            {
                group = new List<Triangle>();
                groups[materialName] = group;
            }

            group.Add(triangle);
        }

        return groups;
    }

    /// <summary>
    /// 为单个材质组提取 Primitive 数据
    /// </summary>
    private PrimitiveData ExtractPrimitiveData(List<Triangle> triangles, string materialName, int bufferOffset)
    {
        var positions = new List<float>();
        var normals = new List<float>();
        var texCoords = new List<float>();
        var indices = new List<ushort>();

        bool hasNormals = triangles.Any(t => t.HasVertexNormals());
        bool hasTexCoords = triangles.Any(t => t.HasUVCoordinates());

        ushort vertexIndex = 0;
        foreach (var triangle in triangles)
        {
            var vertices = new[] {
                (triangle.V1, triangle.Normal1, triangle.UV1),
                (triangle.V2, triangle.Normal2, triangle.UV2),
                (triangle.V3, triangle.Normal3, triangle.UV3)
            };

            foreach (var (vertex, normal, uv) in vertices)
            {
                positions.Add((float)vertex.X);
                positions.Add((float)vertex.Y);
                positions.Add((float)vertex.Z);

                if (hasNormals && normal != null)
                {
                    normals.Add((float)normal.X);
                    normals.Add((float)normal.Y);
                    normals.Add((float)normal.Z);
                }

                if (hasTexCoords)
                {
                    if (uv != null)
                    {
                        texCoords.Add((float)uv.U);
                        texCoords.Add((float)uv.V);
                    }
                    else
                    {
                        texCoords.Add(0f);
                        texCoords.Add(0f);
                    }
                }

                indices.Add(vertexIndex++);
            }
        }

        // 计算 min/max
        var (posMin, posMax) = CalculateMinMax(positions.ToArray(), 3);

        // 创建缓冲区数据
        using var bufferStream = new MemoryStream();
        using var writer = new BinaryWriter(bufferStream);

        int positionOffset = 0;
        int positionLength = positions.Count * sizeof(float);
        foreach (var p in positions) writer.Write(p);

        int normalOffset = positionLength;
        int normalLength = normals.Count * sizeof(float);
        foreach (var n in normals) writer.Write(n);

        int texCoordOffset = normalOffset + normalLength;
        int texCoordLength = texCoords.Count * sizeof(float);
        foreach (var t in texCoords) writer.Write(t);

        // 索引需要 4 字节对齐
        int currentPos = positionLength + normalLength + texCoordLength;
        int padding = (4 - (currentPos % 4)) % 4;
        for (int i = 0; i < padding; i++) writer.Write((byte)0);

        int indexOffset = currentPos + padding;
        int indexLength = indices.Count * sizeof(ushort);
        foreach (var idx in indices) writer.Write(idx);

        return new PrimitiveData
        {
            MaterialName = materialName,
            VertexCount = vertexIndex,
            IndexCount = indices.Count,
            HasNormals = hasNormals,
            HasTexCoords = hasTexCoords,
            PositionMin = posMin,
            PositionMax = posMax,
            BufferData = bufferStream.ToArray(),
            BufferOffset = bufferOffset,
            PositionBufferViewOffset = positionOffset,
            PositionBufferViewLength = positionLength,
            NormalBufferViewOffset = normalOffset,
            NormalBufferViewLength = normalLength,
            TexCoordBufferViewOffset = texCoordOffset,
            TexCoordBufferViewLength = texCoordLength,
            IndexBufferViewOffset = indexOffset,
            IndexBufferViewLength = indexLength
        };
    }

    /// <summary>
    /// 将材质纹理嵌入到缓冲区中
    /// 增加纹理压缩和优化以减小文件大小
    /// </summary>
    private void EmbedMaterialTextures(
        Material material,
        List<byte> bufferData,
        List<EmbeddedImageInfo> embeddedImages,
        ref int currentOffset)
    {
        var textures = material.GetAllTextures().ToList();
        _logger.LogInformation("EmbedMaterialTextures: 材质 '{Name}' 有 {Count} 个纹理", material.Name, textures.Count);

        foreach (var texture in textures)
        {
            _logger.LogDebug("  检查纹理: Type={Type}, Path={Path}", texture.Type, texture.FilePath ?? "(null)");

            if (string.IsNullOrEmpty(texture.FilePath))
            {
                _logger.LogWarning("  ✗ 跳过: 纹理路径为空");
                continue;
            }

            if (!File.Exists(texture.FilePath))
            {
                _logger.LogWarning("  ✗ 跳过: 文件不存在 - {Path}", texture.FilePath);
                continue;
            }

            var fileInfo = new FileInfo(texture.FilePath);
            _logger.LogInformation("  ✓ 发现纹理文件: {Path}, 大小={Size:F2} MB",
                Path.GetFileName(texture.FilePath), fileInfo.Length / 1024.0 / 1024.0);

            // 检查是否已经嵌入过这个纹理
            if (embeddedImages.Any(i => i.OriginalPath == texture.FilePath))
            {
                _logger.LogDebug("  - 已嵌入过，跳过");
                continue;
            }

            try
            {
                // 加载并压缩纹理
                byte[] imageData;
                string mimeType;

                _logger.LogDebug("  压缩设置: EnableCompression={Enable}, MaxSize={MaxSize}, Quality={Quality}",
                    TextureOptions.EnableCompression, TextureOptions.MaxTextureSize, TextureOptions.JpegQuality);

                if (TextureOptions.EnableCompression)
                {
                    // 使用压缩和降采样
                    (imageData, mimeType) = CompressAndResizeTexture(
                        texture.FilePath,
                        TextureOptions.MaxTextureSize,
                        TextureOptions.JpegQuality);
                    _logger.LogInformation("  ✓ 压缩完成: {OriginalSize:F2}MB → {CompressedSize:F2}MB ({Ratio:F1}%)",
                        fileInfo.Length / 1024.0 / 1024.0,
                        imageData.Length / 1024.0 / 1024.0,
                        (1.0 - (double)imageData.Length / fileInfo.Length) * 100);
                }
                else
                {
                    // 不压缩，直接读取
                    imageData = File.ReadAllBytes(texture.FilePath);
                    var ext = Path.GetExtension(texture.FilePath).ToLowerInvariant();
                    mimeType = ext switch
                    {
                        ".png" => "image/png",
                        ".jpg" or ".jpeg" => "image/jpeg",
                        _ => "image/png"
                    };
                    _logger.LogInformation("  ✓ 直接读取（未压缩）: {Size:F2}MB", imageData.Length / 1024.0 / 1024.0);
                }

                // 4 字节对齐
                int padding = (4 - (currentOffset % 4)) % 4;
                for (int i = 0; i < padding; i++)
                {
                    bufferData.Add(0);
                    currentOffset++;
                }

                int imageOffset = currentOffset;
                bufferData.AddRange(imageData);
                currentOffset += imageData.Length;

                embeddedImages.Add(new EmbeddedImageInfo
                {
                    OriginalPath = texture.FilePath,
                    BufferViewOffset = imageOffset,
                    BufferViewLength = imageData.Length,
                    MimeType = mimeType
                });

                _logger.LogDebug("嵌入纹理: {Path}, 压缩后{Size}KB (压缩={Compressed})",
                    texture.FilePath, imageData.Length / 1024, TextureOptions.EnableCompression);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "嵌入纹理失败: {Path}", texture.FilePath);
            }
        }
    }

    /// <summary>
    /// 压缩和调整纹理大小
    /// 使用JPEG压缩和降采样来显著减小纹理大小
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
                // 保持宽高比缩放
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
                // 有透明通道使用PNG（但仍可压缩）
                image.SaveAsPng(ms);
                mimeType = "image/png";
            }
            else
            {
                // 无透明通道使用JPEG压缩
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
            // 回退到原始文件
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
        // 采样检查（性能优化：不检查每个像素）
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
    /// 创建包含多个 Primitive 和嵌入纹理的 glTF JSON
    /// </summary>
    private string CreateGltfJsonWithPrimitives(
        List<PrimitiveData> primitives,
        int totalBufferLength,
        Dictionary<string, Material>? materials,
        List<EmbeddedImageInfo> embeddedImages)
    {
        var accessorsList = new List<object>();
        var bufferViewsList = new List<object>();
        var primitivesList = new List<object>();

        // 创建材质名到索引的映射
        var materialIndexMap = new Dictionary<string, int>();
        if (materials != null)
        {
            int idx = 0;
            foreach (var name in materials.Keys)
            {
                materialIndexMap[name] = idx++;
            }
        }

        // 为每个 Primitive 创建 accessor 和 bufferView
        int accessorIndex = 0;
        int bufferViewIndex = 0;

        foreach (var primitive in primitives)
        {
            int baseOffset = primitive.BufferOffset;

            // Position BufferView
            int positionBufferViewIndex = bufferViewIndex++;
            bufferViewsList.Add(new
            {
                buffer = 0,
                byteOffset = baseOffset + primitive.PositionBufferViewOffset,
                byteLength = primitive.PositionBufferViewLength,
                target = 34962
            });

            // Position Accessor
            int positionAccessorIndex = accessorIndex++;
            accessorsList.Add(new
            {
                bufferView = positionBufferViewIndex,
                componentType = 5126,
                count = primitive.VertexCount,
                type = "VEC3",
                max = primitive.PositionMax.Select(v => (double)v).ToArray(),
                min = primitive.PositionMin.Select(v => (double)v).ToArray()
            });

            var attributesDict = new Dictionary<string, int> { { "POSITION", positionAccessorIndex } };

            // Normal
            if (primitive.HasNormals && primitive.NormalBufferViewLength > 0)
            {
                int normalBufferViewIndex = bufferViewIndex++;
                bufferViewsList.Add(new
                {
                    buffer = 0,
                    byteOffset = baseOffset + primitive.NormalBufferViewOffset,
                    byteLength = primitive.NormalBufferViewLength,
                    target = 34962
                });

                int normalAccessorIndex = accessorIndex++;
                accessorsList.Add(new
                {
                    bufferView = normalBufferViewIndex,
                    componentType = 5126,
                    count = primitive.VertexCount,
                    type = "VEC3"
                });
                attributesDict["NORMAL"] = normalAccessorIndex;
            }

            // TexCoord
            if (primitive.HasTexCoords && primitive.TexCoordBufferViewLength > 0)
            {
                int texCoordBufferViewIndex = bufferViewIndex++;
                bufferViewsList.Add(new
                {
                    buffer = 0,
                    byteOffset = baseOffset + primitive.TexCoordBufferViewOffset,
                    byteLength = primitive.TexCoordBufferViewLength,
                    target = 34962
                });

                int texCoordAccessorIndex = accessorIndex++;
                accessorsList.Add(new
                {
                    bufferView = texCoordBufferViewIndex,
                    componentType = 5126,
                    count = primitive.VertexCount,
                    type = "VEC2"
                });
                attributesDict["TEXCOORD_0"] = texCoordAccessorIndex;
            }

            // Indices
            int indexBufferViewIndex = bufferViewIndex++;
            bufferViewsList.Add(new
            {
                buffer = 0,
                byteOffset = baseOffset + primitive.IndexBufferViewOffset,
                byteLength = primitive.IndexBufferViewLength,
                target = 34963
            });

            int indexAccessorIndex = accessorIndex++;
            accessorsList.Add(new
            {
                bufferView = indexBufferViewIndex,
                componentType = 5123,
                count = primitive.IndexCount,
                type = "SCALAR"
            });

            // 创建 Primitive 对象
            var primitiveObj = new Dictionary<string, object>
            {
                { "attributes", attributesDict },
                { "indices", indexAccessorIndex },
                { "mode", 4 }
            };

            // 设置材质引用（跳过无材质组）
            const string NoMaterialKey = "__no_material__";
            if (primitive.MaterialName != NoMaterialKey && materialIndexMap.TryGetValue(primitive.MaterialName, out var matIdx))
            {
                primitiveObj["material"] = matIdx;
            }

            primitivesList.Add(primitiveObj);
        }

        // 为嵌入的图像创建 BufferView
        var imageBufferViewIndices = new Dictionary<string, int>();
        foreach (var img in embeddedImages)
        {
            int imgBufferViewIndex = bufferViewIndex++;
            bufferViewsList.Add(new
            {
                buffer = 0,
                byteOffset = img.BufferViewOffset,
                byteLength = img.BufferViewLength
            });
            imageBufferViewIndices[img.OriginalPath] = imgBufferViewIndex;
        }

        // 构建材质、纹理、图像数组
        var materialsList = new List<object>();
        var texturesList = new List<object>();
        var imagesList = new List<object>();
        var samplersList = new List<object>();

        if (materials != null && materials.Count > 0)
        {
            // 添加默认采样器
            samplersList.Add(new
            {
                magFilter = 9729,
                minFilter = 9987,
                wrapS = 10497,
                wrapT = 10497
            });

            var textureIndexMap = new Dictionary<string, int>();
            int textureIndex = 0;

            foreach (var (materialName, material) in materials)
            {
                var pbrDict = new Dictionary<string, object>();

                // 基础颜色
                if (material.DiffuseColor != null)
                {
                    pbrDict["baseColorFactor"] = new[] {
                        material.DiffuseColor.R,
                        material.DiffuseColor.G,
                        material.DiffuseColor.B,
                        material.Opacity
                    };
                }

                // 漫反射纹理
                if (material.DiffuseTexture != null && !string.IsNullOrEmpty(material.DiffuseTexture.FilePath))
                {
                    var texPath = material.DiffuseTexture.FilePath;
                    if (!textureIndexMap.TryGetValue(texPath, out var texIdx))
                    {
                        // 创建图像引用
                        int imageIndex = imagesList.Count;
                        if (imageBufferViewIndices.TryGetValue(texPath, out var imgBvIdx))
                        {
                            var imgInfo = embeddedImages.First(i => i.OriginalPath == texPath);
                            imagesList.Add(new
                            {
                                bufferView = imgBvIdx,
                                mimeType = imgInfo.MimeType
                            });
                        }
                        else
                        {
                            // 外部引用（fallback）
                            imagesList.Add(new { uri = Path.GetFileName(texPath) });
                        }

                        // 创建纹理引用
                        texturesList.Add(new
                        {
                            source = imageIndex,
                            sampler = 0
                        });

                        texIdx = textureIndex++;
                        textureIndexMap[texPath] = texIdx;
                    }

                    pbrDict["baseColorTexture"] = new { index = texIdx };
                }

                pbrDict["metallicFactor"] = 0.0;
                pbrDict["roughnessFactor"] = 1.0;

                var materialObj = new Dictionary<string, object>
                {
                    { "name", materialName },
                    { "pbrMetallicRoughness", pbrDict }
                };

                if (material.Opacity < 1.0)
                {
                    materialObj["alphaMode"] = "BLEND";
                }

                materialsList.Add(materialObj);
            }
        }

        // 构建 glTF 对象
        var gltfDict = new Dictionary<string, object>
        {
            { "asset", new { version = "2.0", generator = "RealScene3D.GltfGenerator" } },
            { "scene", 0 },
            { "scenes", new[] { new { nodes = new[] { 0 } } } },
            { "nodes", new[] { new { mesh = 0 } } },
            { "meshes", new[] { new { primitives = primitivesList.ToArray() } } },
            { "accessors", accessorsList.ToArray() },
            { "bufferViews", bufferViewsList.ToArray() },
            { "buffers", new[] { new { byteLength = totalBufferLength } } }
        };

        if (materialsList.Count > 0)
            gltfDict["materials"] = materialsList.ToArray();
        if (texturesList.Count > 0)
            gltfDict["textures"] = texturesList.ToArray();
        if (imagesList.Count > 0)
            gltfDict["images"] = imagesList.ToArray();
        if (samplersList.Count > 0)
            gltfDict["samplers"] = samplersList.ToArray();

        return JsonSerializer.Serialize(gltfDict, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Primitive 数据结构
    /// </summary>
    private class PrimitiveData
    {
        public string MaterialName { get; set; } = string.Empty;
        public int VertexCount { get; set; }
        public int IndexCount { get; set; }
        public bool HasNormals { get; set; }
        public bool HasTexCoords { get; set; }
        public float[] PositionMin { get; set; } = Array.Empty<float>();
        public float[] PositionMax { get; set; } = Array.Empty<float>();
        public byte[] BufferData { get; set; } = Array.Empty<byte>();
        public int BufferOffset { get; set; }
        public int PositionBufferViewOffset { get; set; }
        public int PositionBufferViewLength { get; set; }
        public int NormalBufferViewOffset { get; set; }
        public int NormalBufferViewLength { get; set; }
        public int TexCoordBufferViewOffset { get; set; }
        public int TexCoordBufferViewLength { get; set; }
        public int IndexBufferViewOffset { get; set; }
        public int IndexBufferViewLength { get; set; }
    }

    /// <summary>
    /// 嵌入图像信息
    /// </summary>
    private class EmbeddedImageInfo
    {
        public string OriginalPath { get; set; } = string.Empty;
        public int BufferViewOffset { get; set; }
        public int BufferViewLength { get; set; }
        public string MimeType { get; set; } = "image/png";
    }

    /// <summary>
    /// 生成GLTF格式数据（JSON + 外部BIN文件）
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="bounds">包围盒</param>
    /// <param name="binFileName">外部BIN文件名（默认：data.bin）</param>
    /// <param name="materials">材质字典</param>
    /// <returns>(gltfJson, binaryData) 元组</returns>
    public (string gltfJson, byte[] binaryData) GenerateGLTF(
        List<Triangle> triangles,
        BoundingBox3D bounds,
        string binFileName = "data.bin",
        Dictionary<string, Material>? materials = null)
    {
        ValidateInput(triangles, bounds);

        _logger.LogDebug("开始生成GLTF: 三角形数={Count}", triangles.Count);

        try
        {
            // 1. 提取顶点数据（包含位置、法线、纹理坐标）
            var vertexData = ExtractVertexDataWithAttributes(triangles, materials);

            // 2. 创建Binary Buffer
            var binaryData = CreateBinaryBuffer(vertexData);

            // 3. 创建glTF JSON（包含外部URI引用）
            var gltfJson = CreateGltfJson(vertexData, binaryData.Length, binFileName, materials);

            _logger.LogInformation("GLTF生成完成: JSON={JsonSize}字节, BIN={BinSize}字节",
                gltfJson.Length, binaryData.Length);

            return (gltfJson, binaryData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成GLTF失败");
            throw;
        }
    }

    /// <summary>
    /// 提取顶点数据（位置、法线、纹理坐标）及索引
    /// 不进行去重，保留完整的顶点属性
    /// 支持纹理图集 UV 坐标变换
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="materials">材质字典（用于纹理图集UV变换）</param>
    private VertexData ExtractVertexDataWithAttributes(List<Triangle> triangles, Dictionary<string, Material>? materials = null)
    {
        var positions = new List<float>();
        var normals = new List<float>();
        var texCoords = new List<float>();
        var indices = new List<ushort>();

        bool hasNormals = triangles.Any(t => t.HasVertexNormals());
        bool hasTexCoords = triangles.Any(t => t.HasUVCoordinates());

        _logger.LogDebug("顶点属性: 法线={HasNormals}, 纹理坐标={HasTexCoords}", hasNormals, hasTexCoords);

        ushort vertexIndex = 0;
        int transformedUVCount = 0;

        foreach (var triangle in triangles)
        {
            var vertices = new[] {
                (triangle.V1, triangle.Normal1, triangle.UV1),
                (triangle.V2, triangle.Normal2, triangle.UV2),
                (triangle.V3, triangle.Normal3, triangle.UV3)
            };

            // 获取三角形的纹理图集变换信息
            TextureInfo? textureInfo = null;
            if (materials != null && !string.IsNullOrEmpty(triangle.MaterialName))
            {
                if (materials.TryGetValue(triangle.MaterialName, out var material))
                {
                    textureInfo = material.DiffuseTexture; // 主要使用漫反射纹理的图集信息
                }
            }

            foreach (var (vertex, normal, uv) in vertices)
            {
                // 添加顶点位置
                positions.Add((float)vertex.X);
                positions.Add((float)vertex.Y);
                positions.Add((float)vertex.Z);

                // 添加法线
                if (hasNormals && normal != null)
                {
                    normals.Add((float)normal.X);
                    normals.Add((float)normal.Y);
                    normals.Add((float)normal.Z);
                }

                // 添加纹理坐标（应用图集变换）
                if (hasTexCoords && uv != null)
                {
                    float u, v;

                    // 如果有纹理图集信息，应用UV变换
                    if (textureInfo != null &&
                        textureInfo.AtlasScale != null &&
                        textureInfo.AtlasOffset != null)
                    {
                        // UV变换公式: transformedUV = originalUV * scale + offset
                        u = (float)(uv.U * textureInfo.AtlasScale.U + textureInfo.AtlasOffset.U);
                        v = (float)(uv.V * textureInfo.AtlasScale.V + textureInfo.AtlasOffset.V);
                        transformedUVCount++;
                    }
                    else
                    {
                        // 没有图集信息，使用原始UV坐标
                        u = (float)uv.U;
                        v = (float)uv.V;
                    }

                    texCoords.Add(u);
                    texCoords.Add(v);
                }

                // 添加索引
                indices.Add(vertexIndex++);
            }
        }

        // 计算位置的min/max值（用于accessor）
        var (posMin, posMax) = CalculateMinMax(positions.ToArray(), 3);

        var vertexData = new VertexData
        {
            Positions = positions.ToArray(),
            Normals = hasNormals ? normals.ToArray() : null,
            TexCoords = hasTexCoords ? texCoords.ToArray() : null,
            Indices = indices.ToArray(),
            PositionMin = posMin,
            PositionMax = posMax,
            VertexCount = vertexIndex
        };

        if (transformedUVCount > 0)
        {
            _logger.LogDebug("顶点提取完成: 顶点数={VertexCount}, 三角形={TriangleCount}, UV变换={TransformedUVs}个",
                vertexIndex, triangles.Count, transformedUVCount);
        }
        else
        {
            _logger.LogDebug("顶点提取完成: 顶点数={VertexCount}, 三角形={TriangleCount}",
                vertexIndex, triangles.Count);
        }

        return vertexData;
    }

    /// <summary>
    /// 计算顶点属性的min/max值
    /// </summary>
    private (float[] min, float[] max) CalculateMinMax(float[] data, int componentCount)
    {
        var min = new float[componentCount];
        var max = new float[componentCount];

        for (int i = 0; i < componentCount; i++)
        {
            min[i] = float.MaxValue;
            max[i] = float.MinValue;
        }

        for (int i = 0; i < data.Length; i++)
        {
            int component = i % componentCount;
            min[component] = Math.Min(min[component], data[i]);
            max[component] = Math.Max(max[component], data[i]);
        }

        return (min, max);
    }

    /// <summary>
    /// 创建二进制缓冲区
    /// 布局: [positions...] [normals...] [texCoords...] [indices...]
    /// </summary>
    private byte[] CreateBinaryBuffer(VertexData vertexData)
    {
        int positionsSize = vertexData.Positions.Length * sizeof(float);
        int normalsSize = vertexData.Normals != null ? vertexData.Normals.Length * sizeof(float) : 0;
        int texCoordsSize = vertexData.TexCoords != null ? vertexData.TexCoords.Length * sizeof(float) : 0;
        int indicesSize = vertexData.Indices.Length * sizeof(ushort);

        // indices需要对齐到4字节
        int indicesPadding = (4 - (indicesSize % 4)) % 4;
        int totalSize = positionsSize + normalsSize + texCoordsSize + indicesSize + indicesPadding;

        var buffer = new byte[totalSize];
        int offset = 0;

        // 写入positions
        Buffer.BlockCopy(vertexData.Positions, 0, buffer, offset, positionsSize);
        offset += positionsSize;

        // 写入normals
        if (vertexData.Normals != null)
        {
            Buffer.BlockCopy(vertexData.Normals, 0, buffer, offset, normalsSize);
            offset += normalsSize;
        }

        // 写入texCoords
        if (vertexData.TexCoords != null)
        {
            Buffer.BlockCopy(vertexData.TexCoords, 0, buffer, offset, texCoordsSize);
            offset += texCoordsSize;
        }

        // 写入indices
        for (int i = 0; i < vertexData.Indices.Length; i++)
        {
            var indexBytes = BitConverter.GetBytes(vertexData.Indices[i]);
            Buffer.BlockCopy(indexBytes, 0, buffer, offset + i * sizeof(ushort), sizeof(ushort));
        }

        return buffer;
    }

    /// <summary>
    /// 创建glTF JSON描述
    /// 定义场景、网格、访问器、缓冲区视图等
    /// 支持POSITION, NORMAL, TEXCOORD_0属性
    /// 支持材质、纹理序列化
    /// </summary>
    /// <param name="vertexData">顶点数据</param>
    /// <param name="binaryBufferLength">二进制缓冲区长度</param>
    /// <param name="binFileName">外部BIN文件名（null表示嵌入GLB）</param>
    /// <param name="materials">材质字典</param>
    private string CreateGltfJson(VertexData vertexData, int binaryBufferLength, string? binFileName, Dictionary<string, Material>? materials = null)
    {
        int vertexCount = vertexData.VertexCount;
        int indexCount = vertexData.Indices.Length;
        bool hasNormals = vertexData.Normals != null;
        bool hasTexCoords = vertexData.TexCoords != null;

        // 动态构建attributes
        var attributesDict = new Dictionary<string, int> { { "POSITION", 0 } };
        int accessorIndex = 1;

        if (hasNormals)
        {
            attributesDict["NORMAL"] = accessorIndex++;
        }
        if (hasTexCoords)
        {
            attributesDict["TEXCOORD_0"] = accessorIndex++;
        }

        int indicesAccessorIndex = accessorIndex;

        // 构建accessors列表
        var accessorsList = new List<object>
        {
            // Accessor 0: POSITION
            new
            {
                bufferView = 0,
                componentType = 5126, // FLOAT
                count = vertexCount,
                type = "VEC3",
                max = vertexData.PositionMax.Select(v => (double)v).ToArray(),
                min = vertexData.PositionMin.Select(v => (double)v).ToArray()
            }
        };

        int bufferViewIndex = 1;

        // NORMAL accessor
        if (hasNormals)
        {
            accessorsList.Add(new
            {
                bufferView = bufferViewIndex++,
                componentType = 5126, // FLOAT
                count = vertexCount,
                type = "VEC3"
            });
        }

        // TEXCOORD_0 accessor
        if (hasTexCoords)
        {
            accessorsList.Add(new
            {
                bufferView = bufferViewIndex++,
                componentType = 5126, // FLOAT
                count = vertexCount,
                type = "VEC2"
            });
        }

        // INDICES accessor
        accessorsList.Add(new
        {
            bufferView = bufferViewIndex,
            componentType = 5123, // UNSIGNED_SHORT
            count = indexCount,
            type = "SCALAR"
        });

        // 构建bufferViews列表
        var bufferViewsList = new List<object>
        {
            // BufferView 0: positions
            new
            {
                buffer = 0,
                byteOffset = 0,
                byteLength = vertexCount * 3 * sizeof(float),
                target = 34962 // ARRAY_BUFFER
            }
        };

        int currentOffset = vertexCount * 3 * sizeof(float);

        // BufferView for NORMAL
        if (hasNormals)
        {
            bufferViewsList.Add(new
            {
                buffer = 0,
                byteOffset = currentOffset,
                byteLength = vertexCount * 3 * sizeof(float),
                target = 34962 // ARRAY_BUFFER
            });
            currentOffset += vertexCount * 3 * sizeof(float);
        }

        // BufferView for TEXCOORD_0
        if (hasTexCoords)
        {
            bufferViewsList.Add(new
            {
                buffer = 0,
                byteOffset = currentOffset,
                byteLength = vertexCount * 2 * sizeof(float),
                target = 34962 // ARRAY_BUFFER
            });
            currentOffset += vertexCount * 2 * sizeof(float);
        }

        // BufferView for indices
        bufferViewsList.Add(new
        {
            buffer = 0,
            byteOffset = currentOffset,
            byteLength = indexCount * sizeof(ushort),
            target = 34963 // ELEMENT_ARRAY_BUFFER
        });

        // 构建buffer对象（根据是否有外部URI）
        object bufferObj;
        if (binFileName != null)
        {
            // GLTF格式：包含外部URI
            bufferObj = new { uri = binFileName, byteLength = binaryBufferLength };
        }
        else
        {
            // GLB格式：不包含URI
            bufferObj = new { byteLength = binaryBufferLength };
        }

        // 构建材质、纹理、图像、采样器数组
        List<object>? materialsList = null;
        List<object>? texturesList = null;
        List<object>? imagesList = null;
        List<object>? samplersList = null;
        int? primitiveMaterialIndex = null;

        if (materials != null && materials.Count > 0)
        {
            var (mats, texs, imgs, samps) = CreateMaterialsTexturesImages(materials);
            materialsList = mats;
            texturesList = texs;
            imagesList = imgs;
            samplersList = samps;
            primitiveMaterialIndex = 0; // 链接到第一个材质
        }

        // 构建primitive对象（支持材质）
        var primitiveObj = new Dictionary<string, object>
        {
            { "attributes", attributesDict },
            { "indices", indicesAccessorIndex },
            { "mode", 4 } // TRIANGLES
        };

        if (primitiveMaterialIndex.HasValue)
        {
            primitiveObj["material"] = primitiveMaterialIndex.Value;
        }

        // 构建glTF对象（动态添加材质相关数组）
        var gltfDict = new Dictionary<string, object>
        {
            { "asset", new { version = "2.0", generator = "RealScene3D.GltfGenerator" } },
            { "scene", 0 },
            { "scenes", new[] { new { nodes = new[] { 0 } } } },
            { "nodes", new[] { new { mesh = 0 } } },
            { "meshes", new[] { new { primitives = new[] { primitiveObj } } } },
            { "accessors", accessorsList.ToArray() },
            { "bufferViews", bufferViewsList.ToArray() },
            { "buffers", new[] { bufferObj } }
        };

        if (materialsList != null && materialsList.Count > 0)
        {
            gltfDict["materials"] = materialsList.ToArray();
        }

        if (texturesList != null && texturesList.Count > 0)
        {
            gltfDict["textures"] = texturesList.ToArray();
        }

        if (imagesList != null && imagesList.Count > 0)
        {
            gltfDict["images"] = imagesList.ToArray();
        }

        if (samplersList != null && samplersList.Count > 0)
        {
            gltfDict["samplers"] = samplersList.ToArray();
        }

        return JsonSerializer.Serialize(gltfDict, new JsonSerializerOptions
        {
            WriteIndented = binFileName != null, // GLTF格式使用可读缩进，GLB使用紧凑格式
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// 生成纹理图集并更新材质的UV映射
    /// </summary>
    /// <param name="materials">材质字典</param>
    /// <param name="atlasOutputPath">图集输出路径（可选，如果提供则保存图集文件）</param>
    /// <param name="downsampleFactor">降采样因子（默认1.0，无缩放）。用于LOD纹理优化</param>
    /// <param name="useJpegCompression">是否使用JPEG压缩（默认true）</param>
    /// <returns>(更新后的材质字典, 图集结果)</returns>
    public async Task<(Dictionary<string, Material> UpdatedMaterials, TextureAtlasGenerator.AtlasResult? AtlasResult)>
        GenerateTextureAtlasAsync(
            Dictionary<string, Material> materials,
            string? atlasOutputPath = null,
            double downsampleFactor = 1.0,
            bool useJpegCompression = true)
    {
        if (_textureAtlasGenerator == null)
        {
            _logger.LogWarning("TextureAtlasGenerator未注入，跳过纹理图集生成");
            return (materials, null);
        }

        if (materials == null || materials.Count == 0)
        {
            return (materials ?? new Dictionary<string, Material>(), null);
        }

        // 1. 收集所有纹理路径
        var texturePaths = new HashSet<string>();
        foreach (var material in materials.Values)
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
            return (materials, null);
        }

        _logger.LogInformation("开始生成纹理图集: {Count}张纹理, 降采样因子={Factor:F2}, 压缩={Compression}",
            texturePaths.Count, downsampleFactor, useJpegCompression ? "JPEG" : "PNG");

        // 2. 生成纹理图集（应用降采样和压缩）
        var atlasResult = await _textureAtlasGenerator.GenerateAtlasAsync(
            texturePaths,
            maxAtlasSize: 4096,
            padding: 2,
            downsampleFactor: downsampleFactor,
            useJpegCompression: useJpegCompression);

        // 3. 保存图集文件（如果指定了路径）
        if (!string.IsNullOrEmpty(atlasOutputPath))
        {
            var directory = Path.GetDirectoryName(atlasOutputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(atlasOutputPath, atlasResult.ImageData);
            _logger.LogInformation("纹理图集已保存: {Path}, {Width}x{Height}, {Size}KB",
                atlasOutputPath, atlasResult.Width, atlasResult.Height, atlasResult.ImageData.Length / 1024);
        }

        // 4. 更新材质的纹理引用，设置图集UV偏移和缩放
        var updatedMaterials = new Dictionary<string, Material>();
        foreach (var (name, material) in materials)
        {
            var updatedMaterial = CloneMaterial(material);

            // 更新每个纹理的图集UV信息
            UpdateTextureAtlasInfo(updatedMaterial.DiffuseTexture, atlasResult);
            UpdateTextureAtlasInfo(updatedMaterial.NormalTexture, atlasResult);
            UpdateTextureAtlasInfo(updatedMaterial.SpecularTexture, atlasResult);
            UpdateTextureAtlasInfo(updatedMaterial.EmissiveTexture, atlasResult);
            UpdateTextureAtlasInfo(updatedMaterial.OpacityTexture, atlasResult);
            UpdateTextureAtlasInfo(updatedMaterial.MetallicTexture, atlasResult);
            UpdateTextureAtlasInfo(updatedMaterial.RoughnessTexture, atlasResult);

            updatedMaterials[name] = updatedMaterial;
        }

        _logger.LogInformation("材质纹理图集映射更新完成");
        return (updatedMaterials, atlasResult);
    }

    /// <summary>
    /// 更新纹理的图集UV信息
    /// </summary>
    private void UpdateTextureAtlasInfo(TextureInfo? texture, TextureAtlasGenerator.AtlasResult atlasResult)
    {
        if (texture == null || string.IsNullOrEmpty(texture.FilePath))
            return;

        if (atlasResult.TextureRegions.TryGetValue(texture.FilePath, out var region))
        {
            texture.AtlasOffset = region.UVMin;
            texture.AtlasScale = new Vector2D(
                region.UVMax.U - region.UVMin.U,
                region.UVMax.V - region.UVMin.V
            );

            _logger.LogDebug("纹理 {Path} 图集UV: offset=[{OffsetU:F4},{OffsetV:F4}], scale=[{ScaleU:F4},{ScaleV:F4}]",
                texture.FilePath,
                texture.AtlasOffset.U, texture.AtlasOffset.V,
                texture.AtlasScale.U, texture.AtlasScale.V);
        }
    }

    /// <summary>
    /// 克隆材质对象（深拷贝）
    /// </summary>
    private Material CloneMaterial(Material source)
    {
        return new Material
        {
            Name = source.Name,
            AmbientColor = source.AmbientColor != null ? new Color3D(source.AmbientColor.R, source.AmbientColor.G, source.AmbientColor.B) : null,
            DiffuseColor = source.DiffuseColor != null ? new Color3D(source.DiffuseColor.R, source.DiffuseColor.G, source.DiffuseColor.B) : null,
            SpecularColor = source.SpecularColor != null ? new Color3D(source.SpecularColor.R, source.SpecularColor.G, source.SpecularColor.B) : null,
            EmissiveColor = source.EmissiveColor != null ? new Color3D(source.EmissiveColor.R, source.EmissiveColor.G, source.EmissiveColor.B) : null,
            Shininess = source.Shininess,
            Opacity = source.Opacity,
            RefractiveIndex = source.RefractiveIndex,
            DiffuseTexture = CloneTextureInfo(source.DiffuseTexture),
            NormalTexture = CloneTextureInfo(source.NormalTexture),
            SpecularTexture = CloneTextureInfo(source.SpecularTexture),
            EmissiveTexture = CloneTextureInfo(source.EmissiveTexture),
            OpacityTexture = CloneTextureInfo(source.OpacityTexture),
            MetallicTexture = CloneTextureInfo(source.MetallicTexture),
            RoughnessTexture = CloneTextureInfo(source.RoughnessTexture)
        };
    }

    /// <summary>
    /// 克隆纹理信息对象
    /// </summary>
    private TextureInfo? CloneTextureInfo(TextureInfo? source)
    {
        if (source == null) return null;

        return new TextureInfo
        {
            Type = source.Type,
            FilePath = source.FilePath,
            WrapU = source.WrapU,
            WrapV = source.WrapV,
            FilterMode = source.FilterMode,
            AtlasOffset = source.AtlasOffset,
            AtlasScale = source.AtlasScale
        };
    }

    /// <summary>
    /// 合并Metallic和Roughness纹理为符合glTF 2.0规范的metallicRoughness纹理
    /// glTF规范: B通道存储metallic值, G通道存储roughness值, R和A通道未使用(设为255)
    /// </summary>
    /// <param name="metallicTexturePath">Metallic纹理路径（可为null）</param>
    /// <param name="roughnessTexturePath">Roughness纹理路径（可为null）</param>
    /// <param name="metallicFactor">Metallic因子（0-1），用于没有metallic纹理时的常量值</param>
    /// <param name="roughnessFactor">Roughness因子（0-1），用于没有roughness纹理时的常量值</param>
    /// <returns>合并后的PNG格式图像数据（RGBA8格式）</returns>
    /// <remarks>
    /// 处理场景:
    /// 1. 两个纹理都存在：从各自纹理提取数据，合并到正确通道
    /// 2. 仅metallic存在：使用metallic纹理的B通道 + roughnessFactor常量值填充G通道
    /// 3. 仅roughness存在：使用roughness纹理的G通道 + metallicFactor常量值填充B通道
    /// 4. 两者都不存在：返回null（调用方应使用metallicFactor和roughnessFactor常量）
    ///
    /// 纹理通道假设:
    /// - Metallic纹理：假设金属值存储在R通道（灰度图）或所有通道（单通道数据）
    /// - Roughness纹理：假设粗糙度值存储在R通道（灰度图）或所有通道（单通道数据）
    /// </remarks>
    private async Task<byte[]?> CombineMetallicRoughnessTexturesAsync(
        string? metallicTexturePath,
        string? roughnessTexturePath,
        float metallicFactor = 1.0f,
        float roughnessFactor = 1.0f)
    {
        // 如果两个纹理都不存在，返回null
        if (string.IsNullOrEmpty(metallicTexturePath) && string.IsNullOrEmpty(roughnessTexturePath))
        {
            _logger.LogDebug("metallicRoughness纹理：两者均不存在，将使用常量值");
            return null;
        }

        // 生成缓存key
        var cacheKey = $"{metallicTexturePath ?? "none"}|{roughnessTexturePath ?? "none"}|{metallicFactor}|{roughnessFactor}";
        if (_metallicRoughnessCombinedCache.TryGetValue(cacheKey, out var cachedData))
        {
            _logger.LogDebug("metallicRoughness纹理：使用缓存数据");
            return cachedData;
        }

        try
        {
            Image<Rgba32>? metallicImage = null;
            Image<Rgba32>? roughnessImage = null;

            // 加载纹理图像
            if (!string.IsNullOrEmpty(metallicTexturePath) && File.Exists(metallicTexturePath))
            {
                metallicImage = await Image.LoadAsync<Rgba32>(metallicTexturePath);
                _logger.LogDebug("metallicRoughness纹理：加载metallic纹理 {Path} ({Width}x{Height})",
                    metallicTexturePath, metallicImage.Width, metallicImage.Height);
            }

            if (!string.IsNullOrEmpty(roughnessTexturePath) && File.Exists(roughnessTexturePath))
            {
                roughnessImage = await Image.LoadAsync<Rgba32>(roughnessTexturePath);
                _logger.LogDebug("metallicRoughness纹理：加载roughness纹理 {Path} ({Width}x{Height})",
                    roughnessTexturePath, roughnessImage.Width, roughnessImage.Height);
            }

            // 确定输出纹理尺寸（使用较大的纹理尺寸）
            int width, height;
            if (metallicImage != null && roughnessImage != null)
            {
                width = Math.Max(metallicImage.Width, roughnessImage.Width);
                height = Math.Max(metallicImage.Height, roughnessImage.Height);

                // 如果尺寸不一致，调整较小的纹理
                if (metallicImage.Width != width || metallicImage.Height != height)
                {
                    _logger.LogDebug("metallicRoughness纹理：调整metallic纹理大小从 {OldW}x{OldH} 到 {NewW}x{NewH}",
                        metallicImage.Width, metallicImage.Height, width, height);
                    metallicImage.Mutate(x => x.Resize(width, height));
                }

                if (roughnessImage.Width != width || roughnessImage.Height != height)
                {
                    _logger.LogDebug("metallicRoughness纹理：调整roughness纹理大小从 {OldW}x{OldH} 到 {NewW}x{NewH}",
                        roughnessImage.Width, roughnessImage.Height, width, height);
                    roughnessImage.Mutate(x => x.Resize(width, height));
                }
            }
            else if (metallicImage != null)
            {
                width = metallicImage.Width;
                height = metallicImage.Height;
            }
            else if (roughnessImage != null)
            {
                width = roughnessImage.Width;
                height = roughnessImage.Height;
            }
            else
            {
                // 两个纹理都加载失败
                _logger.LogWarning("metallicRoughness纹理：无法加载任何纹理文件");
                return null;
            }

            // 创建输出图像（RGBA格式）
            var combinedImage = new Image<Rgba32>(width, height);

            // 预计算常量值（0-255范围）
            byte metallicConstant = (byte)(metallicFactor * 255);
            byte roughnessConstant = (byte)(roughnessFactor * 255);

            // 逐像素处理
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte metallicValue;
                    byte roughnessValue;

                    // 从metallic纹理获取值（使用R通道作为灰度值）
                    if (metallicImage != null)
                    {
                        var pixel = metallicImage[x, y];
                        // 使用R通道（假设metallic纹理是灰度图）
                        metallicValue = pixel.R;
                    }
                    else
                    {
                        metallicValue = metallicConstant;
                    }

                    // 从roughness纹理获取值（使用R通道作为灰度值）
                    if (roughnessImage != null)
                    {
                        var pixel = roughnessImage[x, y];
                        // 使用R通道（假设roughness纹理是灰度图）
                        roughnessValue = pixel.R;
                    }
                    else
                    {
                        roughnessValue = roughnessConstant;
                    }

                    // 按照glTF规范设置通道:
                    // R: 未使用，设为255
                    // G: roughness
                    // B: metallic
                    // A: 未使用，设为255
                    combinedImage[x, y] = new Rgba32(255, roughnessValue, metallicValue, 255);
                }
            }

            // 释放源图像
            metallicImage?.Dispose();
            roughnessImage?.Dispose();

            // 编码为PNG格式
            using var ms = new MemoryStream();
            await combinedImage.SaveAsPngAsync(ms);
            var pngData = ms.ToArray();

            // 释放合并后的图像
            combinedImage.Dispose();

            // 缓存结果
            _metallicRoughnessCombinedCache[cacheKey] = pngData;

            _logger.LogInformation("metallicRoughness纹理合并完成: {Width}x{Height}, 大小={Size}KB, 已缓存",
                width, height, pngData.Length / 1024);

            return pngData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "合并metallicRoughness纹理失败: metallic={MetallicPath}, roughness={RoughnessPath}",
                metallicTexturePath, roughnessTexturePath);
            return null;
        }
    }

    /// <summary>
    /// 创建材质、纹理、图像数组（符合glTF 2.0 PBR规范）
    /// 完整支持metallicRoughness纹理合并处理和采样器配置
    /// </summary>
    /// <param name="materials">材质字典</param>
    /// <returns>(materials数组, textures数组, images数组, samplers数组)</returns>
    private (List<object> materials, List<object> textures, List<object> images, List<object> samplers) CreateMaterialsTexturesImages(Dictionary<string, Material> materials)
    {
        var materialsList = new List<object>();
        var texturesList = new List<object>();
        var imagesList = new List<object>();
        var samplersList = new List<object>();
        var textureIndexMap = new Dictionary<string, int>(); // 纹理路径 -> 纹理索引
        var samplerIndexMap = new Dictionary<string, int>(); // 采样器配置key -> 采样器索引

        int textureIndex = 0;
        int imageIndex = 0;
        int samplerIndex = 0;

        foreach (var (materialName, mat) in materials)
        {
            var materialObj = new Dictionary<string, object>
            {
                { "name", mat.Name ?? materialName }
            };

            // PBR Metallic-Roughness工作流
            var pbrMetallicRoughness = new Dictionary<string, object>();

            // baseColorFactor (基础颜色因子)
            if (mat.DiffuseColor != null)
            {
                pbrMetallicRoughness["baseColorFactor"] = new[]
                {
                    mat.DiffuseColor.R,
                    mat.DiffuseColor.G,
                    mat.DiffuseColor.B,
                    mat.Opacity // Alpha通道
                };
            }
            else
            {
                // 没有漫反射颜色时，使用白色作为默认值
                // 这样纹理可以正常显示，不会是黑色
                pbrMetallicRoughness["baseColorFactor"] = new[] { 1.0, 1.0, 1.0, mat.Opacity };
                _logger.LogDebug("材质 {Name} 没有DiffuseColor，使用默认白色", mat.Name);
            }

            // baseColorTexture (基础颜色纹理)
            if (mat.DiffuseTexture != null && !string.IsNullOrEmpty(mat.DiffuseTexture.FilePath))
            {
                var texIdx = GetOrCreateTexture(mat.DiffuseTexture, textureIndexMap, samplerIndexMap, texturesList, imagesList, samplersList, ref textureIndex, ref imageIndex, ref samplerIndex);
                pbrMetallicRoughness["baseColorTexture"] = new { index = texIdx };
            }

            // metallicRoughnessTexture处理 - 完整实现符合glTF 2.0规范
            // glTF规范要求metallicRoughnessTexture的B通道存储metallic，G通道存储roughness
            if (mat.MetallicTexture != null || mat.RoughnessTexture != null)
            {
                var metallicPath = mat.MetallicTexture?.FilePath;
                var roughnessPath = mat.RoughnessTexture?.FilePath;

                // 计算metallicFactor和roughnessFactor
                // 如果没有纹理，这些值将用作常量
                double metallicFactor = 0.0;
                double roughnessFactor = 0.9;

                // 从Shininess转换为roughness (Shininess范围0-1000，越高越光滑)
                if (mat.Shininess > 0)
                {
                    roughnessFactor = 1.0 - Math.Min(mat.Shininess / 1000.0, 1.0);
                }

                // 尝试合并纹理（使用.GetAwaiter().GetResult()同步等待）
                byte[]? combinedTextureData = null;
                try
                {
                    combinedTextureData = CombineMetallicRoughnessTexturesAsync(
                        metallicPath,
                        roughnessPath,
                        (float)metallicFactor,
                        (float)roughnessFactor
                    ).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "合并metallicRoughness纹理失败，材质={MaterialName}", materialName);
                }

                // 如果成功合并了纹理，创建纹理引用
                if (combinedTextureData != null)
                {
                    // 创建合并后的纹理
                    var combinedTextureKey = $"combined_mr_{materialName}";

                    if (!textureIndexMap.ContainsKey(combinedTextureKey))
                    {
                        // 创建图像对象（使用Data URI嵌入）
                        var base64 = Convert.ToBase64String(combinedTextureData);
                        var dataUri = $"data:image/png;base64,{base64}";

                        var imageObj = new Dictionary<string, object>
                        {
                            { "uri", dataUri },
                            { "name", $"{materialName}_metallicRoughness" }
                        };
                        imagesList.Add(imageObj);

                        // 创建纹理对象
                        var textureObj = new Dictionary<string, object>
                        {
                            { "source", imageIndex },
                            { "name", $"{materialName}_metallicRoughness" }
                        };
                        texturesList.Add(textureObj);

                        textureIndexMap[combinedTextureKey] = textureIndex;

                        _logger.LogDebug("材质 {Name}: 使用合并后的metallicRoughness纹理 (纹理索引={Index})",
                            materialName, textureIndex);

                        pbrMetallicRoughness["metallicRoughnessTexture"] = new { index = textureIndex };
                        pbrMetallicRoughness["metallicFactor"] = 1.0;
                        pbrMetallicRoughness["roughnessFactor"] = 1.0;

                        textureIndex++;
                        imageIndex++;
                    }
                    else
                    {
                        // 复用已存在的合并纹理
                        var existingIndex = textureIndexMap[combinedTextureKey];
                        pbrMetallicRoughness["metallicRoughnessTexture"] = new { index = existingIndex };
                        pbrMetallicRoughness["metallicFactor"] = 1.0;
                        pbrMetallicRoughness["roughnessFactor"] = 1.0;
                    }
                }
                else
                {
                    // 合并失败或不需要合并，使用常量值
                    pbrMetallicRoughness["metallicFactor"] = metallicFactor;
                    pbrMetallicRoughness["roughnessFactor"] = roughnessFactor;

                    _logger.LogDebug("材质 {Name}: 使用metallicFactor={Metallic:F2}, roughnessFactor={Roughness:F2}",
                        materialName, metallicFactor, roughnessFactor);
                }
            }
            else
            {
                // 没有金属/粗糙度纹理，使用默认值或从Shininess转换
                pbrMetallicRoughness["metallicFactor"] = 0.0; // 默认非金属

                // 从Shininess转换为roughness (Shininess范围0-1000，越高越光滑)
                var roughness = mat.Shininess > 0 ? 1.0 - Math.Min(mat.Shininess / 1000.0, 1.0) : 0.9;
                pbrMetallicRoughness["roughnessFactor"] = roughness;
            }

            materialObj["pbrMetallicRoughness"] = pbrMetallicRoughness;

            // normalTexture (法线贴图)
            if (mat.NormalTexture != null && !string.IsNullOrEmpty(mat.NormalTexture.FilePath))
            {
                var texIdx = GetOrCreateTexture(mat.NormalTexture, textureIndexMap, samplerIndexMap, texturesList, imagesList, samplersList, ref textureIndex, ref imageIndex, ref samplerIndex);
                materialObj["normalTexture"] = new { index = texIdx };
            }

            // emissiveTexture和emissiveFactor
            if (mat.EmissiveTexture != null && !string.IsNullOrEmpty(mat.EmissiveTexture.FilePath))
            {
                var texIdx = GetOrCreateTexture(mat.EmissiveTexture, textureIndexMap, samplerIndexMap, texturesList, imagesList, samplersList, ref textureIndex, ref imageIndex, ref samplerIndex);
                materialObj["emissiveTexture"] = new { index = texIdx };
            }

            if (mat.EmissiveColor != null)
            {
                materialObj["emissiveFactor"] = new[]
                {
                    mat.EmissiveColor.R,
                    mat.EmissiveColor.G,
                    mat.EmissiveColor.B
                };
            }

            // alphaMode和alphaCutoff
            if (mat.Opacity < 1.0)
            {
                materialObj["alphaMode"] = mat.Opacity > 0.0 ? "BLEND" : "MASK";
                if (mat.Opacity > 0.0 && mat.Opacity < 0.5)
                {
                    materialObj["alphaCutoff"] = 0.5;
                }
            }

            // doubleSided（默认false）
            materialObj["doubleSided"] = false;

            materialsList.Add(materialObj);
        }

        _logger.LogDebug("材质序列化完成: {MaterialCount}个材质, {TextureCount}个纹理, {ImageCount}个图像, {SamplerCount}个采样器",
            materialsList.Count, texturesList.Count, imagesList.Count, samplersList.Count);

        return (materialsList, texturesList, imagesList, samplersList);
    }

    /// <summary>
    /// 获取或创建纹理索引（避免重复纹理）
    /// 完整支持采样器配置
    /// 优化：使用外部纹理引用而不是嵌入，大幅减小文件大小
    /// 修正：保留纹理的相对路径结构，支持子目录纹理
    /// </summary>
    private int GetOrCreateTexture(
        TextureInfo textureInfo,
        Dictionary<string, int> textureIndexMap,
        Dictionary<string, int> samplerIndexMap,
        List<object> texturesList,
        List<object> imagesList,
        List<object> samplersList,
        ref int textureIndex,
        ref int imageIndex,
        ref int samplerIndex)
    {
        var texturePath = textureInfo.FilePath;

        // 检查是否已存在（纹理路径相同且采样器配置相同）
        var samplerKey = GetSamplerKey(textureInfo);
        var textureKey = $"{texturePath}|{samplerKey}";

        if (textureIndexMap.TryGetValue(textureKey, out var existingIndex))
        {
            return existingIndex;
        }

        // 创建新的图像 - 使用外部URI引用而不是嵌入（大幅减小文件大小）
        var imageObj = new Dictionary<string, object>();

        // 获取纹理的相对URI引用
        // 保留原始目录结构，以支持子目录中的纹理文件
        string textureUri = GetTextureUri(texturePath);
        imageObj["uri"] = textureUri;

        // 如果纹理文件存在，记录详细信息
        if (File.Exists(texturePath))
        {
            _logger.LogDebug("纹理引用为外部URI: {Uri} (绝对路径: {AbsolutePath})", textureUri, texturePath);
        }
        else
        {
            _logger.LogWarning("纹理文件不存在: {Path}，仍将使用URI引用: {Uri}", texturePath, textureUri);
        }

        imagesList.Add(imageObj);

        // 获取或创建采样器索引
        int currentSamplerIndex = GetOrCreateSampler(textureInfo, samplerIndexMap, samplersList, ref samplerIndex);

        // 创建新的纹理
        var textureObj = new Dictionary<string, object>
        {
            { "source", imageIndex }, // 引用图像索引
            { "sampler", currentSamplerIndex } // 引用采样器索引
        };

        texturesList.Add(textureObj);

        // 记录索引
        var currentIndex = textureIndex;
        textureIndexMap[textureKey] = currentIndex;

        textureIndex++;
        imageIndex++;

        return currentIndex;
    }

    /// <summary>
    /// 获取纹理URI - 将绝对路径转换为相对URI引用
    /// 保留相对路径结构以支持子目录纹理
    /// </summary>
    /// <param name="absoluteTexturePath">纹理文件的绝对路径</param>
    /// <returns>用于GLTF引用的URI字符串</returns>
    private string GetTextureUri(string absoluteTexturePath)
    {
        try
        {
            // 规范化路径分隔符为正斜杠（GLTF标准）
            var uri = absoluteTexturePath.Replace('\\', '/');

            // 只使用文件名（最简单的方式）
            // 注意：这假设纹理文件会被复制到与GLTF/GLB相同的目录
            // 如果需要保留子目录结构，可以在此处实现更复杂的相对路径计算
            var fileName = Path.GetFileName(uri);

            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理纹理URI失败: {Path}", absoluteTexturePath);
            return Path.GetFileName(absoluteTexturePath);
        }
    }

    /// <summary>
    /// 获取或创建采样器索引
    /// </summary>
    private int GetOrCreateSampler(
        TextureInfo textureInfo,
        Dictionary<string, int> samplerIndexMap,
        List<object> samplersList,
        ref int samplerIndex)
    {
        var samplerKey = GetSamplerKey(textureInfo);

        // 检查是否已存在相同配置的采样器
        if (samplerIndexMap.TryGetValue(samplerKey, out var existingIndex))
        {
            return existingIndex;
        }

        // 创建新的采样器
        var samplerConfig = CreateSamplerConfig(textureInfo);
        samplersList.Add(samplerConfig);

        var currentIndex = samplerIndex;
        samplerIndexMap[samplerKey] = currentIndex;
        samplerIndex++;

        _logger.LogDebug("创建采样器 #{Index}: {Key}", currentIndex, samplerKey);

        return currentIndex;
    }

    /// <summary>
    /// 生成采样器的唯一键
    /// </summary>
    private string GetSamplerKey(TextureInfo textureInfo)
    {
        return $"{textureInfo.WrapU}|{textureInfo.WrapV}|{textureInfo.FilterMode}";
    }

    /// <summary>
    /// 创建纹理采样器配置对象
    /// 根据TextureInfo的设置生成符合glTF规范的采样器参数
    /// </summary>
    private Dictionary<string, object> CreateSamplerConfig(TextureInfo textureInfo)
    {
        // glTF sampler参数映射
        var wrapS = textureInfo.WrapU switch
        {
            TextureWrapMode.Repeat => 10497, // REPEAT
            TextureWrapMode.ClampToEdge => 33071, // CLAMP_TO_EDGE
            TextureWrapMode.MirroredRepeat => 33648, // MIRRORED_REPEAT
            _ => 10497
        };

        var wrapT = textureInfo.WrapV switch
        {
            TextureWrapMode.Repeat => 10497,
            TextureWrapMode.ClampToEdge => 33071,
            TextureWrapMode.MirroredRepeat => 33648,
            _ => 10497
        };

        var magFilter = textureInfo.FilterMode switch
        {
            TextureFilterMode.Nearest => 9728, // NEAREST
            TextureFilterMode.Linear => 9729, // LINEAR
            _ => 9729
        };

        var minFilter = textureInfo.FilterMode switch
        {
            TextureFilterMode.Nearest => 9728,
            TextureFilterMode.Linear => 9729,
            TextureFilterMode.Mipmap => 9987, // LINEAR_MIPMAP_LINEAR
            _ => 9729
        };

        return new Dictionary<string, object>
        {
            { "wrapS", wrapS },
            { "wrapT", wrapT },
            { "magFilter", magFilter },
            { "minFilter", minFilter }
        };
    }

    /// <summary>
    /// 获取瓦片格式名称
    /// </summary>
    protected override string GetFormatName() => "GLTF";

    /// <summary>
    /// 保存瓦片文件到磁盘 - 实现抽象方法
    /// 默认保存为GLB格式
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="bounds">空间包围盒</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="materials">材质字典（可选）</param>
    public override async Task SaveTileAsync(List<Triangle> triangles, BoundingBox3D bounds, string outputPath, Dictionary<string, Material>? materials = null)
    {
        await SaveGLBFileAsync(triangles, bounds, outputPath, materials);
    }

    /// <summary>
    /// 保存GLB文件到磁盘
    /// </summary>
    /// <param name="materials">材质字典</param>
    public async Task SaveGLBFileAsync(List<Triangle> triangles, BoundingBox3D bounds, string outputPath, Dictionary<string, Material>? materials = null)
    {
        _logger.LogInformation("保存GLB文件: {Path}", outputPath);

        try
        {
            var glbData = GenerateGLB(triangles, bounds, materials);

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
    /// 保存GLTF文件到磁盘（JSON + BIN）
    /// </summary>
    public async Task SaveGLTFFilesAsync(
        List<Triangle> triangles,
        BoundingBox3D bounds,
        string gltfPath,
        string? binPath = null)
    {
        _logger.LogInformation("保存GLTF文件: {Path}", gltfPath);

        try
        {
            // 确定bin文件路径
            binPath ??= Path.ChangeExtension(gltfPath, ".bin");
            var binFileName = Path.GetFileName(binPath);

            var (gltfJson, binaryData) = GenerateGLTF(triangles, bounds, binFileName);

            // 确保目录存在
            var directory = Path.GetDirectoryName(gltfPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 保存GLTF JSON文件
            await File.WriteAllTextAsync(gltfPath, gltfJson);

            // 保存BIN二进制文件
            await File.WriteAllBytesAsync(binPath, binaryData);

            _logger.LogInformation("GLTF文件保存成功: {GltfPath}, {BinPath}",
                gltfPath, binPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存GLTF文件失败: {Path}", gltfPath);
            throw;
        }
    }

    /// <summary>
    /// 顶点数据结构 - 包含所有顶点属性
    /// </summary>
    private class VertexData
    {
        public float[] Positions { get; set; } = Array.Empty<float>();
        public float[]? Normals { get; set; }
        public float[]? TexCoords { get; set; }
        public ushort[] Indices { get; set; } = Array.Empty<ushort>();
        public float[] PositionMin { get; set; } = Array.Empty<float>();
        public float[] PositionMax { get; set; } = Array.Empty<float>();
        public int VertexCount { get; set; }
    }
}
