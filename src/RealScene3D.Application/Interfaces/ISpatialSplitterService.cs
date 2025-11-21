using RealScene3D.Domain.Entities;
using System.Collections.Generic;

namespace RealScene3D.Application.Interfaces;

public interface ISpatialSplitterService
{
    /// <summary>
    /// 递归地将网格分割成更小的子网格。
    /// </summary>
    /// <param name="initialTriangles">要分割的初始三角形列表。</param>
    /// <param name="initialMaterials">初始材质字典。</param>
    /// <param name="maxDepth">最大分割深度。</param>
    /// <param name="minTrianglesPerSplit">每个分割区域的最小三角形数量。</param>
    /// <returns>分割后的子网格列表，每个子网格包含其三角形、材质和包围盒。</returns>
    Task<List<SplitMeshResult>> SplitMeshRecursivelyAsync(
        List<Triangle> initialTriangles,
        Dictionary<string, Material> initialMaterials,
        int maxDepth,
        int minTrianglesPerSplit);
}

/// <summary>
/// 表示一个分割后的网格及其相关的几何和材质数据。
/// </summary>
public class SplitMeshResult
{
    /// <summary>
    /// 构成此子网格的三角形列表。
    /// </summary>
    public List<Triangle> Triangles { get; set; } = new List<Triangle>();

    /// <summary>
    /// 此子网格使用的材质字典。
    /// </summary>
    public Dictionary<string, Material> Materials { get; set; } = new Dictionary<string, Material>();

    /// <summary>
    /// 此子网格的轴对齐包围盒。
    /// </summary>
    public BoundingBox3D BoundingBox { get; set; } = new BoundingBox3D();

    /// <summary>
    /// 此子网格的唯一标识符（例如，用于文件名）。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 此子网格在空间分割网格中的坐标。
    /// </summary>
    public TileCoordinates Coordinates { get; set; } = new TileCoordinates(0, 0, 0); // 默认值
}

/// <summary>
/// 表示一个瓦片在空间分割网格中的3D坐标。
/// </summary>
public class TileCoordinates
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }

    public TileCoordinates(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public override bool Equals(object? obj)
    {
        return obj is TileCoordinates coordinates &&
               X == coordinates.X &&
               Y == coordinates.Y &&
               Z == coordinates.Z;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }

    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }
}
