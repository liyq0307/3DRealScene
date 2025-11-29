using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Geometry;
using System.Text;

namespace RealScene3D.Application.Services.Generators;

/// <summary>
/// CMPT生成器 - 生成Cesium 3D Tiles的Composite格式
/// 将多个不同格式的瓦片（B3DM、I3DM、PNTS等）组合成一个复合瓦片
/// 参考: Cesium 3D Tiles Specification - CMPT Format
/// 适用场景：混合数据类型、复杂场景优化、批量瓦片合并
/// 重构说明：已迁移到 MeshT 架构
/// </summary>
public class CmptGenerator : TileGenerator
{
    /// <summary>
    /// 子瓦片数据结构
    /// </summary>
    public class TileData
    {
        /// <summary>
        /// 瓦片格式类型（如 "b3dm", "i3dm", "pnts"）
        /// </summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>
        /// 瓦片的二进制数据
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// 瓦片描述（可选）
        /// </summary>
        public string? Description { get; set; }
    }

    private readonly B3dmGenerator? _b3dmGenerator;
    private readonly I3dmGenerator? _i3dmGenerator;
    private readonly PntsGenerator? _pntsGenerator;

    /// <summary>
    /// 构造函数
    /// </summary>
    public CmptGenerator(
        ILogger<CmptGenerator> logger,
        B3dmGenerator? b3dmGenerator = null,
        I3dmGenerator? i3dmGenerator = null,
        PntsGenerator? pntsGenerator = null) : base(logger)
    {
        _b3dmGenerator = b3dmGenerator;
        _i3dmGenerator = i3dmGenerator;
        _pntsGenerator = pntsGenerator;
    }

    /// <summary>
    /// 生成瓦片文件数据 - 实现抽象方法
    /// 默认实现：将网格数据生成为B3DM瓦片
    /// </summary>
    /// <param name="mesh">网格数据</param>
    /// <returns>CMPT瓦片文件的二进制数据</returns>
    public override byte[] GenerateTile(MeshT mesh)
    {
        if (_b3dmGenerator == null)
            throw new InvalidOperationException("B3dmGenerator未注入，无法使用默认实现");

        var b3dmData = _b3dmGenerator.GenerateTile(mesh);
        var tiles = new[]
        {
            new TileData
            {
                Format = "b3dm",
                Data = b3dmData,
                Description = "Default B3DM tile"
            }
        };

        return GenerateCMPT(tiles);
    }

    /// <summary>
    /// 保存瓦片文件到磁盘 - 实现抽象方法
    /// </summary>
    /// <param name="mesh">网格数据</param>
    /// <param name="outputPath">输出文件路径</param>
    public override async Task SaveTileAsync(MeshT mesh, string outputPath)
    {
        await SaveCMPTFileAsync(mesh, outputPath, true, false);
    }

    /// <summary>
    /// 获取瓦片格式名称
    /// </summary>
    protected override string GetFormatName() => "CMPT";

    /// <summary>
    /// 生成CMPT文件数据 - 从多个子瓦片数据
    /// 算法流程: 子瓦片数组 → CMPT (Header + Tiles)
    /// </summary>
    /// <param name="tiles">子瓦片数据数组</param>
    /// <returns>CMPT文件的二进制数据</returns>
    public byte[] GenerateCMPT(TileData[] tiles)
    {
        if (tiles == null || tiles.Length == 0)
            throw new ArgumentException("子瓦片数组不能为空", nameof(tiles));

        _logger.LogDebug("开始生成CMPT: 子瓦片数量={TileCount}", tiles.Length);

        try
        {
            // 验证所有子瓦片数据
            foreach (var tile in tiles)
            {
                if (tile.Data == null || tile.Data.Length == 0)
                    throw new ArgumentException("子瓦片数据不能为空");

                ValidateTileFormat(tile);
            }

            // 1. 计算总长度
            int headerLength = 16; // CMPT header固定16字节
            int tilesDataLength = tiles.Sum(t => t.Data.Length);
            int totalLength = headerLength + tilesDataLength;

            // 2. 写入CMPT数据
            using var ms = new MemoryStream(totalLength);
            using var writer = new BinaryWriter(ms);

            // CMPT Header (16 bytes)
            writer.Write(Encoding.UTF8.GetBytes("cmpt")); // magic (4 bytes)
            writer.Write((uint)1);                         // version (4 bytes)
            writer.Write((uint)totalLength);               // byteLength (4 bytes)
            writer.Write((uint)tiles.Length);              // tilesLength (4 bytes)

            // 写入所有子瓦片数据
            foreach (var tile in tiles)
            {
                writer.Write(tile.Data);
            }

            var result = ms.ToArray();

            _logger.LogInformation("CMPT生成完成: 子瓦片数={TileCount}, 总大小={Size}字节 ({SizeKB:F2}KB)",
                tiles.Length, result.Length, result.Length / 1024.0);

            // 输出详细信息
            for (int i = 0; i < tiles.Length; i++)
            {
                _logger.LogDebug("子瓦片[{Index}]: 格式={Format}, 大小={Size}字节, 描述={Description}",
                    i, tiles[i].Format, tiles[i].Data.Length, tiles[i].Description ?? "无");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成CMPT失败");
            throw;
        }
    }

    /// <summary>
    /// 生成CMPT文件 - 从网格数据生成多种格式的组合
    /// 算法：同时生成B3DM（网格）、PNTS（点云）并组合
    /// </summary>
    /// <param name="mesh">网格数据</param>
    /// <param name="includeB3dm">是否包含B3DM</param>
    /// <param name="includePnts">是否包含PNTS点云</param>
    /// <returns>CMPT文件的二进制数据</returns>
    public byte[] GenerateCMPTFromMesh(
        MeshT mesh,
        bool includeB3dm = true,
        bool includePnts = false)
    {
        ValidateInput(mesh);

        var tilesList = new List<TileData>();

        // 生成B3DM
        if (includeB3dm)
        {
            if (_b3dmGenerator == null)
                throw new InvalidOperationException("B3dmGenerator未注入");

            var b3dmData = _b3dmGenerator.GenerateTile(mesh);
            tilesList.Add(new TileData
            {
                Format = "b3dm",
                Data = b3dmData,
                Description = $"网格模型 ({mesh.Faces.Count}个三角形)"
            });
        }

        // 生成PNTS
        if (includePnts)
        {
            if (_pntsGenerator == null)
                throw new InvalidOperationException("PntsGenerator未注入");

            var pntsData = _pntsGenerator.GenerateTile(mesh);
            tilesList.Add(new TileData
            {
                Format = "pnts",
                Data = pntsData,
                Description = "点云数据"
            });
        }

        if (tilesList.Count == 0)
            throw new InvalidOperationException("至少需要包含一种瓦片格式");

        return GenerateCMPT(tilesList.ToArray());
    }

    /// <summary>
    /// 验证子瓦片格式的有效性
    /// </summary>
    private void ValidateTileFormat(TileData tile)
    {
        // 检查魔数（前4个字节）
        if (tile.Data.Length < 4)
            throw new ArgumentException($"瓦片数据太小，无法识别格式: {tile.Data.Length}字节");

        var magic = Encoding.UTF8.GetString(tile.Data, 0, 4);
        var validFormats = new[] { "b3dm", "i3dm", "pnts", "cmpt", "vctr", "geom" };

        if (!validFormats.Contains(magic))
        {
            _logger.LogWarning("子瓦片格式标识可能无效: {Magic}, 期望: {ValidFormats}",
                magic, string.Join(", ", validFormats));
        }
        else if (!string.IsNullOrEmpty(tile.Format) && tile.Format != magic)
        {
            _logger.LogWarning("子瓦片格式不匹配: 声明={Declared}, 实际={Actual}",
                tile.Format, magic);
        }

        // 更新格式标识
        if (string.IsNullOrEmpty(tile.Format))
        {
            tile.Format = magic;
        }
    }

    /// <summary>
    /// 分析CMPT文件结构 - 解析已有的CMPT文件
    /// </summary>
    /// <param name="cmptData">CMPT文件数据</param>
    /// <returns>子瓦片信息数组</returns>
    public TileData[] ParseCMPT(byte[] cmptData)
    {
        if (cmptData == null || cmptData.Length < 16)
            throw new ArgumentException("CMPT数据太小", nameof(cmptData));

        using var ms = new MemoryStream(cmptData);
        using var reader = new BinaryReader(ms);

        // 读取Header
        var magic = Encoding.UTF8.GetString(reader.ReadBytes(4));
        if (magic != "cmpt")
            throw new InvalidDataException($"无效的CMPT魔数: {magic}");

        var version = reader.ReadUInt32();
        var byteLength = reader.ReadUInt32();
        var tilesLength = reader.ReadUInt32();

        _logger.LogInformation("解析CMPT: 版本={Version}, 总大小={Size}字节, 子瓦片数={TileCount}",
            version, byteLength, tilesLength);

        // 读取子瓦片
        var tiles = new List<TileData>();
        for (int i = 0; i < tilesLength; i++)
        {
            // 读取子瓦片的魔数
            var tileMagic = Encoding.UTF8.GetString(reader.ReadBytes(4));

            // 回退4字节
            ms.Seek(-4, SeekOrigin.Current);

            // 读取版本以确定长度字段位置
            ms.Seek(4, SeekOrigin.Current);
            var tileVersion = reader.ReadUInt32();
            var tileByteLength = reader.ReadUInt32();

            // 回退到瓦片起始位置
            ms.Seek(-12, SeekOrigin.Current);

            // 读取完整的子瓦片数据
            var tileData = reader.ReadBytes((int)tileByteLength);

            tiles.Add(new TileData
            {
                Format = tileMagic,
                Data = tileData,
                Description = $"子瓦片 {i + 1}/{tilesLength}"
            });

            _logger.LogDebug("解析子瓦片[{Index}]: 格式={Format}, 大小={Size}字节",
                i, tileMagic, tileByteLength);
        }

        return tiles.ToArray();
    }

    /// <summary>
    /// 保存CMPT文件到磁盘
    /// </summary>
    public async Task SaveCMPTFileAsync(TileData[] tiles, string outputPath)
    {
        _logger.LogInformation("保存CMPT文件: {Path}", outputPath);

        try
        {
            var cmptData = GenerateCMPT(tiles);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(outputPath, cmptData);

            _logger.LogInformation("CMPT文件保存成功: {Path}, 大小={Size}字节, 子瓦片数={TileCount}",
                outputPath, cmptData.Length, tiles.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存CMPT文件失败: {Path}", outputPath);
            throw;
        }
    }

    /// <summary>
    /// 保存CMPT文件 - 从网格数据
    /// </summary>
    public async Task SaveCMPTFileAsync(
        MeshT mesh,
        string outputPath,
        bool includeB3dm = true,
        bool includePnts = false)
    {
        var cmptData = GenerateCMPTFromMesh(mesh, includeB3dm, includePnts);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(outputPath, cmptData);

        _logger.LogInformation("CMPT文件保存成功: {Path}, 大小={Size}字节",
            outputPath, cmptData.Length);
    }
}
