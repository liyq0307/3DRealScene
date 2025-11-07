using Microsoft.Extensions.Logging;
using RealScene3D.Application.Services;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using System.Text;
using System.Text.Json;

namespace RealScene3D.Application.Services;

/// <summary>
/// B3DM生成器 - 生成Cesium 3D Tiles的Batched 3D Model格式
/// 将三角形网格数据转换为GLB(Binary glTF)格式,并封装为B3DM文件
/// 参考: Cesium 3D Tiles Specification 1.0
/// </summary>
public class B3dmGenerator
{
    private readonly ILogger<B3dmGenerator> _logger;

    public B3dmGenerator(ILogger<B3dmGenerator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 生成B3DM文件数据
    /// 算法流程: 三角形 → GLB → B3DM
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="bounds">包围盒</param>
    /// <returns>B3DM文件的二进制数据</returns>
    public byte[] GenerateB3DM(List<Triangle> triangles, BoundingBox3D bounds)
    {
        if (triangles == null || triangles.Count == 0)
        {
            throw new ArgumentException("三角形列表不能为空", nameof(triangles));
        }

        _logger.LogDebug("开始生成B3DM: 三角形数={Count}", triangles.Count);

        try
        {
            // 1. 生成GLB二进制数据
            var glbData = GenerateGLB(triangles);

            // 2. 构建Feature Table (必需)
            var featureTableJson = GenerateFeatureTableJson(triangles.Count);
            var featureTableJsonBytes = Encoding.UTF8.GetBytes(featureTableJson);

            // 对齐到4字节边界
            var featureTableJsonPadded = PadTo4ByteBoundary(featureTableJsonBytes);

            // Feature Table Binary (当前为空)
            var featureTableBinary = Array.Empty<byte>();

            // 3. 构建Batch Table (可选,当前为空)
            var batchTableJson = "{}";
            var batchTableJsonBytes = Encoding.UTF8.GetBytes(batchTableJson);
            var batchTableJsonPadded = PadTo4ByteBoundary(batchTableJsonBytes);
            var batchTableBinary = Array.Empty<byte>();

            // 4. 计算总长度
            int headerLength = 28; // B3DM header固定28字节
            int featureTableJsonLength = featureTableJsonPadded.Length;
            int featureTableBinaryLength = featureTableBinary.Length;
            int batchTableJsonLength = batchTableJsonPadded.Length;
            int batchTableBinaryLength = batchTableBinary.Length;
            int glbLength = glbData.Length;

            int totalLength = headerLength +
                            featureTableJsonLength +
                            featureTableBinaryLength +
                            batchTableJsonLength +
                            batchTableBinaryLength +
                            glbLength;

            // 5. 写入B3DM数据
            using var ms = new MemoryStream(totalLength);
            using var writer = new BinaryWriter(ms);

            // B3DM Header (28 bytes)
            writer.Write(Encoding.UTF8.GetBytes("b3dm")); // magic (4 bytes)
            writer.Write((uint)1);                         // version (4 bytes)
            writer.Write((uint)totalLength);               // byteLength (4 bytes)
            writer.Write((uint)featureTableJsonLength);    // featureTableJSONByteLength (4 bytes)
            writer.Write((uint)featureTableBinaryLength);  // featureTableBinaryByteLength (4 bytes)
            writer.Write((uint)batchTableJsonLength);      // batchTableJSONByteLength (4 bytes)
            writer.Write((uint)batchTableBinaryLength);    // batchTableBinaryByteLength (4 bytes)

            // Feature Table
            writer.Write(featureTableJsonPadded);
            writer.Write(featureTableBinary);

            // Batch Table
            writer.Write(batchTableJsonPadded);
            writer.Write(batchTableBinary);

            // GLB
            writer.Write(glbData);

            _logger.LogDebug("B3DM生成完成: 总大小={Size}字节", totalLength);

            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成B3DM失败");
            throw;
        }
    }

    /// <summary>
    /// 生成GLB (Binary glTF 2.0) 数据
    /// GLB格式: Header + JSON Chunk + Binary Chunk
    /// </summary>
    private byte[] GenerateGLB(List<Triangle> triangles)
    {
        // 1. 提取顶点数据
        var (positions, indices) = ExtractVertexData(triangles);

        // 2. 创建Binary Buffer
        var binaryData = CreateBinaryBuffer(positions, indices);

        // 3. 创建glTF JSON
        var gltfJson = CreateGltfJson(positions.Length / 3, indices.Length, binaryData.Length);
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
        writer.Write((uint)0x4E4F534A);           // chunkType "JSON" (4 bytes)
        writer.Write(gltfJsonPadded);             // chunkData

        // Binary Chunk
        writer.Write((uint)binaryDataPadded.Length); // chunkLength (4 bytes)
        writer.Write((uint)0x004E4942);           // chunkType "BIN\0" (4 bytes)
        writer.Write(binaryDataPadded);           // chunkData

        return ms.ToArray();
    }

    /// <summary>
    /// 提取顶点位置和索引数据
    /// 使用字典去重,优化顶点数量
    /// </summary>
    private (float[] positions, ushort[] indices) ExtractVertexData(List<Triangle> triangles)
    {
        var vertexMap = new Dictionary<string, ushort>();
        var positionsList = new List<float>();
        var indicesList = new List<ushort>();

        ushort currentIndex = 0;

        foreach (var triangle in triangles)
        {
            foreach (var vertex in triangle.Vertices)
            {
                // 使用坐标字符串作为key进行去重
                var key = $"{vertex.X:F6}_{vertex.Y:F6}_{vertex.Z:F6}";

                if (!vertexMap.TryGetValue(key, out ushort index))
                {
                    index = currentIndex++;
                    vertexMap[key] = index;

                    // 添加顶点位置 (x, y, z)
                    positionsList.Add((float)vertex.X);
                    positionsList.Add((float)vertex.Y);
                    positionsList.Add((float)vertex.Z);
                }

                indicesList.Add(index);
            }
        }

        _logger.LogDebug("顶点提取: 原始={Original}, 去重后={Unique}, 三角形={Triangles}",
            triangles.Count * 3, currentIndex, triangles.Count);

        return (positionsList.ToArray(), indicesList.ToArray());
    }

    /// <summary>
    /// 创建二进制缓冲区 (positions + indices)
    /// 布局: [positions...] [indices...]
    /// </summary>
    private byte[] CreateBinaryBuffer(float[] positions, ushort[] indices)
    {
        int positionsSize = positions.Length * sizeof(float);
        int indicesSize = indices.Length * sizeof(ushort);

        // indices需要对齐到4字节
        int indicesPadding = (4 - (indicesSize % 4)) % 4;
        int totalSize = positionsSize + indicesSize + indicesPadding;

        var buffer = new byte[totalSize];

        // 写入positions
        Buffer.BlockCopy(positions, 0, buffer, 0, positionsSize);

        // 写入indices
        for (int i = 0; i < indices.Length; i++)
        {
            var indexBytes = BitConverter.GetBytes(indices[i]);
            Buffer.BlockCopy(indexBytes, 0, buffer, positionsSize + i * sizeof(ushort), sizeof(ushort));
        }

        return buffer;
    }

    /// <summary>
    /// 创建glTF JSON描述
    /// 定义场景、网格、访问器、缓冲区视图等
    /// </summary>
    private string CreateGltfJson(int vertexCount, int indexCount, int binaryBufferLength)
    {
        var gltf = new
        {
            asset = new { version = "2.0", generator = "RealScene3D.B3dmGenerator" },
            scene = 0,
            scenes = new[] { new { nodes = new[] { 0 } } },
            nodes = new[] { new { mesh = 0 } },
            meshes = new[]
            {
                new
                {
                    primitives = new[]
                    {
                        new
                        {
                            attributes = new { POSITION = 0 },
                            indices = 1,
                            mode = 4 // TRIANGLES
                        }
                    }
                }
            },
            accessors = new[]
            {
                // Accessor 0: POSITION
                new
                {
                    bufferView = 0,
                    componentType = 5126, // FLOAT
                    count = vertexCount,
                    type = "VEC3",
                    max = new[] { 1.0, 1.0, 1.0 }, // 实际应计算真实范围
                    min = new[] { -1.0, -1.0, -1.0 }
                },
                // Accessor 1: INDICES
                new
                {
                    bufferView = 1,
                    componentType = 5123, // UNSIGNED_SHORT
                    count = indexCount,
                    type = "SCALAR"
                }
            },
            bufferViews = new[]
            {
                // BufferView 0: positions
                new
                {
                    buffer = 0,
                    byteOffset = 0,
                    byteLength = vertexCount * 3 * sizeof(float),
                    target = 34962 // ARRAY_BUFFER
                },
                // BufferView 1: indices
                new
                {
                    buffer = 0,
                    byteOffset = vertexCount * 3 * sizeof(float),
                    byteLength = indexCount * sizeof(ushort),
                    target = 34963 // ELEMENT_ARRAY_BUFFER
                }
            },
            buffers = new[]
            {
                new { byteLength = binaryBufferLength }
            }
        };

        return JsonSerializer.Serialize(gltf, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// 生成Feature Table JSON
    /// 定义batch的数量
    /// </summary>
    private string GenerateFeatureTableJson(int triangleCount)
    {
        var featureTable = new
        {
            BATCH_LENGTH = 0 // 当前不使用batch,设为0
        };

        return JsonSerializer.Serialize(featureTable);
    }

    /// <summary>
    /// 将字节数组填充到4字节边界
    /// GLB和B3DM格式要求
    /// </summary>
    private byte[] PadTo4ByteBoundary(byte[] data)
    {
        int padding = (4 - (data.Length % 4)) % 4;
        if (padding == 0)
            return data;

        var padded = new byte[data.Length + padding];
        Array.Copy(data, padded, data.Length);

        // 填充空格(0x20)用于JSON,填充0x00用于二进制
        byte paddingByte = 0x20; // 假设是JSON
        for (int i = data.Length; i < padded.Length; i++)
            padded[i] = paddingByte;

        return padded;
    }

    /// <summary>
    /// 保存B3DM文件到磁盘
    /// </summary>
    public async Task SaveB3DMFileAsync(List<Triangle> triangles, BoundingBox3D bounds, string outputPath)
    {
        _logger.LogInformation("保存B3DM文件: {Path}", outputPath);

        try
        {
            var b3dmData = GenerateB3DM(triangles, bounds);

            // 确保目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(outputPath, b3dmData);

            _logger.LogInformation("B3DM文件保存成功: {Path}, 大小={Size}字节",
                outputPath, b3dmData.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存B3DM文件失败: {Path}", outputPath);
            throw;
        }
    }
}
