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
        services.AddTransient<GltfGenerator>();

        var serviceProvider = services.BuildServiceProvider();

        // 获取日志实例
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        // 定义测试文件路径（请根据实际情况修改）
        string objFilePath = @"E:\Data\3D\odm_texturing\odm_textured_model_geo.obj";
        string glbOutputPath1 = @"E:\Data\3D\test_output_loader.glb";
        string glbOutputPath2 = @"E:\Data\3D\test_output_meshutils.glb";

        try
        {
            // 测试1: 使用 ObjModelLoader
            await TestWithObjModelLoader(logger, serviceProvider, objFilePath, glbOutputPath1);

            logger.LogInformation("");
            logger.LogInformation("");

            // 测试2: 使用 MeshUtils.LoadMesh
            await TestWithMeshUtils(logger, serviceProvider, objFilePath, glbOutputPath2);

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
}
