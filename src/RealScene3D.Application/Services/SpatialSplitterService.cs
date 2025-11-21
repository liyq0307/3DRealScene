using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Application.Services.Slicing;
using RealScene3D.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RealScene3D.Application.Services;

public class SpatialSplitterService : ISpatialSplitterService
{
    private readonly ILogger<SpatialSplitterService> _logger;
    private readonly MeshSplitter _meshSplitter;

    public SpatialSplitterService(ILogger<SpatialSplitterService> logger, MeshSplitter meshSplitter)
    {
        _logger = logger;
        _meshSplitter = meshSplitter;
    }

    /// <summary>
    /// 递归地将网格分割成更小的子网格。
    /// </summary>
    /// <param name="initialTriangles">要分割的初始三角形列表。</param>
    /// <param name="initialMaterials">初始材质字典。</param>
    /// <param name="maxDepth">最大分割深度。</param>
    /// <param name="minTrianglesPerSplit">每个分割区域的最小三角形数量。</param>
    /// <returns>分割后的子网格列表，每个子网格包含其三角形、材质和包围盒。</returns>
    public async Task<List<SplitMeshResult>> SplitMeshRecursivelyAsync(
        List<Triangle> initialTriangles,
        Dictionary<string, Material> initialMaterials,
        int maxDepth,
        int minTrianglesPerSplit)
    {
        var results = new List<SplitMeshResult>();
        await PerformSplitRecursively(
            initialTriangles,
            initialMaterials,
            0,
            maxDepth,
            minTrianglesPerSplit,
            results,
            new TileCoordinates(0, 0, 0)); // 初始坐标为(0,0,0)

        return results;
    }

    private async Task PerformSplitRecursively(
        List<Triangle> currentTriangles,
        Dictionary<string, Material> currentMaterials,
        int currentDepth,
        int maxDepth,
        int minTrianglesPerSplit,
        List<SplitMeshResult> results,
        TileCoordinates currentCoordinates) // 新增参数
    {
        // 计算当前网格的包围盒
        var currentBounds = _meshSplitter.ComputeBoundingBox(currentTriangles);

        // 检查终止条件
        if (currentDepth >= maxDepth ||
            currentTriangles.Count <= minTrianglesPerSplit ||
            currentBounds.IsEmpty()) // 如果包围盒为空，说明没有有效几何体
        {
            if (currentTriangles.Count > 0)
            {
                results.Add(new SplitMeshResult
                {
                    Triangles = currentTriangles,
                    Materials = currentMaterials,
                    BoundingBox = currentBounds,
                    Coordinates = currentCoordinates // 赋值坐标
                });
            }
            return;
        }

        // 选择最佳分割轴
        var axis = _meshSplitter.SelectBestAxis(currentBounds);
        var threshold = _meshSplitter.ComputeSplitThreshold(currentBounds, axis);

        _logger.LogDebug("深度 {Depth}, 坐标 {Coords}: 分割 {Count} 个三角形，沿 {Axis} 轴，阈值 {Threshold:F3}",
            currentDepth, currentCoordinates.ToString(), currentTriangles.Count, axis, threshold);

        // 执行分割
        var (leftTriangles, leftMaterials, rightTriangles, rightMaterials) =
            _meshSplitter.Split(currentTriangles, currentMaterials, axis, threshold);

        // 根据分割轴生成新的子坐标
        TileCoordinates leftChildCoordinates = new TileCoordinates(
            axis == MeshSplitter.SplitAxis.X ? currentCoordinates.X * 2 : currentCoordinates.X,
            axis == MeshSplitter.SplitAxis.Y ? currentCoordinates.Y * 2 : currentCoordinates.Y,
            axis == MeshSplitter.SplitAxis.Z ? currentCoordinates.Z * 2 : currentCoordinates.Z
        );
        TileCoordinates rightChildCoordinates = new TileCoordinates(
            axis == MeshSplitter.SplitAxis.X ? currentCoordinates.X * 2 + 1 : currentCoordinates.X,
            axis == MeshSplitter.SplitAxis.Y ? currentCoordinates.Y * 2 + 1 : currentCoordinates.Y,
            axis == MeshSplitter.SplitAxis.Z ? currentCoordinates.Z * 2 + 1 : currentCoordinates.Z
        );

        // 递归处理左侧
        await PerformSplitRecursively(
            leftTriangles,
            leftMaterials,
            currentDepth + 1,
            maxDepth,
            minTrianglesPerSplit,
            results,
            leftChildCoordinates); // 传递新的坐标

        // 递归处理右侧
        await PerformSplitRecursively(
            rightTriangles,
            rightMaterials,
            currentDepth + 1,
            maxDepth,
            minTrianglesPerSplit,
            results,
            rightChildCoordinates); // 传递新的坐标
    }
}
