namespace RealScene3D.MeshTiling.Geometry;

/// <summary>
/// 网格接口，定义网格的基本操作
/// </summary>
public interface IMesh
{
    /// <summary>
    /// 网格名称
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// 网格的包围盒
    /// </summary>
    Box3 Bounds { get; }

    /// <summary>
    /// 按指定轴分割网格
    /// </summary>
    /// <param name="utils">顶点工具</param>
    /// <param name="q">分割位置</param>
    /// <param name="left">左部分网格</param>
    /// <param name="right">右部分网格</param>
    /// <returns>分割结果</returns>
    int Split(IVertexUtils utils, double q, out IMesh left,
        out IMesh right);

    /// <summary>
    /// 获取顶点重心
    /// </summary>
    /// <returns>顶点重心</returns>
    Vertex3 GetVertexBaricenter();

    /// <summary>
    /// 将网格写入OBJ文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="removeUnused">是否移除未使用的顶点</param>
    void WriteObj(string path, bool removeUnused = true);

    /// <summary>
    /// 面的数量
    /// </summary>
    int FacesCount { get; }

    /// <summary>
    /// 顶点的数量
    /// </summary>
    int VertexCount { get; }
}