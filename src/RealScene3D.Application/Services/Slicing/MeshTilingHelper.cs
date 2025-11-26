using Microsoft.Extensions.Logging;
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
        var normals = new List<Vertex3>();
        var faces = new List<FaceT>();
        var meshMaterials = new Dictionary<string, RealScene3D.MeshTiling.Materials.Material>();

        // 构建映射表（去重）
        var vertexMap = new Dictionary<Vector3D, int>();
        var uvMap = new Dictionary<(double, double), int>();
        var normalMap = new Dictionary<Vector3D, int>();

        foreach (var tri in triangles)
        {
            // 添加顶点
            int v1 = AddVertex(tri.Vertex1, vertices, vertexMap);
            int v2 = AddVertex(tri.Vertex2, vertices, vertexMap);
            int v3 = AddVertex(tri.Vertex3, vertices, vertexMap);

            // 添加UV
            int uv1 = AddUV(tri.UV1, uvs, uvMap);
            int uv2 = AddUV(tri.UV2, uvs, uvMap);
            int uv3 = AddUV(tri.UV3, uvs, uvMap);

            // 添加法线
            int n1 = AddNormal(tri.Normal1, normals, normalMap);
            int n2 = AddNormal(tri.Normal2, normals, normalMap);
            int n3 = AddNormal(tri.Normal3, normals, normalMap);

            // 创建面
            faces.Add(new FaceT
            {
                V1 = v1, V2 = v2, V3 = v3,
                Vt1 = uv1, Vt2 = uv2, Vt3 = uv3,
                Vn1 = n1, Vn2 = n2, Vn3 = n3,
                MaterialName = tri.MaterialName ?? ""
            });
        }

        // 转换材质
        foreach (var (name, mat) in materials)
        {
            meshMaterials[name] = ConvertMaterial(mat);
        }

        return new MeshT(vertices, uvs, normals, faces, meshMaterials);
    }

    /// <summary>
    /// 转换：MeshT → RealScene3D.Triangle[]
    /// </summary>
    public static List<Triangle> FromMeshT(MeshT meshT)
    {
        var triangles = new List<Triangle>();

        foreach (var face in meshT.Faces)
        {
            var v1 = meshT.Vertices[face.V1];
            var v2 = meshT.Vertices[face.V2];
            var v3 = meshT.Vertices[face.V3];

            var uv1 = meshT.TextureVertices[face.Vt1];
            var uv2 = meshT.TextureVertices[face.Vt2];
            var uv3 = meshT.TextureVertices[face.Vt3];

            var n1 = meshT.Normals.Count > face.Vn1 ? meshT.Normals[face.Vn1] : null;
            var n2 = meshT.Normals.Count > face.Vn2 ? meshT.Normals[face.Vn2] : null;
            var n3 = meshT.Normals.Count > face.Vn3 ? meshT.Normals[face.Vn3] : null;

            triangles.Add(new Triangle
            {
                Vertex1 = new Vector3D(v1.X, v1.Y, v1.Z),
                Vertex2 = new Vector3D(v2.X, v2.Y, v2.Z),
                Vertex3 = new Vector3D(v3.X, v3.Y, v3.Z),
                UV1 = new Vector2D(uv1.X, uv1.Y),
                UV2 = new Vector2D(uv2.X, uv2.Y),
                UV3 = new Vector2D(uv3.X, uv3.Y),
                Normal1 = n1 != null ? new Vector3D(n1.X, n1.Y, n1.Z) : null,
                Normal2 = n2 != null ? new Vector3D(n2.X, n2.Y, n2.Z) : null,
                Normal3 = n3 != null ? new Vector3D(n3.X, n3.Y, n3.Z) : null,
                MaterialName = face.MaterialName
            });
        }

        return triangles;
    }

    /// <summary>
    /// 更新材质纹理路径（从 MeshT 同步回 Domain.Material）
    /// </summary>
    public static void UpdateMaterialTextures(
        Dictionary<string, Material> domainMaterials,
        Dictionary<string, RealScene3D.MeshTiling.Materials.Material> meshMaterials)
    {
        foreach (var (name, meshMat) in meshMaterials)
        {
            if (domainMaterials.TryGetValue(name, out var domainMat))
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

    private static int AddNormal(Vector3D? n, List<Vertex3> normals, Dictionary<Vector3D, int> map)
    {
        var normal = n ?? new Vector3D(0, 0, 1);

        if (map.TryGetValue(normal, out int index))
            return index;

        index = normals.Count;
        normals.Add(new Vertex3(normal.X, normal.Y, normal.Z));
        map[normal] = index;
        return index;
    }

    private static RealScene3D.MeshTiling.Materials.Material ConvertMaterial(Material source)
    {
        return new RealScene3D.MeshTiling.Materials.Material(
            name: source.Name,
            texture: source.DiffuseTexture?.FilePath,
            normalMap: source.NormalTexture?.FilePath,
            ambientColor: source.AmbientColor != null
                ? new RealScene3D.MeshTiling.Materials.RGB(source.AmbientColor.R, source.AmbientColor.G, source.AmbientColor.B)
                : null,
            diffuseColor: source.DiffuseColor != null
                ? new RealScene3D.MeshTiling.Materials.RGB(source.DiffuseColor.R, source.DiffuseColor.G, source.DiffuseColor.B)
                : null,
            specularColor: source.SpecularColor != null
                ? new RealScene3D.MeshTiling.Materials.RGB(source.SpecularColor.R, source.SpecularColor.G, source.SpecularColor.B)
                : null,
            specularExponent: source.Shininess,
            dissolve: source.Opacity);
    }

    #endregion
}
