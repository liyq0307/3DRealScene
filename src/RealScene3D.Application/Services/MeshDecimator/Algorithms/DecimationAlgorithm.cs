using RealScene3D.Domain.Geometry;
using RealScene3D.Domain.Utils;

namespace RealScene3D.Application.Services.MeshDecimator.Algorithms;

/// <summary>
/// 网格简化算法。
/// 
/// 子类必须实现初始化、简化和转换为网格的方法。
/// </summary>
public abstract class DecimationAlgorithm
{
    /// <summary>
    /// 简化状态报告的回调。
    /// </summary>
    /// <param name="iteration">当前迭代，从零开始。</param>
    /// <param name="originalTris">原始三角形数量。</param>
    /// <param name="currentTris">当前三角形数量。</param>
    /// <param name="targetTris">目标三角形数量。</param>
    public delegate void StatusReportCallback(int iteration, int originalTris, int currentTris, int targetTris);

    /// <summary>
    /// 最大顶点数量。
    /// </summary>
    private int maxVertexCount = 0;

    /// <summary>
    /// 状态报告事件的调用者。
    /// </summary>
    private StatusReportCallback? statusReportInvoker = null;

    /// <summary>
    /// 获取或设置是否应保留边界。
    /// 默认值：false
    /// </summary>
    public bool PreserveBorders { get; set; } = false;

    /// <summary>
    /// 获取或设置最大顶点数量。设为零表示无限制。
    /// 默认值：0（无限制）
    /// </summary>
    public int MaxVertexCount
    {
        get => maxVertexCount;
        set => maxVertexCount = MathHelper.Max(value, 0);
    }

    /// <summary>
    /// 获取或设置是否应在控制台中打印详细信息。
    /// 默认值：false
    /// </summary>
    public bool Verbose { get; set; } = false;

    /// <summary>
    /// 此算法的状态报告事件。
    /// </summary>
    public event StatusReportCallback StatusReport
    {
        add { statusReportInvoker += value; }
        remove { statusReportInvoker -= value; }
    }

    /// <summary>
    /// 报告简化的当前状态。
    /// </summary>
    /// <param name="iteration">当前迭代，从零开始。</param>
    /// <param name="originalTris">原始三角形数量。</param>
    /// <param name="currentTris">当前三角形数量。</param>
    /// <param name="targetTris">目标三角形数量。</param>
    protected void ReportStatus(int iteration, int originalTris, int currentTris, int targetTris)
    {
        var statusReportInvoker = this.statusReportInvoker;
        if (statusReportInvoker != null)
        {
            statusReportInvoker.Invoke(iteration, originalTris, currentTris, targetTris);
        }
    }

    /// <summary>
    /// 使用原始网格初始化算法。
    /// </summary>
    /// <param name="mesh">网格。</param>
    public abstract void Initialize(SimpleMesh mesh);

    /// <summary>
    /// 简化网格。
    /// </summary>
    /// <param name="targetTrisCount">目标三角形数量。</param>
    public abstract void DecimateMesh(int targetTrisCount);

    /// <summary>
    /// 在不损失任何质量的情况下简化网格。
    /// </summary>
    public abstract void DecimateMeshLossless();

    /// <summary>
    /// 返回结果网格。
    /// </summary>
    /// <returns>结果网格。</returns>
    public abstract SimpleMesh ToMesh();

    /// <summary>
    /// 使用原始 MeshT 网格初始化算法。
    /// </summary>
    /// <param name="mesh">MeshT 网格。</param>
    public abstract void Initialize(MeshT mesh);

    /// <summary>
    /// 返回结果 MeshT 网格。
    /// </summary>
    /// <param name="originalMesh">原始 MeshT,用于保留材质信息。</param>
    /// <returns>结果 MeshT 网格。</returns>
    public abstract MeshT ToMeshT(MeshT? originalMesh = null);
}