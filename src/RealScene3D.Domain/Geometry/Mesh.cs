using SixLabors.ImageSharp;
using System.Globalization;
using RealScene3D.Domain.Utils;
using RealScene3D.Domain.Enums;

namespace RealScene3D.Domain.Geometry;

/// <summary>
/// 网格类，实现网格的基本操作，包括分割、包围盒计算和OBJ文件导出等功能
/// </summary>
public class Mesh : IMesh
{
    /// <summary>
    /// 顶点列表
    /// </summary>
    private List<Vertex3> _vertices;

    /// <summary>
    /// 面列表（只读）
    /// </summary>
    private readonly List<Face> _faces;

    /// <summary>
    /// 获取只读顶点列表
    /// </summary>
    public IReadOnlyList<Vertex3> Vertices => _vertices;

    /// <summary>
    /// 获取只读面列表
    /// </summary>
    public IReadOnlyList<Face> Faces => _faces;

    /// <summary>
    /// 默认网格名称
    /// </summary>
    public const string DefaultName = "Mesh";

    /// <summary>
    /// 网格名称
    /// </summary>
    public string Name { get; set; } = DefaultName;

    /// <summary>
    /// 使用指定的顶点和面列表构造网格
    /// </summary>
    /// <param name="vertices">顶点序列</param>
    /// <param name="faces">面序列</param>
    public Mesh(IEnumerable<Vertex3> vertices, IEnumerable<Face> faces)
    {
        _vertices = new List<Vertex3>(vertices);
        _faces = new List<Face>(faces);
    }

    /// <summary>
    /// 根据指定的分割平面将网格分割为左右两部分
    /// </summary>
    /// <param name="utils">顶点工具，用于获取维度和计算交叉点</param>
    /// <param name="q">分割平面的位置</param>
    /// <param name="left">分割后的左侧网格</param>
    /// <param name="right">分割后的右侧网格</param>
    /// <returns>分割操作中处理的交叉面数量</returns>
    public int Split(IVertexUtils utils, double q, out IMesh left, out IMesh right)
    {
        // 初始化左侧和右侧的顶点字典，用于存储分割后的顶点及其索引
        var leftVertices = new Dictionary<Vertex3, int>(_vertices.Count);
        var rightVertices = new Dictionary<Vertex3, int>(_vertices.Count);

        // 初始化左侧和右侧的面列表，用于存储分割后的面
        var leftFaces = new List<Face>(_faces.Count);
        var rightFaces = new List<Face>(_faces.Count);

        // 记录分割过程中处理的交叉面数量
        var count = 0;

        for (var index = 0; index < _faces.Count; index++)
        {
            var face = _faces[index];

            var vA = _vertices[face.IndexA];
            var vB = _vertices[face.IndexB];
            var vC = _vertices[face.IndexC];

            var aSide = utils.GetDimension(vA) < q;
            var bSide = utils.GetDimension(vB) < q;
            var cSide = utils.GetDimension(vC) < q;

            // 根据三个顶点的位置关系进行不同的处理
            if (aSide)
            {
                if (bSide)
                {
                    if (cSide)
                    {
                        // 所有顶点都在左侧，将整个面添加到左侧网格
                        var indexALeft = leftVertices.AddIndex(vA);
                        var indexBLeft = leftVertices.AddIndex(vB);
                        var indexCLeft = leftVertices.AddIndex(vC);

                        leftFaces.Add(new Face(indexALeft, indexBLeft, indexCLeft));
                    }
                    else
                    {
                        // 顶点A和B在左侧，C在右侧，需要分割面
                        IntersectRight2D(utils, q, face.IndexC, face.IndexA, face.IndexB, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                }
                else
                {
                    if (cSide)
                    {
                        // 顶点A和C在左侧，B在右侧，需要分割面
                        IntersectRight2D(utils, q, face.IndexB, face.IndexC, face.IndexA, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                    else
                    {
                        // 只有顶点A在左侧，B和C在右侧，需要分割面
                        IntersectLeft2D(utils, q, face.IndexA, face.IndexB, face.IndexC, leftVertices, rightVertices,
                            leftFaces, rightFaces);
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
                        IntersectRight2D(utils, q, face.IndexA, face.IndexB, face.IndexC, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                    else
                    {
                        IntersectLeft2D(utils, q, face.IndexB, face.IndexC, face.IndexA, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                }
                else
                {
                    if (cSide)
                    {
                        IntersectLeft2D(utils, q, face.IndexC, face.IndexA, face.IndexB, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                    else
                    {
                        // 全部都在右侧
                        var indexARight = rightVertices.AddIndex(vA);
                        var indexBRight = rightVertices.AddIndex(vB);
                        var indexCRight = rightVertices.AddIndex(vC);
                        rightFaces.Add(new Face(indexARight, indexBRight, indexCRight));
                    }
                }
            }
        }

        // 对顶点按添加顺序排序，确保索引连续性
        var orderedLeftVertices = leftVertices.OrderBy(x => x.Value).Select(x => x.Key);
        var orderedRightVertices = rightVertices.OrderBy(x => x.Value).Select(x => x.Key);

        // 创建左侧网格，使用排序后的顶点和面，并设置名称
        left = new Mesh(orderedLeftVertices, leftFaces)
        {
            Name = $"{Name}-{utils.Axis}L"
        };

        // 创建右侧网格，使用排序后的顶点和面，并设置名称
        right = new Mesh(orderedRightVertices, rightFaces)
        {
            Name = $"{Name}-{utils.Axis}R"
        };

        // 返回分割过程中处理的交叉面数量
        return count;
    }

    /// <summary>
    /// 处理左2D交叉分割（一个顶点在左侧，两个在右侧）
    /// </summary>
    private void IntersectLeft2D(IVertexUtils utils, double q, int indexVL, int indexVR1, int indexVR2,
        IDictionary<Vertex3, int> leftVertices,
        IDictionary<Vertex3, int> rightVertices, ICollection<Face> leftFaces,
        ICollection<Face> rightFaces)
    {
        var vL = _vertices[indexVL];
        var vR1 = _vertices[indexVR1];
        var vR2 = _vertices[indexVR2];

        var indexVLLeft = leftVertices.AddIndex(vL);

        if (Math.Abs(utils.GetDimension(vR1) - q) < Common.Epsilon &&
            Math.Abs(utils.GetDimension(vR2) - q) < Common.Epsilon)
        {
            // 右侧顶点在分割线上
            var indexVR1Left = leftVertices.AddIndex(vR1);
            var indexVR2Left = leftVertices.AddIndex(vR2);

            leftFaces.Add(new Face(indexVLLeft, indexVR1Left, indexVR2Left));
            return;
        }

        var indexVR1Right = rightVertices.AddIndex(vR1);
        var indexVR2Right = rightVertices.AddIndex(vR2);

        // 一个顶点在左侧，两个在右侧
        // 第一次交叉
        var t1 = utils.CutEdge(vL, vR1, q);
        var indexT1Left = leftVertices.AddIndex(t1);
        var indexT1Right = rightVertices.AddIndex(t1);

        // 第二次交叉
        var t2 = utils.CutEdge(vL, vR2, q);
        var indexT2Left = leftVertices.AddIndex(t2);
        var indexT2Right = rightVertices.AddIndex(t2);

        var lface = new Face(indexVLLeft, indexT1Left, indexT2Left);
        leftFaces.Add(lface);

        var rface1 = new Face(indexT1Right, indexVR1Right, indexVR2Right);
        rightFaces.Add(rface1);

        var rface2 = new Face(indexT1Right, indexVR2Right, indexT2Right);
        rightFaces.Add(rface2);
    }

    /// <summary>
    /// 处理右2D交叉分割（一个顶点在右侧，两个在左侧）
    /// 当三角形的一个顶点在分割线的右侧，另两个在左侧时，将三角形分割成一个右面和两个左面
    /// </summary>
    private void IntersectRight2D(IVertexUtils utils, double q, int indexVR, int indexVL1, int indexVL2,
        IDictionary<Vertex3, int> leftVertices, IDictionary<Vertex3, int> rightVertices,
        ICollection<Face> leftFaces, ICollection<Face> rightFaces)
    {
        // 获取右侧顶点和左侧顶点
        var vR = _vertices[indexVR];   // 右侧顶点
        var vL1 = _vertices[indexVL1]; // 左侧顶点1
        var vL2 = _vertices[indexVL2]; // 左侧顶点2

        // 将右侧顶点添加到右顶点字典中
        var indexVRRight = rightVertices.AddIndex(vR);

        // 检查左侧顶点是否在分割线上（精确到epsilon范围内）
        if (Math.Abs(utils.GetDimension(vL1) - q) < Common.Epsilon &&
            Math.Abs(utils.GetDimension(vL2) - q) < Common.Epsilon)
        {
            // 左侧顶点在分割线上，将它们添加到右顶点并创建右面
            var indexVL1Right = rightVertices.AddIndex(vL1);
            var indexVL2Right = rightVertices.AddIndex(vL2);

            rightFaces.Add(new Face(indexVRRight, indexVL1Right, indexVL2Right));

            return;
        }

        // 将左侧顶点添加到左顶点字典中
        var indexVL1Left = leftVertices.AddIndex(vL1);
        var indexVL2Left = leftVertices.AddIndex(vL2);

        // 一个顶点在右侧，两个在左侧
        // 计算第一次交叉点（右侧顶点到左侧顶点1的边与分割线的交点）
        var t1 = utils.CutEdge(vR, vL1, q);
        var indexT1Left = leftVertices.AddIndex(t1);   // 交叉点在左顶点中的索引
        var indexT1Right = rightVertices.AddIndex(t1); // 交叉点在右顶点中的索引

        // 计算第二次交叉点（右侧顶点到左侧顶点2的边与分割线的交点）
        var t2 = utils.CutEdge(vR, vL2, q);
        var indexT2Left = leftVertices.AddIndex(t2);   // 交叉点在左顶点中的索引
        var indexT2Right = rightVertices.AddIndex(t2); // 交叉点在右顶点中的索引

        // 创建右面（由右侧顶点和两个交叉点组成）
        var rface = new Face(indexVRRight, indexT1Right, indexT2Right);
        rightFaces.Add(rface);

        // 创建第一个左面（由交叉点2和两个左侧顶点组成）
        var lface1 = new Face(indexT2Left, indexVL1Left, indexVL2Left);
        leftFaces.Add(lface1);

        // 创建第二个左面（由交叉点2、交叉点1和左侧顶点1组成）
        var lface2 = new Face(indexT2Left, indexT1Left, indexVL1Left);
        leftFaces.Add(lface2);
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

                maxX = v.X < maxX ? maxX : v.X;
                maxY = v.Y < maxY ? maxY : v.Y;
                maxZ = v.Z < maxZ ? maxZ : v.Z;
            }

            return new Box3(minX, minY, minZ, maxX, maxY, maxZ);
        }
    }

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
    /// <param name="removeUnused">是否移除未使用的顶点（默认为true）</param>
    public void WriteObj(string path, bool removeUnused = true)
    {

        if (removeUnused) RemoveUnusedVertices();

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

    public int FacesCount => _faces.Count;
    public int VertexCount => _vertices.Count;

    /// <summary>
    /// 是否包含纹理数据（对于无纹理网格，始终返回false）
    /// </summary>
    public bool HasTexture => false;

    /// <summary>
    /// 纹理顶点列表（对于无纹理网格，返回null）
    /// </summary>
    public IReadOnlyList<Vertex2>? TextureVertices => null;

    /// <summary>
    /// 材质列表（对于无纹理网格，返回null）
    /// </summary>
    public IReadOnlyList<Materials.Material>? Materials => null;

    /// <summary>
    /// 纹理处理策略（对于无纹理网格，此属性无实际作用）
    /// </summary>
    public TexturesStrategy TexturesStrategy { get; set; }

    /// <summary>
    /// 打包材质：移除未使用的顶点和UV，并重新打包纹理
    /// 此方法会就地修改当前 mesh，避免文件I/O开销
    /// </summary>
    /// <param name="removeUnused"是否移除未使用的顶点</param>
    /// <returns>返回当前 mesh 实例</returns>
    public IMesh PackMaterials(bool removeUnused = true)
    {
        // 无纹理网格仅移除未使用的顶点
        RemoveUnusedVertices();
        return this;
    }
}
