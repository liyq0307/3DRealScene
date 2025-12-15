using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealScene3D.Application.Services.Loaders;
using RealScene3D.Application.Services.Generators;
using RealScene3D.Domain.Utils;

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

        var serviceProvider = services.BuildServiceProvider();

        // 获取日志实例
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        // 定义测试文件路径（请根据实际情况修改）
        string objFilePath = @"E:\Data\3D\odm_texturing\odm_textured_model_geo.obj";
        string osgbFilePath = @"E:\Data\3D\Tile_+005_+006\Tile_+005_+006.osgb";
        string glbInputPath = @"E:\Data\3D\odm_texturing\odm_textured_model_geo.glb";
        string glbOutputPath1 = @"E:\Data\3D\test_output_loader.glb";
        string glbOutputPath2 = @"E:\Data\3D\test_output_meshutils.glb";
        string glbOutputPath3 = @"E:\Data\3D\Tile_+005_+006.glb";
        string objOutputPath = @"E:\Data\3D\test_output_from_glb.obj";

        try
        {
            // 测试1: 使用 ObjModelLoader
            await TestWithObjModelLoader(logger, serviceProvider, objFilePath, glbOutputPath1);

            logger.LogInformation("");
            logger.LogInformation("");

            // 测试2: 使用 MeshUtils.LoadMesh
            await TestWithMeshUtils(logger, serviceProvider, objFilePath, glbOutputPath2);

            logger.LogInformation("");
            logger.LogInformation("");

            // 测试3: 使用 OsgbModelLoader
            await TestWithOsgbModelLoader(logger, serviceProvider, osgbFilePath, glbOutputPath3);

            logger.LogInformation("");
            logger.LogInformation("");

            // 测试4: 使用 GltfModelLoader 加载 GLB 并生成 OBJ
            await TestWithGltfModelLoader(logger, serviceProvider, glbInputPath, objOutputPath);

            logger.LogInformation("");
            logger.LogInformation("========================================");
            logger.LogInformation("所有测试完成！");
            logger.LogInformation("========================================");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "测试执行失败");
            Environment.Exit(1);
        }
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
}
