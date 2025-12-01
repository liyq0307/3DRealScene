using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RealScene3D.Application.Services.MeshDecimator.Algorithms;
using RealScene3D.Domain.Geometry;
using RealScene3D.Domain.Materials;

namespace RealScene3D.Application.Services.MeshDecimator;

/// <summary>
/// 网格简化算法。
/// </summary>
public enum Algorithm
{
    /// <summary>
    /// 默认算法。
    /// </summary>
    Default,
    /// <summary>
    /// 快速四元网格简化算法。
    /// </summary>
    FastQuadricMesh
}

/// <summary>
/// 网格简化 API。
/// </summary>
public static class MeshDecimation
{
    private static readonly ILogger _logger = NullLogger.Instance;

    /// <summary>
    /// 创建特定的简化算法。
    /// </summary>
    /// <param name="algorithm">期望的算法。</param>
    /// <returns>简化算法。</returns>
    public static DecimationAlgorithm CreateAlgorithm(Algorithm algorithm)
    {
        DecimationAlgorithm alg;

        switch (algorithm)
        {
            case Algorithm.Default:
            case Algorithm.FastQuadricMesh:
                alg = new FastQuadricMeshSimplification();
                break;
            default:
                throw new ArgumentException("The specified algorithm is not supported.", "algorithm");
        }

        return alg;
    }

    #region SimpleMesh 方法

    /// <summary>
    /// 简化网格。
    /// </summary>
    /// <param name="mesh">要简化的网格。</param>
    /// <param name="targetTriangleCount">目标三角形数量。</param>
    /// <returns>简化的网格。</returns>
    public static SimpleMesh DecimateMesh(SimpleMesh mesh, int targetTriangleCount)
    {
        return DecimateMesh(Algorithm.Default, mesh, targetTriangleCount);
    }

    /// <summary>
    /// 简化网格。
    /// </summary>
    /// <param name="algorithm">期望的算法。</param>
    /// <param name="mesh">要简化的网格。</param>
    /// <param name="targetTriangleCount">目标三角形数量。</param>
    /// <returns>简化的网格。</returns>
    public static SimpleMesh DecimateMesh(Algorithm algorithm, SimpleMesh mesh, int targetTriangleCount)
    {
        if (mesh == null)
            throw new ArgumentNullException("mesh");

        var decimationAlgorithm = CreateAlgorithm(algorithm);
        return DecimateMesh(decimationAlgorithm, mesh, targetTriangleCount);
    }

    /// <summary>
    /// 简化网格。
    /// </summary>
    /// <param name="algorithm">简化算法。</param>
    /// <param name="mesh">要简化的网格。</param>
    /// <param name="targetTriangleCount">目标三角形数量。</param>
    /// <returns>简化的网格。</returns>
    public static SimpleMesh DecimateMesh(DecimationAlgorithm algorithm, SimpleMesh mesh, int targetTriangleCount)
    {
        if (algorithm == null)
            throw new ArgumentNullException("algorithm");
        else if (mesh == null)
            throw new ArgumentNullException("mesh");

        int currentTriangleCount = mesh.TriangleCount;
        if (targetTriangleCount > currentTriangleCount)
            targetTriangleCount = currentTriangleCount;
        else if (targetTriangleCount < 0)
            targetTriangleCount = 0;

        algorithm.Initialize(mesh);
        algorithm.DecimateMesh(targetTriangleCount);
        return algorithm.ToMesh();
    }

    /// <summary>
    /// 无损简化网格。
    /// </summary>
    /// <param name="mesh">要简化的网格。</param>
    /// <returns>简化的网格。</returns>
    public static SimpleMesh DecimateMeshLossless(SimpleMesh mesh)
    {
        return DecimateMeshLossless(Algorithm.Default, mesh);
    }

    /// <summary>
    /// 无损简化网格。
    /// </summary>
    /// <param name="algorithm">期望的算法。</param>
    /// <param name="mesh">要简化的网格。</param>
    /// <returns>简化的网格。</returns>
    public static SimpleMesh DecimateMeshLossless(Algorithm algorithm, SimpleMesh mesh)
    {
        if (mesh == null)
            throw new ArgumentNullException("mesh");

        var decimationAlgorithm = CreateAlgorithm(algorithm);
        return DecimateMeshLossless(decimationAlgorithm, mesh);
    }

    /// <summary>
    /// 无损简化网格。
    /// </summary>
    /// <param name="algorithm">简化算法。</param>
    /// <param name="mesh">要简化的网格。</param>
    /// <returns>简化的网格。</returns>
    public static SimpleMesh DecimateMeshLossless(DecimationAlgorithm algorithm, SimpleMesh mesh)
    {
        if (algorithm == null)
            throw new ArgumentNullException("algorithm");
        else if (mesh == null)
            throw new ArgumentNullException("mesh");

        int currentTriangleCount = mesh.TriangleCount;
        algorithm.Initialize(mesh);
        algorithm.DecimateMeshLossless();
        return algorithm.ToMesh();
    }

    #endregion

    #region MeshT 方法

    /// <summary>
    /// 简化 MeshT 网格 - 直接操作,无需转换。
    /// </summary>
    /// <param name="mesh">要简化的 MeshT 网格。</param>
    /// <param name="targetTriangleCount">目标三角形数量。</param>
    /// <returns>简化的 MeshT 网格。</returns>
    public static MeshT DecimateMeshT(MeshT mesh, int targetTriangleCount)
    {
        return DecimateMeshT(Algorithm.Default, mesh, targetTriangleCount);
    }

    /// <summary>
    /// 简化 MeshT 网格 - 直接操作,无需转换。
    /// </summary>
    /// <param name="algorithm">期望的算法。</param>
    /// <param name="mesh">要简化的 MeshT 网格。</param>
    /// <param name="targetTriangleCount">目标三角形数量。</param>
    /// <returns>简化的 MeshT 网格。</returns>
    public static MeshT DecimateMeshT(Algorithm algorithm, MeshT mesh, int targetTriangleCount)
    {
        if (mesh == null)
            throw new ArgumentNullException(nameof(mesh));

        var decimationAlgorithm = CreateAlgorithm(algorithm);
        return DecimateMeshT(decimationAlgorithm, mesh, targetTriangleCount);
    }

    /// <summary>
    /// 简化 MeshT 网格 - 直接操作,无需转换。
    /// </summary>
    /// <param name="algorithm">简化算法。</param>
    /// <param name="mesh">要简化的 MeshT 网格。</param>
    /// <param name="targetTriangleCount">目标三角形数量。</param>
    /// <returns>简化的 MeshT 网格。</returns>
    public static MeshT DecimateMeshT(DecimationAlgorithm algorithm, MeshT mesh, int targetTriangleCount)
    {
        if (algorithm == null)
            throw new ArgumentNullException(nameof(algorithm));
        else if (mesh == null)
            throw new ArgumentNullException(nameof(mesh));

        int currentTriangleCount = mesh.Faces.Count;
        if (targetTriangleCount > currentTriangleCount)
            targetTriangleCount = currentTriangleCount;
        else if (targetTriangleCount < 0)
            targetTriangleCount = 0;

        algorithm.Initialize(mesh);
        algorithm.DecimateMesh(targetTriangleCount);
        return algorithm.ToMeshT(mesh);
    }

    /// <summary>
    /// 无损简化 MeshT 网格 - 直接操作,无需转换。
    /// </summary>
    /// <param name="mesh">要简化的 MeshT 网格。</param>
    /// <returns>简化的 MeshT 网格。</returns>
    public static MeshT DecimateMeshTLossless(MeshT mesh)
    {
        return DecimateMeshTLossless(Algorithm.Default, mesh);
    }

    /// <summary>
    /// 无损简化 MeshT 网格 - 直接操作,无需转换。
    /// </summary>
    /// <param name="algorithm">期望的算法。</param>
    /// <param name="mesh">要简化的 MeshT 网格。</param>
    /// <returns>简化的 MeshT 网格。</returns>
    public static MeshT DecimateMeshTLossless(Algorithm algorithm, MeshT mesh)
    {
        if (mesh == null)
            throw new ArgumentNullException(nameof(mesh));

        var decimationAlgorithm = CreateAlgorithm(algorithm);
        return DecimateMeshTLossless(decimationAlgorithm, mesh);
    }

    /// <summary>
    /// 无损简化 MeshT 网格 - 直接操作,无需转换。
    /// </summary>
    /// <param name="algorithm">简化算法。</param>
    /// <param name="mesh">要简化的 MeshT 网格。</param>
    /// <returns>简化的 MeshT 网格。</returns>
    public static MeshT DecimateMeshTLossless(DecimationAlgorithm algorithm, MeshT mesh)
    {
        if (algorithm == null)
            throw new ArgumentNullException(nameof(algorithm));
        else if (mesh == null)
            throw new ArgumentNullException(nameof(mesh));

        algorithm.Initialize(mesh);
        algorithm.DecimateMeshLossless();
        return algorithm.ToMeshT(mesh);
    }

    #endregion

    #region SimpleMesh 和 MeshT 相互转方法

    /// <summary>
    /// 将 MeshT 转换为 SimpleMesh
    /// 完整转换包括顶点、UV坐标和法线
    /// 由于 MeshT 的顶点索引和纹理索引分离，需要展开为统一的顶点数组
    /// </summary>
    /// <param name="meshT">MeshT 对象</param>
    /// <returns>SimpleMesh 对象</returns>
    public static SimpleMesh ToSimpleMesh(MeshT meshT)
    {
        // 使用字典存储唯一的 (顶点索引, 纹理索引) 组合
        // Key: (vertexIndex, textureIndex), Value: 新的顶点索引
        var vertexMap = new Dictionary<(int vertexIndex, int textureIndex), int>();
        var expandedVertices = new List<Vector3d>();
        var expandedUVs = new List<Vector2>();

        // 按材质分组的索引
        var materialIndices = new Dictionary<int, List<int>>();

        // 第一遍：展开顶点和UV，构建映射
        for (int faceIndex = 0; faceIndex < meshT.Faces.Count; faceIndex++)
        {
            var face = meshT.Faces[faceIndex];

            // 确保材质索引有对应的列表
            if (!materialIndices.ContainsKey(face.MaterialIndex))
            {
                materialIndices[face.MaterialIndex] = new List<int>();
            }

            // 处理三个顶点
            var faceIndices = new[] {
                (face.IndexA, face.TextureIndexA),
                (face.IndexB, face.TextureIndexB),
                (face.IndexC, face.TextureIndexC)
            };

            foreach (var (vertexIndex, textureIndex) in faceIndices)
            {
                var key = (vertexIndex, textureIndex);

                if (!vertexMap.ContainsKey(key))
                {
                    // 添加新的展开顶点
                    var vertex = meshT.Vertices[vertexIndex];
                    expandedVertices.Add(new Vector3d(vertex.X, vertex.Y, vertex.Z));

                    // 添加对应的UV坐标
                    if (textureIndex >= 0 && textureIndex < meshT.TextureVertices.Count)
                    {
                        var uv = meshT.TextureVertices[textureIndex];
                        expandedUVs.Add(new Vector2((float)uv.X, (float)uv.Y));
                    }
                    else
                    {
                        // 默认UV
                        expandedUVs.Add(new Vector2(0f, 0f));
                    }

                    vertexMap[key] = expandedVertices.Count - 1;
                }

                // 添加索引到对应的材质列表
                materialIndices[face.MaterialIndex].Add(vertexMap[key]);
            }
        }

        // 转换为数组
        var vertices = expandedVertices.ToArray();
        var uvs = expandedUVs.ToArray();

        // 创建 SimpleMesh
        SimpleMesh simpleMesh;

        if (materialIndices.Count == 1)
        {
            // 单材质：使用单一索引数组
            var indices = materialIndices.Values.First().ToArray();
            simpleMesh = new SimpleMesh(vertices, indices);
        }
        else
        {
            // 多材质：为每个材质创建子网格
            var sortedMaterials = materialIndices.OrderBy(kvp => kvp.Key).ToList();
            var subMeshIndices = new int[sortedMaterials.Count][];

            for (int i = 0; i < sortedMaterials.Count; i++)
            {
                subMeshIndices[i] = sortedMaterials[i].Value.ToArray();
            }

            simpleMesh = new SimpleMesh(vertices, subMeshIndices);
        }

        // 设置UV坐标（使用第一个UV通道）
        if (uvs.Length > 0 && uvs.Length == vertices.Length)
        {
            simpleMesh.UV1 = uvs;
        }

        // 计算法线
        try
        {
            simpleMesh.RecalculateNormals();
            _logger.LogDebug("为 SimpleMesh 计算了法线：{Count} 个顶点", vertices.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "计算法线失败，继续使用无法线的网格");
        }

        _logger.LogDebug("MeshT → SimpleMesh 转换完成：原始顶点={OrigVerts}/{OrigUVs}, 展开后顶点={ExpandedVerts}, 子网格数={SubMeshCount}",
            meshT.Vertices.Count, meshT.TextureVertices.Count, vertices.Length, simpleMesh.SubMeshCount);

        return simpleMesh;
    }

    /// <summary>
    /// 将 SimpleMesh 转换为 MeshT
    /// 用于简化流程中需要将简化后的网格转换回 MeshT 格式
    /// 支持多 UV 通道（优先使用 UV1）和法线信息
    /// </summary>
    /// <param name="simpleMesh">SimpleMesh 对象</param>
    /// <param name="originalMesh">原始 MeshT 对象，用于复制材质信息（可选）</param>
    /// <returns>MeshT 对象</returns>
    public static MeshT ToMeshT(SimpleMesh simpleMesh, MeshT? originalMesh = null)
    {
        // 转换顶点
        var vertices = new List<Vertex3>(simpleMesh.VertexCount);
        for (int i = 0; i < simpleMesh.VertexCount; i++)
        {
            var v = simpleMesh.Vertices[i];
            vertices.Add(new Vertex3(v.x, v.y, v.z));
        }

        // 转换纹理顶点（UV坐标）
        var textureVertices = new List<Vertex2>();

        // 优先使用 UV1，然后尝试 UV2、UV3、UV4
        Vector2[]? uvSource = simpleMesh.UV1;
        int uvDimension = simpleMesh.GetUVDimension(0);

        if (uvSource == null || uvSource.Length == 0)
        {
            // 尝试其他 UV 通道
            for (int channel = 1; channel < SimpleMesh.UVChannelCount; channel++)
            {
                uvDimension = simpleMesh.GetUVDimension(channel);
                if (uvDimension == 2)
                {
                    uvSource = simpleMesh.GetUVs2D(channel);
                    if (uvSource != null && uvSource.Length > 0)
                    {
                        _logger.LogDebug("使用 UV 通道 {Channel} 进行转换", channel);
                        break;
                    }
                }
            }
        }

        if (uvSource != null && uvSource.Length > 0)
        {
            // 如果有 UV 坐标，使用它们
            if (uvSource.Length != simpleMesh.VertexCount)
            {
                _logger.LogWarning("UV 数量 ({UVCount}) 与顶点数量 ({VertexCount}) 不匹配，使用默认 UV",
                    uvSource.Length, simpleMesh.VertexCount);

                // 使用默认 UV
                for (int i = 0; i < simpleMesh.VertexCount; i++)
                {
                    textureVertices.Add(new Vertex2(0.0, 0.0));
                }
            }
            else
            {
                for (int i = 0; i < uvSource.Length; i++)
                {
                    textureVertices.Add(new Vertex2(uvSource[i].x, uvSource[i].y));
                }
            }
        }
        else
        {
            // 如果没有 UV 坐标，创建默认的 UV（全部为 0,0）
            _logger.LogDebug("SimpleMesh 没有 UV 坐标，使用默认值 (0,0)");
            for (int i = 0; i < simpleMesh.VertexCount; i++)
            {
                textureVertices.Add(new Vertex2(0.0, 0.0));
            }
        }

        // 转换面
        var faces = new List<FaceT>();
        int subMeshCount = simpleMesh.SubMeshCount;

        for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
        {
            var indices = simpleMesh.GetIndices(subMeshIndex);

            // 验证索引有效性
            if (indices.Length % 3 != 0)
            {
                _logger.LogWarning("子网格 {SubMeshIndex} 的索引数量 ({Count}) 不是 3 的倍数，跳过",
                    subMeshIndex, indices.Length);
                continue;
            }

            // 每个三角形由3个索引组成
            for (int i = 0; i < indices.Length; i += 3)
            {
                int indexA = indices[i];
                int indexB = indices[i + 1];
                int indexC = indices[i + 2];

                // 验证索引范围
                if (indexA < 0 || indexA >= vertices.Count ||
                    indexB < 0 || indexB >= vertices.Count ||
                    indexC < 0 || indexC >= vertices.Count)
                {
                    _logger.LogWarning("子网格 {SubMeshIndex} 包含无效的顶点索引，跳过该三角形", subMeshIndex);
                    continue;
                }

                // 纹理索引与顶点索引相同（1:1映射）
                int textureIndexA = indexA;
                int textureIndexB = indexB;
                int textureIndexC = indexC;

                // 材质索引为子网格索引
                int materialIndex = subMeshIndex;

                faces.Add(new FaceT(indexA, indexB, indexC,
                    textureIndexA, textureIndexB, textureIndexC,
                    materialIndex));
            }
        }

        // 创建材质列表
        var materials = new List<Material>();

        if (originalMesh != null && originalMesh.Materials.Count > 0)
        {
            // 如果有原始网格，复制其材质
            // 确保材质数量至少与子网格数量相同
            int materialsNeeded = Math.Max(subMeshCount, originalMesh.Materials.Count);

            for (int i = 0; i < materialsNeeded; i++)
            {
                if (i < originalMesh.Materials.Count)
                {
                    materials.Add((Material)originalMesh.Materials[i].Clone());
                }
                else
                {
                    // 如果原始材质不足，创建默认材质
                    materials.Add(new Material($"Material_{i}"));
                }
            }
        }
        else
        {
            // 创建默认材质
            for (int i = 0; i < Math.Max(1, subMeshCount); i++)
            {
                materials.Add(new Material($"Material_{i}"));
            }
        }

        _logger.LogDebug("SimpleMesh → MeshT 转换完成：顶点={VertexCount}, UV={UVCount}, 面={FaceCount}, 材质={MaterialCount}, 子网格={SubMeshCount}",
            vertices.Count, textureVertices.Count, faces.Count, materials.Count, subMeshCount);

        // 创建并返回 MeshT 对象
        return new MeshT(vertices, textureVertices, faces, materials);
    }

    #endregion
}