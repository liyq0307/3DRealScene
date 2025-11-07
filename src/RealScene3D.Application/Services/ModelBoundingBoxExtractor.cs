using Microsoft.Extensions.Logging;
using SharpGLTF.Schema2;
using System.Numerics;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Application.Services;

/// <summary>
/// 模型包围盒提取器 - 从GLB/GLTF文件中提取包围盒信息
/// </summary>
public class ModelBoundingBoxExtractor
{
    private readonly ILogger _logger;

    public ModelBoundingBoxExtractor(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 从GLB/GLTF文件中提取包围盒
    /// </summary>
    /// <param name="modelPath">模型文件路径</param>
    /// <returns>包围盒数据</returns>
    public BoundingBox3D? ExtractBoundingBox(string modelPath)
    {
        if (string.IsNullOrEmpty(modelPath))
        {
            _logger.LogWarning("模型路径为空，无法提取包围盒");
            return null;
        }

        if (!File.Exists(modelPath))
        {
            _logger.LogWarning("模型文件不存在: {ModelPath}", modelPath);
            return null;
        }

        try
        {
            _logger.LogInformation("开始从模型中提取包围盒: {ModelPath}", modelPath);

            // 加载GLTF/GLB模型
            var model = ModelRoot.Load(modelPath);

            if (model == null)
            {
                _logger.LogWarning("无法加载模型文件: {ModelPath}", modelPath);
                return null;
            }

            // 初始化包围盒边界值
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float minZ = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            float maxZ = float.MinValue;

            bool foundVertices = false;
            int totalVertexCount = 0;
            int meshCount = 0;
            int primitiveCount = 0;

            // 遍历所有网格和图元
            foreach (var mesh in model.LogicalMeshes)
            {
                meshCount++;
                foreach (var primitive in mesh.Primitives)
                {
                    primitiveCount++;
                    // 获取位置属性访问器
                    var positionAccessor = primitive.GetVertexAccessor("POSITION");
                    if (positionAccessor == null)
                    {
                        _logger.LogWarning("Primitive {PrimitiveIndex} 在 Mesh {MeshIndex} 中没有POSITION属性", primitiveCount, meshCount);
                        continue;
                    }

                    // 遍历所有顶点
                    var positions = positionAccessor.AsVector3Array();
                    int vertexCount = positions.Count;
                    totalVertexCount += vertexCount;

                    _logger.LogDebug("Mesh {MeshIndex}, Primitive {PrimitiveIndex}: {VertexCount} 个顶点",
                        meshCount, primitiveCount, vertexCount);

                    foreach (var position in positions)
                    {
                        foundVertices = true;

                        minX = Math.Min(minX, position.X);
                        minY = Math.Min(minY, position.Y);
                        minZ = Math.Min(minZ, position.Z);
                        maxX = Math.Max(maxX, position.X);
                        maxY = Math.Max(maxY, position.Y);
                        maxZ = Math.Max(maxZ, position.Z);
                    }
                }
            }

            _logger.LogInformation("模型统计: {MeshCount} 个网格, {PrimitiveCount} 个图元, {VertexCount} 个顶点",
                meshCount, primitiveCount, totalVertexCount);

            if (!foundVertices)
            {
                _logger.LogWarning("模型中未找到任何顶点数据: {ModelPath}", modelPath);
                return null;
            }

            var boundingBox = new BoundingBox3D
            {
                MinX = minX,
                MinY = minY,
                MinZ = minZ,
                MaxX = maxX,
                MaxY = maxY,
                MaxZ = maxZ
            };

            // 计算包围盒尺寸
            var sizeX = maxX - minX;
            var sizeY = maxY - minY;
            var sizeZ = maxZ - minZ;

            _logger.LogInformation(
                "成功提取包围盒: Min({MinX:F6}, {MinY:F6}, {MinZ:F6}), Max({MaxX:F6}, {MaxY:F6}, {MaxZ:F6})",
                minX, minY, minZ, maxX, maxY, maxZ);

            _logger.LogInformation(
                "包围盒尺寸: Width={Width:F6}, Height={Height:F6}, Depth={Depth:F6}, IsValid={IsValid}",
                sizeX, sizeY, sizeZ, boundingBox.IsValid());

            return boundingBox;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取模型包围盒失败: {ModelPath}", modelPath);
            return null;
        }
    }

    /// <summary>
    /// 从GLB/GLTF文件中提取包围盒（异步版本）
    /// </summary>
    /// <param name="modelPath">模型文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包围盒数据</returns>
    public async Task<BoundingBox3D?> ExtractBoundingBoxAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => ExtractBoundingBox(modelPath), cancellationToken);
    }
}
