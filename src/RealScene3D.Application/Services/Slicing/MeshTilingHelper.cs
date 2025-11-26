using RealScene3D.Domain.Entities;
using RealScene3D.MeshTiling.Geometry;

namespace RealScene3D.Application.Services.Slicing;

/// <summary>
/// MeshTiling 辅助工具 - 数据转换
/// 提供 RealScene3D 数据结构与 MeshT 之间的转换
/// </summary>
public static class MeshTilingHelper
{
    /// <summary>
    /// 转换：RealScene3D.Triangle[] → MeshT
    /// </summary>
    public static MeshT ToMeshT(
        List<Triangle> triangles,
        Dictionary<string, Material> materials)
    {
        var vertices = new List<Vertex3>();
        var uvs = new List<Vertex2>();
        var faces = new List<FaceT>();

        // 建立材质名称到索引的映射
        var materialNameToIndex = new Dictionary<string, int>();
        var meshMaterials = new List<MeshTiling.Materials.Material>();

        foreach (var (name, mat) in materials)
        {
            materialNameToIndex[name] = meshMaterials.Count;
            meshMaterials.Add(ConvertMaterial(mat));
        }

        // 构建顶点和UV映射表（去重）
        var vertexMap = new Dictionary<Vector3D, int>();
        var uvMap = new Dictionary<(double, double), int>();

        foreach (var tri in triangles)
        {
            // 添加顶点
            int v1 = AddVertex(tri.V1, vertices, vertexMap);
            int v2 = AddVertex(tri.V2, vertices, vertexMap);
            int v3 = AddVertex(tri.V3, vertices, vertexMap);

            // 添加UV
            int uv1 = AddUV(tri.UV1, uvs, uvMap);
            int uv2 = AddUV(tri.UV2, uvs, uvMap);
            int uv3 = AddUV(tri.UV3, uvs, uvMap);

            // 获取材质索引
            int matIndex = 0;
            if (!string.IsNullOrEmpty(tri.MaterialName) && materialNameToIndex.TryGetValue(tri.MaterialName, out int idx))
            {
                matIndex = idx;
            }

            // 创建面
            faces.Add(new FaceT(v1, v2, v3, uv1, uv2, uv3, matIndex));
        }

        return new MeshT(vertices, uvs, faces, meshMaterials);
    }

    /// <summary>
    /// 转换：MeshT → RealScene3D.Triangle[]
    /// </summary>
    public static List<Triangle> FromMeshT(MeshT meshT)
    {
        var triangles = new List<Triangle>();

        foreach (var face in meshT.Faces)
        {
            var v1 = meshT.Vertices[face.IndexA];
            var v2 = meshT.Vertices[face.IndexB];
            var v3 = meshT.Vertices[face.IndexC];

            var uv1 = meshT.TextureVertices[face.TextureIndexA];
            var uv2 = meshT.TextureVertices[face.TextureIndexB];
            var uv3 = meshT.TextureVertices[face.TextureIndexC];

            // 获取材质名称
            string? materialName = null;
            if (face.MaterialIndex >= 0 && face.MaterialIndex < meshT.Materials.Count)
            {
                materialName = meshT.Materials[face.MaterialIndex].Name;
            }

            triangles.Add(new Triangle
            {
                V1 = new Vector3D(v1.X, v1.Y, v1.Z),
                V2 = new Vector3D(v2.X, v2.Y, v2.Z),
                V3 = new Vector3D(v3.X, v3.Y, v3.Z),
                UV1 = new Vector2D(uv1.X, uv1.Y),
                UV2 = new Vector2D(uv2.X, uv2.Y),
                UV3 = new Vector2D(uv3.X, uv3.Y),
                MaterialName = materialName
            });
        }

        return triangles;
    }

    /// <summary>
    /// 更新材质纹理路径（从 MeshT 同步回 Domain.Material）
    /// </summary>
    public static void UpdateMaterialTextures(
        Dictionary<string, Material> domainMaterials,
        IReadOnlyList<MeshTiling.Materials.Material> meshMaterials)
    {
        foreach (var meshMat in meshMaterials)
        {
            if (!string.IsNullOrEmpty(meshMat.Name) && domainMaterials.TryGetValue(meshMat.Name, out var domainMat))
            {
                // 更新漫反射纹理路径（MeshT 打包后的新路径）
                if (domainMat.DiffuseTexture != null && meshMat.Texture != null)
                {
                    domainMat.DiffuseTexture.FilePath = meshMat.Texture;
                }
            }
        }
    }

    #region 私有辅助方法

    private static int AddVertex(Vector3D v, List<Vertex3> vertices, Dictionary<Vector3D, int> map)
    {
        if (map.TryGetValue(v, out int index))
            return index;

        index = vertices.Count;
        vertices.Add(new Vertex3(v.X, v.Y, v.Z));
        map[v] = index;
        return index;
    }

    private static int AddUV(Vector2D? uv, List<Vertex2> uvs, Dictionary<(double, double), int> map)
    {
        var key = uv != null ? (uv.U, uv.V) : (0.0, 0.0);

        if (map.TryGetValue(key, out int index))
            return index;

        index = uvs.Count;
        uvs.Add(new Vertex2(key.Item1, key.Item2));
        map[key] = index;
        return index;
    }

    private static MeshTiling.Materials.Material ConvertMaterial(Material source)
    {
        return new MeshTiling.Materials.Material(
            name: source.Name,
            texture: source.DiffuseTexture?.FilePath,
            normalMap: source.NormalTexture?.FilePath,
            ambientColor: source.AmbientColor != null
                ? new MeshTiling.Materials.RGB(source.AmbientColor.R, source.AmbientColor.G, source.AmbientColor.B)
                : null,
            diffuseColor: source.DiffuseColor != null
                ? new MeshTiling.Materials.RGB(source.DiffuseColor.R, source.DiffuseColor.G, source.DiffuseColor.B)
                : null,
            specularColor: source.SpecularColor != null
                ? new MeshTiling.Materials.RGB(source.SpecularColor.R, source.SpecularColor.G, source.SpecularColor.B)
                : null,
            specularExponent: source.Shininess,
            dissolve: source.Opacity);
    }

    #endregion
}
