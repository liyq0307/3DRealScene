using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Geometry;
using RealScene3D.Domain.Materials;
using System.Text;
using System.Text.Json;

namespace RealScene3D.Application.Services.Generators;

/// <summary>
/// B3DM生成器 - 生成Cesium 3D Tiles的Batched 3D Model格式
/// 将三角形网格数据转换为GLB(Binary glTF)格式,并封装为B3DM文件
/// 完整支持顶点位置、法线和纹理坐标
/// 参考: Cesium 3D Tiles Specification 1.0
/// 继承自TileGenerator,实现B3DM格式的具体生成逻辑
/// </summary>
public class B3dmGenerator : TileGenerator
{
    private readonly GltfGenerator _gltfGenerator;

    /// <summary>
    /// 构造函数 - 注入日志记录器和 GLTF 生成器
    /// </summary>
    /// <param name="logger">日志记录器实例</param>
    /// <param name="gltfGenerator">GLTF 生成器（用于生成内嵌的 GLB 数据）</param>
    public B3dmGenerator(ILogger<B3dmGenerator> logger, GltfGenerator gltfGenerator) : base(logger)
    {
        _gltfGenerator = gltfGenerator ?? throw new ArgumentNullException(nameof(gltfGenerator));
    }

    /// <summary>
    /// 生成瓦片文件数据 - 实现抽象方法
    /// 调用B3DM格式的具体实现
    /// 支持有纹理（MeshT）和无纹理（Mesh）两种情况
    /// </summary>
    /// <param name="mesh">网格数据（IMesh接口）</param>
    /// <returns>B3DM瓦片文件的二进制数据</returns>
    public override byte[] GenerateTile(IMesh mesh)
    {
        return GenerateB3DM(mesh);
    }

    /// <summary>
    /// 获取瓦片格式名称
    /// </summary>
    /// <returns>格式名称 "B3DM"</returns>
    protected override string GetFormatName()
    {
        return "B3DM";
    }

    /// <summary>
    /// 生成B3DM文件数据
    /// 算法流程: MeshT → GLB → B3DM
    /// </summary>
    /// <param name="mesh">网格数据</param>
    /// <returns>B3DM文件的二进制数据</returns>
    public byte[] GenerateB3DM(IMesh mesh)
    {
        ValidateInput(mesh);

        _logger.LogDebug("开始生成B3DM: 类型={MeshType}, 三角形数={Count}",
            mesh.GetType().Name, mesh.FacesCount);

        try
        {
            // 1. 生成GLB二进制数据（_gltfGenerator 已支持 IMesh）
            var glbData = GenerateGLB(mesh);

            // 2. 构建Feature Table (必需)
            int batchCount = mesh.HasTexture && mesh is MeshT meshT
                ? meshT.Materials.Count
                : 1; // 无纹理使用默认材质(1个批次)

            var featureTableJson = GenerateFeatureTableJson(batchCount);
            var featureTableJsonBytes = Encoding.UTF8.GetBytes(featureTableJson);

            // 对齐到4字节边界
            var featureTableJsonPadded = PadTo4ByteBoundary(featureTableJsonBytes);

            // Feature Table Binary (当前为空)
            var featureTableBinary = Array.Empty<byte>();

            // 3. 构建Batch Table (包含材质信息)
            var batchTableJson = GenerateBatchTableJson(mesh);
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
    /// 支持POSITION, NORMAL, TEXCOORD_0属性
    /// 委托给 GltfGenerator 实现，确保材质支持的一致性
    /// </summary>
    /// <param name="mesh">网格数据（IMesh接口）</param>
    /// <returns>GLB文件的二进制数据</returns>
    private byte[] GenerateGLB(IMesh mesh)
    {
        // 使用 GltfGenerator 生成 GLB，确保材质支持（已支持 IMesh）
        return _gltfGenerator.GenerateGLB(mesh);
    }

    /// <summary>
    /// 生成Feature Table JSON
    /// 定义batch的数量（如果有材质，batch数量等于材质数量）
    /// </summary>
    private string GenerateFeatureTableJson(int batchCount)
    {
        var featureTable = new
        {
            BATCH_LENGTH = batchCount
        };

        return JsonSerializer.Serialize(featureTable);
    }

    /// <summary>
    /// 生成Batch Table JSON
    /// 存储每个batch的材质ID和名称
    /// 支持有纹理（MeshT）和无纹理（Mesh）两种情况
    /// </summary>
    /// <param name="mesh">网格数据（IMesh接口）</param>
    /// <returns>Batch Table JSON字符串</returns>
    private string GenerateBatchTableJson(IMesh mesh)
    {
        // 无纹理网格：返回默认批次表
        if (!mesh.HasTexture || mesh is not MeshT meshT || meshT.Materials.Count == 0)
        {
            var defaultBatchTable = new
            {
                MaterialID = new[] { 0 },
                MaterialName = new[] { "DefaultMaterial" }
            };
            return JsonSerializer.Serialize(defaultBatchTable);
        }

        // 有纹理网格：创建材质名称到ID的映射
        var materialIds = new List<int>();
        var materialNames = new List<string>();

        for (int i = 0; i < meshT.Materials.Count; i++)
        {
            materialIds.Add(i);
            materialNames.Add(meshT.Materials[i].Name ?? $"Material_{i}");
        }

        var batchTable = new
        {
            MaterialID = materialIds.ToArray(),
            MaterialName = materialNames.ToArray()
        };

        var json = JsonSerializer.Serialize(batchTable, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _logger.LogDebug("Batch Table 包含 {Count} 个材质", materialIds.Count);
        return json;
    }

    /// <summary>
    /// 保存瓦片文件到磁盘 - 实现抽象方法
    /// </summary>
    /// <param name="mesh">网格数据</param>
    /// <param name="outputPath">输出文件路径</param>
    public override async Task SaveTileAsync(IMesh mesh, string outputPath)
    {
        await SaveB3DMFileAsync(mesh, outputPath);
    }

    /// <summary>
    /// 保存B3DM文件到磁盘
    /// </summary>
    /// <param name="mesh">网格数据</param>
    /// <param name="outputPath">输出文件路径</param>
    public async Task SaveB3DMFileAsync(IMesh mesh, string outputPath)
    {
        _logger.LogInformation("保存B3DM文件: {Path}", outputPath);

        try
        {
            var b3dmData = GenerateB3DM(mesh);

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
