using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealScene3D.Application.Interfaces;
using RealScene3D.Application.Services.MeshDecimator;
using RealScene3D.Application.Services.MeshDecimator.Algorithms;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Geometry;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Domain.Materials;
using RealScene3D.Domain.Utils;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// 三维切片处理器 
///
/// 核心流程：简化-分割-生成
/// 1. 简化（Simplification）：对原始网格进行 LOD 简化
/// 2. 分割（Split）：使用 IMesh.Split 方法进行递归空间分割
/// 3. 生成（Generate）：为每个分割后的网格生成切片文件
///
/// 主要职责：
/// - 管理切片任务队列
/// - 执行完整的切片处理流程
/// - 实时更新任务进度
/// - 保存切片结果到数据库
/// </summary>
public class SlicingProcessor : ISlicingProcessor
{
    private readonly IRepository<SlicingTask> _slicingTaskRepository;
    private readonly IRepository<Slice> _sliceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SlicingProcessor> _logger;
    private readonly SlicingDataService _dataService;
    private readonly IncrementalUpdateService _incrementalUpdateService;

    public SlicingProcessor(
        IRepository<SlicingTask> slicingTaskRepository,
        IRepository<Slice> sliceRepository,
        IUnitOfWork unitOfWork,
        ILogger<SlicingProcessor> logger,
        SlicingDataService dataService,
        IncrementalUpdateService incrementalUpdateService)
    {
        _slicingTaskRepository = slicingTaskRepository ?? throw new ArgumentNullException(nameof(slicingTaskRepository));
        _sliceRepository = sliceRepository ?? throw new ArgumentNullException(nameof(sliceRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _incrementalUpdateService = incrementalUpdateService ?? throw new ArgumentNullException(nameof(incrementalUpdateService));
    }

    /// <summary>
    /// 处理切片任务队列
    /// </summary>
    public async Task ProcessSlicingQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始处理切片任务队列");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var allTasks = await _slicingTaskRepository.GetAllAsync();
                var queuedTasks = allTasks.Where(t => t.Status == SlicingTaskStatus.Queued);

                foreach (var task in queuedTasks)
                {
                    await ProcessSlicingTaskAsync(task.Id, cancellationToken);
                }

                await Task.Delay(5000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理切片队列时发生错误");
                await Task.Delay(10000, cancellationToken);
            }
        }

        _logger.LogInformation("切片任务队列处理结束");
    }

    /// <summary>
    /// 处理单个切片任务
    /// </summary>
    public async Task ProcessSlicingTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _slicingTaskRepository.GetByIdAsync(taskId);
        if (task == null)
        {
            _logger.LogWarning("切片任务不存在：{TaskId}", taskId);
            return;
        }

        try
        {
            _logger.LogInformation("开始处理切片任务：{TaskId} ({TaskName})", taskId, task.Name);

            task.Status = SlicingTaskStatus.Processing;
            task.StartedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            // 执行切片处理
            await PerformSlicingAsync(task, cancellationToken);

            task.Status = SlicingTaskStatus.Completed;
            task.Progress = 100;
            task.CompletedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("切片任务处理完成：{TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切片任务处理失败：{TaskId}", taskId);

            task.Status = SlicingTaskStatus.Failed;
            task.ErrorMessage = ex.Message;
            task.CompletedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task UpdateProgressAsync(Guid taskId, SlicingProgress progress)
    {
        var task = await _slicingTaskRepository.GetByIdAsync(taskId);
        if (task != null)
        {
            task.Progress = progress.Progress;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 执行切片处理 - 新架构：简化-分割-生成
    /// </summary>
    private async Task PerformSlicingAsync(SlicingTask task, CancellationToken cancellationToken)
    {
        var config = ParseSlicingConfig(task.SlicingConfig);
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("========== 开始瓦片生成流水线处理（新架构） ==========");
        _logger.LogInformation("任务ID: {TaskId}, 任务名称: {TaskName}", task.Id, task.Name);
        _logger.LogInformation("源模型: {SourceModel}", task.SourceModelPath);
        _logger.LogInformation("配置: LOD级别={LodLevels}, 递归深度={Divisions}, 格式={Format}",
            config.LodLevels, config.Divisions, config.OutputFormat);

        try
        {
            // ========== Stage 0: 加载模型数据（进度：5%） ==========
            _logger.LogInformation("---------- Stage 0: 加载模型数据 ----------");
            await UpdateProgressAsync(task.Id, new SlicingProgress
            {
                TaskId = task.Id,
                Progress = 5,
                CurrentStage = "加载模型数据"
            });

            var (originalMesh, modelBounds) = await LoadModelDataAsync(task, cancellationToken);

            if (originalMesh.Faces.Count == 0)
            {
                _logger.LogWarning("模型中没有面数据，无法进行切片");
                return;
            }

            _logger.LogInformation("模型加载完成：顶点数={VertexCount}, 面数={FaceCount}, 材质数={MaterialCount}",
                originalMesh.Vertices.Count, originalMesh.Faces.Count, originalMesh.Materials?.Count);

            // ========== Stage 1: 网格简化（进度：10%） ==========
            _logger.LogInformation("---------- Stage 1: 简化（Simplification） - LOD 生成 ----------");
            await UpdateProgressAsync(task.Id, new SlicingProgress
            {
                TaskId = task.Id,
                Progress = 10,
                CurrentStage = "生成 LOD 级别"
            });

            var lodMeshes = GenerateLODMeshes(originalMesh, config);
            _logger.LogInformation("LOD 生成完成：共 {Count} 个级别", lodMeshes.Count);

            // ========== Stage 2 & 3: 空间分割和切片生成（进度：20% - 90%） ==========
            _logger.LogInformation("---------- Stage 2 & 3: 分割（Split）和 生成（Generate） ----------");
            await UpdateProgressAsync(task.Id, new SlicingProgress
            {
                TaskId = task.Id,
                Progress = 20,
                CurrentStage = "开始空间分割"
            });

            var allSlices = new List<Slice>();

            for (int lodLevel = 0; lodLevel < lodMeshes.Count; lodLevel++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var lodMesh = lodMeshes[lodLevel];
                _logger.LogInformation("处理 LOD {Level}: {FaceCount} 个面", lodLevel, lodMesh.Faces.Count);

                // 更新进度
                int progressPercent = 20 + (lodLevel * 70 / lodMeshes.Count);
                await UpdateProgressAsync(task.Id, new SlicingProgress
                {
                    TaskId = task.Id,
                    Progress = progressPercent,
                    CurrentStage = $"处理 LOD {lodLevel}/{lodMeshes.Count - 1}"
                });

                // 空间分割 - 使用 IMesh.Split 方法
                var spatialCells = await QuadtreeSplitAsync(
                    lodMesh,
                    modelBounds,
                    lodLevel,
                    config.Divisions,
                    cancellationToken);

                _logger.LogInformation("LOD {Level} 分割完成：{Count} 个空间单元", lodLevel, spatialCells.Count);

                // 生成切片
                var lodSlices = await GenerateTilesForCellsAsync(
                    spatialCells,
                    task,
                    config,
                    cancellationToken);

                allSlices.AddRange(lodSlices);
                _logger.LogInformation("LOD {Level} 切片生成完成：{Count} 个切片", lodLevel, lodSlices.Count);
            }

            _logger.LogInformation("所有切片生成完成：总计 {Count} 个切片", allSlices.Count);

            // ========== Stage 4: 生成 tileset.json（进度：95%） ==========
            if (config.GenerateTileset && allSlices.Count > 0)
            {
                _logger.LogInformation("---------- Stage 4: 生成 tileset.json ----------");
                await UpdateProgressAsync(task.Id, new SlicingProgress
                {
                    TaskId = task.Id,
                    Progress = 95,
                    CurrentStage = "生成 tileset.json"
                });

                await GenerateTilesetJsonAsync(allSlices, modelBounds, config, task, cancellationToken);
            }

            // 保存切片到数据库
            await SaveSlicesToDatabaseAsync(allSlices, task, config, cancellationToken);

            // 生成增量更新索引（如果需要）
            if (config.EnableIncrementalUpdates)
            {
                _logger.LogInformation("生成增量更新索引：任务{TaskId}", task.Id);
                await _incrementalUpdateService.GenerateIncrementalUpdateIndexAsync(task, config, cancellationToken);
            }

            var processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("========== 切片处理完成，耗时{Time:F2}秒 ==========", processingTime.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "瓦片生成流水线处理失败");
            throw;
        }
    }

    /// <summary>
    /// 保存切片到数据库
    /// </summary>
    private async Task SaveSlicesToDatabaseAsync(
        List<Slice> slices,
        SlicingTask task,
        SlicingConfig config,
        CancellationToken cancellationToken)
    {
        const int batchSize = 50;
        int savedCount = 0;

        for (int i = 0; i < slices.Count; i += batchSize)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var batch = slices.Skip(i).Take(batchSize).ToList();

            foreach (var slice in batch)
            {
                await _sliceRepository.AddAsync(slice);
            }

            await _unitOfWork.SaveChangesAsync();
            savedCount += batch.Count;

            _logger.LogDebug("保存切片进度：{Saved}/{Total}", savedCount, slices.Count);
        }

        _logger.LogInformation("切片保存完成：共{Count}个切片", savedCount);
    }

    /// <summary>
    /// 解析切片配置
    /// </summary>
    private static SlicingConfig ParseSlicingConfig(string configJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize<SlicingConfig>(configJson);
            return config ?? new SlicingConfig();
        }
        catch
        {
            return new SlicingConfig();
        }
    }

    /// <summary>
    /// 空间单元 - 代表空间分割后的一个区域
    /// </summary>
    private class SpatialCell
    {
        public string QuadrantPath { get; set; } = "";  // 象限路径，如 "XL-YL-XR-YR"
        public int Depth { get; set; }
        public int LodLevel { get; set; }
        public IMesh Mesh { get; set; } = null!;
        public Box3 Bounds { get; set; } = null!;
    }

    /// <summary>
    /// 加载模型数据 - 使用新架构
    /// </summary>
    private async Task<(IMesh mesh, Box3 bounds)>
        LoadModelDataAsync(SlicingTask task, CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始加载模型：{Path}", task.SourceModelPath);

        var (mesh, bounds) = await _dataService.LoadMeshFromModelAsync(
            task.SourceModelPath,
            cancellationToken);

        if (mesh == null || mesh.FacesCount == 0)
        {
            _logger.LogWarning("模型加载失败或没有面数据");
 
            return (new MeshT([], [], [], []), new Box3(0, 0, 0, 0, 0, 0));
        }

        if (!bounds.IsValid())
        {
            bounds = mesh.Bounds;
        }

        return (mesh, bounds);
    }

    /// <summary>
    /// 生成LOD网格 - 简化阶段
    /// 策略：为每个 LOD 级别创建简化的网格
    /// 支持有纹理（MeshT）和无纹理（Mesh）两种情况
    /// </summary>
    private List<IMesh> GenerateLODMeshes(IMesh originalMesh, SlicingConfig config)
    {
        var lodMeshes = new List<IMesh>();

        if (!config.EnableMeshDecimation || config.LodLevels <= 1)
        {
            _logger.LogInformation("跳过网格简化（未启用或LOD级别<=1），使用原始网格");
            // LOD-0 使用原始网格
            lodMeshes.Add(originalMesh);
            return lodMeshes;
        }

        _logger.LogInformation("为整个模型生成 {LodLevels} 个 LOD 级别", config.LodLevels);

        // LOD-0: 原始网格（100%）
        lodMeshes.Add(originalMesh);
        _logger.LogInformation("LOD 0: {Count} 个面（原始网格）", originalMesh.FacesCount);

        // 计算每个 LOD 级别的质量参数
        var qualities = Enumerable.Range(0, config.LodLevels - 1).Select(i => 1.0f - ((i + 1) / (float)config.LodLevels)).ToArray();

        // LOD-1 到 LOD-N: 简化网格 - 直接使用 IMesh 的 DecimateMesh
        for (int index = 0; index < qualities.Length; index++)
        {
            try
            {
                var quality = MathHelper.Clamp01(qualities[index]);
                _logger.LogInformation("开始简化 LOD {Level}: ", index + 1);
                _logger.LogInformation("目标质量: {Quality:P1}", quality);

                var algorithm = new FastQuadricMeshSimplification
                {
                    PreserveSeams = true,
                    Verbose = true,
                    PreserveBorders = true
                };

                var targetTriangleCount = (int)Math.Ceiling(originalMesh.FacesCount * quality);

                // 使用 IMesh 版本的 DecimateMesh
                var lodMesh = MeshDecimation.DecimateMesh(algorithm, originalMesh, targetTriangleCount);
                lodMeshes.Add(lodMesh);

                _logger.LogInformation("  LOD {Level}: {Count} 个面（简化后）", index + 1, lodMesh.FacesCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LOD {Level} 简化失败，使用原始网格", index + 1);
                lodMeshes.Add(originalMesh);
            }
        }

        return lodMeshes;
    }

    /// <summary>
    /// 对网格进行四叉树空间分割 - 分割阶段
    /// 使用 MeshUtils.RecurseSplitXY 方法进行递归分割
    /// </summary>
    private async Task<List<SpatialCell>> QuadtreeSplitAsync(
        IMesh mesh,
        Box3 modelBounds,
        int lodLevel,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始LOD {Level}的四叉树分割：面数={Count}, 最大深度={MaxDepth}",
            lodLevel, mesh.FacesCount, maxDepth);

        var meshes = new System.Collections.Concurrent.ConcurrentBag<IMesh>();

        // 使用 MeshUtils.RecurseSplitXY 进行分割
        var splitCount = await MeshUtils.RecurseSplitXY(mesh, maxDepth, modelBounds, meshes);

        _logger.LogInformation("LOD {Level}四叉树分割完成：分割操作={SplitCount}次, 生成 {Count} 个非空叶子节点",
            lodLevel, splitCount, meshes.Count);

        // 将分割后的网格转换为 SpatialCell
        // 每个网格分配一个唯一的空间坐标，避免所有切片被分组到同一位置
        var cells = new List<SpatialCell>();
        int index = 0;

        // 计算网格尺寸：使用 sqrt 估算行列数
        int gridSize = (int)Math.Ceiling(Math.Sqrt(meshes.Count));

        foreach (var splitMesh in meshes)
        {
            if (splitMesh.FacesCount > 0)
            {
                // 将索引转换为 (x, y) 坐标，确保每个切片有唯一的空间位置
                int x = index % gridSize;
                int y = index / gridSize;

                cells.Add(new SpatialCell
                {
                    QuadrantPath = $"{x}-{y}",  // 使用坐标作为路径
                    Depth = maxDepth,
                    LodLevel = lodLevel,
                    Mesh = splitMesh,
                    Bounds = splitMesh.Bounds  // 使用网格自身的边界
                });
                index++;
            }
        }

        return cells;
    }

    /// <summary>
    /// 为空间单元生成切片 - 生成阶段
    /// </summary>
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

    /// <summary>
    /// 为单个空间单元生成切片
    /// </summary>
    private async Task<Slice?> GenerateSliceForCellAsync(
        SlicingTask task,
        SpatialCell cell,
        SlicingConfig config,
        CancellationToken cancellationToken)
    {
        if (cell.Mesh.Faces.Count == 0)
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

            // 使用新架构：直接传递 MeshT
            var generated = await _dataService.GenerateSliceFileAsync(
                slice,
                config,
                cell.Mesh,
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
    /// 将象限路径转换为坐标
    /// 支持两种格式：
    /// 1. 新格式："x-y" → (x, y, 0)
    /// 2. 旧格式："XL-YL-XR-YR" → 递归计算坐标
    /// </summary>
    private (int x, int y, int z) ParseQuadrantPathToCoords(string quadrantPath)
    {
        if (string.IsNullOrEmpty(quadrantPath))
            return (0, 0, 0);

        var parts = quadrantPath.Split('-');

        // 新格式：直接解析数字坐标 "x-y"
        if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
        {
            return (x, y, 0);
        }

        // 旧格式：XL-YL-XR-YR 递归坐标计算（向后兼容）
        x = 0;
        y = 0;
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

    /// <summary>
    /// 生成 tileset.json 文件
    /// </summary>
    private async Task GenerateTilesetJsonAsync(
        List<Slice> slices,
        Box3 modelBounds,
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
