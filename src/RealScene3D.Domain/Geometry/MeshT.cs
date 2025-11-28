using System.Globalization;
using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Path = System.IO.Path;
using RealScene3D.Domain.Algorithms;
using RealScene3D.Domain.Materials;
using RealScene3D.Domain.Utils;

namespace RealScene3D.Domain.Geometry;

/// <summary>
/// 纹理网格类，实现带有纹理坐标的网格操作，包括分割和纹理处理等功能
/// </summary>
public class MeshT : IMesh
{
    /// <summary>
    /// 顶点列表
    /// </summary>
    private List<Vertex3> _vertices;

    /// <summary>
    /// 纹理顶点列表
    /// </summary>
    private List<Vertex2> _textureVertices;

    /// <summary>
    /// 面列表
    /// </summary>
    private readonly List<FaceT> _faces;

    /// <summary>
    /// 材质列表
    /// </summary>
    private List<Material> _materials;

    /// <summary>
    /// 顶点列表
    /// </summary>
    public IReadOnlyList<Vertex3> Vertices => _vertices;

    /// <summary>
    /// 纹理顶点列表
    /// </summary>
    public IReadOnlyList<Vertex2> TextureVertices => _textureVertices;

    /// <summary>
    /// 面列表
    /// </summary>
    public IReadOnlyList<FaceT> Faces => _faces;

    /// <summary>
    /// 材质列表
    /// </summary>
    public IReadOnlyList<Material> Materials => _materials;

    /// <summary>
    /// 默认网格名称
    /// </summary>
    public const string DefaultName = "Mesh";

    /// <summary>
    /// 网格名称
    /// </summary>
    public string Name { get; set; } = DefaultName;

    /// <summary>
    /// 纹理处理策略
    /// </summary>
    public TexturesStrategy TexturesStrategy { get; set; }

    public MeshT(IEnumerable<Vertex3> vertices, IEnumerable<Vertex2> textureVertices,
        IEnumerable<FaceT> faces, IEnumerable<Material> materials)
    {
        _vertices = [.. vertices];
        _textureVertices = [.. textureVertices];
        _faces = [.. faces];
        _materials = [.. materials];
    }

    /// <summary>
    /// 根据指定的维度和分割值，将网格分割为左右两个子网格
    /// </summary>
    /// <param name="utils">顶点工具，用于获取维度和切割边</param>
    /// <param name="q">分割值</param>
    /// <param name="left">输出左侧子网格</param>
    /// <param name="right">输出右侧子网格</param>
    /// <returns>分割产生的新面数量</returns>
    public int Split(IVertexUtils utils, double q, out IMesh left, out IMesh right)
    {
        var leftVertices = new Dictionary<Vertex3, int>(_vertices.Count);
        var rightVertices = new Dictionary<Vertex3, int>(_vertices.Count);

        var leftFaces = new List<FaceT>(_faces.Count);
        var rightFaces = new List<FaceT>(_faces.Count);

        var leftTextureVertices = new Dictionary<Vertex2, int>(_textureVertices.Count);
        var rightTextureVertices = new Dictionary<Vertex2, int>(_textureVertices.Count);

        var count = 0;

        for (var index = 0; index < _faces.Count; index++)
        {
            var face = _faces[index];

            var vA = _vertices[face.IndexA];
            var vB = _vertices[face.IndexB];
            var vC = _vertices[face.IndexC];

            var vtA = _textureVertices[face.TextureIndexA];
            var vtB = _textureVertices[face.TextureIndexB];
            var vtC = _textureVertices[face.TextureIndexC];

            var aSide = utils.GetDimension(vA) < q;
            var bSide = utils.GetDimension(vB) < q;
            var cSide = utils.GetDimension(vC) < q;

            if (aSide)
            {
                if (bSide)
                {
                    if (cSide)
                    {
                        // 全部在左侧
                        var indexALeft = leftVertices.AddIndex(vA);
                        var indexBLeft = leftVertices.AddIndex(vB);
                        var indexCLeft = leftVertices.AddIndex(vC);

                        var indexATextureLeft = leftTextureVertices!.AddIndex(vtA);
                        var indexBTextureLeft = leftTextureVertices!.AddIndex(vtB);
                        var indexCTextureLeft = leftTextureVertices!.AddIndex(vtC);

                        leftFaces.Add(new FaceT(indexALeft, indexBLeft, indexCLeft,
                            indexATextureLeft, indexBTextureLeft, indexCTextureLeft,
                            face.MaterialIndex));
                    }
                    else
                    {
                        IntersectRight2DWithTexture(utils, q, face.IndexC, face.IndexA, face.IndexB,
                            leftVertices,
                            rightVertices,
                            face.TextureIndexC, face.TextureIndexA, face.TextureIndexB,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex, leftFaces, rightFaces
                        );
                        count++;
                    }
                }
                else
                {
                    if (cSide)
                    {
                        IntersectRight2DWithTexture(utils, q, face.IndexB, face.IndexC, face.IndexA,
                            leftVertices,
                            rightVertices,
                            face.TextureIndexB, face.TextureIndexC, face.TextureIndexA,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex, leftFaces,
                            rightFaces);
                        count++;
                    }
                    else
                    {
                        IntersectLeft2DWithTexture(utils, q, face.IndexA, face.IndexB, face.IndexC,
                            leftVertices,
                            rightVertices,
                            face.TextureIndexA, face.TextureIndexB, face.TextureIndexC,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex, leftFaces,
                            rightFaces);
                        count++;
                    }
                }
            }
            else
            {
                if (bSide)
                {
                    if (cSide)
                    {
                        IntersectRight2DWithTexture(utils, q, face.IndexA, face.IndexB, face.IndexC,
                            leftVertices,
                            rightVertices,
                            face.TextureIndexA, face.TextureIndexB, face.TextureIndexC,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex, leftFaces,
                            rightFaces);
                        count++;
                    }
                    else
                    {
                        IntersectLeft2DWithTexture(utils, q, face.IndexB, face.IndexC, face.IndexA,
                            leftVertices,
                            rightVertices,
                            face.TextureIndexB, face.TextureIndexC, face.TextureIndexA,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex, leftFaces,
                            rightFaces);
                        count++;
                    }
                }
                else
                {
                    if (cSide)
                    {
                        IntersectLeft2DWithTexture(utils, q, face.IndexC, face.IndexA, face.IndexB,
                            leftVertices,
                            rightVertices,
                            face.TextureIndexC, face.TextureIndexA, face.TextureIndexB,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex, leftFaces,
                            rightFaces);
                        count++;
                    }
                    else
                    {
                        // 全部在右侧
                        var indexARight = rightVertices.AddIndex(vA);
                        var indexBRight = rightVertices.AddIndex(vB);
                        var indexCRight = rightVertices.AddIndex(vC);

                        var indexATextureRight = rightTextureVertices!.AddIndex(vtA);
                        var indexBTextureRight = rightTextureVertices!.AddIndex(vtB);
                        var indexCTextureRight = rightTextureVertices!.AddIndex(vtC);

                        rightFaces.Add(new FaceT(indexARight, indexBRight, indexCRight,
                            indexATextureRight, indexBTextureRight, indexCTextureRight,
                            face.MaterialIndex));
                    }
                }
            }
        }

        var orderedLeftVertices = leftVertices.OrderBy(x => x.Value).Select(x => x.Key);
        var orderedRightVertices = rightVertices.OrderBy(x => x.Value).Select(x => x.Key);
        var rightMaterials = _materials.Select(mat => (Material)mat.Clone());

        var orderedLeftTextureVertices = leftTextureVertices.OrderBy(x => x.Value).Select(x => x.Key);
        var orderedRightTextureVertices = rightTextureVertices.OrderBy(x => x.Value).Select(x => x.Key);
        var leftMaterials = _materials.Select(mat => (Material)mat.Clone());

        left = new MeshT(orderedLeftVertices, orderedLeftTextureVertices, leftFaces, leftMaterials)
        {
            Name = $"{Name}-{utils.Axis}L"
        };
        right = new MeshT(orderedRightVertices, orderedRightTextureVertices, rightFaces, rightMaterials)
        {
            Name = $"{Name}-{utils.Axis}R"
        };

        return count;
    }

    /// <summary>
    /// 处理左侧顶点与纹理的相交情况
    /// </summary>
    /// <param name="utils">顶点工具</param>
    /// <param name="q">分割值</param>
    /// <param name="indexVL">左侧顶点索引</param>
    /// <param name="indexVR1">右侧顶点1索引</param>
    /// <param name="indexVR2">右侧顶点2索引</param>
    /// <param name="leftVertices">左侧顶点字典</param>
    /// <param name="rightVertices">右侧顶点字典</param>
    /// <param name="indexTextureVL">左侧纹理顶点索引</param>
    /// <param name="indexTextureVR1">右侧纹理顶点1索引</param>
    /// <param name="indexTextureVR2">右侧纹理顶点2索引</param>
    /// <param name="leftTextureVertices">左侧纹理顶点字典</param>
    /// <param name="rightTextureVertices">右侧纹理顶点字典</param>
    /// <param name="materialIndex">材质索引</param>
    /// <param name="leftFaces">左侧面集合</param>
    /// <param name="rightFaces">右侧面集合</param>
    /// <returns></returns>
    private void IntersectLeft2DWithTexture(IVertexUtils utils, double q, int indexVL,
        int indexVR1, int indexVR2,
        IDictionary<Vertex3, int> leftVertices, IDictionary<Vertex3, int> rightVertices,
        int indexTextureVL, int indexTextureVR1, int indexTextureVR2,
        IDictionary<Vertex2, int> leftTextureVertices, IDictionary<Vertex2, int> rightTextureVertices,
        int materialIndex, ICollection<FaceT> leftFaces, ICollection<FaceT> rightFaces)
    {
        var vL = _vertices[indexVL];
        var vR1 = _vertices[indexVR1];
        var vR2 = _vertices[indexVR2];

        var tVL = _textureVertices[indexTextureVL];
        var tVR1 = _textureVertices[indexTextureVR1];
        var tVR2 = _textureVertices[indexTextureVR2];

        var indexVLLeft = leftVertices.AddIndex(vL);
        var indexTextureVLLeft = leftTextureVertices.AddIndex(tVL);

        if (Math.Abs(utils.GetDimension(vR1) - q) < Common.Epsilon &&
            Math.Abs(utils.GetDimension(vR2) - q) < Common.Epsilon)
        {
            // Right Vertices are on the line

            var indexVR1Left = leftVertices.AddIndex(vR1);
            var indexVR2Left = leftVertices.AddIndex(vR2);

            var indexTextureVR1Left = leftTextureVertices.AddIndex(tVR1);
            var indexTextureVR2Left = leftTextureVertices.AddIndex(tVR2);

            leftFaces.Add(new FaceT(indexVLLeft, indexVR1Left, indexVR2Left,
                indexTextureVLLeft, indexTextureVR1Left, indexTextureVR2Left, materialIndex));

            return;
        }

        var indexVR1Right = rightVertices.AddIndex(vR1);
        var indexVR2Right = rightVertices.AddIndex(vR2);

        // a在左侧，b和c在右侧

        // 第一次相交
        var t1 = utils.CutEdge(vL, vR1, q);
        var indexT1Left = leftVertices.AddIndex(t1);
        var indexT1Right = rightVertices.AddIndex(t1);

        // 第二次相交
        var t2 = utils.CutEdge(vL, vR2, q);
        var indexT2Left = leftVertices.AddIndex(t2);
        var indexT2Right = rightVertices.AddIndex(t2);

        // 分割纹理
        var indexTextureVR1Right = rightTextureVertices.AddIndex(tVR1);
        var indexTextureVR2Right = rightTextureVertices.AddIndex(tVR2);

        var perc1 = Common.GetIntersectionPerc(vL, vR1, t1);

        // 第一次纹理相交
        var t1t = tVL.CutEdgePerc(tVR1, perc1);
        var indexTextureT1Left = leftTextureVertices.AddIndex(t1t);
        var indexTextureT1Right = rightTextureVertices.AddIndex(t1t);

        var perc2 = Common.GetIntersectionPerc(vL, vR2, t2);

        // 第二次纹理相交
        var t2t = tVL.CutEdgePerc(tVR2, perc2);
        var indexTextureT2Left = leftTextureVertices.AddIndex(t2t);
        var indexTextureT2Right = rightTextureVertices.AddIndex(t2t);

        var lface = new FaceT(indexVLLeft, indexT1Left, indexT2Left,
            indexTextureVLLeft, indexTextureT1Left, indexTextureT2Left, materialIndex);
        leftFaces.Add(lface);

        var rface1 = new FaceT(indexT1Right, indexVR1Right, indexVR2Right,
            indexTextureT1Right, indexTextureVR1Right, indexTextureVR2Right, materialIndex);
        rightFaces.Add(rface1);

        var rface2 = new FaceT(indexT1Right, indexVR2Right, indexT2Right,
            indexTextureT1Right, indexTextureVR2Right, indexTextureT2Right, materialIndex);
        rightFaces.Add(rface2);
    }

    /// <summary>
    /// 处理右侧顶点与纹理的相交情况
    /// </summary>
    private void IntersectRight2DWithTexture(IVertexUtils utils, double q, int indexVR,
        int indexVL1, int indexVL2,
        IDictionary<Vertex3, int> leftVertices, IDictionary<Vertex3, int> rightVertices,
        int indexTextureVR, int indexTextureVL1, int indexTextureVL2,
        IDictionary<Vertex2, int> leftTextureVertices, IDictionary<Vertex2, int> rightTextureVertices,
        int materialIndex, ICollection<FaceT> leftFaces, ICollection<FaceT> rightFaces)
    {
        var vR = _vertices[indexVR];
        var vL1 = _vertices[indexVL1];
        var vL2 = _vertices[indexVL2];

        var tVR = _textureVertices[indexTextureVR];
        var tVL1 = _textureVertices[indexTextureVL1];
        var tVL2 = _textureVertices[indexTextureVL2];

        var indexVRRight = rightVertices.AddIndex(vR);
        var indexTextureVRRight = rightTextureVertices.AddIndex(tVR);

        if (Math.Abs(utils.GetDimension(vL1) - q) < Common.Epsilon &&
            Math.Abs(utils.GetDimension(vL2) - q) < Common.Epsilon)
        {
            // 左侧顶点在分割线上
            var indexVL1Right = rightVertices.AddIndex(vL1);
            var indexVL2Right = rightVertices.AddIndex(vL2);

            var indexTextureVL1Right = rightTextureVertices.AddIndex(tVL1);
            var indexTextureVL2Right = rightTextureVertices.AddIndex(tVL2);

            rightFaces.Add(new FaceT(indexVRRight, indexVL1Right, indexVL2Right,
                indexTextureVRRight, indexTextureVL1Right, indexTextureVL2Right, materialIndex));

            return;
        }

        var indexVL1Left = leftVertices.AddIndex(vL1);
        var indexVL2Left = leftVertices.AddIndex(vL2);

        // a在右侧，b和c在左侧

        // 第一次相交
        var t1 = utils.CutEdge(vR, vL1, q);
        var indexT1Left = leftVertices.AddIndex(t1);
        var indexT1Right = rightVertices.AddIndex(t1);

        // 第二次相交
        var t2 = utils.CutEdge(vR, vL2, q);
        var indexT2Left = leftVertices.AddIndex(t2);
        var indexT2Right = rightVertices.AddIndex(t2);

        // 分割纹理
        var indexTextureVL1Left = leftTextureVertices.AddIndex(tVL1);
        var indexTextureVL2Left = leftTextureVertices.AddIndex(tVL2);

        var perc1 = Common.GetIntersectionPerc(vR, vL1, t1);

        // 第一次纹理相交
        var t1t = tVR.CutEdgePerc(tVL1, perc1);
        var indexTextureT1Left = leftTextureVertices.AddIndex(t1t);
        var indexTextureT1Right = rightTextureVertices.AddIndex(t1t);

        var perc2 = Common.GetIntersectionPerc(vR, vL2, t2);

        // 第二次纹理相交
        var t2t = tVR.CutEdgePerc(tVL2, perc2);
        var indexTextureT2Left = leftTextureVertices.AddIndex(t2t);
        var indexTextureT2Right = rightTextureVertices.AddIndex(t2t);

        var rface = new FaceT(indexVRRight, indexT1Right, indexT2Right,
            indexTextureVRRight, indexTextureT1Right, indexTextureT2Right, materialIndex);
        rightFaces.Add(rface);

        var lface1 = new FaceT(indexT2Left, indexVL1Left, indexVL2Left,
            indexTextureT2Left, indexTextureVL1Left, indexTextureVL2Left, materialIndex);
        leftFaces.Add(lface1);

        var lface2 = new FaceT(indexT2Left, indexT1Left, indexVL1Left,
            indexTextureT2Left, indexTextureT1Left, indexTextureVL1Left, materialIndex);
        leftFaces.Add(lface2);
    }

    /// <summary>
    /// 修剪纹理，优化纹理布局以减少浪费
    /// </summary>
    /// <param name="targetFolder">目标文件夹，用于保存新的纹理文件</param>
    private void TrimTextures(string targetFolder)
    {
        Debug.WriteLine("修剪 " + Name + " 的纹理");

        var tasks = new List<Task>();

        LoadTexturesCache();

        var facesByMaterial = GetFacesByMaterial();

        var newTextureVertices = new Dictionary<Vertex2, int>(_textureVertices.Count);

        var sw = new Stopwatch();

        for (var m = 0; m < facesByMaterial.Count; m++)
        {
            var material = _materials[m];
            var facesIndexes = facesByMaterial[m];
            Debug.WriteLine($"处理材质 {m} -> {material.Name}");

            if (facesIndexes.Count == 0)
            {
                Debug.WriteLine("此材质没有面");
                continue;
            }

            sw.Restart();

            Debug.WriteLine("创建边映射器");
            var edgesMapper = GetEdgesMapper(facesIndexes);
            Debug.WriteLine("完成耗时 " + sw.ElapsedMilliseconds + "ms");
            sw.Restart();

            Debug.WriteLine("创建面映射器");
            var facesMapper = GetFacesMapper(edgesMapper);
            Debug.WriteLine("完成耗时 " + sw.ElapsedMilliseconds + "ms");
            sw.Restart();

            Debug.WriteLine("组装面簇");
            var clusters = GetFacesClusters(facesIndexes, facesMapper);
            Debug.WriteLine("完成耗时 " + sw.ElapsedMilliseconds + "ms");
            sw.Restart();

            Debug.WriteLine("排序簇");

            // 按计数排序簇（提高打包密度，如果发现瓶颈可移除）
            clusters.Sort((a, b) => b.Count.CompareTo(a.Count));
            Debug.WriteLine("完成耗时 " + sw.ElapsedMilliseconds + "ms");
            sw.Restart();

            Debug.WriteLine($"材质 {material.Name} 有 {clusters.Count} 个簇");

            Debug.WriteLine("二进制打包簇");
            BinPackTextures(targetFolder, m, clusters, newTextureVertices, tasks);
            Debug.WriteLine("完成耗时 " + sw.ElapsedMilliseconds + "ms");
        }

        Debug.WriteLine("排序新的纹理顶点");
        sw.Restart();
        _textureVertices = newTextureVertices.OrderBy(item => item.Value).Select(item => item.Key).ToList();
        Debug.WriteLine("完成耗时 " + sw.ElapsedMilliseconds + "ms");

        Debug.WriteLine("等待保存任务完成");
        sw.Restart();
        Task.WaitAll(tasks.ToArray());
        Debug.WriteLine("完成耗时 " + sw.ElapsedMilliseconds + "ms");
    }

    /// <summary>
    /// 加载纹理缓存
    /// </summary>
    private void LoadTexturesCache()
    {
        Parallel.ForEach(_materials, material =>
        {
            if (!string.IsNullOrEmpty(material.Texture))
                TexturesCache.GetTexture(material.Texture);
            if (!string.IsNullOrEmpty(material.NormalMap))
                TexturesCache.GetTexture(material.NormalMap);
        });
    }

    /// <summary>
    /// JPEG 编码器实例，用于纹理压缩
    /// </summary>
    private static readonly JpegEncoder encoder = new JpegEncoder { Quality = 75 };

    /// <summary>
    /// 二进制打包纹理
    /// </summary>
    /// <param name="targetFolder">目标文件夹</param>
    /// <param name="materialIndex">材质索引</param>
    /// <param name="clusters">面簇列表</param>
    /// <param name="newTextureVertices">新的纹理顶点字典</param>
    /// <param name="tasks">保存任务集合</param>
    /// <returns></returns>
    private void BinPackTextures(string targetFolder, int materialIndex, IReadOnlyList<List<int>> clusters,
        IDictionary<Vertex2, int> newTextureVertices, ICollection<Task> tasks)
    {
        const int PADDING = 2; // <-- bleed ring

        var material = _materials[materialIndex];

        if (material.Texture == null && material.NormalMap == null) return;

        var texture = material.Texture != null ? TexturesCache.GetTexture(material.Texture) : null;
        var normalMap = material.NormalMap != null ? TexturesCache.GetTexture(material.NormalMap) : null;

        int textureWidth = material.Texture != null ? texture!.Width : normalMap!.Width;
        int textureHeight = material.Texture != null ? texture!.Height : normalMap!.Height;

        var clustersRects = clusters.Select(GetClusterRect).ToArray();

        CalculateMaxMinAreaRect(clustersRects, textureWidth, textureHeight, PADDING, out var maxWidth, out var maxHeight,
            out var textureArea);

        Debug.WriteLine("Texture area: " + textureArea);

        var edgeLength = Math.Max(Common.NextPowerOfTwo((int)Math.Sqrt(textureArea)), 32);

        if (edgeLength < maxWidth)
            edgeLength = Common.NextPowerOfTwo((int)maxWidth);

        if (edgeLength < maxHeight)
            edgeLength = Common.NextPowerOfTwo((int)maxHeight);

        Debug.WriteLine("Edge length: " + edgeLength);

        // 注意：我们可以启用旋转，但这会稍微复杂一些
        var binPack = new MaxRectanglesBinPack(edgeLength, edgeLength, false);

        var newTexture = material.Texture != null ? new Image<Rgba32>(edgeLength, edgeLength) : null;
        var newNormalMap = material.NormalMap != null ? new Image<Rgba32>(edgeLength, edgeLength) : null;

        string? textureFileName = null, normalMapFileName = null, newPathTexture = null, newPathNormalMap = null;
        int count = 0;

        for (int i = 0; i < clusters.Count; i++)
        {
            var cluster = clusters[i];
            var clusterBoundary = clustersRects[i]; // [0..1] UV box

            // 从聚类中获取绝对边界
            double u0 = clusterBoundary.Left;
            double v0 = clusterBoundary.Top;
            double u1 = u0 + clusterBoundary.Width;
            double v1 = v0 + clusterBoundary.Height;

            // ---- UDIM 图块定位 ----
            // 确定此簇所属的UDIM图块。
            int tileU = (int)Math.Floor(u0 + 1e-4);
            int tileV = (int)Math.Floor(v0 + 1e-4);

            // 如果簇跨越多个图块，请考虑按图块分割；
            // 目前我们只是将其固定到此图块（断言/记录以捕获它）。
            if (Math.Floor(u1 - 1e-4) != tileU || Math.Floor(v1 - 1e-4) != tileV)
            {
                Debug.WriteLine($"[UDIM] Cluster spans multiple tiles: U[{u0},{u1}] V[{v0},{v1}]");
            }

            // 分数（图块本地）UV在[0,1]中
            double u0f = Math.Clamp(u0 - tileU, 0.0, 1.0);
            double v0f = Math.Clamp(v0 - tileV, 0.0, 1.0);
            double u1f = Math.Clamp(u1 - tileU, 0.0, 1.0);
            double v1f = Math.Clamp(v1 - tileV, 0.0, 1.0);

            // ---- 源图块中的像素中心裁剪 ----
            int sx = Math.Clamp((int)Math.Floor(u0f * textureWidth + 0.5), 0, textureWidth - 1);
            int ex = Math.Clamp((int)Math.Ceiling(u1f * textureWidth - 0.5), 1, textureWidth);
            int sb = Math.Clamp((int)Math.Floor(v0f * textureHeight + 0.5), 0, textureHeight - 1);
            int eb = Math.Clamp((int)Math.Ceiling(v1f * textureHeight - 0.5), 1, textureHeight);

            int sw = Math.Max(1, ex - sx);
            int sh = Math.Max(1, eb - sb);

            // ImageSharp使用左上角原点；OBJ UV是左下角→转换Y
            int syTL = Math.Clamp(textureHeight - eb, 0, textureHeight - sh);
            var srcRect = new Rectangle(sx, syTL, sw, sh);

            // ---------- 用填充保留atlas空间 ----------
            var packRect = binPack.Insert(sw + 2 * PADDING, sh + 2 * PADDING,
                                          FreeRectangleChoiceHeuristic.RectangleBestAreaFit);

            // 如果空间不足：保存当前atlas，开始新的atlas（保持原有行为）
            if (packRect.Width == 0)
            {
                textureFileName = material.Texture != null
                    ? $"{Name}-texture-diffuse-{material.Name}{Path.GetExtension(material.Texture)}" : null;
                normalMapFileName = material.NormalMap != null
                    ? $"{Name}-texture-normal-{material.Name}{Path.GetExtension(material.NormalMap)}" : null;

                if (material.Texture != null)
                {
                    newPathTexture = Path.Combine(targetFolder, textureFileName!);
                    newTexture!.Save(newPathTexture); newTexture!.Dispose();
                }

                if (material.NormalMap != null)
                {
                    newPathNormalMap = Path.Combine(targetFolder, normalMapFileName!);
                    newNormalMap!.Save(newPathNormalMap);
                    newNormalMap!.Dispose();
                }

                // 新的atlas
                newTexture = material.Texture != null ? new Image<Rgba32>(edgeLength, edgeLength) : null;
                newNormalMap = material.NormalMap != null ? new Image<Rgba32>(edgeLength, edgeLength) : null;
                binPack = new MaxRectanglesBinPack(edgeLength, edgeLength, false);
                material.Texture = textureFileName;
                material.NormalMap = normalMapFileName;

                // 避免名称冲突，克隆材质
                count++;
                material = new Material(material.Name + "-" + count, textureFileName, normalMapFileName,
                    material.AmbientColor, material.DiffuseColor, material.SpecularColor,
                    material.SpecularExponent, material.Dissolve, material.IlluminationModel);
                _materials.Add(material);
                materialIndex = _materials.Count - 1;

                // 再次尝试
                packRect = binPack.Insert(sw + 2 * PADDING, sh + 2 * PADDING,
                                          FreeRectangleChoiceHeuristic.RectangleBestAreaFit);
                if (packRect.Width == 0)
                    throw new Exception($"Packing failed for {sw}x{sh} into {edgeLength}x{edgeLength} (occ {binPack.Occupancy()})");
            }

            int destInnerX = packRect.X + PADDING;
            int destInnerY = packRect.Y + PADDING;
            int destOuterX = destInnerX - PADDING;
            int destOuterY = destInnerY - PADDING;


            if (material.Texture != null)
            {
                using var block = BuildPaddedBlock(texture!, srcRect, PADDING);
                newTexture!.Mutate(c => c.DrawImage(block, new Point(destOuterX, destOuterY), 1f));
            }
            if (material.NormalMap != null)
            {
                using var blockN = BuildPaddedBlock(normalMap!, srcRect, PADDING);
                newNormalMap!.Mutate(c => c.DrawImage(blockN, new Point(destOuterX, destOuterY), 1f));
            }

            // 内部矩形尺寸，以像素为单位
            double innerWpx = sw;                 // 裁剪宽度
            double innerHpx = sh;                 // 裁剪高度

            double atlasU0 = destInnerX / (double)edgeLength;
            double atlasV0 = (edgeLength - (destInnerY + sh)) / (double)edgeLength;
            double innerUw = sw / (double)edgeLength;
            double innerVh = sh / (double)edgeLength;

            Vertex2 MapUV(double rx, double ry) =>
                new((float)Math.Clamp(atlasU0 + rx * innerUw, 0, 1),
                    (float)Math.Clamp(atlasV0 + ry * innerVh, 0, 1));

            for (int idx = 0; idx < cluster.Count; idx++)
            {
                var faceIndex = cluster[idx];
                var face = _faces[faceIndex];

                var vtA = _textureVertices[face.TextureIndexA];
                var vtB = _textureVertices[face.TextureIndexB];
                var vtC = _textureVertices[face.TextureIndexC];

                // 图表本地[0..1]（避免混合完整纹理尺度）
                double rxA = (vtA.X - u0) / Math.Max(u1 - u0, double.Epsilon);
                double ryA = (vtA.Y - v0) / Math.Max(v1 - v0, double.Epsilon);
                double rxB = (vtB.X - u0) / Math.Max(u1 - u0, double.Epsilon);
                double ryB = (vtB.Y - v0) / Math.Max(v1 - v0, double.Epsilon);
                double rxC = (vtC.X - u0) / Math.Max(u1 - u0, double.Epsilon);
                double ryC = (vtC.Y - v0) / Math.Max(v1 - v0, double.Epsilon);

                var newVtA = MapUV(rxA, ryA);
                var newVtB = MapUV(rxB, ryB);
                var newVtC = MapUV(rxC, ryC);

                var newIndexVtA = newTextureVertices.AddIndex(newVtA);
                var newIndexVtB = newTextureVertices.AddIndex(newVtB);
                var newIndexVtC = newTextureVertices.AddIndex(newVtC);

                face.TextureIndexA = newIndexVtA;
                face.TextureIndexB = newIndexVtB;
                face.TextureIndexC = newIndexVtC;
                face.MaterialIndex = materialIndex;
            }
        }

        // ---------- 保存（未更改）----------
        if (material.Texture != null)
        {
            textureFileName = TexturesStrategy == TexturesStrategy.Repack
                ? $"{Name}-texture-diffuse-{material.Name}{Path.GetExtension(material.Texture)}"
                : $"{Name}-texture-diffuse-{material.Name}.jpg";
            newPathTexture = Path.Combine(targetFolder, textureFileName);
        }

        if (material.NormalMap != null)
        {
            normalMapFileName = TexturesStrategy == TexturesStrategy.Repack
                ? $"{Name}-texture-normal-{material.Name}{Path.GetExtension(material.NormalMap)}"
                : $"{Name}-texture-normal-{material.Name}.jpg";
            newPathNormalMap = Path.Combine(targetFolder, normalMapFileName);
        }

        var saveTaskTexture = new Task(t =>
        {
            var tx = t as Image<Rgba32>;
            switch (TexturesStrategy)
            {
                case TexturesStrategy.RepackCompressed: tx!.SaveAsJpeg(newPathTexture!, encoder); break;
                case TexturesStrategy.Repack: tx!.Save(newPathTexture!); break;
                default: throw new InvalidOperationException("KeepOriginal/Compress are meaningless here");
            }
            Debug.WriteLine("Saved texture to " + newPathTexture);
            tx!.Dispose();
        }, newTexture, TaskCreationOptions.LongRunning);

        var saveTaskNormalMap = new Task(t =>
        {
            var tx = t as Image<Rgba32>;
            switch (TexturesStrategy)
            {
                case TexturesStrategy.RepackCompressed: tx!.SaveAsJpeg(newPathNormalMap!, encoder); break;
                case TexturesStrategy.Repack: tx!.Save(newPathNormalMap!); break;
                default: throw new InvalidOperationException("KeepOriginal/Compress are meaningless here");
            }
            Debug.WriteLine("Saved texture to " + newPathNormalMap);
            tx!.Dispose();
        }, newNormalMap, TaskCreationOptions.LongRunning);

        if (material.Texture != null)
        {
            tasks.Add(saveTaskTexture);
            saveTaskTexture.Start();
            material.Texture = textureFileName;

        }

        if (material.NormalMap != null)
        {
            tasks.Add(saveTaskNormalMap);
            saveTaskNormalMap.Start();
            material.NormalMap = normalMapFileName;
        }
    }

    /// <summary>
    /// 在估算总面积和最大尺寸时，为每个图表添加出血填充
    /// </summary>
    private void CalculateMaxMinAreaRect(
        RectangleF[] clustersRects,
        int textureWidth,
        int textureHeight,
        int paddingPx,                        // <-- 新增
        out double maxWidth,                  // 像素（已填充）
        out double maxHeight,                 // 像素（已填充）
        out double textureArea)               // 像素^2（填充图表面积的总和）
    {
        long areaPx = 0;
        int maxW = 0;
        int maxH = 0;

        for (int index = 0; index < clustersRects.Length; index++)
        {
            var rect = clustersRects[index];

            // 从UV分数计算图表在像素中的尺寸
            int w = Math.Max(1, (int)Math.Ceiling(rect.Width * textureWidth));
            int h = Math.Max(1, (int)Math.Ceiling(rect.Height * textureHeight));

            // 在两侧添加填充
            int wPad = Math.Max(1, w + 2 * paddingPx);
            int hPad = Math.Max(1, h + 2 * paddingPx);

            areaPx += (long)wPad * (long)hPad;
            if (wPad > maxW) maxW = wPad;
            if (hPad > maxH) maxH = hPad;
        }

        maxWidth = maxW;          // 已为像素（不再进一步相乘）
        maxHeight = maxH;         // 已为像素（不再进一步相乘）
        textureArea = areaPx;     // 像素^2
    }


    /// <summary>
    /// 计算一组点的包围盒。
    /// </summary>
    /// <param name="cluster"></param>
    /// <returns></returns>
    private RectangleF GetClusterRect(IReadOnlyList<int> cluster)
    {
        double maxX = double.MinValue, maxY = double.MinValue;
        double minX = double.MaxValue, minY = double.MaxValue;

        for (var n = 0; n < cluster.Count; n++)
        {
            var face = _faces[cluster[n]];

            var vtA = _textureVertices[face.TextureIndexA];
            var vtB = _textureVertices[face.TextureIndexB];
            var vtC = _textureVertices[face.TextureIndexC];

            maxX = Math.Max(Math.Max(Math.Max(maxX, vtC.X), vtB.X), vtA.X);
            maxY = Math.Max(Math.Max(Math.Max(maxY, vtC.Y), vtB.Y), vtA.Y);

            minX = Math.Min(Math.Min(Math.Min(minX, vtC.X), vtB.X), vtA.X);
            minY = Math.Min(Math.Min(Math.Min(minY, vtC.Y), vtB.Y), vtA.Y);
        }

        return new RectangleF((float)minX, (float)minY, (float)(maxX - minX), (float)(maxY - minY));
    }

    /// <summary>
    /// 获取纹理区域面积
    /// </summary>
    /// <param name="facesIndexes">面索引列表</param>
    /// <returns>纹理区域面积</returns>
    private double GetTextureArea(IReadOnlyList<int> facesIndexes)
    {
        double area = 0;

        for (var index = 0; index < facesIndexes.Count; index++)
        {
            var faceIndex = facesIndexes[index];

            var vtA = _textureVertices[_faces[faceIndex].TextureIndexA];
            var vtB = _textureVertices[_faces[faceIndex].TextureIndexB];
            var vtC = _textureVertices[_faces[faceIndex].TextureIndexC];

            area += Common.Area(vtA, vtB, vtC);
        }

        return area;
    }

    /// <summary>
    /// 获取面簇
    /// </summary>
    /// <param name="facesIndexes">面索引列表</param>
    /// <param name="facesMapper">面映射器</param>
    /// <returns></returns>
    private static List<List<int>> GetFacesClusters(IEnumerable<int> facesIndexes,
        IReadOnlyDictionary<int, List<int>> facesMapper)
    {

        var clusters = new List<List<int>>();
        var remainingFacesIndexes = new List<int>(facesIndexes);

        var currentCluster = new List<int> { remainingFacesIndexes[0] };
        var currentClusterCache = new HashSet<int> { remainingFacesIndexes[0] };
        remainingFacesIndexes.RemoveAt(0);

        var lastRemainingFacesCount = remainingFacesIndexes.Count;

        while (remainingFacesIndexes.Count > 0)
        {
            var cnt = currentCluster.Count;

            for (var index = 0; index < currentCluster.Count; index++)
            {
                var faceIndex = currentCluster[index];

                if (!facesMapper.TryGetValue(faceIndex, out var connectedFaces))
                    continue;

                for (var i = 0; i < connectedFaces.Count; i++)
                {
                    var connectedFace = connectedFaces[i];
                    if (currentClusterCache.Contains(connectedFace)) continue;

                    currentCluster.Add(connectedFace);
                    currentClusterCache.Add(connectedFace);
                    remainingFacesIndexes.Remove(connectedFace);
                }
            }

            // 没有添加新面
            if (cnt == currentCluster.Count)
            {
                // 添加簇
                clusters.Add(currentCluster);

                // 如果没有更多面，退出
                if (remainingFacesIndexes.Count == 0) break;

                // 继续下一个簇
                currentCluster = [remainingFacesIndexes[0]];
                currentClusterCache = [remainingFacesIndexes[0]];
                remainingFacesIndexes.RemoveAt(0);
            }

            if (lastRemainingFacesCount == remainingFacesIndexes.Count)
            {
                Debug.WriteLine("Discarding " + remainingFacesIndexes.Count + " faces.");
                break;
            }

            lastRemainingFacesCount = remainingFacesIndexes.Count;
        }

        // 添加簇
        clusters.Add(currentCluster);
        return clusters;
    }

    /// <summary>
    /// 获取面映射器
    /// </summary>
    /// <param name="edgesMapper">边映射器</param>
    /// <returns></returns>
    private static Dictionary<int, List<int>> GetFacesMapper(Dictionary<Edge, List<int>> edgesMapper)
    {
        var facesMapper = new Dictionary<int, List<int>>();

        foreach (var edge in edgesMapper)
        {
            for (var i = 0; i < edge.Value.Count; i++)
            {
                var faceIndex = edge.Value[i];
                if (!facesMapper.ContainsKey(faceIndex))
                    facesMapper.Add(faceIndex, []);

                for (var index = 0; index < edge.Value.Count; index++)
                {
                    var f = edge.Value[index];
                    if (f != faceIndex)
                        facesMapper[faceIndex].Add(f);
                }
            }
        }

        return facesMapper;
    }

    /// <summary>
    /// 获取边映射器
    /// </summary>
    /// <param name="facesIndexes">面索引列表</param>
    /// <returns></returns>
    private Dictionary<Edge, List<int>> GetEdgesMapper(IReadOnlyList<int> facesIndexes)
    {
        var edgesMapper = new Dictionary<Edge, List<int>>();
        edgesMapper.EnsureCapacity(facesIndexes.Count * 3);

        for (var idx = 0; idx < facesIndexes.Count; idx++)
        {
            var faceIndex = facesIndexes[idx];
            var f = _faces[faceIndex];

            var e1 = new Edge(f.TextureIndexA, f.TextureIndexB);
            var e2 = new Edge(f.TextureIndexB, f.TextureIndexC);
            var e3 = new Edge(f.TextureIndexA, f.TextureIndexC);

            if (!edgesMapper.ContainsKey(e1))
                edgesMapper.Add(e1, []);

            if (!edgesMapper.ContainsKey(e2))
                edgesMapper.Add(e2, []);

            if (!edgesMapper.ContainsKey(e3))
                edgesMapper.Add(e3, []);

            edgesMapper[e1].Add(faceIndex);
            edgesMapper[e2].Add(faceIndex);
            edgesMapper[e3].Add(faceIndex);
        }

        return edgesMapper;
    }

    /// <summary>
    /// 按材质获取面的索引列表
    /// </summary>
    /// <returns></returns>
    private List<List<int>> GetFacesByMaterial()
    {
        var res = _materials.Select(_ => new List<int>()).ToList();

        for (var i = 0; i < _faces.Count; i++)
        {
            var f = _faces[i];

            res[f.MaterialIndex].Add(i);
        }

        return res;
    }

    /// <summary>
    /// 获取网格的包围盒
    /// </summary>
    public Box3 Bounds
    {
        get
        {
            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var minZ = double.MaxValue;

            var maxX = double.MinValue;
            var maxY = double.MinValue;
            var maxZ = double.MinValue;

            for (var index = 0; index < _vertices.Count; index++)
            {
                var v = _vertices[index];
                minX = minX < v.X ? minX : v.X;
                minY = minY < v.Y ? minY : v.Y;
                minZ = minZ < v.Z ? minZ : v.Z;

                maxX = v.X > maxX ? v.X : maxX;
                maxY = v.Y > maxY ? v.Y : maxY;
                maxZ = v.Z > maxZ ? v.Z : maxZ;
            }

            return new Box3(minX, minY, minZ, maxX, maxY, maxZ);
        }
    }

    /// <summary>
    /// 获取平均面朝向
    /// </summary>
    /// <returns></returns>
    public Vertex3 GetAverageOrientation()
    {
        double x = 0;
        double y = 0;
        double z = 0;

        for (var index = 0; index < _faces.Count; index++)
        {
            var f = _faces[index];
            var v1 = _vertices[f.IndexA];
            var v2 = _vertices[f.IndexB];
            var v3 = _vertices[f.IndexC];

            var orientation = Common.Orientation(v1, v2, v3);

            x += orientation.X;
            y += orientation.Y;
            z += orientation.Z;
        }

        x /= _faces.Count;
        y /= _faces.Count;
        z /= _faces.Count;

        // 计算x、y和z角度
        var xAngle = Math.Atan2(y, z);
        var yAngle = Math.Atan2(x, z);
        var zAngle = Math.Atan2(y, x);

        return new Vertex3(xAngle, yAngle, zAngle);
    }

    /// <summary>
    /// 获取顶点重心
    /// </summary>
    /// <returns></returns>
    public Vertex3 GetVertexBaricenter()
    {
        var x = 0.0;
        var y = 0.0;
        var z = 0.0;

        for (var index = 0; index < _vertices.Count; index++)
        {
            var v = _vertices[index];
            x += v.X;
            y += v.Y;
            z += v.Z;
        }

        x /= _vertices.Count;
        y /= _vertices.Count;
        z /= _vertices.Count;

        return new Vertex3(x, y, z);
    }

    /// <summary>
    /// 将网格写入OBJ文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="removeUnused">是否移除未使用的顶点和UV</param>
    public void WriteObj(string path, bool removeUnused = true)
    {
        if (_materials.Count == 0 || _textureVertices.Count == 0)
            _WriteObjWithoutTexture(path, removeUnused);
        else
            _WriteObjWithTexture(path, removeUnused);
    }

    /// <summary>
    /// 移除未使用的顶点
    /// </summary>
    private void RemoveUnusedVertices()
    {
        var newVertexes = new Dictionary<Vertex3, int>(_vertices.Count);

        for (var f = 0; f < _faces.Count; f++)
        {
            var face = _faces[f];

            var vA = _vertices[face.IndexA];
            var vB = _vertices[face.IndexB];
            var vC = _vertices[face.IndexC];

            if (!newVertexes.TryGetValue(vA, out var newVA))
                newVA = newVertexes.AddIndex(vA);

            face.IndexA = newVA;

            if (!newVertexes.TryGetValue(vB, out var newVB))
                newVB = newVertexes.AddIndex(vB);

            face.IndexB = newVB;

            if (!newVertexes.TryGetValue(vC, out var newVC))
                newVC = newVertexes.AddIndex(vC);

            face.IndexC = newVC;
        }

        _vertices = newVertexes.Keys.ToList();
    }

    /// <summary>
    /// 移除未使用的顶点和UV
    /// </summary>
    private void RemoveUnusedVerticesAndUvs()
    {
        var newVertexes = new Dictionary<Vertex3, int>(_vertices.Count);
        var newUvs = new Dictionary<Vertex2, int>(_textureVertices.Count);
        var newMaterials = new Dictionary<Material, int>(_materials.Count);

        for (var f = 0; f < _faces.Count; f++)
        {
            var face = _faces[f];

            // Vertices

            var vA = _vertices[face.IndexA];
            var vB = _vertices[face.IndexB];
            var vC = _vertices[face.IndexC];

            if (!newVertexes.TryGetValue(vA, out var newVA))
                newVA = newVertexes.AddIndex(vA);

            face.IndexA = newVA;

            if (!newVertexes.TryGetValue(vB, out var newVB))
                newVB = newVertexes.AddIndex(vB);

            face.IndexB = newVB;

            if (!newVertexes.TryGetValue(vC, out var newVC))
                newVC = newVertexes.AddIndex(vC);

            face.IndexC = newVC;

            // Texture vertices

            var uvA = _textureVertices[face.TextureIndexA];
            var uvB = _textureVertices[face.TextureIndexB];
            var uvC = _textureVertices[face.TextureIndexC];

            if (!newUvs.TryGetValue(uvA, out var newUvA))
                newUvA = newUvs.AddIndex(uvA);

            face.TextureIndexA = newUvA;

            if (!newUvs.TryGetValue(uvB, out var newUvB))
                newUvB = newUvs.AddIndex(uvB);

            face.TextureIndexB = newUvB;

            if (!newUvs.TryGetValue(uvC, out var newUvC))
                newUvC = newUvs.AddIndex(uvC);

            face.TextureIndexC = newUvC;

            // Materials

            var material = _materials[face.MaterialIndex];

            if (!newMaterials.TryGetValue(material, out var newMaterial))
                newMaterial = newMaterials.AddIndex(material);

            face.MaterialIndex = newMaterial;
        }

        _vertices = newVertexes.Keys.ToList();
        _textureVertices = newUvs.Keys.ToList();
        _materials = newMaterials.Keys.ToList();
    }

    /// <summary>
    /// 通过复制边缘像素来构建填充的图表块（ImageSharp安全版本）
    /// </summary>
    /// <param name="src">源图像</param>
    /// <param name="srcRect">源矩形区域</param>
    /// <param name="padding">填充大小（像素）</param>
    /// <returns>填充后的图像块</returns>
    private static Image<Rgba32> BuildPaddedBlock(Image<Rgba32> src, Rectangle srcRect, int padding)
    {
        // 限制裁剪区域
        int sx = Math.Clamp(srcRect.X, 0, Math.Max(0, src.Width - 1));
        int sy = Math.Clamp(srcRect.Y, 0, Math.Max(0, src.Height - 1));
        int sw = Math.Clamp(srcRect.Width, 1, src.Width - sx);
        int sh = Math.Clamp(srcRect.Height, 1, src.Height - sy);

        using var crop = src.Clone(c => c.Crop(new SixLabors.ImageSharp.Rectangle(sx, sy, sw, sh)));

        var block = new Image<Rgba32>(sw + 2 * padding, sh + 2 * padding);

        // 在 (padding, padding) 位置粘贴内部内容
        block.Mutate(c => c.DrawImage(crop, new Point(padding, padding), 1f));

        if (padding <= 0) return block;

        // 顶部/底部条带
        using (var top = crop.Clone(c => c.Crop(new SixLabors.ImageSharp.Rectangle(0, 0, sw, 1)).Resize(sw, padding)))
            block.Mutate(c => c.DrawImage(top, new Point(padding, 0), 1f));
        using (var bot = crop.Clone(c => c.Crop(new SixLabors.ImageSharp.Rectangle(0, sh - 1, sw, 1)).Resize(sw, padding)))
            block.Mutate(c => c.DrawImage(bot, new Point(padding, padding + sh), 1f));

        // 左侧/右侧条带
        using (var left = crop.Clone(c => c.Crop(new SixLabors.ImageSharp.Rectangle(0, 0, 1, sh)).Resize(padding, sh)))
            block.Mutate(c => c.DrawImage(left, new Point(0, padding), 1f));
        using (var right = crop.Clone(c => c.Crop(new SixLabors.ImageSharp.Rectangle(sw - 1, 0, 1, sh)).Resize(padding, sh)))
            block.Mutate(c => c.DrawImage(right, new Point(padding + sw, padding), 1f));

        // 角落填充
        void Corner(int sx0, int sy0, int dx, int dy)
        {
            using var px = crop.Clone(c => c.Crop(new SixLabors.ImageSharp.Rectangle(sx0, sy0, 1, 1)).Resize(padding, padding));
            block.Mutate(c => c.DrawImage(px, new Point(dx, dy), 1f));
        }
        Corner(0, 0, 0, 0);
        Corner(sw - 1, 0, padding + sw, 0);
        Corner(0, sh - 1, 0, padding + sh);
        Corner(sw - 1, sh - 1, padding + sw, padding + sh);

        return block;
    }

    /// <summary>
    /// 保存带纹理的OBJ文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="removeUnused">是否移除未使用的顶点和UV</param>
    private void _WriteObjWithTexture(string path, bool removeUnused = true)
    {
        if (removeUnused)
            RemoveUnusedVerticesAndUvs();

        var materialsPath = Path.ChangeExtension(path, "mtl");

        var folderPath = Path.GetDirectoryName(path) ?? string.Empty;

        if (TexturesStrategy == TexturesStrategy.Repack || TexturesStrategy == TexturesStrategy.RepackCompressed)
            TrimTextures(folderPath);

        using (var writer = new FormattingStreamWriter(path, CultureInfo.InvariantCulture))
        {
            writer.Write("o ");
            writer.WriteLine(string.IsNullOrWhiteSpace(Name) ? DefaultName : Name);

            writer.WriteLine("mtllib {0}", Path.GetFileName(materialsPath));

            foreach (var vertex in _vertices)
            {
                writer.Write("v ");
                writer.Write(vertex.X);
                writer.Write(" ");
                writer.Write(vertex.Y);
                writer.Write(" ");
                writer.WriteLine(vertex.Z);
            }

            foreach (var textureVertex in _textureVertices)
            {
                writer.Write("vt ");
                writer.Write(textureVertex.X);
                writer.Write(" ");
                writer.WriteLine(textureVertex.Y);
            }

            var materialFaces = from face in _faces
                                group face by face.MaterialIndex
                into g
                                select g;

            // 注意：如果有无材质的面组，它们必须放在开头
            foreach (var grp in materialFaces.OrderBy(item => item.Key))
            {
                writer.WriteLine($"usemtl {_materials[grp.Key].Name}");

                foreach (var face in grp)
                    writer.WriteLine(face.ToObj());
            }
        }

        var mtlFilePath = Path.ChangeExtension(path, "mtl");

        using (var writer = new FormattingStreamWriter(mtlFilePath, CultureInfo.InvariantCulture))
        {
            for (var index = 0; index < _materials.Count; index++)
            {
                var material = _materials[index];

                if (material.Texture != null)
                {
                    switch (TexturesStrategy)
                    {
                        case TexturesStrategy.KeepOriginal:
                            {
                                var folder = Path.GetDirectoryName(path);

                                var textureFileName =
                                    $"{Path.GetFileNameWithoutExtension(path)}-texture-{index}{Path.GetExtension(material.Texture)}";

                                var newTexturePath =
                                    folder != null ? Path.Combine(folder, textureFileName) : textureFileName;

                                if (!File.Exists(newTexturePath))
                                    File.Copy(material.Texture, newTexturePath, true);

                                material.Texture = textureFileName;
                                break;
                            }
                        case TexturesStrategy.Compress:
                            {
                                var folder = Path.GetDirectoryName(path);

                                var textureFileName =
                                    $"{Path.GetFileNameWithoutExtension(path)}-texture-{index}.jpg";

                                var newTexturePath =
                                    folder != null ? Path.Combine(folder, textureFileName) : textureFileName;

                                if (File.Exists(newTexturePath))

                                    File.Delete(newTexturePath);

                                Console.WriteLine($" -> Compressing texture '{material.Texture}'");

                                using (var image = Image.Load(material.Texture))
                                {
                                    image.SaveAsJpeg(newTexturePath, encoder);
                                }

                                material.Texture = textureFileName;
                                break;
                            }
                    }
                }

                writer.WriteLine(material.ToMtl());
            }
        }
    }

    /// <summary>
    /// 保存不带纹理的OBJ文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="removeUnused">是否移除未使用的顶点</param>
    private void _WriteObjWithoutTexture(string path, bool removeUnused = true)
    {
        if (removeUnused)
            RemoveUnusedVertices();

        using var writer = new FormattingStreamWriter(path, CultureInfo.InvariantCulture);

        writer.Write("o ");
        writer.WriteLine(string.IsNullOrWhiteSpace(Name) ? DefaultName : Name);

        for (var index = 0; index < _vertices.Count; index++)
        {
            var vertex = _vertices[index];
            writer.Write("v ");
            writer.Write(vertex.X);
            writer.Write(" ");
            writer.Write(vertex.Y);
            writer.Write(" ");
            writer.WriteLine(vertex.Z);
        }

        for (var index = 0; index < _faces.Count; index++)
        {
            var face = _faces[index];
            writer.WriteLine(face.ToObj());
        }
    }

    public int FacesCount => _faces.Count;
    public int VertexCount => _vertices.Count;
}

/// <summary>
/// 纹理处理策略
/// </summary>
public enum TexturesStrategy
{
    /// <summary>
    /// 保持原样，不进行任何处理。
    /// </summary>
    KeepOriginal,

    /// <summary>
    /// 压缩纹理以减小文件大小，但保持其原始分辨率。
    /// </summary>
    Compress,

    /// <summary>
    /// 重新打包纹理以优化空间利用率，但保持其原始分辨率。
    /// </summary>
    Repack,

    /// <summary>
    /// 重新打包并压缩纹理以优化空间利用率和文件大小。
    /// </summary>
    RepackCompressed
}
