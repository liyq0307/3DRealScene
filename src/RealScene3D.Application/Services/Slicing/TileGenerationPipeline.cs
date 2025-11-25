using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using System.Text.Json;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// 瓦片生成流水线 - 集成网格简化、空间分割和切片生成
///
/// 流程说明：
/// 1. 加载模型数据（支持多种格式）
/// 2. 可选：对整个模型进行网格简化，生成多级LOD
/// 3. 对每个LOD网格进行递归四叉树空间分割，生成多个空间单元
/// 4. 为每个空间单元生成3D Tiles切片（支持多种输出格式）
/// 5. 可选：生成整体tileset.json索引文件
/// 实现细节：
/// - 四叉树分割同时沿X和Y轴分割
/// - 使用精确的三角面-AABB相交测试，确保空间分割的准确性
/// - 切片中只包含实际使用的材质，减小切片大小
/// - 支持并行处理，提高性能
/// 推荐使用场景：
/// - 需要高质量空间分割的倾斜摄影模型切片
/// - 需要多级LOD以适应不同视距的场景
/// - 需要灵活选择切片输出格式的应用
/// 注意事项：
/// - 对于超大模型，建议先进行预处理（如简化或分割）以提高性能
/// - 切片生成过程可能消耗较多内存，需根据实际环境调整
/// 作者：liyq
/// 日期：2025-11-15
/// </summary>
public class TileGenerationPipeline
{
    private readonly ILogger<TileGenerationPipeline> _logger;
    private readonly MeshDecimationService _decimationService;
    private readonly MeshSplitter _meshSplitter;
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

    public TileGenerationPipeline(
        ILogger<TileGenerationPipeline> logger,
        MeshDecimationService decimationService,
        MeshSplitter meshSplitter,
        SlicingDataService dataService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _decimationService = decimationService ?? throw new ArgumentNullException(nameof(decimationService));
        _meshSplitter = meshSplitter ?? throw new ArgumentNullException(nameof(meshSplitter));
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
        _logger.LogInformation("========== 开始瓦片生成流水线处理 ==========");
        _logger.LogInformation("任务ID: {TaskId}, 任务名称: {TaskName}", task.Id, task.Name);
        _logger.LogInformation("源模型: {SourceModel}", task.SourceModelPath);
        _logger.LogInformation("配置: LOD级别={LodLevels}, 四叉树递归深度={Divisions} (理论最多{Cells}个叶子节点), 格式={Format}",
            config.LodLevels, config.Divisions, Math.Pow(4, config.Divisions), config.OutputFormat);
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
            _logger.LogError(ex, "瓦片生成流水线处理失败");
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
                // 只保留该切片三角形实际使用的材质（避免每个切片都嵌入所有纹理）
                var usedMaterials = FilterUsedMaterials(triangles, materials);

                cells.Add(new SpatialCell
                {
                    QuadrantPath = quadrantPath,
                    Depth = depth,
                    LodLevel = lodLevel,
                    Triangles = triangles,
                    Materials = usedMaterials,
                    Bounds = bounds
                });

                _logger.LogDebug("叶子节点：LOD={Lod}, 深度={Depth}, 路径={Path}, 三角形={Count}, 使用材质={MatCount}",
                    lodLevel, depth, quadrantPath, triangles.Count, usedMaterials.Count);
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
    /// 筛选与指定包围盒相交的三角形
    /// 使用精确的三角面-AABB相交测试（基于分离轴定理），确保空间分割的准确性
    /// </summary>
    private List<Triangle> FilterTrianglesInBounds(List<Triangle> triangles, BoundingBox3D bounds)
    {
        var result = new List<Triangle>();

        foreach (var triangle in triangles)
        {
            if (Intersects(triangle, bounds))
            {
                result.Add(triangle);
            }
        }

        return result;
    }

    /// <summary>
    /// 筛选三角形实际使用的材质
    /// 避免每个切片都嵌入所有纹理，显著减小切片大小
    /// </summary>
    private Dictionary<string, Material> FilterUsedMaterials(
        List<Triangle> triangles,
        Dictionary<string, Material> allMaterials)
    {
        if (allMaterials == null || allMaterials.Count == 0)
            return new Dictionary<string, Material>();

        // 收集所有三角形使用的材质名称
        var usedMaterialNames = new HashSet<string>();
        foreach (var triangle in triangles)
        {
            if (!string.IsNullOrEmpty(triangle.MaterialName))
            {
                usedMaterialNames.Add(triangle.MaterialName);
            }
        }

        // 只返回使用到的材质
        var usedMaterials = new Dictionary<string, Material>();
        foreach (var materialName in usedMaterialNames)
        {
            if (allMaterials.TryGetValue(materialName, out var material))
            {
                usedMaterials[materialName] = material;
            }
        }

        return usedMaterials;
    }

    /// <summary>
    /// 执行三角面与AABB（轴对齐包围盒）的相交测试
    /// 这是基于分离轴定理（SAT）的优化实现
    /// </summary>
    private bool Intersects(Triangle triangle, BoundingBox3D box)
    {
        // 三角形的三个顶点
        var v0 = triangle.V1;
        var v1 = triangle.V2;
        var v2 = triangle.V3;

        // 包围盒的中心和半长
        var boxCenter = new Vector3D((box.MinX + box.MaxX) / 2, (box.MinY + box.MaxY) / 2, (box.MinZ + box.MaxZ) / 2);
        var boxHalfSize = new Vector3D((box.MaxX - box.MinX) / 2, (box.MaxY - box.MinY) / 2, (box.MaxZ - box.MinZ) / 2);

        // 将顶点移动到以包围盒中心为原点的坐标系
        v0 -= boxCenter;
        v1 -= boxCenter;
        v2 -= boxCenter;

        // 三角形的三条边
        var e0 = v1 - v0;
        var e1 = v2 - v1;
        var e2 = v0 - v2;

        // --- 分离轴测试 ---

        // 1. 测试3个AABB的法线（即坐标轴X, Y, Z）
        if (Math.Max(v0.X, Math.Max(v1.X, v2.X)) < -boxHalfSize.X || Math.Min(v0.X, Math.Min(v1.X, v2.X)) > boxHalfSize.X) return false;
        if (Math.Max(v0.Y, Math.Max(v1.Y, v2.Y)) < -boxHalfSize.Y || Math.Min(v0.Y, Math.Min(v1.Y, v2.Y)) > boxHalfSize.Y) return false;
        if (Math.Max(v0.Z, Math.Max(v1.Z, v2.Z)) < -boxHalfSize.Z || Math.Min(v0.Z, Math.Min(v1.Z, v2.Z)) > boxHalfSize.Z) return false;

        // 2. 测试三角形的法线
        var normal = e0.Cross(e1);
        var p0 = v0.Dot(normal);
        var p1 = v1.Dot(normal);
        var p2 = v2.Dot(normal);
        var r = boxHalfSize.X * Math.Abs(normal.X) + boxHalfSize.Y * Math.Abs(normal.Y) + boxHalfSize.Z * Math.Abs(normal.Z);
        if (Math.Max(p0, Math.Max(p1, p2)) < -r || Math.Min(p0, Math.Min(p1, p2)) > r) return false;

        // 3. 测试9个边与坐标轴的叉乘构成的轴
        // 优化：使用下面的函数来处理这9个测试
        Func<double, double, double, double, double, double, double, bool> testAxis =
            (a, b, fa, fb, pa, pb, rad) =>
        {
            var p = pa * a - pb * b;
            var min = Math.Min(p0, Math.Min(p1, p2));
            var max = Math.Max(p0, Math.Max(p1, p2));
            return min > rad || max < -rad;
        };

        // 叉乘(e0, (1,0,0)), 叉乘(e0, (0,1,0)), 叉乘(e0, (0,0,1))
        if (testAxis(e0.Y, e0.Z, v0.Y, v0.Z, v2.Y, v2.Z, boxHalfSize.Y * Math.Abs(e0.Z) + boxHalfSize.Z * Math.Abs(e0.Y))) return false;
        if (testAxis(e0.X, e0.Z, v0.X, v0.Z, v2.X, v2.X, boxHalfSize.X * Math.Abs(e0.Z) + boxHalfSize.Z * Math.Abs(e0.X))) return false;
        if (testAxis(e0.X, e0.Y, v0.X, v0.Y, v1.Y, v1.X, boxHalfSize.X * Math.Abs(e0.Y) + boxHalfSize.Y * Math.Abs(e0.X))) return false;

        // 叉乘(e1, (1,0,0)), 叉乘(e1, (0,1,0)), 叉乘(e1, (0,0,1))
        if (testAxis(e1.Y, e1.Z, v0.Y, v0.Z, v2.Y, v2.Z, boxHalfSize.Y * Math.Abs(e1.Z) + boxHalfSize.Z * Math.Abs(e1.Y))) return false;
        if (testAxis(e1.X, e1.Z, v0.X, v0.Z, v2.X, v2.Z, boxHalfSize.X * Math.Abs(e1.Z) + boxHalfSize.Z * Math.Abs(e1.X))) return false;
        if (testAxis(e1.X, e1.Y, v0.X, v0.Y, v0.Y, v0.X, boxHalfSize.X * Math.Abs(e1.Y) + boxHalfSize.Y * Math.Abs(e1.X))) return false;

        // 叉乘(e2, (1,0,0)), 叉乘(e2, (0,1,0)), 叉乘(e2, (0,0,1))
        if (testAxis(e2.Y, e2.Z, v0.Y, v0.Z, v1.Y, v1.Z, boxHalfSize.Y * Math.Abs(e2.Z) + boxHalfSize.Z * Math.Abs(e2.Y))) return false;
        if (testAxis(e2.X, e2.Z, v0.X, v0.Z, v1.X, v1.Z, boxHalfSize.X * Math.Abs(e2.Z) + boxHalfSize.Z * Math.Abs(e2.X))) return false;
        if (testAxis(e2.X, e2.Y, v1.Y, v1.X, v2.Y, v2.X, boxHalfSize.X * Math.Abs(e2.Y) + boxHalfSize.Y * Math.Abs(e2.X))) return false;


        // 如果所有13个分离轴测试都失败（即找不到任何一个可以将它们分开的轴），则它们相交
        return true;
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