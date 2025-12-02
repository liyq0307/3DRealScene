using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using RealScene3D.Domain.Materials;
using RealScene3D.Domain.Geometry;
using System.Text;

namespace RealScene3D.Domain.Utils;

/// <summary>
/// 网格工具类，提供加载OBJ文件和递归分割网格的功能
/// </summary>
public class MeshUtils
{
    /// <summary>
    /// 加载OBJ文件并构建索引化网格（IMesh）
    /// </summary>
    /// <param name="fileName">OBJ文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含网格、依赖文件列表、法线列表的元组和模型包围盒</returns>
    /// <exception cref="FileNotFoundException">文件未找到异常</exception>
    /// <exception cref="NotSupportedException">不支持的OBJ元素异常</exception>
    /// <exception cref="Exception">其他异常</exception>
    /// <remarks>
    /// 支持顶点(v)、法线(vn)、纹理坐标(vt)和面片(f)数据的解析
    /// 支持MTL材质文件加载和材质关联
    /// 直接构建索引化的MeshT网格，避免中间转换开销
    /// </remarks>
    public static async Task<(IMesh, string[], List<Vertex3>, Box3)> LoadMesh(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        // 初始化用于存储几何数据的列表
        var vertices = new List<Vertex3>();                 // 顶点列表
        var textureVertices = new List<Vertex2>();          // 纹理坐标列表
        var faces = new List<Face>();                       // 带纹理的面列表或者无纹理的面列表
        var materials = new List<Material>();               // 材质列表
        var materialsDict = new Dictionary<string, int>();  // 材质名称到索引的映射
        var currentMaterial = string.Empty;                 // 当前材质
        var deps = new List<string>();                      // 依赖项列表
        var normals = new List<Vertex3>();                  // 法线列表

        // 初始化包围盒为极值
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

        // 逐行读取文件
        string? line;
        int lineNumber = 0;
        int vertexCount = 0;
        int normalCount = 0;
        int texCoordCount = 0;

        using (var reader = new StreamReader(fileName, Encoding.UTF8))
        {
            while ((line = await reader.ReadLineAsync()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lineNumber++;

                // 跳过空行和注释
                line = line.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                try
                {
                    // 按空格分割行
                    var segs = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    switch (segs[0])
                    {
                        case "v" when segs.Length >= 4: // 顶点定义
                            {
                                var vertex = ParseVertex(segs);
                                vertices.Add(vertex);
                                vertexCount++;

                                // 更新包围盒
                                minX = Math.Min(minX, vertex.X);
                                minY = Math.Min(minY, vertex.Y);
                                minZ = Math.Min(minZ, vertex.Z);
                                maxX = Math.Max(maxX, vertex.X);
                                maxY = Math.Max(maxY, vertex.Y);
                                maxZ = Math.Max(maxZ, vertex.Z);

                                break;
                            }
                        case "vt" when segs.Length >= 3: // 纹理坐标定义
                            {
                                var vtx = ParseTexCoord(segs);

                                if (vtx.X < 0 || vtx.Y < 0)
                                    throw new Exception("Invalid texture coordinates: " + vtx);

                                texCoordCount++;
                                textureVertices.Add(vtx);

                                break;
                            }
                        case "vn" when segs.Length >= 4: // 法线定义
                            {
                                normals.Add(ParseNormal(segs));
                                normalCount++;
                                break;
                            }
                        case "usemtl" when segs.Length == 2: // 使用材质
                            {
                                if (!materialsDict.ContainsKey(segs[1]))
                                    throw new Exception($"Material {segs[1]} not found");

                                currentMaterial = segs[1];
                                break;
                            }
                        case "f" when segs.Length >= 4: // 面定义(三角面片和多边形)
                            {
                                var face = ParseFace(
                                    segs,
                                    vertexCount,
                                    texCoordCount,
                                    normalCount,
                                    currentMaterial != string.Empty ? materialsDict[currentMaterial] : 0);

                                faces.AddRange(face);

                                break;
                            }
                        case "mtllib" when segs.Length == 2: // 材质库文件
                            {
                                var mtlFileName = segs[1];
                                var mtlFilePath = Path.Combine(Path.GetDirectoryName(fileName) ?? string.Empty, mtlFileName);

                                var (mats, mtlDeps) = await Material.ReadMtlAsync(mtlFilePath);

                                deps.AddRange(mtlDeps);
                                deps.Add(mtlFilePath);

                                foreach (var mat in mats)
                                {
                                    materials.Add(mat);
                                    materialsDict.Add(mat.Name, materials.Count - 1);
                                }

                                break;
                            }
                        case "l" or "cstype" or "deg" or "bmat" or "step" or "curv" or "curv2" or "surf" or "parm" or "trim"
                            or "end" or "hole" or "scrv" or "sp" or "con": // 不支持的OBJ元素

                            throw new NotSupportedException("不支持的元素: '" + line + "'");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"解析OBJ文件时第{lineNumber}出错: {ex.Message}, 行内容: {line}");
                }
            }
        }

        // 构建包围盒
        var boundingBox = new Box3(
            minX != double.MaxValue ? minX : 0,
            minY != double.MaxValue ? minY : 0,
            minZ != double.MaxValue ? minZ : 0,
            maxX != double.MinValue ? maxX : 0,
            maxY != double.MinValue ? maxY : 0,
            maxZ != double.MinValue ? maxZ : 0);

        // 根据是否有纹理坐标创建不同的网格类型
        return textureVertices.Count != 0
            ? (new MeshT(vertices, textureVertices, faces, materials), deps.ToArray(), normals, boundingBox) // 带纹理的网格
            : (new Mesh(vertices, faces), deps.ToArray(), normals, boundingBox); // 无纹理的网格
    }

    /// <summary>
    /// 解析顶点坐标行
    /// 格式: v x y z [w]
    /// </summary>
    private static Vertex3 ParseVertex(string[] parts)
    {
        return new Vertex3(
            double.Parse(parts[1], CultureInfo.InvariantCulture),
            double.Parse(parts[2], CultureInfo.InvariantCulture),
            double.Parse(parts[3], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 解析法线向量行
    /// 格式: vn x y z
    /// </summary>
    private static Vertex3 ParseNormal(string[] parts)
    {
        return new Vertex3(
            double.Parse(parts[1], CultureInfo.InvariantCulture),
            double.Parse(parts[2], CultureInfo.InvariantCulture),
            double.Parse(parts[3], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 解析纹理坐标行
    /// 格式: vt u v [w]
    /// </summary>
    private static Vertex2 ParseTexCoord(string[] parts)
    {
        return new Vertex2(
            double.Parse(parts[1], CultureInfo.InvariantCulture),
            parts.Length > 2 ? double.Parse(parts[2], CultureInfo.InvariantCulture) : 0.0);
    }

    /// <summary>
    /// 解析面片行并转换为三角形面
    /// 格式: f v1[/vt1][/vn1] v2[/vt2][/vn2] v3[/vt3][/vn3] [v4[/vt4][/vn4] ...]
    /// 支持多种格式:
    /// - f v1 v2 v3 (仅顶点)
    /// - f v1/vt1 v2/vt2 v3/vt3 (顶点和纹理)
    /// - f v1//vn1 v2//vn2 v3//vn3 (顶点和法线)
    /// - f v1/vt1/vn1 v2/vt2/vn2 v3/vt3/vn3 (完整)
    /// 支持三角形和多边形(自动三角化)
    /// </summary>
    private static List<Face> ParseFace(
        string[] parts,
        int vertexCount,
        int texCoordCount,
        int normalCount,
        int materialIndex)
    {
        var faces = new List<Face>();

        var first = parts[1].Split('/');
        var second = parts[2].Split('/');
        var third = parts[3].Split('/');

        // 检查是否包含纹理坐标
        var hasTexture = first.Length > 1 && first[1].Length > 0 && second.Length > 1 &&
                        second[1].Length > 0 && third.Length > 1 && third[1].Length > 0;

        // 解析顶点索引
        var faceVertices = new List<(int v, int vt)>();
        for (int i = 1; i < parts.Length; i++)
        {
            var (vIdx, vtIdx, _) = ParseFaceVertex(parts[i], vertexCount, texCoordCount, normalCount);
            faceVertices.Add((vIdx, vtIdx));
        }

        // 三角化:扇形三角化算法，对于n边形,生成(n-2)个三角形
        if (faceVertices.Count >= 3)
        {
            for (int i = 1; i < faceVertices.Count - 1; i++)
            {
                var (v0, vt0) = faceVertices[0];
                var (v1, vt1) = faceVertices[i];
                var (v2, vt2) = faceVertices[i + 1];

                if (hasTexture)
                    faces.Add(new Face(v0, v1, v2, vt0, vt1, vt2, materialIndex));
                else
                    faces.Add(new Face(v0, v1, v2));
            }
        }

        return faces;
    }

    /// <summary>
    /// 解析单个面片顶点
    /// 格式: v[/vt][/vn]
    /// 返回: (顶点索引, 纹理坐标索引, 法线索引)
    /// OBJ 索引从1开始，转换为从0开始
    /// </summary>
    private static (int vIdx, int vtIdx, int vnIdx) ParseFaceVertex(
        string vertexStr,
        int vertexCount,
        int texCoordCount,
        int normalCount)
    {
        var parts = vertexStr.Split('/');
        int vIdx = 0, vtIdx = 0, vnIdx = 0;

        // 解析顶点索引
        if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
        {
            vIdx = int.Parse(parts[0]);
            vIdx = vIdx < 0 ? vertexCount + vIdx : vIdx - 1; // 转换为从0开始
        }

        // 解析纹理坐标索引
        if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) && texCoordCount > 0)
        {
            vtIdx = int.Parse(parts[1]);
            vtIdx = vtIdx < 0 ? texCoordCount + vtIdx : vtIdx - 1;
        }

        // 解析法线索引
        if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]) && normalCount > 0)
        {
            vnIdx = int.Parse(parts[2]);
            vnIdx = vnIdx < 0 ? normalCount + vnIdx : vnIdx - 1;
        }

        return (vIdx, vtIdx, vnIdx);
    }

    // 顶点工具类的静态实例，用于X、Y、Z轴上的分割操作
    private static readonly IVertexUtils yutils3 = new VertexUtilsY(); // Y轴分割工具
    private static readonly IVertexUtils xutils3 = new VertexUtilsX(); // X轴分割工具
    private static readonly IVertexUtils zutils3 = new VertexUtilsZ(); // Z轴分割工具

    /// <summary>
    /// 递归分割网格为XY平面内的四个象限
    /// </summary>
    /// <param name="mesh">要分割的网格</param>
    /// <param name="depth">递归深度</param>
    /// <param name="bounds">边界框</param>
    /// <param name="meshes">分割后的网格集合</param>
    /// <returns>分割操作的计数</returns>
    public static async Task<int> RecurseSplitXY(IMesh mesh, int depth, Box3 bounds, ConcurrentBag<IMesh> meshes)
    {
        Debug.WriteLine($"RecurseSplitXY('{mesh.Name}' {mesh.VertexCount}, {depth}, {bounds})");

        if (depth == 0) // 递归结束条件
        {
            if (mesh.FacesCount > 0)
                meshes.Add(mesh);
            return 0;
        }

        var center = bounds.Center; // 边界框中心点

        // 先按X轴分割，再按Y轴分割
        var count = mesh.Split(xutils3, center.X, out var left, out var right);
        count += left.Split(yutils3, center.Y, out var topleft, out var topright); // 左半部分分割为上下
        count += right.Split(yutils3, center.Y, out var bottomleft, out var bottomright); // 右半部分分割为上下

        // 计算子边界框
        var xbounds = bounds.Split(Axis.X); // X轴分割
        var ybounds1 = xbounds[0].Split(Axis.Y); // 左边界框的Y轴分割
        var ybounds2 = xbounds[1].Split(Axis.Y); // 右边界框的Y轴分割

        var nextDepth = depth - 1; // 下一级深度

        var tasks = new List<Task<int>>(); // 异步任务列表

        // 为每个有面的子网格创建递归任务
        if (topleft.FacesCount > 0) tasks.Add(RecurseSplitXY(topleft, nextDepth, ybounds1[0], meshes));
        if (bottomleft.FacesCount > 0) tasks.Add(RecurseSplitXY(bottomleft, nextDepth, ybounds2[0], meshes));
        if (topright.FacesCount > 0) tasks.Add(RecurseSplitXY(topright, nextDepth, ybounds1[1], meshes));
        if (bottomright.FacesCount > 0) tasks.Add(RecurseSplitXY(bottomright, nextDepth, ybounds2[1], meshes));

        await Task.WhenAll(tasks); // 等待所有子任务完成

        return count + tasks.Sum(t => t.Result); // 返回总分割计数
    }

    /// <summary>
    /// 递归分割网格为XY平面内的四个象限，使用自定义分割点函数
    /// </summary>
    /// <param name="mesh">要分割的网格</param>
    /// <param name="depth">递归深度</param>
    /// <param name="getSplitPoint">获取分割点的函数</param>
    /// <param name="meshes">分割后的网格集合</param>
    /// <returns>分割操作的计数</returns>
    public static async Task<int> RecurseSplitXY(IMesh mesh, int depth, Func<IMesh, Vertex3> getSplitPoint,
        ConcurrentBag<IMesh> meshes)
    {
        var center = getSplitPoint(mesh);

        var count = mesh.Split(xutils3, center.X, out var left, out var right);
        count += left.Split(yutils3, center.Y, out var topleft, out var bottomleft);
        count += right.Split(yutils3, center.Y, out var topright, out var bottomright);

        var nextDepth = depth - 1;

        if (nextDepth == 0)
        {
            if (topleft.FacesCount > 0) meshes.Add(topleft);
            if (bottomleft.FacesCount > 0) meshes.Add(bottomleft);
            if (topright.FacesCount > 0) meshes.Add(topright);
            if (bottomright.FacesCount > 0) meshes.Add(bottomright);

            return count;
        }

        var tasks = new List<Task<int>>();

        if (topleft.FacesCount > 0) tasks.Add(RecurseSplitXY(topleft, nextDepth, getSplitPoint, meshes));
        if (bottomleft.FacesCount > 0) tasks.Add(RecurseSplitXY(bottomleft, nextDepth, getSplitPoint, meshes));
        if (topright.FacesCount > 0) tasks.Add(RecurseSplitXY(topright, nextDepth, getSplitPoint, meshes));
        if (bottomright.FacesCount > 0) tasks.Add(RecurseSplitXY(bottomright, nextDepth, getSplitPoint, meshes));

        await Task.WhenAll(tasks);

        return count + tasks.Sum(t => t.Result);
    }

    /// <summary>
    /// 递归分割网格为XYZ空间内的八个象限，使用自定义分割点函数
    /// </summary>
    /// <param name="mesh">要分割的网格</param>
    /// <param name="depth">递归深度</param>
    /// <param name="getSplitPoint">获取分割点的函数</param>
    /// <param name="meshes">分割后的网格集合</param>
    /// <returns>分割操作的计数</returns>
    public static async Task<int> RecurseSplitXYZ(IMesh mesh, int depth, Func<IMesh, Vertex3> getSplitPoint,
        ConcurrentBag<IMesh> meshes)
    {
        var center = getSplitPoint(mesh); // 使用自定义函数获取分割点

        // 先按X轴分割，再按Y轴分割，最后按Z轴分割
        var count = mesh.Split(xutils3, center.X, out var left, out var right);
        count += left.Split(yutils3, center.Y, out var topleft, out var bottomleft); // 左半部分分割为上下
        count += right.Split(yutils3, center.Y, out var topright, out var bottomright); // 右半部分分割为上下

        // 每个XY象限再按Z轴分割为前后
        count += topleft.Split(zutils3, center.Z, out var topleftnear, out var topleftfar);
        count += bottomleft.Split(zutils3, center.Z, out var bottomleftnear, out var bottomleftfar);
        count += topright.Split(zutils3, center.Z, out var toprightnear, out var toprightfar);
        count += bottomright.Split(zutils3, center.Z, out var bottomrightnear, out var bottomrightfar);

        var nextDepth = depth - 1; // 下一级深度

        if (nextDepth == 0) // 递归结束
        {
            // 添加所有有面的八个子网格
            if (topleftnear.FacesCount > 0) meshes.Add(topleftnear);
            if (topleftfar.FacesCount > 0) meshes.Add(topleftfar);
            if (bottomleftnear.FacesCount > 0) meshes.Add(bottomleftnear);
            if (bottomleftfar.FacesCount > 0) meshes.Add(bottomleftfar);

            if (toprightnear.FacesCount > 0) meshes.Add(toprightnear);
            if (toprightfar.FacesCount > 0) meshes.Add(toprightfar);
            if (bottomrightnear.FacesCount > 0) meshes.Add(bottomrightnear);
            if (bottomrightfar.FacesCount > 0) meshes.Add(bottomrightfar);

            return count;
        }

        var tasks = new List<Task<int>>(); // 异步任务列表

        // 为每个有面的子网格创建递归任务
        if (topleftnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(topleftnear, nextDepth, getSplitPoint, meshes));
        if (topleftfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(topleftfar, nextDepth, getSplitPoint, meshes));
        if (bottomleftnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomleftnear, nextDepth, getSplitPoint, meshes));
        if (bottomleftfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomleftfar, nextDepth, getSplitPoint, meshes));

        if (toprightnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(toprightnear, nextDepth, getSplitPoint, meshes));
        if (toprightfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(toprightfar, nextDepth, getSplitPoint, meshes));
        if (bottomrightnear.FacesCount > 0)
            tasks.Add(RecurseSplitXYZ(bottomrightnear, nextDepth, getSplitPoint, meshes));
        if (bottomrightfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomrightfar, nextDepth, getSplitPoint, meshes));

        await Task.WhenAll(tasks); // 等待所有子任务完成

        return count + tasks.Sum(t => t.Result); // 返回总分割计数
    }

    /// <summary>
    /// 递归分割网格为XYZ空间内的八个象限
    /// </summary>
    /// <param name="mesh">要分割的网格</param>
    /// <param name="depth">递归深度</param>
    /// <param name="bounds">边界框</param>
    /// <param name="meshes">分割后的网格集合</param>
    /// <returns>分割操作的计数</returns>
    public static async Task<int> RecurseSplitXYZ(IMesh mesh, int depth, Box3 bounds, ConcurrentBag<IMesh> meshes)
    {
        Debug.WriteLine($"RecurseSplitXYZ('{mesh.Name}' {mesh.VertexCount}, {depth}, {bounds})");

        if (depth == 0) // 递归结束条件
        {
            if (mesh.FacesCount > 0)
                meshes.Add(mesh);
            return 0;
        }

        var center = bounds.Center; // 边界框中心点

        // 先按X轴分割，再按Y轴分割，最后按Z轴分割
        var count = mesh.Split(xutils3, center.X, out var left, out var right);
        count += left.Split(yutils3, center.Y, out var topleft, out var bottomleft); // 左半部分分割为上下
        count += right.Split(yutils3, center.Y, out var topright, out var bottomright); // 右半部分分割为上下

        // 每个XY象限再按Z轴分割为前后
        count += topleft.Split(zutils3, center.Z, out var topleftnear, out var topleftfar);
        count += bottomleft.Split(zutils3, center.Z, out var bottomleftnear, out var bottomleftfar);
        count += topright.Split(zutils3, center.Z, out var toprightnear, out var toprightfar);
        count += bottomright.Split(zutils3, center.Z, out var bottomrightnear, out var bottomrightfar);

        // 计算子边界框
        var xbounds = bounds.Split(Axis.X); // X轴分割
        var ybounds1 = xbounds[0].Split(Axis.Y); // 左边界框的Y轴分割
        var ybounds2 = xbounds[1].Split(Axis.Y); // 右边界框的Y轴分割

        var zbounds1 = ybounds1[0].Split(Axis.Z); // 左上边界框的Z轴分割
        var zbounds2 = ybounds1[1].Split(Axis.Z); // 左下边界框的Z轴分割
        var zbounds3 = ybounds2[0].Split(Axis.Z); // 右上边界框的Z轴分割
        var zbounds4 = ybounds2[1].Split(Axis.Z); // 右下边界框的Z轴分割

        var nextDepth = depth - 1; // 下一级深度

        var tasks = new List<Task<int>>(); // 异步任务列表

        // 为每个有面的子网格创建递归任务
        if (topleftnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(topleftnear, nextDepth, zbounds1[0], meshes));
        if (topleftfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(topleftfar, nextDepth, zbounds1[1], meshes));
        if (bottomleftnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomleftnear, nextDepth, zbounds2[0], meshes));
        if (bottomleftfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomleftfar, nextDepth, zbounds2[1], meshes));
        if (toprightnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(toprightnear, nextDepth, zbounds3[0], meshes));
        if (toprightfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(toprightfar, nextDepth, zbounds3[1], meshes));
        if (bottomrightnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomrightnear, nextDepth, zbounds4[0], meshes));
        if (bottomrightfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomrightfar, nextDepth, zbounds4[1], meshes));

        await Task.WhenAll(tasks); // 等待所有子任务完成

        return count + tasks.Sum(t => t.Result); // 返回总分割计数
    }
}