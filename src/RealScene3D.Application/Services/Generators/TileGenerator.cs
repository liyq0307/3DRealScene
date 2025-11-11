using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;

namespace RealScene3D.Application.Services.Generators;

/// <summary>
/// 瓦片生成器抽象基类 - 为各种3D Tiles格式提供统一接口
/// 支持的格式：B3DM (Batched 3D Model)、I3DM (Instanced 3D Model)、
/// PNTS (Point Cloud)、CMPT (Composite) 等
/// 设计模式：模板方法模式 + 策略模式
/// </summary>
public abstract class TileGenerator
{
    /// <summary>
    /// 日志记录器 - 子类可以使用
    /// </summary>
    protected readonly ILogger _logger;

    /// <summary>
    /// 构造函数 - 子类必须调用
    /// </summary>
    /// <param name="logger">日志记录器实例</param>
    protected TileGenerator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 生成瓦片文件数据 - 核心抽象方法
    /// 子类必须实现此方法以生成特定格式的瓦片文件
    /// </summary>
    /// <param name="triangles">三角形网格数据</param>
    /// <param name="bounds">空间包围盒，定义瓦片的空间范围</param>
    /// <param name="materials">材质字典（材质名称->材质对象），可为空</param>
    /// <returns>瓦片文件的二进制数据</returns>
    /// <exception cref="ArgumentException">当输入参数无效时抛出</exception>
    /// <exception cref="InvalidOperationException">当生成过程失败时抛出</exception>
    public abstract byte[] GenerateTile(List<Triangle> triangles, BoundingBox3D bounds, Dictionary<string, Material>? materials = null);

    /// <summary>
    /// 保存瓦片文件到磁盘 - 抽象方法
    /// 子类必须实现此方法以将生成的瓦片数据保存到指定路径
    /// 此方法应包含目录创建、文件写入、错误处理等通用逻辑
    /// </summary>
    /// <param name="triangles">三角形网格数据</param>
    /// <param name="bounds">空间包围盒</param>
    /// <param name="outputPath">输出文件的完整路径</param>
    /// <param name="materials">材质字典（可选）</param>
    /// <returns>异步任务</returns>
    /// <exception cref="ArgumentException">当输入参数无效时抛出</exception>
    /// <exception cref="IOException">当文件写入失败时抛出</exception>
    public abstract Task SaveTileAsync(List<Triangle> triangles, BoundingBox3D bounds, string outputPath, Dictionary<string, Material>? materials = null);

    /// <summary>
    /// 验证输入参数的有效性 - 通用验证逻辑
    /// 子类可以在 GenerateTile 开始时调用此方法
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <param name="bounds">包围盒</param>
    /// <exception cref="ArgumentNullException">当参数为null时抛出</exception>
    /// <exception cref="ArgumentException">当参数无效时抛出</exception>
    protected virtual void ValidateInput(List<Triangle> triangles, BoundingBox3D bounds)
    {
        if (triangles == null)
            throw new ArgumentNullException(nameof(triangles), "三角形列表不能为空");

        if (triangles.Count == 0)
            throw new ArgumentException("三角形列表不能为空集合", nameof(triangles));

        if (bounds == null)
            throw new ArgumentNullException(nameof(bounds), "包围盒不能为空");

        if (!bounds.IsValid())
            throw new ArgumentException("包围盒坐标无效", nameof(bounds));
    }

    /// <summary>
    /// 将字节数组填充到4字节边界对齐
    /// 算法：根据 3D Tiles 规范，许多数据块需要4字节对齐
    /// 应用：GLB chunks、Feature Table、Batch Table 等
    /// </summary>
    /// <param name="data">原始数据</param>
    /// <returns>填充后的数据，长度为4的倍数</returns>
    protected byte[] PadTo4ByteBoundary(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        // 计算需要填充的字节数：(4 - length % 4) % 4
        int padding = (4 - (data.Length % 4)) % 4;

        // 如果已经对齐，直接返回
        if (padding == 0)
            return data;

        // 创建新数组并填充空白字节
        var padded = new byte[data.Length + padding];
        Array.Copy(data, 0, padded, 0, data.Length);

        // 填充字节使用空格 (0x20) 以符合规范
        for (int i = data.Length; i < padded.Length; i++)
        {
            padded[i] = 0x20; // ASCII空格
        }

        return padded;
    }

    /// <summary>
    /// 将字节数组填充到8字节边界对齐
    /// 算法：某些格式（如I3DM）需要8字节对齐
    /// 应用：特定的二进制数据块
    /// </summary>
    /// <param name="data">原始数据</param>
    /// <returns>填充后的数据，长度为8的倍数</returns>
    protected byte[] PadTo8ByteBoundary(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        // 计算需要填充的字节数
        int padding = (8 - (data.Length % 8)) % 8;

        if (padding == 0)
            return data;

        var padded = new byte[data.Length + padding];
        Array.Copy(data, 0, padded, 0, data.Length);

        // 填充空字节
        for (int i = data.Length; i < padded.Length; i++)
        {
            padded[i] = 0x00;
        }

        return padded;
    }

    /// <summary>
    /// 计算三角形列表的包围盒 - 通用几何算法
    /// 算法：遍历所有顶点，找出最小和最大坐标
    /// 复杂度：O(n)，n为顶点总数
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <returns>计算得到的包围盒</returns>
    protected BoundingBox3D CalculateBoundingBox(List<Triangle> triangles)
    {
        if (triangles == null || triangles.Count == 0)
            throw new ArgumentException("无法从空三角形列表计算包围盒", nameof(triangles));

        // 初始化为极值
        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double minZ = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;
        double maxZ = double.MinValue;

        // 遍历所有三角形的所有顶点
        foreach (var triangle in triangles)
        {
            if (triangle.Vertices == null || triangle.Vertices.Length < 3)
                continue;

            foreach (var vertex in triangle.Vertices)
            {
                minX = Math.Min(minX, vertex.X);
                minY = Math.Min(minY, vertex.Y);
                minZ = Math.Min(minZ, vertex.Z);
                maxX = Math.Max(maxX, vertex.X);
                maxY = Math.Max(maxY, vertex.Y);
                maxZ = Math.Max(maxZ, vertex.Z);
            }
        }

        return new BoundingBox3D
        {
            MinX = minX,
            MinY = minY,
            MinZ = minZ,
            MaxX = maxX,
            MaxY = maxY,
            MaxZ = maxZ
        };
    }

    /// <summary>
    /// 计算几何中心点 - 包围盒中心
    /// 应用：用于LOD距离计算、视锥剔除等
    /// </summary>
    /// <param name="bounds">包围盒</param>
    /// <returns>中心点坐标</returns>
    protected Vector3D CalculateCenter(BoundingBox3D bounds)
    {
        return new Vector3D
        {
            X = (bounds.MinX + bounds.MaxX) / 2.0,
            Y = (bounds.MinY + bounds.MaxY) / 2.0,
            Z = (bounds.MinZ + bounds.MaxZ) / 2.0
        };
    }

    /// <summary>
    /// 计算包围盒对角线长度 - 用于几何误差计算
    /// 算法：三维空间距离公式
    /// 应用：LOD几何误差、瓦片优先级排序
    /// </summary>
    /// <param name="bounds">包围盒</param>
    /// <returns>对角线长度</returns>
    protected double CalculateDiagonalLength(BoundingBox3D bounds)
    {
        var dx = bounds.MaxX - bounds.MinX;
        var dy = bounds.MaxY - bounds.MinY;
        var dz = bounds.MaxZ - bounds.MinZ;

        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// 获取瓦片格式名称 - 用于日志和调试
    /// 子类应该重写此方法返回具体的格式名称
    /// </summary>
    /// <returns>格式名称（如 "B3DM", "I3DM", "PNTS"）</returns>
    protected virtual string GetFormatName()
    {
        return "Generic Tile";
    }

    /// <summary>
    /// 记录生成统计信息 - 通用日志模板
    /// 子类可以在生成完成后调用此方法
    /// </summary>
    /// <param name="triangleCount">三角形数量</param>
    /// <param name="vertexCount">顶点数量</param>
    /// <param name="outputSize">输出文件大小（字节）</param>
    protected void LogGenerationStats(int triangleCount, int vertexCount, int outputSize)
    {
        _logger.LogInformation(
            "{Format}生成完成: 三角形={Triangles}, 顶点={Vertices}, 文件大小={Size:N0}字节 ({SizeKB:F2}KB)",
            GetFormatName(),
            triangleCount,
            vertexCount,
            outputSize,
            outputSize / 1024.0);
    }

    /// <summary>
    /// 验证三角形数据的完整性 - 数据质量检查
    /// 检查退化三角形、无效顶点等问题
    /// </summary>
    /// <param name="triangles">三角形列表</param>
    /// <returns>有效的三角形列表</returns>
    protected List<Triangle> ValidateTriangles(List<Triangle> triangles)
    {
        var validTriangles = new List<Triangle>();
        int invalidCount = 0;

        foreach (var triangle in triangles)
        {
            // 检查顶点数组
            if (triangle.Vertices == null || triangle.Vertices.Length != 3)
            {
                invalidCount++;
                continue;
            }

            // 检查退化三角形（面积为0）
            var v1 = triangle.Vertices[0];
            var v2 = triangle.Vertices[1];
            var v3 = triangle.Vertices[2];

            // 检查是否有重复顶点
            if (AreVerticesEqual(v1, v2) || AreVerticesEqual(v2, v3) || AreVerticesEqual(v1, v3))
            {
                invalidCount++;
                continue;
            }

            // 检查顶点坐标是否有效（非NaN、非Infinity）
            if (!IsValidVertex(v1) || !IsValidVertex(v2) || !IsValidVertex(v3))
            {
                invalidCount++;
                continue;
            }

            validTriangles.Add(triangle);
        }

        if (invalidCount > 0)
        {
            _logger.LogWarning("发现{Count}个无效三角形，已过滤", invalidCount);
        }

        return validTriangles;
    }

    /// <summary>
    /// 判断两个顶点是否相等 - 使用容差比较
    /// </summary>
    private bool AreVerticesEqual(Vector3D v1, Vector3D v2)
    {
        const double epsilon = 1e-10;
        return Math.Abs(v1.X - v2.X) < epsilon &&
               Math.Abs(v1.Y - v2.Y) < epsilon &&
               Math.Abs(v1.Z - v2.Z) < epsilon;
    }

    /// <summary>
    /// 检查顶点坐标是否有效
    /// </summary>
    private bool IsValidVertex(Vector3D vertex)
    {
        return double.IsFinite(vertex.X) &&
               double.IsFinite(vertex.Y) &&
               double.IsFinite(vertex.Z);
    }
}
