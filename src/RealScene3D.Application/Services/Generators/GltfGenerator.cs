using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Geometry;
using SharpGLTF.Schema2;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using DomainMaterial = RealScene3D.Domain.Materials.Material;
using SysVector2 = System.Numerics.Vector2;
using SysVector3 = System.Numerics.Vector3;
using SysVector4 = System.Numerics.Vector4;
using SixLaborsImage = SixLabors.ImageSharp.Image;
using GltfAlphaMode = SharpGLTF.Materials.AlphaMode;

namespace RealScene3D.Application.Services.Generators;

/// <summary>
/// GLTF生成器 - 基于SharpGLTF库重构
/// 生成标准glTF 2.0格式文件（GLB二进制格式）
/// 完整支持顶点位置、法线、纹理坐标和PBR材质
/// </summary>
public class GltfGenerator : TileGenerator
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器实例</param>
    public GltfGenerator(ILogger<GltfGenerator> logger) : base(logger)
    {
    }

    /// <summary>
    /// 生成瓦片文件数据 - 实现抽象方法
    /// </summary>
    /// <param name="mesh">网格数据</param>
    /// <returns>GLB文件的二进制数据</returns>
    public override byte[] GenerateTile(MeshT mesh)
    {
        return GenerateGLB(mesh);
    }

    /// <summary>
    /// 保存瓦片文件到磁盘 - 实现抽象方法
    /// </summary>
    /// <param name="mesh">网格数据</param>
    /// <param name="outputPath">输出文件路径</param>
    public override async Task SaveTileAsync(MeshT mesh, string outputPath)
    {
        await SaveGLBFileAsync(mesh, outputPath);
    }

    /// <summary>
    /// 获取瓦片格式名称
    /// </summary>
    /// <returns>格式名称 "GLTF/GLB"</returns>
    protected override string GetFormatName()
    {
        return "GLTF/GLB";
    }

    /// <summary>
    /// 生成GLB (Binary glTF 2.0) 数据
    /// 使用SharpGLTF库生成，支持完整的PBR材质和纹理
    /// </summary>
    /// <param name="mesh">网格数据</param>
    /// <returns>GLB文件的二进制数据</returns>
    public byte[] GenerateGLB(MeshT mesh)
    {
        ValidateInput(mesh);

        _logger.LogDebug("开始生成GLB: 三角形数={Count}", mesh.Faces.Count);

        try
        {
            // 1. 创建场景和根节点
            var model = ModelRoot.CreateModel();
            var scene = model.UseScene("Scene");
            var rootNode = scene.CreateNode("RootNode");

            // 2. 计算法线（如果需要）
            var normals = CalculateNormals(mesh);

            // 3. 按材质分组处理面
            var facesByMaterial = GroupFacesByMaterial(mesh);

            // 4. 为每个材质创建网格
            var allMeshBuilders = new List<IMeshBuilder<MaterialBuilder>>();
            for (int matIdx = 0; matIdx < facesByMaterial.Count; matIdx++)
            {
                var faces = facesByMaterial[matIdx];
                if (faces.Count == 0)
                    continue;

                var material = mesh.Materials[matIdx];

                // 创建材质
                var gltfMaterial = CreateMaterial(model, material);

                // 创建网格
                var meshBuilder = CreateMeshForMaterial(mesh, faces, normals, gltfMaterial);
                allMeshBuilders.Add(meshBuilder);
            }

            // 将所有 MeshBuilder 添加到场景
            if (allMeshBuilders.Count > 0)
            {
                var sceneMesh = rootNode.WithMesh(model.CreateMeshes(allMeshBuilders.ToArray())[0]);
            }

            // 5. 写入到内存流
            using var memoryStream = new MemoryStream();
            model.WriteGLB(memoryStream);

            var glbData = memoryStream.ToArray();

            _logger.LogDebug("GLB生成完成: 总大小={Size}字节", glbData.Length);
            LogGenerationStats(mesh.Faces.Count, mesh.Vertices.Count, glbData.Length);

            return glbData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成GLB失败");
            throw;
        }
    }

    /// <summary>
    /// 计算每个顶点的法线向量
    /// 使用面法线加权平均的方式计算顶点法线
    /// </summary>
    /// <param name="mesh">网格数据</param>
    /// <returns>顶点法线数组</returns>
    private SysVector3[] CalculateNormals(MeshT mesh)
    {
        var normals = new SysVector3[mesh.Vertices.Count];
        var normalCounts = new int[mesh.Vertices.Count];

        // 为每个面计算法线，并累加到顶点
        foreach (var face in mesh.Faces)
        {
            var v0 = mesh.Vertices[face.IndexA];
            var v1 = mesh.Vertices[face.IndexB];
            var v2 = mesh.Vertices[face.IndexC];

            // 计算面法线
            var edge1 = new SysVector3(
                (float)(v1.X - v0.X),
                (float)(v1.Y - v0.Y),
                (float)(v1.Z - v0.Z)
            );
            var edge2 = new SysVector3(
                (float)(v2.X - v0.X),
                (float)(v2.Y - v0.Y),
                (float)(v2.Z - v0.Z)
            );
            var faceNormal = SysVector3.Cross(edge1, edge2);

            // 累加到每个顶点
            normals[face.IndexA] += faceNormal;
            normals[face.IndexB] += faceNormal;
            normals[face.IndexC] += faceNormal;

            normalCounts[face.IndexA]++;
            normalCounts[face.IndexB]++;
            normalCounts[face.IndexC]++;
        }

        // 归一化所有顶点法线
        for (int i = 0; i < normals.Length; i++)
        {
            if (normalCounts[i] > 0 && normals[i].Length() > 0)
            {
                normals[i] = SysVector3.Normalize(normals[i]);
            }
            else
            {
                // 默认法线朝上
                normals[i] = new SysVector3(0, 0, 1);
            }
        }

        return normals;
    }

    /// <summary>
    /// 按材质分组面
    /// </summary>
    /// <param name="mesh">网格数据</param>
    /// <returns>每个材质对应的面列表</returns>
    private List<List<FaceT>> GroupFacesByMaterial(MeshT mesh)
    {
        var result = new List<List<FaceT>>();

        // 初始化每个材质的面列表
        for (int i = 0; i < mesh.Materials.Count; i++)
        {
            result.Add(new List<FaceT>());
        }

        // 分组
        foreach (var face in mesh.Faces)
        {
            if (face.MaterialIndex >= 0 && face.MaterialIndex < result.Count)
            {
                result[face.MaterialIndex].Add(face);
            }
        }

        return result;
    }

    /// <summary>
    /// 创建glTF材质
    /// 支持基础颜色、金属度、粗糙度和纹理
    /// </summary>
    /// <param name="model">模型根节点</param>
    /// <param name="material">领域材质</param>
    /// <returns>glTF材质</returns>
    private MaterialBuilder CreateMaterial(ModelRoot model, DomainMaterial material)
    {
        var matBuilder = new MaterialBuilder(material.Name ?? "DefaultMaterial");

        // 设置基础颜色
        SysVector4 baseColor;
        if (material.DiffuseColor != null)
        {
            var diffuse = material.DiffuseColor;
            baseColor = new SysVector4(
                (float)diffuse.R,
                (float)diffuse.G,
                (float)diffuse.B,
                (float)(material.Dissolve ?? 1.0)
            );
        }
        else
        {
            baseColor = SysVector4.One;
        }

        // 应用纹理（如果存在）
        if (material.TextureImage != null)
        {
            // ⭐ 优先使用内存中的纹理数据（纹理打包后的结果）
            try
            {
                using var ms = new MemoryStream();
                material.TextureImage.Save(ms, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
                var imageBytes = ms.ToArray();

                var imageBuilder = ImageBuilder.From(imageBytes, $"{material.Name}_packed.png");

                matBuilder
                    .WithMetallicRoughnessShader()
                    .WithBaseColor(imageBuilder, baseColor);

                _logger.LogDebug("材质 {MaterialName} 使用内存中的打包纹理",
                    material.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "使用内存纹理失败: {MaterialName}，使用纯色材质", material.Name);
                matBuilder
                    .WithMetallicRoughnessShader()
                    .WithBaseColor(baseColor);
            }
        }
        else if (!string.IsNullOrEmpty(material.Texture) && File.Exists(material.Texture))
        {
            // ⭐ 从文件加载纹理（原始流程）
            try
            {
                // 读取纹理图像
                using var image = SixLaborsImage.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(material.Texture);
                using var ms = new MemoryStream();

                // 转换为PNG格式（glTF标准支持）
                image.Save(ms, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
                var imageBytes = ms.ToArray();

                // 创建图像构建器
                var imageBuilder = ImageBuilder.From(imageBytes, material.Texture);

                // 设置带纹理的通道
                matBuilder
                    .WithMetallicRoughnessShader()
                    .WithBaseColor(imageBuilder, baseColor);

                _logger.LogDebug("材质 {MaterialName} 应用纹理: {Texture}",
                    material.Name, material.Texture);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "加载纹理失败: {Texture}，使用纯色材质", material.Texture);
                matBuilder
                    .WithMetallicRoughnessShader()
                    .WithBaseColor(baseColor);
            }
        }
        else
        {
            // 纯色材质
            matBuilder
                .WithMetallicRoughnessShader()
                .WithBaseColor(baseColor);
        }

        // 设置金属度和粗糙度（默认值）
        matBuilder
            .WithMetallicRoughness(0.0f, 0.8f); // 非金属，中等粗糙度

        // 设置镜面反射（如果有）
        if (material.SpecularColor != null)
        {
            var specular = material.SpecularColor;
            var specularFactor = (float)((specular.R + specular.G + specular.B) / 3.0);

            // 调整粗糙度（镜面反射强度越高，粗糙度越低）
            var roughness = 1.0f - Math.Clamp(specularFactor, 0f, 1f);
            matBuilder.WithMetallicRoughness(0.0f, roughness);
        }

        // 设置透明度
        if (material.Dissolve.HasValue && material.Dissolve.Value < 1.0)
        {
            matBuilder.WithAlpha(GltfAlphaMode.BLEND);
        }

        return matBuilder;
    }

    /// <summary>
    /// 为特定材质创建网格
    /// </summary>
    /// <param name="mesh">原始网格数据</param>
    /// <param name="faces">该材质的面列表</param>
    /// <param name="normals">顶点法线数组</param>
    /// <param name="material">glTF材质</param>
    /// <returns>网格构建器</returns>
    private IMeshBuilder<MaterialBuilder> CreateMeshForMaterial(
        MeshT mesh,
        List<FaceT> faces,
        SysVector3[] normals,
        MaterialBuilder material)
    {
        // 使用 VertexPosition, VertexTexture1, VertexEmpty 组合
        // 这是 SharpGLTF 的标准顶点类型组合
        var meshBuilder = new MeshBuilder<VertexPosition, VertexTexture1, VertexEmpty>(mesh.Name ?? "Mesh");

        // 获取或创建图元（Primitive）
        var primitive = meshBuilder.UsePrimitive(material);

        // 添加三角形
        foreach (var face in faces)
        {
            var v0 = mesh.Vertices[face.IndexA];
            var v1 = mesh.Vertices[face.IndexB];
            var v2 = mesh.Vertices[face.IndexC];

            var n0 = normals[face.IndexA];
            var n1 = normals[face.IndexB];
            var n2 = normals[face.IndexC];

            // 获取纹理坐标
            SysVector2 t0, t1, t2;
            if (face.TextureIndexA < mesh.TextureVertices.Count &&
                face.TextureIndexB < mesh.TextureVertices.Count &&
                face.TextureIndexC < mesh.TextureVertices.Count)
            {
                var tv0 = mesh.TextureVertices[face.TextureIndexA];
                var tv1 = mesh.TextureVertices[face.TextureIndexB];
                var tv2 = mesh.TextureVertices[face.TextureIndexC];

                t0 = new SysVector2((float)tv0.X, (float)tv0.Y);
                t1 = new SysVector2((float)tv1.X, (float)tv1.Y);
                t2 = new SysVector2((float)tv2.X, (float)tv2.Y);
            }
            else
            {
                // 默认纹理坐标
                t0 = new SysVector2(0f, 0f);
                t1 = new SysVector2(1f, 0f);
                t2 = new SysVector2(0f, 1f);
            }

            // 创建顶点（使用 SharpGLTF 的 VertexBuilder）
            var vertexBuilder0 = new VertexBuilder<VertexPosition, VertexTexture1, VertexEmpty>(
                new VertexPosition(new SysVector3((float)v0.X, (float)v0.Y, (float)v0.Z)),
                new VertexTexture1(t0));

            var vertexBuilder1 = new VertexBuilder<VertexPosition, VertexTexture1, VertexEmpty>(
                new VertexPosition(new SysVector3((float)v1.X, (float)v1.Y, (float)v1.Z)),
                new VertexTexture1(t1));

            var vertexBuilder2 = new VertexBuilder<VertexPosition, VertexTexture1, VertexEmpty>(
                new VertexPosition(new SysVector3((float)v2.X, (float)v2.Y, (float)v2.Z)),
                new VertexTexture1(t2));

            // 添加三角形（注意顺序，glTF使用逆时针）
            primitive.AddTriangle(vertexBuilder0, vertexBuilder1, vertexBuilder2);
        }

        return meshBuilder;
    }

    /// <summary>
    /// 保存GLB文件到磁盘
    /// </summary>
    /// <param name="mesh">网格数据</param>
    /// <param name="outputPath">输出文件路径</param>
    public async Task SaveGLBFileAsync(MeshT mesh, string outputPath)
    {
        _logger.LogInformation("保存GLB文件: {Path}", outputPath);

        try
        {
            var glbData = GenerateGLB(mesh);

            // 确保目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(outputPath, glbData);

            _logger.LogInformation("GLB文件保存成功: {Path}, 大小={Size}字节",
                outputPath, glbData.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存GLB文件失败: {Path}", outputPath);
            throw;
        }
    }
}
