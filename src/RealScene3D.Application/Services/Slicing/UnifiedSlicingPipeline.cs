using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.MinIO;
using System.Text.Json;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// 统一切片流水线 - 完全符合 Obj2Tiles 的实现
///
/// 正确的三阶段流程（参考 Obj2Tiles 文档）：
/// Stage 1: Decimation（网格简化） - 为整个模型生成多个 LOD 级别
/// Stage 2: Splitting（空间分割） - **四叉树递归分割（每次同时沿X和Y轴分割）**
/// Stage 3: Tile Generation（切片生成） - 将分割后的片段转换为 3D Tiles
///
/// 关键理解（来自 Obj2Tiles 文档和文件命名分析）：
/// "For every decimated mesh, the program splits it recursively"
/// - 每个 LOD 级别的网格都会被独立分割
/// - 使用四叉树分割：每次同时沿 X 和 Y 轴分割，产生 4 个子节点
/// - divisions=2 表示递归深度2层，最终产生 4^2 = 16 个叶子节点
/// - 文件命名模式：Mesh-X1-Y1-X2-Y2.b3dm（每个组件表示一层的象限选择）
/// </summary>
public class UnifiedSlicingPipeline
{
    private readonly ILogger<UnifiedSlicingPipeline> _logger;
    private readonly MeshDecimationService _decimationService;
    private readonly MeshSplitter _meshSplitter;
    private readonly ITileGeneratorFactory _tileGeneratorFactory;
    private readonly IModelLoaderFactory _modelLoaderFactory;
    private readonly IMinioStorageService _minioService;
    private readonly SlicingDataService _dataService;

    // 配置参数
    private const int MinTrianglesPerSlice = 1; // 最少三角形数（降低阈值，避免过早终止）

    /// <summary>
    /// 空间单元 - 代表空间分割后的一个区域
    /// </summary>
    private class SpatialCell
    {
        public string QuadrantPath { get; set; } = "";  // 象限路径，如 "XL-YL-XR-YR"
        public int Depth { get; set; }
        public int LodLevel { get; set; }
        public List<Triangle> Triangles { get; set; } = new();
        public Dictionary<string, Material> Materials { get; set; } = new();
        public BoundingBox3D Bounds { get; set; } = new();
    }

    public UnifiedSlicingPipeline(
        ILogger<UnifiedSlicingPipeline> logger,
        MeshDecimationService decimationService,
        MeshSplitter meshSplitter,
        ITileGeneratorFactory tileGeneratorFactory,
        IModelLoaderFactory modelLoaderFactory,
        IMinioStorageService minioService,
        SlicingDataService dataService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _decimationService = decimationService ?? throw new ArgumentNullException(nameof(decimationService));
        _meshSplitter = meshSplitter ?? throw new ArgumentNullException(nameof(meshSplitter));
        _tileGeneratorFactory = tileGeneratorFactory ?? throw new ArgumentNullException(nameof(tileGeneratorFactory));
        _modelLoaderFactory = modelLoaderFactory ?? throw new ArgumentNullException(nameof(modelLoaderFactory));
        _minioService = minioService ?? throw new ArgumentNullException(nameof(minioService));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
    }

    public class SlicingResult
    {
        public List<Slice> Slices { get; set; } = new();
        public int TotalTriangles { get; set; }
        public int TotalSlices { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public Dictionary<int, int> SlicesPerLevel { get; set; } = new();
    }

    public async Task<SlicingResult> ProcessAsync(
        SlicingTask task,
        SlicingConfig config,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("========== 开始统一切片流水线处理 ==========");
        _logger.LogInformation("任务ID: {TaskId}, 任务名称: {TaskName}", task.Id, task.Name);
        _logger.LogInformation("源模型: {SourceModel}", task.SourceModelPath);
        _logger.LogInformation("配置: LOD级别={LodLevels}, 四叉树递归深度={Divisions} (理论最多{Cells}个叶子节点), 格式={Format}",
            config.LodLevels, config.Divisions, Math.Pow(4, config.Divisions), config.TileFormat);
        _logger.LogInformation("预估切片总数: {EstimatedTotal} 个 (= {LodLevels} LOD × {Cells} 空间单元)",
            config.LodLevels * Math.Pow(4, config.Divisions), config.LodLevels, Math.Pow(4, config.Divisions));

        try
        {
            // Stage 0: 加载模型数据
            _logger.LogInformation("---------- Stage 0: 加载模型数据 ----------");
            var (originalTriangles, modelBounds, materials) = await LoadModelDataAsync(task, cancellationToken);

            if (originalTriangles.Count == 0)
            {
                _logger.LogWarning("模型中没有三角形数据，无法进行切片");
                return new SlicingResult();
            }

            _logger.LogInformation("模型加载完成：三角形数={Count}, 包围盒=[{MinX:F2},{MinY:F2},{MinZ:F2}]-[{MaxX:F2},{MaxY:F2},{MaxZ:F2}]",
                originalTriangles.Count, modelBounds.MinX, modelBounds.MinY, modelBounds.MinZ,
                modelBounds.MaxX, modelBounds.MaxY, modelBounds.MaxZ);

            // Stage 1: Decimation
            _logger.LogInformation("---------- Stage 1: Decimation（网格简化） ----------");
            var lodMeshes = GenerateLODMeshes(originalTriangles, config);

            if (lodMeshes.Count == 0)
            {
                for (int i = 0; i < config.LodLevels; i++)
                {
                    lodMeshes.Add(new MeshDecimationService.DecimatedMesh
                    {
                        Triangles = originalTriangles,
                        SimplifiedTriangleCount = originalTriangles.Count,
                        ReductionRatio = 0.0
                    });
                }
            }

            // Stage 2 & 3: 对每个 LOD 网格分别进行四叉树分割和切片生成
            _logger.LogInformation("---------- Stage 2 & 3: Quadtree Splitting & Tile Generation ----------");
            var allSlices = new List<Slice>();

            for (int lodLevel = 0; lodLevel < lodMeshes.Count; lodLevel++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var lodMesh = lodMeshes[lodLevel];
                _logger.LogInformation("处理 LOD {Level}: {Count} 个三角形（简化率 {Ratio:F1}%）",
                    lodLevel, lodMesh.SimplifiedTriangleCount, lodMesh.ReductionRatio * 100);

                // 对该 LOD 级别的网格进行四叉树空间分割
                var spatialCells = await QuadtreeSplitForLODAsync(
                    lodMesh.Triangles,
                    materials,
                    modelBounds,
                    lodLevel,
                    config.Divisions,
                    cancellationToken);

                _logger.LogInformation("LOD {Level} 四叉树分割完成：生成 {Count} 个空间单元",
                    lodLevel, spatialCells.Count);

                // 为该 LOD 级别的所有空间单元生成切片
                var lodSlices = await GenerateTilesForCellsAsync(
                    spatialCells,
                    task,
                    config,
                    cancellationToken);

                allSlices.AddRange(lodSlices);

                _logger.LogInformation("LOD {Level} 切片生成完成：{Count} 个切片",
                    lodLevel, lodSlices.Count);
            }

            _logger.LogInformation("所有切片生成完成：总计 {Count} 个切片", allSlices.Count);

            // Stage 4: 生成 tileset.json
            if (config.GenerateTileset && allSlices.Count > 0)
            {
                _logger.LogInformation("---------- Stage 4: 生成 tileset.json ----------");
                await GenerateTilesetJsonAsync(allSlices, modelBounds, config, task, cancellationToken);
            }

            var processingTime = DateTime.UtcNow - startTime;
            var slicesPerLevel = allSlices.GroupBy(s => s.Level).ToDictionary(g => g.Key, g => g.Count());

            _logger.LogInformation("========== 切片处理完成 ==========");
            _logger.LogInformation("总切片数: {TotalSlices}", allSlices.Count);
            _logger.LogInformation("处理时间: {Time:F2} 秒", processingTime.TotalSeconds);
            foreach (var (level, count) in slicesPerLevel.OrderBy(x => x.Key))
            {
                _logger.LogInformation("  LOD {Level}: {Count} 个切片", level, count);
            }

            return new SlicingResult
            {
                Slices = allSlices,
                TotalTriangles = originalTriangles.Count,
                TotalSlices = allSlices.Count,
                ProcessingTime = processingTime,
                SlicesPerLevel = slicesPerLevel
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "统一切片流水线处理失败");
            throw;
        }
    }

    private async Task<(List<Triangle> triangles, BoundingBox3D bounds, Dictionary<string, Material> materials)>
        LoadModelDataAsync(SlicingTask task, CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始加载模型：{Path}", task.SourceModelPath);

        var (triangles, bounds, materials) = await _dataService.LoadTrianglesFromModelAsync(
            task.SourceModelPath,
            cancellationToken);

        if (triangles == null || triangles.Count == 0)
        {
            _logger.LogWarning("模型加载失败或没有三角形数据");
            return (new List<Triangle>(), new BoundingBox3D(), new Dictionary<string, Material>());
        }

        if (!bounds.IsValid())
        {
            bounds = _meshSplitter.ComputeBoundingBox(triangles);
        }

        return (triangles, bounds, materials ?? new Dictionary<string, Material>());
    }

    private List<MeshDecimationService.DecimatedMesh> GenerateLODMeshes(
        List<Triangle> originalTriangles,
        SlicingConfig config)
    {
        if (!config.EnableMeshDecimation || config.LodLevels <= 1)
        {
            _logger.LogInformation("跳过网格简化（未启用或LOD级别<=1）");
            return new List<MeshDecimationService.DecimatedMesh>();
        }

        _logger.LogInformation("为整个模型生成 {LodLevels} 个 LOD 级别", config.LodLevels);

        var lodMeshes = _decimationService.GenerateLODs(
            originalTriangles,
            config.LodLevels,
            enableParallel: true);

        for (int i = 0; i < lodMeshes.Count; i++)
        {
            var mesh = lodMeshes[i];
            _logger.LogInformation("  LOD {Level}: {Count} 个三角形（简化率 {Ratio:F1}%）",
                i, mesh.SimplifiedTriangleCount, mesh.ReductionRatio * 100);
        }

        return lodMeshes;
    }

    /// <summary>
    /// 对单个 LOD 级别的网格进行四叉树空间分割
    /// </summary>
    private async Task<List<SpatialCell>> QuadtreeSplitForLODAsync(
        List<Triangle> triangles,
        Dictionary<string, Material> materials,
        BoundingBox3D modelBounds,
        int lodLevel,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        var cells = new List<SpatialCell>();

        _logger.LogInformation("开始LOD {Level}的四叉树分割：三角形数={Count}, 最大深度={MaxDepth}",
            lodLevel, triangles.Count, maxDepth);

        await RecursiveQuadtreeSplitAsync(
            triangles,
            materials,
            modelBounds,
            lodLevel,
            "",  // 初始象限路径为空
            0,   // 初始深度
            maxDepth,
            cells,
            cancellationToken);

        _logger.LogInformation("LOD {Level}四叉树分割完成：生成 {Count} 个非空叶子节点", lodLevel, cells.Count);

        return cells;
    }

    /// <summary>
    /// 递归四叉树分割（每次同时沿 X 和 Y 轴分割，产生 4 个子节点）
    /// 完全符合 Obj2Tiles 的分割逻辑
    /// </summary>
    private async Task RecursiveQuadtreeSplitAsync(
        List<Triangle> triangles,
        Dictionary<string, Material> materials,
        BoundingBox3D bounds,
        int lodLevel,
        string quadrantPath,
        int depth,
        int maxDepth,
        List<SpatialCell> cells,
        CancellationToken cancellationToken)
    {
        await Task.Yield();

        if (cancellationToken.IsCancellationRequested)
            return;

        // 终止条件：达到最大深度
        if (depth >= maxDepth)
        {
            if (triangles.Count > 0)
            {
                cells.Add(new SpatialCell
                {
                    QuadrantPath = quadrantPath,
                    Depth = depth,
                    LodLevel = lodLevel,
                    Triangles = triangles,
                    Materials = materials,
                    Bounds = bounds
                });

                _logger.LogDebug("叶子节点：LOD={Lod}, 深度={Depth}, 路径={Path}, 三角形={Count}",
                    lodLevel, depth, quadrantPath, triangles.Count);
            }
            return;
        }

        // 计算 X 和 Y 轴的分割阈值
        double xMid = (bounds.MinX + bounds.MaxX) / 2.0;
        double yMid = (bounds.MinY + bounds.MaxY) / 2.0;

        _logger.LogDebug("四叉树分割：LOD={Lod}, 深度={Depth}, 路径={Path}, 三角形={Count}, X中点={XMid:F3}, Y中点={YMid:F3}",
            lodLevel, depth, string.IsNullOrEmpty(quadrantPath) ? "根节点" : quadrantPath, triangles.Count, xMid, yMid);

        // 四个象限：XL-YL, XL-YR, XR-YL, XR-YR
        var quadrants = new[]
        {
            ("XL-YL", bounds.MinX, xMid, bounds.MinY, yMid),  // 左下
            ("XL-YR", bounds.MinX, xMid, yMid, bounds.MaxY),  // 左上
            ("XR-YL", xMid, bounds.MaxX, bounds.MinY, yMid),  // 右下
            ("XR-YR", xMid, bounds.MaxX, yMid, bounds.MaxY)   // 右上
        };

        var tasks = new List<Task>();

        foreach (var (name, minX, maxX, minY, maxY) in quadrants)
        {
            // 创建该象限的包围盒
            var quadBounds = new BoundingBox3D
            {
                MinX = minX,
                MaxX = maxX,
                MinY = minY,
                MaxY = maxY,
                MinZ = bounds.MinZ,
                MaxZ = bounds.MaxZ
            };

            // 筛选属于该象限的三角形
            var quadTriangles = FilterTrianglesInBounds(triangles, quadBounds);

            _logger.LogDebug("  象限 {Name}: {Count} 个三角形", name, quadTriangles.Count);

            if (quadTriangles.Count > 0)
            {
                string newPath = string.IsNullOrEmpty(quadrantPath) ? name : $"{quadrantPath}-{name}";

                var task = RecursiveQuadtreeSplitAsync(
                    quadTriangles,
                    materials,
                    quadBounds,
                    lodLevel,
                    newPath,
                    depth + 1,
                    maxDepth,
                    cells,
                    cancellationToken);

                tasks.Add(task);
            }
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 筛选在指定包围盒内的三角形
    /// 使用三角形中心点判断归属，避免边界重复
    /// </summary>
    private List<Triangle> FilterTrianglesInBounds(List<Triangle> triangles, BoundingBox3D bounds)
    {
        var result = new List<Triangle>();

        foreach (var triangle in triangles)
        {
            // 计算三角形的中心点
            var center = triangle.ComputeCenter();

            // 检查中心点是否在包围盒内
            // 注意：为了避免边界三角形重复，MaxX和MaxY使用开区间（<而非<=）
            // 只有当中心点是整个模型的最大边界时才使用闭区间
            bool inBounds = center.X >= bounds.MinX && center.X < bounds.MaxX &&
                           center.Y >= bounds.MinY && center.Y < bounds.MaxY &&
                           center.Z >= bounds.MinZ && center.Z <= bounds.MaxZ;

            if (!inBounds)
            {
                // 特殊处理：如果中心点恰好在最大边界上，仍然包含该三角形
                // 这样可以确保边界三角形不会被丢失
                double epsilon = 1e-10;
                if (Math.Abs(center.X - bounds.MaxX) < epsilon)
                {
                    inBounds = center.X >= bounds.MinX && center.X <= bounds.MaxX &&
                              center.Y >= bounds.MinY && center.Y < bounds.MaxY &&
                              center.Z >= bounds.MinZ && center.Z <= bounds.MaxZ;
                }
                if (!inBounds && Math.Abs(center.Y - bounds.MaxY) < epsilon)
                {
                    inBounds = center.X >= bounds.MinX && center.X < bounds.MaxX &&
                              center.Y >= bounds.MinY && center.Y <= bounds.MaxY &&
                              center.Z >= bounds.MinZ && center.Z <= bounds.MaxZ;
                }
                if (!inBounds && Math.Abs(center.X - bounds.MaxX) < epsilon && Math.Abs(center.Y - bounds.MaxY) < epsilon)
                {
                    inBounds = center.X >= bounds.MinX && center.X <= bounds.MaxX &&
                              center.Y >= bounds.MinY && center.Y <= bounds.MaxY &&
                              center.Z >= bounds.MinZ && center.Z <= bounds.MaxZ;
                }
            }

            if (inBounds)
            {
                result.Add(triangle);
            }
        }

        return result;
    }

    private async Task<List<Slice>> GenerateTilesForCellsAsync(
        List<SpatialCell> cells,
        SlicingTask task,
        SlicingConfig config,
        CancellationToken cancellationToken)
    {
        var slices = new List<Slice>();

        foreach (var cell in cells)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var slice = await GenerateSliceForCellAsync(task, cell, config, cancellationToken);

            if (slice != null)
            {
                slices.Add(slice);
            }
        }

        return slices;
    }

    private async Task<Slice?> GenerateSliceForCellAsync(
        SlicingTask task,
        SpatialCell cell,
        SlicingConfig config,
        CancellationToken cancellationToken)
    {
        if (cell.Triangles.Count == 0)
            return null;

        try
        {
            var fileExtension = config.OutputFormat.ToLower() switch
            {
                "gltf" => ".gltf",
                "b3dm" => ".b3dm",
                "i3dm" => ".i3dm",
                "pnts" => ".pnts",
                "cmpt" => ".cmpt",
                _ => ".b3dm"
            };

            // 路径格式: {OutputPath}/{LOD-Level}/Mesh-{QuadrantPath}{Extension}
            var fileName = string.IsNullOrEmpty(cell.QuadrantPath)
                ? $"Mesh-Root{fileExtension}"
                : $"Mesh-{cell.QuadrantPath}{fileExtension}";

            var filePath = Path.Combine(
                task.OutputPath ?? "tiles",
                $"LOD-{cell.LodLevel}",
                fileName
            );

            // 从象限路径解析坐标（用于兼容现有的 Slice 实体）
            var (x, y, z) = ParseQuadrantPathToCoords(cell.QuadrantPath);

            var slice = new Slice
            {
                SlicingTaskId = task.Id,
                Level = cell.LodLevel,
                X = x,
                Y = y,
                Z = z,
                FilePath = filePath,
                BoundingBox = JsonSerializer.Serialize(cell.Bounds),
                CreatedAt = DateTime.UtcNow
            };

            var generated = await _dataService.GenerateSliceFileAsync(
                slice,
                config,
                cell.Triangles,
                cell.Materials,
                cancellationToken);

            if (!generated)
            {
                _logger.LogDebug("切片生成失败：LOD={Lod}, 路径={Path}",
                    cell.LodLevel, cell.QuadrantPath);
                return null;
            }

            return slice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成切片失败：LOD={Lod}, 路径={Path}",
                cell.LodLevel, cell.QuadrantPath);
            return null;
        }
    }

    /// <summary>
    /// 将象限路径转换为坐标（用于向后兼容）
    /// 例如：XL-YL-XR-YR → (x, y, z)
    /// </summary>
    private (int x, int y, int z) ParseQuadrantPathToCoords(string quadrantPath)
    {
        if (string.IsNullOrEmpty(quadrantPath))
            return (0, 0, 0);

        var parts = quadrantPath.Split('-');
        int x = 0, y = 0;

        for (int i = 0; i < parts.Length; i += 2)
        {
            if (i + 1 < parts.Length)
            {
                int xPart = parts[i] == "XR" ? 1 : 0;
                int yPart = parts[i + 1] == "YR" ? 1 : 0;

                x = x * 2 + xPart;
                y = y * 2 + yPart;
            }
        }

        return (x, y, 0);
    }

    private async Task GenerateTilesetJsonAsync(
        List<Slice> slices,
        BoundingBox3D modelBounds,
        SlicingConfig config,
        SlicingTask task,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dataService.GenerateTilesetJsonAsync(
                slices,
                config,
                modelBounds,
                task.OutputPath ?? string.Empty,
                config.StorageLocation,
                cancellationToken);

            _logger.LogInformation("tileset.json 生成成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成 tileset.json 失败");
        }
    }
}
