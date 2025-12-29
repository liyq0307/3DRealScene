using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealScene3D.Application.Services.Loaders;
using RealScene3D.Application.Services.Generators;
using RealScene3D.Application.Services.Slicing;
using RealScene3D.Domain.Utils;
using RealScene3D.Domain.Entities;

namespace RealScene3D.Tests;

class Program
{
    static async Task Main(string[] args)
    {
        // 配置依赖注入和日志
        var services = new ServiceCollection();

        // 添加日志服务
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // 注册Loader和Generator
        services.AddTransient<ObjModelLoader>();
        services.AddTransient<OsgbModelLoader>();
        services.AddTransient<GltfModelLoader>();
        services.AddTransient<GltfGenerator>();
        services.AddTransient<B3dmGenerator>();
        services.AddTransient<TilesetGenerator>();
        services.AddTransient<OsgbLODSlicingService>();
        services.AddTransient<RealScene3D.Application.Services.Parsers.OsgbMetadataParser>();
        services.AddTransient<OsgbTiledDatasetSlicingService>();

        var serviceProvider = services.BuildServiceProvider();

        // 获取日志实例
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        // 定义测试文件路径（请根据实际情况修改）
        string objFilePath = @"E:\Data\3D\odm_texturing\odm_textured_model_geo.obj";
        string osgbFilePath = @"E:\Data\3D\Tile_+005_+006\Tile_+005_+006_L20_000002020.osgb";
        string osgbRootFilePath = @"E:\Data\3D\Tile_+005_+006\Tile_+005_+006.osgb";
        string osgbDatasetPath = @"E:\Data\3D\g_tsg_osgb";  // 倾斜摄影数据集根目录
        string glbInputPath = @"E:\Data\3D\odm_texturing\odm_textured_model_geo.glb";
        string glbOutputPath1 = @"E:\Data\3D\test_output_loader.glb";
        string glbOutputPath2 = @"E:\Data\3D\test_output_meshutils.glb";
        string glbOutputPath3 = @"E:\Data\3D\Tile_+005_+006_L12_0.glb";
        string objOutputPath = @"E:\Data\3D\test_output_from_glb.obj";

        try
        {
            // 显示测试菜单
            ShowTestMenu(logger);

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await TestWithObjModelLoader(logger, serviceProvider, objFilePath, glbOutputPath1);
                    break;
                case "2":
                    await TestWithMeshUtils(logger, serviceProvider, objFilePath, glbOutputPath2);
                    break;
                case "3":
                    await TestWithOsgbModelLoader(logger, serviceProvider, osgbFilePath, glbOutputPath3);
                    break;
                case "4":
                    await TestWithGltfModelLoader(logger, serviceProvider, glbInputPath, objOutputPath);
                    break;
                case "5":
                    await TestOsgbLODHierarchy(logger, serviceProvider, osgbRootFilePath);
                    break;
                case "6":
                    await TestOsgbLODSlicing(logger, serviceProvider, osgbRootFilePath);
                    break;
                case "7":
                    await TestOsgbTiledDataset(logger, serviceProvider, osgbDatasetPath);
                    break;
                default:
                    logger.LogWarning("无效选择");
                    return;
            }

            logger.LogInformation("");
            logger.LogInformation("========================================");
            logger.LogInformation("测试完成！");
            logger.LogInformation("========================================");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "测试执行失败");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// 显示测试菜单
    /// </summary>
    static void ShowTestMenu(ILogger<Program> logger)
    {
        logger.LogInformation("========================================");
        logger.LogInformation("RealScene3D 测试程序");
        logger.LogInformation("========================================");
        logger.LogInformation("请选择要运行的测试:");
        logger.LogInformation("  1. OBJ 加载 -> GLB 生成");
        logger.LogInformation("  2. MeshUtils 加载 -> GLB 生成");
        logger.LogInformation("  3. OSGB 加载 -> GLB 生成");
        logger.LogInformation("  4. GLB 加载 -> OBJ 生成");
        logger.LogInformation("  5. OSGB PagedLOD 层次结构加载测试");
        logger.LogInformation("  6. OSGB PagedLOD 分层切片测试");
        logger.LogInformation("  7. OSGB 倾斜摄影数据集切片测试 (新)");
        logger.LogInformation("========================================");
        logger.LogInformation("输入选项 (1-7): ");
    }

    /// <summary>
    /// 测试5: OSGB PagedLOD 层次结构加载
    /// </summary>
    static async Task TestOsgbLODHierarchy(
        ILogger<Program> logger,
        ServiceProvider serviceProvider,
        string osgbRootPath)
    {
        logger.LogInformation("========================================");
        logger.LogInformation("测试5：OSGB PagedLOD 层次结构加载");
        logger.LogInformation("========================================");
        logger.LogInformation("输入文件: {OsgbPath}", osgbRootPath);
        logger.LogInformation("========================================");

        if (!File.Exists(osgbRootPath))
        {
            logger.LogWarning("⚠️ 测试文件不存在: {Path}", osgbRootPath);
            logger.LogInformation("请修改 osgbRootFilePath 变量指向实际的 OSGB 根文件");
            return;
        }

        var lodSlicingService = serviceProvider.GetRequiredService<OsgbLODSlicingService>();

        logger.LogInformation("步骤1: 加载 PagedLOD 层次结构...");

        var lodTiles = await lodSlicingService.LoadWithLODHierarchyAsync(osgbRootPath, maxDepth: 0);

        logger.LogInformation("✅ 加载成功! 共 {Count} 个 LOD 层级", lodTiles.Count);
        logger.LogInformation("");

        // 按层级分组统计
        var lodStats = lodTiles.GroupBy(t => t.Level)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Level = g.Key,
                Count = g.Count(),
                TotalFaces = g.Sum(t => t.Mesh.FacesCount),
                TotalVertices = g.Sum(t => t.Mesh.VertexCount)
            })
            .ToList();

        logger.LogInformation("层级统计:");
        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        logger.LogInformation("{0,-10} {1,-10} {2,-15} {3,-15}", "LOD 层级", "切片数", "总面数", "总顶点数");
        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        foreach (var stat in lodStats)
        {
            logger.LogInformation("{0,-10} {1,-10} {2,-15:N0} {3,-15:N0}",
                $"LOD-{stat.Level}",
                stat.Count,
                stat.TotalFaces,
                stat.TotalVertices);
        }

        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        logger.LogInformation("");

        // 显示前 5 个详细信息
        logger.LogInformation("前 5 个切片详细信息:");
        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        foreach (var tile in lodTiles.Take(5))
        {
            logger.LogInformation("LOD-{Level}: {File}", tile.Level, Path.GetFileName(tile.FileName));
            logger.LogInformation("  路径: {RelPath}", tile.RelativePath);
            logger.LogInformation("  顶点: {Vertices:N0}, 面: {Faces:N0}",
                tile.Mesh.VertexCount, tile.Mesh.FacesCount);
            logger.LogInformation("  几何误差: {Error:F2}", tile.GeometricError);
            logger.LogInformation("  包围盒: [{MinX:F2}, {MinY:F2}, {MinZ:F2}] -> [{MaxX:F2}, {MaxY:F2}, {MaxZ:F2}]",
                tile.BoundingBox.Min.X, tile.BoundingBox.Min.Y, tile.BoundingBox.Min.Z,
                tile.BoundingBox.Max.X, tile.BoundingBox.Max.Y, tile.BoundingBox.Max.Z);
            logger.LogInformation("");
        }

        if (lodTiles.Count > 5)
        {
            logger.LogInformation("... 还有 {Count} 个切片未显示", lodTiles.Count - 5);
        }
    }

    /// <summary>
    /// 测试6: OSGB PagedLOD 分层切片生成
    /// </summary>
    static async Task TestOsgbLODSlicing(
        ILogger<Program> logger,
        ServiceProvider serviceProvider,
        string osgbRootPath)
    {
        logger.LogInformation("========================================");
        logger.LogInformation("测试6：OSGB PagedLOD 分层切片生成");
        logger.LogInformation("========================================");
        logger.LogInformation("输入文件: {OsgbPath}", osgbRootPath);
        logger.LogInformation("========================================");

        if (!File.Exists(osgbRootPath))
        {
            logger.LogWarning("⚠️ 测试文件不存在: {Path}", osgbRootPath);
            logger.LogInformation("请修改 osgbRootFilePath 变量指向实际的 OSGB 根文件");
            return;
        }

        var outputDir = Path.Combine(
            Path.GetDirectoryName(osgbRootPath) ?? "",
            "Output_3DTiles_Test"
        );

        logger.LogInformation("输出目录: {OutputDir}", outputDir);
        logger.LogInformation("");

        var config = new SlicingConfig
        {
            OutputFormat = "b3dm",
            GenerateTileset = true,
            LodLevels = 1 // 对 OSGB 无影响，使用原生 LOD
        };

        var gpsCoords = new GpsCoords
        {
            Latitude = 39.908692,
            Longitude = 116.397477,
            Altitude = 43.5
        };

        var lodSlicingService = serviceProvider.GetRequiredService<OsgbLODSlicingService>();

        logger.LogInformation("步骤1: 生成 3DTiles 切片...");
        logger.LogInformation("配置: 格式={Format}, GPS=({Lat:F6}, {Lon:F6}, {Alt:F1})",
            config.OutputFormat, gpsCoords.Latitude, gpsCoords.Longitude, gpsCoords.Altitude);
        logger.LogInformation("");

        var startTime = DateTime.UtcNow;

        var slices = await lodSlicingService.GenerateLODTilesAsync(
            osgbRootPath,
            outputDir,
            config,
            gpsCoords
        );

        var elapsed = DateTime.UtcNow - startTime;

        logger.LogInformation("✅ 切片生成成功!");
        logger.LogInformation("  耗时: {Elapsed:F2} 秒", elapsed.TotalSeconds);
        logger.LogInformation("  总切片数: {Count}", slices.Count);
        logger.LogInformation("");

        // 统计信息
        var lodStats = slices.GroupBy(s => s.Level)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Level = g.Key,
                Count = g.Count(),
                TotalSize = g.Sum(s => s.FileSize)
            })
            .ToList();

        logger.LogInformation("切片统计:");
        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        logger.LogInformation("{0,-10} {1,-10} {2,-15}", "LOD 层级", "切片数", "总文件大小(KB)");
        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        foreach (var stat in lodStats)
        {
            logger.LogInformation("{0,-10} {1,-10} {2,-15:N2}",
                $"LOD-{stat.Level}", stat.Count, stat.TotalSize / 1024.0);
        }

        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        logger.LogInformation("");

        // 检查输出文件
        var tilesetPath = Path.Combine(outputDir, "tileset.json");
        if (File.Exists(tilesetPath))
        {
            var fileInfo = new FileInfo(tilesetPath);
            logger.LogInformation("✅ tileset.json 已生成");
            logger.LogInformation("  路径: {Path}", tilesetPath);
            logger.LogInformation("  大小: {Size:N0} 字节", fileInfo.Length);
        }
        else
        {
            logger.LogWarning("⚠️ tileset.json 未生成");
        }

        logger.LogInformation("");
        logger.LogInformation("输出目录结构:");
        logger.LogInformation("  {OutputDir}", outputDir);
        logger.LogInformation("  ├── tileset.json");

        foreach (var levelGroup in lodStats)
        {
            logger.LogInformation("  ├── LOD-{Level}/ ({Count} 个 .b3dm 文件)",
                levelGroup.Level, levelGroup.Count);
        }

        logger.LogInformation("");
        logger.LogInformation("💡 提示: 可以使用 Cesium Viewer 加载 tileset.json 查看效果");
    }

    /// <summary>
    /// 测试1: 使用 ObjModelLoader 加载 OBJ 文件，然后用 GltfGenerator 生成 GLB
    /// </summary>
    static async Task TestWithObjModelLoader(
        ILogger<Program> logger,
        ServiceProvider serviceProvider,
        string objFilePath,
        string glbOutputPath)
    {
        logger.LogInformation("========================================");
        logger.LogInformation("测试1：使用 ObjModelLoader 加载 -> GLB生成");
        logger.LogInformation("========================================");
        logger.LogInformation("输入文件: {ObjPath}", objFilePath);
        logger.LogInformation("输出文件: {GlbPath}", glbOutputPath);
        logger.LogInformation("========================================");

        // 步骤1: 使用ObjModelLoader加载OBJ文件
        logger.LogInformation("步骤1: 使用 ObjModelLoader 加载OBJ文件...");
        var objLoader = serviceProvider.GetRequiredService<ObjModelLoader>();
        var (mesh, boundingBox) = await objLoader.LoadModelAsync(objFilePath);

        logger.LogInformation("OBJ加载成功!");
        logger.LogInformation("  - 网格类型: {MeshType}", mesh.GetType().Name);
        logger.LogInformation("  - 顶点数: {VertexCount}", mesh.VertexCount);
        logger.LogInformation("  - 面片数: {FaceCount}", mesh.FacesCount);
        logger.LogInformation("  - 包围盒: [{MinX:F3}, {MinY:F3}, {MinZ:F3}] - [{MaxX:F3}, {MaxY:F3}, {MaxZ:F3}]",
            boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Min.Z,
            boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Max.Z);

        // 步骤2: 使用GltfGenerator生成GLB文件
        logger.LogInformation("");
        logger.LogInformation("步骤2: 使用 GltfGenerator 生成GLB文件...");
        var gltfGenerator = serviceProvider.GetRequiredService<GltfGenerator>();
        await gltfGenerator.SaveTileAsync(mesh, glbOutputPath);

        logger.LogInformation("GLB生成成功!");
        logger.LogInformation("  - 输出路径: {Path}", glbOutputPath);

        if (File.Exists(glbOutputPath))
        {
            var fileInfo = new FileInfo(glbOutputPath);
            logger.LogInformation("  - 文件大小: {Size:N0} 字节 ({SizeKB:F2} KB)",
                fileInfo.Length, fileInfo.Length / 1024.0);
        }
    }

    /// <summary>
    /// 测试2: 直接使用 MeshUtils.LoadMesh 加载 OBJ 文件，然后用 GltfGenerator 生成 GLB
    /// </summary>
    static async Task TestWithMeshUtils(
        ILogger<Program> logger,
        ServiceProvider serviceProvider,
        string objFilePath,
        string glbOutputPath)
    {
        logger.LogInformation("========================================");
        logger.LogInformation("测试2：使用 MeshUtils.LoadMesh 加载 -> GLB生成");
        logger.LogInformation("========================================");
        logger.LogInformation("输入文件: {ObjPath}", objFilePath);
        logger.LogInformation("输出文件: {GlbPath}", glbOutputPath);
        logger.LogInformation("========================================");

        // 步骤1: 使用 MeshUtils.LoadMesh 加载OBJ文件
        logger.LogInformation("步骤1: 使用 MeshUtils.LoadMesh 加载OBJ文件...");
        var (mesh, deps, normals, boundingBox) = await MeshUtils.LoadMesh(objFilePath);

        logger.LogInformation("OBJ加载成功!");
        logger.LogInformation("  - 网格类型: {MeshType}", mesh.GetType().Name);
        logger.LogInformation("  - 顶点数: {VertexCount}", mesh.VertexCount);
        logger.LogInformation("  - 面片数: {FaceCount}", mesh.FacesCount);
        logger.LogInformation("  - 法线数: {NormalCount}", normals.Count);
        logger.LogInformation("  - 依赖文件数: {DepsCount}", deps.Length);

        if (deps.Length > 0)
        {
            logger.LogInformation("  - 依赖文件:");
            foreach (var dep in deps)
            {
                logger.LogInformation("    * {Dep}", dep);
            }
        }

        logger.LogInformation("  - 包围盒: [{MinX:F3}, {MinY:F3}, {MinZ:F3}] - [{MaxX:F3}, {MaxY:F3}, {MaxZ:F3}]",
            boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Min.Z,
            boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Max.Z);

        // 步骤2: 使用GltfGenerator生成GLB文件
        logger.LogInformation("");
        logger.LogInformation("步骤2: 使用 GltfGenerator 生成GLB文件...");
        var gltfGenerator = serviceProvider.GetRequiredService<GltfGenerator>();
        await gltfGenerator.SaveTileAsync(mesh, glbOutputPath);

        logger.LogInformation("GLB生成成功!");
        logger.LogInformation("  - 输出路径: {Path}", glbOutputPath);

        if (File.Exists(glbOutputPath))
        {
            var fileInfo = new FileInfo(glbOutputPath);
            logger.LogInformation("  - 文件大小: {Size:N0} 字节 ({SizeKB:F2} KB)",
                fileInfo.Length, fileInfo.Length / 1024.0);
        }
    }

    /// <summary>
    /// 测试3: 使用 OsgbModelLoader 加载 OSGB 文件，然后用 GltfGenerator 生成 GLB
    /// </summary>
    static async Task TestWithOsgbModelLoader(
        ILogger<Program> logger,
        ServiceProvider serviceProvider,
        string osgbFilePath,
        string glbOutputPath)
    {
        logger.LogInformation("========================================");
        logger.LogInformation("测试3：使用 OsgbModelLoader 加载 -> GLB生成");
        logger.LogInformation("========================================");
        logger.LogInformation("输入文件: {OsgbPath}", osgbFilePath);
        logger.LogInformation("输出文件: {GlbPath}", glbOutputPath);
        logger.LogInformation("========================================");

        // 步骤1: 使用OsgbModelLoader加载OSGB文件
        logger.LogInformation("步骤1: 使用 OsgbModelLoader 加载OSGB文件...");
        var osgbLoader = serviceProvider.GetRequiredService<OsgbModelLoader>();
        var (mesh, boundingBox) = await osgbLoader.LoadModelAsync(osgbFilePath);

        logger.LogInformation("OSGB加载成功!");
        logger.LogInformation("  - 网格类型: {MeshType}", mesh.GetType().Name);
        logger.LogInformation("  - 顶点数: {VertexCount}", mesh.VertexCount);
        logger.LogInformation("  - 面片数: {FaceCount}", mesh.FacesCount);
        logger.LogInformation("  - 包围盒: [{MinX:F3}, {MinY:F3}, {MinZ:F3}] - [{MaxX:F3}, {MaxY:F3}, {MaxZ:F3}]",
            boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Min.Z,
            boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Max.Z);

        // 步骤2: 使用GltfGenerator生成GLB文件
        logger.LogInformation("");
        logger.LogInformation("步骤2: 使用 GltfGenerator 生成GLB文件...");
        var gltfGenerator = serviceProvider.GetRequiredService<GltfGenerator>();
        await gltfGenerator.SaveTileAsync(mesh, glbOutputPath);

        logger.LogInformation("GLB生成成功!");
        logger.LogInformation("  - 输出路径: {Path}", glbOutputPath);

        if (File.Exists(glbOutputPath))
        {
            var fileInfo = new FileInfo(glbOutputPath);
            logger.LogInformation("  - 文件大小: {Size:N0} 字节 ({SizeKB:F2} KB)",
                fileInfo.Length, fileInfo.Length / 1024.0);
        }
    }

    /// <summary>
    /// 测试4: 使用 GltfModelLoader 加载 GLB 文件，然后使用 IMesh.WriteObj 生成 OBJ
    /// </summary>
    static async Task TestWithGltfModelLoader(
        ILogger<Program> logger,
        ServiceProvider serviceProvider,
        string glbFilePath,
        string objOutputPath)
    {
        logger.LogInformation("========================================");
        logger.LogInformation("测试4：使用 GltfModelLoader 加载 GLB -> OBJ生成");
        logger.LogInformation("========================================");
        logger.LogInformation("输入文件: {GlbPath}", glbFilePath);
        logger.LogInformation("输出文件: {ObjPath}", objOutputPath);
        logger.LogInformation("========================================");

        // 步骤1: 使用GltfModelLoader加载GLB文件
        logger.LogInformation("步骤1: 使用 GltfModelLoader 加载GLB文件...");
        var gltfLoader = serviceProvider.GetRequiredService<GltfModelLoader>();
        var (mesh, boundingBox) = await gltfLoader.LoadModelAsync(glbFilePath);

        logger.LogInformation("GLB加载成功!");
        logger.LogInformation("  - 网格类型: {MeshType}", mesh.GetType().Name);
        logger.LogInformation("  - 顶点数: {VertexCount}", mesh.VertexCount);
        logger.LogInformation("  - 面片数: {FaceCount}", mesh.FacesCount);
        logger.LogInformation("  - 是否包含纹理: {HasTexture}", mesh.HasTexture);

        if (mesh.Materials != null && mesh.Materials.Count > 0)
        {
            logger.LogInformation("  - 材质数量: {MaterialCount}", mesh.Materials.Count);
        }

        logger.LogInformation("  - 包围盒: [{MinX:F3}, {MinY:F3}, {MinZ:F3}] - [{MaxX:F3}, {MaxY:F3}, {MaxZ:F3}]",
            boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Min.Z,
            boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Max.Z);

        // 步骤2: 使用IMesh.WriteObj生成OBJ文件
        logger.LogInformation("");
        logger.LogInformation("步骤2: 使用 IMesh.WriteObj 生成OBJ文件...");
        mesh.WriteObj(objOutputPath, removeUnused: true);

        logger.LogInformation("OBJ生成成功!");
        logger.LogInformation("  - 输出路径: {Path}", objOutputPath);

        if (File.Exists(objOutputPath))
        {
            var fileInfo = new FileInfo(objOutputPath);
            logger.LogInformation("  - 文件大小: {Size:N0} 字节 ({SizeKB:F2} KB)",
                fileInfo.Length, fileInfo.Length / 1024.0);
        }

        // 检查是否生成了MTL文件（当网格包含材质时）
        var mtlPath = Path.ChangeExtension(objOutputPath, "mtl");
        if (File.Exists(mtlPath))
        {
            var mtlFileInfo = new FileInfo(mtlPath);
            logger.LogInformation("  - MTL文件: {Path} ({Size:N0} 字节)",
                mtlPath, mtlFileInfo.Length);
        }
    }

    /// <summary>
    /// 测试7: OSGB 倾斜摄影数据集切片
    /// </summary>
    static async Task TestOsgbTiledDataset(
        ILogger<Program> logger,
        ServiceProvider serviceProvider,
        string datasetPath)
    {
        logger.LogInformation("========================================");
        logger.LogInformation("测试7：OSGB 倾斜摄影数据集切片");
        logger.LogInformation("========================================");
        logger.LogInformation("数据集根目录: {DatasetPath}", datasetPath);
        logger.LogInformation("========================================");

        // 验证数据集目录存在
        if (!Directory.Exists(datasetPath))
        {
            logger.LogWarning("⚠️ 数据集目录不存在: {Path}", datasetPath);
            logger.LogInformation("请修改 osgbDatasetPath 变量指向实际的 OSGB 数据集根目录");
            logger.LogInformation("");
            logger.LogInformation("正确的目录结构:");
            logger.LogInformation("  {DatasetPath}/", datasetPath);
            logger.LogInformation("    ├── metadata.xml (可选)");
            logger.LogInformation("    └── Data/");
            logger.LogInformation("        ├── Tile_+xxx_+yyy/");
            logger.LogInformation("        │   └── Tile_+xxx_+yyy.osgb");
            logger.LogInformation("        └── Tile_+zzz_+www/");
            logger.LogInformation("            └── Tile_+zzz_+www.osgb");
            return;
        }

        // 验证 Data 目录存在
        string dataDir = Path.Combine(datasetPath, "Data");
        if (!Directory.Exists(dataDir))
        {
            logger.LogWarning("⚠️ 未找到 Data 目录: {Path}", dataDir);
            logger.LogInformation("倾斜摄影数据集必须包含 Data 目录");
            return;
        }

        // 统计瓦片数量
        var tileDirectories = Directory.GetDirectories(dataDir);
        logger.LogInformation("发现 {Count} 个瓦片目录", tileDirectories.Length);

        if (tileDirectories.Length == 0)
        {
            logger.LogWarning("⚠️ Data 目录中未找到任何瓦片目录");
            return;
        }

        // 显示前几个瓦片
        logger.LogInformation("");
        logger.LogInformation("瓦片列表 (前5个):");
        foreach (var tileDir in tileDirectories.Take(5))
        {
            string tileName = Path.GetFileName(tileDir);
            string osgbFile = Path.Combine(tileDir, $"{tileName}.osgb");
            bool exists = File.Exists(osgbFile);
            logger.LogInformation("  {Status} {TileName}",
                exists ? "✓" : "✗", tileName);
        }

        if (tileDirectories.Length > 5)
        {
            logger.LogInformation("  ... 还有 {Count} 个瓦片", tileDirectories.Length - 5);
        }

        logger.LogInformation("");

        // 设置输出目录
        var outputDir = Path.Combine(datasetPath, "Output_3DTiles_Dataset");
        logger.LogInformation("输出目录: {OutputDir}", outputDir);
        logger.LogInformation("");

        // 配置切片参数
        var config = new SlicingConfig
        {
            OutputFormat = "b3dm",
            GenerateTileset = true,
            LodLevels = 1 // 对 OSGB 数据集无影响，使用原生 LOD
        };

        logger.LogInformation("开始处理数据集...");
        logger.LogInformation("配置: 格式={Format}, 生成Tileset={GenerateTileset}",
            config.OutputFormat, config.GenerateTileset);
        logger.LogInformation("");

        var startTime = DateTime.UtcNow;

        // 获取服务并处理数据集
        var datasetSlicingService = serviceProvider.GetRequiredService<OsgbTiledDatasetSlicingService>();

        List<Slice> allSlices;
        try
        {
            allSlices = await datasetSlicingService.ProcessDatasetAsync(
                datasetPath,
                outputDir,
                config);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ 数据集处理失败");
            return;
        }

        var elapsed = DateTime.UtcNow - startTime;

        logger.LogInformation("");
        logger.LogInformation("✅ 数据集处理完成!");
        logger.LogInformation("  耗时: {Elapsed:F2} 秒", elapsed.TotalSeconds);
        logger.LogInformation("  总切片数: {Count}", allSlices.Count);
        logger.LogInformation("");

        // 按瓦片分组统计
        var tileStats = allSlices
            .GroupBy(s => {
                // 从文件路径中提取瓦片名称
                var parts = s.FilePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                var dataIndex = Array.FindIndex(parts, p => p == "Data");
                return dataIndex >= 0 && dataIndex + 1 < parts.Length
                    ? parts[dataIndex + 1]
                    : "Unknown";
            })
            .Select(g => new
            {
                TileName = g.Key,
                Count = g.Count(),
                TotalSize = g.Sum(s => s.FileSize),
                Levels = g.Select(s => s.Level).Distinct().Count()
            })
            .OrderBy(t => t.TileName)
            .ToList();

        logger.LogInformation("瓦片统计:");
        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        logger.LogInformation("{0,-20} {1,-10} {2,-10} {3,-15}",
            "瓦片名称", "切片数", "LOD层级", "总大小(MB)");
        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        foreach (var stat in tileStats)
        {
            logger.LogInformation("{0,-20} {1,-10} {2,-10} {3,-15:F2}",
                stat.TileName,
                stat.Count,
                stat.Levels,
                stat.TotalSize / (1024.0 * 1024.0));
        }

        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        // 按 LOD 层级统计
        var lodStats = allSlices
            .GroupBy(s => s.Level)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Level = g.Key,
                Count = g.Count(),
                TotalSize = g.Sum(s => s.FileSize)
            })
            .ToList();

        logger.LogInformation("");
        logger.LogInformation("LOD 层级统计:");
        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        logger.LogInformation("{0,-10} {1,-10} {2,-15}",
            "LOD 层级", "切片数", "总大小(MB)");
        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        foreach (var stat in lodStats)
        {
            logger.LogInformation("{0,-10} {1,-10} {2,-15:F2}",
                $"LOD-{stat.Level}",
                stat.Count,
                stat.TotalSize / (1024.0 * 1024.0));
        }

        logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        logger.LogInformation("");

        // 检查根 tileset.json
        var rootTilesetPath = Path.Combine(outputDir, "tileset.json");
        if (File.Exists(rootTilesetPath))
        {
            var fileInfo = new FileInfo(rootTilesetPath);
            logger.LogInformation("✅ 根 tileset.json 已生成");
            logger.LogInformation("  路径: {Path}", rootTilesetPath);
            logger.LogInformation("  大小: {Size:N0} 字节", fileInfo.Length);
        }
        else
        {
            logger.LogWarning("⚠️ 根 tileset.json 未生成");
        }

        logger.LogInformation("");
        logger.LogInformation("输出目录结构:");
        logger.LogInformation("  {OutputDir}/", outputDir);
        logger.LogInformation("    ├── tileset.json (根 tileset)");
        logger.LogInformation("    └── Data/");

        foreach (var tileStat in tileStats.Take(3))
        {
            logger.LogInformation("        ├── {TileName}/", tileStat.TileName);
            logger.LogInformation("        │   ├── tileset.json");
            logger.LogInformation("        │   └── LOD-*/ ({Count} 个 .b3dm 文件)",
                tileStat.Count);
        }

        if (tileStats.Count > 3)
        {
            logger.LogInformation("        └── ... 还有 {Count} 个瓦片",
                tileStats.Count - 3);
        }

        logger.LogInformation("");
        logger.LogInformation("💡 提示: 使用 Cesium Viewer 加载根 tileset.json 查看完整数据集");
        logger.LogInformation("💡 根 tileset 会引用所有瓦片的子 tileset，实现分块加载");
    }
}
