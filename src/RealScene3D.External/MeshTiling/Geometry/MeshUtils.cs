using System.Collections.Concurrent;
using System.Diagnostics;
using RealScene3D.MeshTiling.Materials;

namespace RealScene3D.MeshTiling.Geometry;

/// <summary>
/// 网格工具类，提供加载OBJ文件和递归分割网格的功能
/// </summary>
public class MeshUtils
{
    /// <summary>
    /// 从OBJ文件加载网格
    /// </summary>
    /// <param name="fileName">OBJ文件名</param>
    /// <returns>加载的网格</returns>
    public static IMesh LoadMesh(string fileName)
    {
        return LoadMesh(fileName, out _);
    }

    /// <summary>
    /// 从OBJ文件加载网格，并输出依赖项
    /// </summary>
    /// <param name="fileName">OBJ文件名</param>
    /// <param name="dependencies">依赖项文件列表</param>
    /// <returns>加载的网格</returns>
    public static IMesh LoadMesh(string fileName, out string[] dependencies)
    {
        // 使用 StreamReader 读取 OBJ 文件
        using var reader = new StreamReader(fileName);

        // 初始化用于存储几何数据的列表
        var vertices = new List<Vertex3>(); // 顶点列表
        var textureVertices = new List<Vertex2>(); // 纹理坐标列表
        var facesT = new List<FaceT>(); // 带纹理的面列表
        var faces = new List<Face>(); // 无纹理的面列表
        var materials = new List<Material>(); // 材质列表
        var materialsDict = new Dictionary<string, int>(); // 材质名称到索引的映射
        var currentMaterial = string.Empty; // 当前材质
        var deps = new List<string>(); // 依赖项列表

        // 逐行读取文件
        while (true)
        {
            var line = reader.ReadLine();

            if (line == null) break; // 文件结束

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue; // 跳过空行和注释

            var segs = line.Split(' ', StringSplitOptions.RemoveEmptyEntries); // 按空格分割行

            switch (segs[0])
            {
                case "v" when segs.Length >= 4:
                    vertices.Add(new Vertex3(
                        double.Parse(segs[1], CultureInfo.InvariantCulture),
                        double.Parse(segs[2], CultureInfo.InvariantCulture),
                        double.Parse(segs[3], CultureInfo.InvariantCulture)));
                    break;
                case "vt" when segs.Length >= 3:

                    var vtx = new Vertex2(
                        double.Parse(segs[1], CultureInfo.InvariantCulture),
                        double.Parse(segs[2], CultureInfo.InvariantCulture));
                    
                    if (vtx.X < 0 || vtx.Y < 0)
                        throw new Exception("Invalid texture coordinates: " + vtx);
                    
                    textureVertices.Add(vtx);
                    break;
                case "vn" when segs.Length == 3:
                    // Skipping normals
                    break;
                case "usemtl" when segs.Length == 2:
                {
                    if (!materialsDict.ContainsKey(segs[1]))
                        throw new Exception($"Material {segs[1]} not found");

                    currentMaterial = segs[1];
                    break;
                }
                case "f" when segs.Length == 4: // 面定义
                {
                    var first = segs[1].Split('/');
                    var second = segs[2].Split('/');
                    var third = segs[3].Split('/');

                    // 检查是否包含纹理坐标
                    var hasTexture = first.Length > 1 && first[1].Length > 0 && second.Length > 1 &&
                                     second[1].Length > 0 && third.Length > 1 && third[1].Length > 0;

                    // 忽略法线
                    // var hasNormals = vertexIndices[0][2] != null && vertexIndices[1][2] != null && vertexIndices[2][2] != null;

                    // 解析顶点索引（OBJ格式索引从1开始，需要减1）
                    var v1 = int.Parse(first[0]);
                    var v2 = int.Parse(second[0]);
                    var v3 = int.Parse(third[0]);

                    if (hasTexture)
                    {
                        // 解析纹理坐标索引
                        var vt1 = int.Parse(first[1]);
                        var vt2 = int.Parse(second[1]);
                        var vt3 = int.Parse(third[1]);

                        var materialIndex = 0;
                        if (currentMaterial != string.Empty)
                        {
                            materialIndex = materialsDict[currentMaterial];
                        }

                        // 创建带纹理的面
                        var faceT = new FaceT(
                            v1 - 1,
                            v2 - 1,
                            v3 - 1,
                            vt1 - 1,
                            vt2 - 1,
                            vt3 - 1,
                            materialIndex);

                        facesT.Add(faceT);
                    }
                    else
                    {
                        // 创建无纹理的面
                        var face = new Face(
                            v1 - 1,
                            v2 - 1,
                            v3 - 1);

                        faces.Add(face);
                    }

                    break;
                }
                case "mtllib" when segs.Length == 2:
                {
                    var mtlFileName = segs[1];
                    var mtlFilePath = Path.Combine(Path.GetDirectoryName(fileName) ?? string.Empty, mtlFileName);
                    
                    var mats = Material.ReadMtl(mtlFilePath, out var mtlDeps);

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

        dependencies = deps.ToArray(); // 输出依赖项数组

        // 根据是否有纹理坐标创建不同的网格类型
        return textureVertices.Count != 0
            ? new MeshT(vertices, textureVertices, facesT, materials) // 带纹理的网格
            : new Mesh(vertices, faces); // 无纹理的网格
    }

    #region 分割器

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

    #endregion
}