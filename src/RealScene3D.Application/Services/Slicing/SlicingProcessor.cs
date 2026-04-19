using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RealScene3D.Application.Interfaces;
using RealScene3D.Application.Services.MeshDecimator;
using RealScene3D.Application.Services.MeshDecimator.Algorithms;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Geometry;
using RealScene3D.Domain.Interfaces;
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
    /// 执行切片处理：简化-分割-生成
    /// </summary>
    private async Task PerformSlicingAsync(SlicingTask task, CancellationToken cancellationToken)
    {
        // 根据模型类型选择不同的处理流程
        if (task.ModelType == "ObliquePhotography")
        {
            _logger.LogInformation("检测到倾斜摄影数据，使用OSGB专用处理流程");
            await PerformObliqueSlicingAsync(task, cancellationToken);
            return;
        }

        // 通用3D模型处理流程
        await PerformGeneralSlicingAsync(task, cancellationToken);
    }

    /// <summary>
    /// 执行倾斜摄影切片处理
    /// </summary>
    private async Task PerformObliqueSlicingAsync(SlicingTask task, CancellationToken cancellationToken)
    {
        var config = ParseSlicingConfig(task.SlicingConfig);
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("========== 开始倾斜摄影切片处理 ==========");
        _logger.LogInformation("任务ID: {TaskId}, 任务名称: {TaskName}", task.Id, task.Name);
        _logger.LogInformation("源数据路径: {SourceModel}", task.SourceModelPath);

        try
        {
            // 使用OSGB倾斜摄影专用处理服务
            var osgbLogger = new Logger<OsgbTiledDatasetSlicingService>(new NullLoggerFactory());
            var osgbService = new OsgbTiledDatasetSlicingService(osgbLogger);
            
            // 确定输出路径
            var outputPath = task.OutputPath;
            if (string.IsNullOrEmpty(outputPath))
            {
                // 如果没有指定输出路径，生成默认路径
                outputPath = GenerateDefaultObliqueOutputPath(task.SourceModelPath, task.Id);
                _logger.LogInformation("使用生成的默认输出路径: {OutputPath}", outputPath);
            }

            // 执行OSGB切片处理
            var success = await osgbService.ProcessDatasetAsync(
                task.SourceModelPath,
                outputPath,
                config,
                cancellationToken
            );

            if (!success)
            {
                throw new InvalidOperationException("倾斜摄影切片处理失败");
            }

            _logger.LogInformation("倾斜摄影切片处理完成，开始扫描和保存元数据");

            // 扫描输出目录，收集切片文件信息
            var sliceFiles = await ScanObliqueSliceFilesAsync(outputPath, task.Id);
            
            if (sliceFiles.Count == 0)
            {
                _logger.LogWarning("未发现切片文件，请检查输出目录: {OutputPath}", outputPath);
            }
            else
            {
                // 保存切片元数据到数据库
                await SaveObliqueSliceMetadataAsync(task.Id, sliceFiles);
            }

            // 更新任务的输出路径
            task.OutputPath = outputPath;
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("任务输出路径已更新: {OutputPath}", outputPath);

            // 更新任务进度
            await UpdateProgressAsync(task.Id, new SlicingProgress
            {
                TaskId = task.Id,
                Progress = 100,
                CurrentStage = "倾斜摄影切片完成"
            });

            var processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("========== 倾斜摄影切片处理完成，耗时{Time:F2}秒 ==========", processingTime.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "倾斜摄影切片处理失败");
            throw;
        }
    }

    /// <summary>
    /// 执行通用3D模型切片处理
    /// </summary>
    private async Task PerformGeneralSlicingAsync(SlicingTask task, CancellationToken cancellationToken)
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

            var (originalMesh, modelBounds) = await LoadModelAsync(task, cancellationToken);

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

                // 空间分割 
                var spatialCells = await SplitAsync(
                    lodMesh,
                    modelBounds,
                    lodLevel,
                    config.Divisions,
                    false,
                    config.TextureStrategy,
                    SplitPointStrategy.VertexBaricenter,
                    cancellationToken);

                _logger.LogInformation("LOD {Level} 分割完成：{Count} 个空间单元", lodLevel, spatialCells.Count);

                // 生成切片
                var lodSlices = await GenerateTilesAsync(
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

                await GenerateTilesetAsync(allSlices, modelBounds, config, task, cancellationToken);
            }

            // 保存切片到数据库
            await SaveSlicesToDatabaseAsync(allSlices, task, config, cancellationToken);

            // 生成增量更新索引
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
    /// 加载模型数据
    /// </summary>
    private async Task<(IMesh mesh, Box3 bounds)>
        LoadModelAsync(SlicingTask task, CancellationToken cancellationToken)
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
    /// 异步分割网格 - 执行四叉树空间分割
    /// 将输入的网格按照指定的分割策略和深度进行递归分割，
    /// 生成多个空间单元（SpatialCell），每个单元包含分割后的网格和空间信息
    /// </summary>
    /// <param name="mesh">要分割的输入网格</param>
    /// <param name="modelBounds">模型的边界框，用于确定分割边界</param>
    /// <param name="lodLevel">LOD级别，用于日志记录</param>
    /// <param name="divisions">分割深度（递归次数）</param>
    /// <param name="zSplit">是否进行Z轴分割（三维分割）</param>
    /// <param name="splitPointStrategy">分割点策略（绝对中心或顶点重心）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分割后的空间单元列表</returns>
    private async Task<List<SpatialCell>> SplitAsync(
        IMesh mesh,
        Box3 modelBounds,
        int lodLevel,
        int divisions,
        bool zSplit = false,
        TexturesStrategy textureStrategy = TexturesStrategy.Repack,
        SplitPointStrategy splitPointStrategy = SplitPointStrategy.VertexBaricenter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始LOD {Level}的四叉树分割：面数={Count}, 最大深度={MaxDepth}",
            lodLevel, mesh.FacesCount, divisions);

        var meshes = new System.Collections.Concurrent.ConcurrentBag<IMesh>();
        int splitCount = 0;

        // 根据是否有有效的模型边界选择不同的分割策略
        if (null != modelBounds && modelBounds.IsValid())
        {
            _logger.LogInformation("LOD {Level}采用边界作为分割依据", lodLevel);

            // 使用模型边界作为分割依据
            splitCount = zSplit
                ? await MeshUtils.RecurseSplitXYZ(mesh, divisions, modelBounds, meshes)  
                : await MeshUtils.RecurseSplitXY(mesh, divisions, modelBounds, meshes); 
        }
        else
        {
            _logger.LogInformation("LOD {Level}采用使用动态计算的分割点策略", lodLevel);

            // 使用动态计算的分割点策略
            Func<IMesh, Vertex3> getSplitPoint = splitPointStrategy switch
            {
                SplitPointStrategy.AbsoluteCenter => m => m.Bounds.Center,           
                SplitPointStrategy.VertexBaricenter => m => m.GetVertexBaricenter(), 
                _ => throw new ArgumentOutOfRangeException(nameof(splitPointStrategy))
            };

            splitCount = zSplit
                ? await MeshUtils.RecurseSplitXYZ(mesh, divisions, getSplitPoint, meshes)  
                : await MeshUtils.RecurseSplitXY(mesh, divisions, getSplitPoint, meshes); 
        }

        _logger.LogInformation("LOD {Level}四叉树分割完成：分割操作={SplitCount}次, 生成 {Count} 个非空叶子节点",
            lodLevel, splitCount, meshes.Count);

        var ms = meshes.ToArray();
        var cells = new List<SpatialCell>();
        foreach (var m in ms)
        {
            // 设置纹理策略并执行材质打包
            if (m is MeshT mt)
            {
                mt.TexturesStrategy = textureStrategy;

                // 直接在内存中打包材质，避免文件I/O
                mt.PackMaterials();
            }

            cells.Add(new SpatialCell
            {
                QuadrantPath = m.Name,  // 使用坐标作为象限路径标识符
                Depth = divisions,      // 分割深度
                LodLevel = lodLevel,    // LOD级别
                Mesh = m,               // 使用打包后的网格
                Bounds = m.Bounds       // 使用网格自身的边界框
            });
        }

        return cells;
    }

    /// <summary>
    /// 为空间单元生成切片 - 生成阶段
    /// </summary>
    private async Task<List<Slice>> GenerateTilesAsync(
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

            var slice = await GenerateTileForCellAsync(task, cell, config, cancellationToken);

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
    private async Task<Slice?> GenerateTileForCellAsync(
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

            var generated = await _dataService.GenerateTileAsync(
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
    /// 将象限路径转换为坐标（用于向后兼容）
    /// 例如：XL-YL-XR-YR → (x, y, z)
    /// </summary>
    private (int x, int y, int z) ParseQuadrantPathToCoords(string quadrantPath, bool zSplit = false)
    {
        if (string.IsNullOrEmpty(quadrantPath))
            return (0, 0, 0);

        var parts = quadrantPath.Split('-');
        int x = 0, y = 0, z = 0;

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

        return (x, y, z);
    }

    /// <summary>
    /// 生成 tileset.json 文件
    /// </summary>
    private async Task GenerateTilesetAsync(
        List<Slice> slices,
        Box3 modelBounds,
        SlicingConfig config,
        SlicingTask task,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dataService.GenerateTilesetAsync(
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

    #region 倾斜摄影切片元数据处理

    /// <summary>
    /// 切片文件信息
    /// </summary>
    private class ObliqueSliceInfo
    {
        public int Level { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    /// <summary>
    /// 扫描倾斜摄影切片输出目录，收集切片文件信息
    /// </summary>
    /// <param name="outputPath">输出目录路径</param>
    /// <param name="taskId">切片任务ID</param>
    /// <returns>切片文件信息列表</returns>
    private async Task<List<ObliqueSliceInfo>> ScanObliqueSliceFilesAsync(string outputPath, Guid taskId)
    {
        _logger.LogInformation("开始扫描倾斜摄影切片输出目录: {OutputPath}", outputPath);

        var sliceFiles = new List<ObliqueSliceInfo>();

        try
        {
            if (!Directory.Exists(outputPath))
            {
                _logger.LogWarning("输出目录不存在: {OutputPath}", outputPath);
                return sliceFiles;
            }

            // 扫描 Data 目录下的所有 Tile_* 目录
            var dataDir = Path.Combine(outputPath, "Data");
            if (!Directory.Exists(dataDir))
            {
                _logger.LogWarning("Data目录不存在: {DataDir}", dataDir);
                return sliceFiles;
            }

            var tileDirectories = Directory.GetDirectories(dataDir, "Tile_*");
            _logger.LogInformation("发现 {Count} 个Tile目录", tileDirectories.Length);

            foreach (var tileDir in tileDirectories)
            {
                // 扫描目录下的所有 .b3dm 文件
                var b3dmFiles = Directory.GetFiles(tileDir, "*.b3dm", SearchOption.AllDirectories);

                foreach (var filePath in b3dmFiles)
                {
                    var fileInfo = new FileInfo(filePath);
                    
                    // 从目录名解析坐标
                    var dirName = Path.GetFileName(tileDir);
                    var (x, y, z) = ParseObliqueSliceCoordinates(dirName);

                    // 从文件路径推断LOD级别（如果有LOD目录结构）
                    int level = 0;
                    var relativePath = Path.GetRelativePath(outputPath, filePath);
                    var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    foreach (var part in pathParts)
                    {
                        if (part.StartsWith("LOD-") && int.TryParse(part.Substring(4), out int lodLevel))
                        {
                            level = lodLevel;
                            break;
                        }
                    }

                    sliceFiles.Add(new ObliqueSliceInfo
                    {
                        Level = level,
                        X = x,
                        Y = y,
                        Z = z,
                        FilePath = filePath,
                        FileSize = fileInfo.Length
                    });
                }
            }

            _logger.LogInformation("扫描完成，共发现 {Count} 个切片文件", sliceFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "扫描切片文件时发生错误");
        }

        return await Task.FromResult(sliceFiles);
    }

    /// <summary>
    /// 从倾斜摄影切片目录名解析坐标
    /// 支持格式：Tile_+000_+000, Tile_-001_+002 等
    /// </summary>
    /// <param name="directoryName">目录名</param>
    /// <returns>解析出的坐标 (x, y, z)</returns>
    private (int x, int y, int z) ParseObliqueSliceCoordinates(string directoryName)
    {
        int x = 0, y = 0, z = 0;

        try
        {
            // 格式: Tile_+000_+000
            if (directoryName.StartsWith("Tile_"))
            {
                var parts = directoryName.Substring(5).Split('_');
                
                if (parts.Length >= 1 && int.TryParse(parts[0], out int parsedX))
                {
                    x = parsedX;
                }
                
                if (parts.Length >= 2 && int.TryParse(parts[1], out int parsedY))
                {
                    y = parsedY;
                }
                
                if (parts.Length >= 3 && int.TryParse(parts[2], out int parsedZ))
                {
                    z = parsedZ;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析切片坐标失败，使用默认值: {DirectoryName}", directoryName);
        }

        return (x, y, z);
    }

    /// <summary>
    /// 保存倾斜摄影切片元数据到数据库
    /// </summary>
    /// <param name="taskId">切片任务ID</param>
    /// <param name="sliceFiles">切片文件信息列表</param>
    private async Task SaveObliqueSliceMetadataAsync(Guid taskId, List<ObliqueSliceInfo> sliceFiles)
    {
        _logger.LogInformation("开始保存切片元数据，任务ID: {TaskId}, 切片数量: {Count}", taskId, sliceFiles.Count);

        try
        {
            const int batchSize = 50;
            int savedCount = 0;

            for (int i = 0; i < sliceFiles.Count; i += batchSize)
            {
                var batch = sliceFiles.Skip(i).Take(batchSize).ToList();

                foreach (var sliceInfo in batch)
                {
                    var slice = new Slice
                    {
                        SlicingTaskId = taskId,
                        Level = sliceInfo.Level,
                        X = sliceInfo.X,
                        Y = sliceInfo.Y,
                        Z = sliceInfo.Z,
                        FilePath = sliceInfo.FilePath,
                        FileSize = sliceInfo.FileSize,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _sliceRepository.AddAsync(slice);
                }

                await _unitOfWork.SaveChangesAsync();
                savedCount += batch.Count;

                _logger.LogDebug("保存切片元数据进度: {Saved}/{Total}", savedCount, sliceFiles.Count);
            }

            _logger.LogInformation("切片元数据保存完成，共保存 {Count} 条记录", savedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存切片元数据失败");
            throw;
        }
    }

    /// <summary>
    /// 生成默认的倾斜摄影输出路径
    /// </summary>
    /// <param name="sourcePath">源数据路径</param>
    /// <param name="taskId">任务ID（用于生成唯一标识）</param>
    /// <returns>默认输出路径</returns>
    private string GenerateDefaultObliqueOutputPath(string sourcePath, Guid taskId)
    {
        try
        {
            var baseName = Path.GetFileNameWithoutExtension(sourcePath);
            var directory = Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory();
            
            // 使用任务ID的短哈希确保唯一性
            var shortHash = taskId.ToString("N").Substring(0, 8);
            
            var outputPath = Path.Combine(directory, $"oblique_{baseName}_{shortHash}");
            
            _logger.LogInformation("生成默认输出路径: {OutputPath}", outputPath);
            
            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成默认输出路径失败");
            return Path.Combine(Directory.GetCurrentDirectory(), $"oblique_output_{taskId:N}");
        }
    }

    #endregion
}
