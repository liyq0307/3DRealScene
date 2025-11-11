using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using System.Text;
using System.Text.Json;

namespace RealScene3D.Application.Services.Generators;

/// <summary>
/// I3DM生成器 - 生成Cesium 3D Tiles的Instanced 3D Model格式
/// 用于大量相同模型的实例化渲染（如树木、路灯、标志牌等）
/// 参考: Cesium 3D Tiles Specification - I3DM Format
/// 适用场景：批量重复对象、GPU实例化渲染优化
/// </summary>
public class I3dmGenerator : TileGenerator
{
    private readonly B3dmGenerator _b3dmGenerator;

    /// <summary>
    /// 构造函数 - 注入依赖
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="b3dmGenerator">B3DM生成器，用于生成嵌入的GLB数据</param>
    public I3dmGenerator(ILogger<I3dmGenerator> logger, B3dmGenerator b3dmGenerator) : base(logger)
    {
        _b3dmGenerator = b3dmGenerator ?? throw new ArgumentNullException(nameof(b3dmGenerator));
    }

    /// <summary>
    /// 生成瓦片文件数据 - 实现抽象方法
    /// </summary>
    /// <param name="triangles">三角形网格数据（用于生成基础模型）</param>
    /// <param name="bounds">空间包围盒</param>
    /// <param name="materials">材质字典</param>
    /// <returns>I3DM瓦片文件的二进制数据</returns>
    public override byte[] GenerateTile(List<Triangle> triangles, BoundingBox3D bounds, Dictionary<string, Material>? materials = null)
    {
        return GenerateI3DM(triangles, bounds, 1, null, materials); // 默认1个实例
    }

    /// <summary>
    /// 生成I3DM文件数据 - 支持多实例
    /// 算法流程: 基础模型 → GLB → I3DM (Feature Table + GLB)
    /// </summary>
    /// <param name="triangles">基础模型的三角形列表</param>
    /// <param name="bounds">包围盒</param>
    /// <param name="instanceCount">实例数量</param>
    /// <param name="positions">实例位置数组（可选，未提供则均匀分布）</param>
    /// <param name="materials">材质字典</param>
    /// <returns>I3DM文件的二进制数据</returns>
    public byte[] GenerateI3DM(
        List<Triangle> triangles,
        BoundingBox3D bounds,
        int instanceCount,
        Vector3D[]? positions = null,
        Dictionary<string, Material>? materials = null)
    {
        ValidateInput(triangles, bounds);

        if (instanceCount <= 0)
            throw new ArgumentException("实例数量必须大于0", nameof(instanceCount));

        _logger.LogDebug("开始生成I3DM: 三角形数={TriangleCount}, 实例数={InstanceCount}",
            triangles.Count, instanceCount);

        try
        {
            // 1. 生成基础模型的GLB数据
            var glbData = _b3dmGenerator.GenerateTile(triangles, bounds, materials);

            // 2. 生成实例位置数据
            var instancePositions = positions ?? GenerateDefaultPositions(bounds, instanceCount);

            // 3. 构建Feature Table
            var (featureTableJson, featureTableBinary) = CreateFeatureTable(instancePositions);
            var featureTableJsonBytes = Encoding.UTF8.GetBytes(featureTableJson);
            var featureTableJsonPadded = PadTo4ByteBoundary(featureTableJsonBytes);
            var featureTableBinaryPadded = PadTo8ByteBoundary(featureTableBinary);

            // 4. 构建Batch Table (可选，当前为空)
            var batchTableJson = "{}";
            var batchTableJsonBytes = Encoding.UTF8.GetBytes(batchTableJson);
            var batchTableJsonPadded = PadTo4ByteBoundary(batchTableJsonBytes);
            var batchTableBinary = Array.Empty<byte>();

            // 5. 计算总长度
            int headerLength = 32; // I3DM header固定32字节
            int featureTableJsonLength = featureTableJsonPadded.Length;
            int featureTableBinaryLength = featureTableBinaryPadded.Length;
            int batchTableJsonLength = batchTableJsonPadded.Length;
            int batchTableBinaryLength = batchTableBinary.Length;
            int glbLength = glbData.Length;

            int totalLength = headerLength +
                            featureTableJsonLength +
                            featureTableBinaryLength +
                            batchTableJsonLength +
                            batchTableBinaryLength +
                            glbLength;

            // 6. 写入I3DM数据
            using var ms = new MemoryStream(totalLength);
            using var writer = new BinaryWriter(ms);

            // I3DM Header (32 bytes)
            writer.Write(Encoding.UTF8.GetBytes("i3dm")); // magic (4 bytes)
            writer.Write((uint)1);                         // version (4 bytes)
            writer.Write((uint)totalLength);               // byteLength (4 bytes)
            writer.Write((uint)featureTableJsonLength);    // featureTableJSONByteLength (4 bytes)
            writer.Write((uint)featureTableBinaryLength);  // featureTableBinaryByteLength (4 bytes)
            writer.Write((uint)batchTableJsonLength);      // batchTableJSONByteLength (4 bytes)
            writer.Write((uint)batchTableBinaryLength);    // batchTableBinaryByteLength (4 bytes)
            writer.Write((uint)1);                         // gltfFormat: 1=embedded GLB (4 bytes)

            // Feature Table
            writer.Write(featureTableJsonPadded);
            writer.Write(featureTableBinaryPadded);

            // Batch Table
            writer.Write(batchTableJsonPadded);
            writer.Write(batchTableBinary);

            // GLB
            writer.Write(glbData);

            var result = ms.ToArray();
            LogGenerationStats(triangles.Count, instancePositions.Length, result.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成I3DM失败");
            throw;
        }
    }

    /// <summary>
    /// 创建Feature Table - 包含实例位置信息
    /// </summary>
    private (string json, byte[] binary) CreateFeatureTable(Vector3D[] positions)
    {
        int instanceCount = positions.Length;
        int positionsByteLength = instanceCount * 3 * sizeof(float);

        // Feature Table JSON
        var featureTable = new
        {
            INSTANCES_LENGTH = instanceCount,
            POSITION = new
            {
                byteOffset = 0
            }
        };

        var json = JsonSerializer.Serialize(featureTable, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = null // 使用大写属性名
        });

        // Feature Table Binary - 位置数据
        var binary = new byte[positionsByteLength];
        int offset = 0;

        foreach (var pos in positions)
        {
            // 写入 x, y, z (float32)
            Buffer.BlockCopy(BitConverter.GetBytes((float)pos.X), 0, binary, offset, sizeof(float));
            offset += sizeof(float);
            Buffer.BlockCopy(BitConverter.GetBytes((float)pos.Y), 0, binary, offset, sizeof(float));
            offset += sizeof(float);
            Buffer.BlockCopy(BitConverter.GetBytes((float)pos.Z), 0, binary, offset, sizeof(float));
            offset += sizeof(float);
        }

        return (json, binary);
    }

    /// <summary>
    /// 生成默认实例位置 - 在包围盒内均匀分布
    /// 算法：网格分布策略
    /// </summary>
    private Vector3D[] GenerateDefaultPositions(BoundingBox3D bounds, int count)
    {
        var positions = new Vector3D[count];
        var center = CalculateCenter(bounds);

        if (count == 1)
        {
            positions[0] = center;
            return positions;
        }

        // 计算网格维度
        int gridSize = (int)Math.Ceiling(Math.Pow(count, 1.0 / 3.0));
        var sizeX = (bounds.MaxX - bounds.MinX) / gridSize;
        var sizeY = (bounds.MaxY - bounds.MinY) / gridSize;
        var sizeZ = (bounds.MaxZ - bounds.MinZ) / gridSize;

        int index = 0;
        for (int z = 0; z < gridSize && index < count; z++)
        {
            for (int y = 0; y < gridSize && index < count; y++)
            {
                for (int x = 0; x < gridSize && index < count; x++)
                {
                    positions[index++] = new Vector3D
                    {
                        X = bounds.MinX + (x + 0.5) * sizeX,
                        Y = bounds.MinY + (y + 0.5) * sizeY,
                        Z = bounds.MinZ + (z + 0.5) * sizeZ
                    };
                }
            }
        }

        return positions;
    }

    /// <summary>
    /// 获取瓦片格式名称
    /// </summary>
    protected override string GetFormatName() => "I3DM";

    /// <summary>
    /// 保存瓦片文件到磁盘 - 实现抽象方法
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="bounds">空间包围盒</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="materials">材质字典（可选）</param>
    public override async Task SaveTileAsync(List<Triangle> triangles, BoundingBox3D bounds, string outputPath, Dictionary<string, Material>? materials = null)
    {
        await SaveI3DMFileAsync(triangles, bounds, outputPath, 1, null);
    }

    /// <summary>
    /// 保存I3DM文件到磁盘
    /// </summary>
    public async Task SaveI3DMFileAsync(
        List<Triangle> triangles,
        BoundingBox3D bounds,
        string outputPath,
        int instanceCount = 1,
        Vector3D[]? positions = null)
    {
        _logger.LogInformation("保存I3DM文件: {Path}", outputPath);

        try
        {
            var i3dmData = GenerateI3DM(triangles, bounds, instanceCount, positions);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(outputPath, i3dmData);

            _logger.LogInformation("I3DM文件保存成功: {Path}, 大小={Size}字节, 实例数={InstanceCount}",
                outputPath, i3dmData.Length, instanceCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存I3DM文件失败: {Path}", outputPath);
            throw;
        }
    }
}
