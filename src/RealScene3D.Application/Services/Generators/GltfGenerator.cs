using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using System.Text;
using System.Text.Json;

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
            // 1. 提取顶点数据（包含位置、法线、纹理坐标）
            var vertexData = ExtractVertexDataWithAttributes(triangles, materials);

            // 2. 创建Binary Buffer
            var binaryData = CreateBinaryBuffer(vertexData);

            // 3. 创建glTF JSON
            var gltfJson = CreateGltfJson(vertexData, binaryData.Length, null, materials);
            var gltfJsonBytes = Encoding.UTF8.GetBytes(gltfJson);
            var gltfJsonPadded = PadTo4ByteBoundary(gltfJsonBytes);

            // Binary Chunk需要对齐
            var binaryDataPadded = PadTo4ByteBoundary(binaryData);

            // 4. 构建GLB
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
            LogGenerationStats(triangles.Count, vertexData.VertexCount, result.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成GLB失败");
            throw;
        }
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

        // 构建材质、纹理、图像数组
        List<object>? materialsList = null;
        List<object>? texturesList = null;
        List<object>? imagesList = null;
        int? primitiveMaterialIndex = null;

        if (materials != null && materials.Count > 0)
        {
            var (mats, texs, imgs) = CreateMaterialsTexturesImages(materials);
            materialsList = mats;
            texturesList = texs;
            imagesList = imgs;
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
    /// <returns>(更新后的材质字典, 图集结果)</returns>
    public async Task<(Dictionary<string, Material> UpdatedMaterials, TextureAtlasGenerator.AtlasResult? AtlasResult)>
        GenerateTextureAtlasAsync(Dictionary<string, Material> materials, string? atlasOutputPath = null)
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

        _logger.LogInformation("开始生成纹理图集: {Count}张纹理", texturePaths.Count);

        // 2. 生成纹理图集
        var atlasResult = await _textureAtlasGenerator.GenerateAtlasAsync(texturePaths);

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
    /// 创建材质、纹理、图像数组（符合glTF 2.0 PBR规范）
    /// </summary>
    /// <param name="materials">材质字典</param>
    /// <returns>(materials数组, textures数组, images数组)</returns>
    private (List<object> materials, List<object> textures, List<object> images) CreateMaterialsTexturesImages(Dictionary<string, Material> materials)
    {
        var materialsList = new List<object>();
        var texturesList = new List<object>();
        var imagesList = new List<object>();
        var textureIndexMap = new Dictionary<string, int>(); // 纹理路径 -> 纹理索引

        int textureIndex = 0;
        int imageIndex = 0;

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

            // baseColorTexture (基础颜色纹理)
            if (mat.DiffuseTexture != null && !string.IsNullOrEmpty(mat.DiffuseTexture.FilePath))
            {
                var texIdx = GetOrCreateTexture(mat.DiffuseTexture, textureIndexMap, texturesList, imagesList, ref textureIndex, ref imageIndex);
                pbrMetallicRoughness["baseColorTexture"] = new { index = texIdx };
            }

            // metallicFactor和roughnessFactor
            // 注意：glTF需要metallicRoughnessTexture包含两个通道，当前简化处理
            if (mat.MetallicTexture != null || mat.RoughnessTexture != null)
            {
                // 优先使用MetallicTexture（如果包含roughness通道）
                if (mat.MetallicTexture != null && !string.IsNullOrEmpty(mat.MetallicTexture.FilePath))
                {
                    var texIdx = GetOrCreateTexture(mat.MetallicTexture, textureIndexMap, texturesList, imagesList, ref textureIndex, ref imageIndex);
                    pbrMetallicRoughness["metallicRoughnessTexture"] = new { index = texIdx };
                }
                else if (mat.RoughnessTexture != null && !string.IsNullOrEmpty(mat.RoughnessTexture.FilePath))
                {
                    var texIdx = GetOrCreateTexture(mat.RoughnessTexture, textureIndexMap, texturesList, imagesList, ref textureIndex, ref imageIndex);
                    pbrMetallicRoughness["metallicRoughnessTexture"] = new { index = texIdx };
                }

                pbrMetallicRoughness["metallicFactor"] = 1.0;
                pbrMetallicRoughness["roughnessFactor"] = 1.0;
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
                var texIdx = GetOrCreateTexture(mat.NormalTexture, textureIndexMap, texturesList, imagesList, ref textureIndex, ref imageIndex);
                materialObj["normalTexture"] = new { index = texIdx };
            }

            // emissiveTexture和emissiveFactor
            if (mat.EmissiveTexture != null && !string.IsNullOrEmpty(mat.EmissiveTexture.FilePath))
            {
                var texIdx = GetOrCreateTexture(mat.EmissiveTexture, textureIndexMap, texturesList, imagesList, ref textureIndex, ref imageIndex);
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

        _logger.LogDebug("材质序列化完成: {MaterialCount}个材质, {TextureCount}个纹理, {ImageCount}个图像",
            materialsList.Count, texturesList.Count, imagesList.Count);

        return (materialsList, texturesList, imagesList);
    }

    /// <summary>
    /// 获取或创建纹理索引（避免重复纹理）
    /// </summary>
    private int GetOrCreateTexture(
        TextureInfo textureInfo,
        Dictionary<string, int> textureIndexMap,
        List<object> texturesList,
        List<object> imagesList,
        ref int textureIndex,
        ref int imageIndex)
    {
        var texturePath = textureInfo.FilePath;

        // 检查是否已存在
        if (textureIndexMap.TryGetValue(texturePath, out var existingIndex))
        {
            return existingIndex;
        }

        // 创建新的图像
        var imageObj = new Dictionary<string, object>
        {
            { "uri", Path.GetFileName(texturePath) } // 使用文件名作为URI
        };
        imagesList.Add(imageObj);

        // 创建新的纹理
        var textureObj = new Dictionary<string, object>
        {
            { "source", imageIndex } // 引用图像索引
        };

        // 添加sampler配置（可选）
        var samplerObj = CreateSampler(textureInfo);
        if (samplerObj != null)
        {
            // 注意：这里简化处理，没有创建单独的samplers数组
            // 完整实现应该创建samplers数组并引用
        }

        texturesList.Add(textureObj);

        // 记录索引
        var currentIndex = textureIndex;
        textureIndexMap[texturePath] = currentIndex;

        textureIndex++;
        imageIndex++;

        return currentIndex;
    }

    /// <summary>
    /// 创建纹理采样器配置
    /// </summary>
    private Dictionary<string, object>? CreateSampler(TextureInfo textureInfo)
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
