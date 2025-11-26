namespace MeshDecimator.Algorithms
{
    /// <summary>
    /// 网格简化算法。
    /// </summary>
    public abstract class DecimationAlgorithm
    {
        #region 委托
        /// <summary>
        /// 简化状态报告的回调。
        /// </summary>
        /// <param name="iteration">当前迭代，从零开始。</param>
        /// <param name="originalTris">原始三角形数量。</param>
        /// <param name="currentTris">当前三角形数量。</param>
        /// <param name="targetTris">目标三角形数量。</param>
        public delegate void StatusReportCallback(int iteration, int originalTris, int currentTris, int targetTris);
        #endregion

        #region 字段

        private int maxVertexCount = 0;

        private StatusReportCallback? statusReportInvoker = null;
        #endregion

        #region 属性

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
            set => maxVertexCount = Math.MathHelper.Max(value, 0);
        }

        /// <summary>
        /// 获取或设置是否应在控制台中打印详细信息。
        /// 默认值：false
        /// </summary>
        public bool Verbose { get; set; } = false;

        #endregion

        #region 事件
        /// <summary>
        /// 此算法的状态报告事件。
        /// </summary>
        public event StatusReportCallback StatusReport
        {
            add { statusReportInvoker += value; }
            remove { statusReportInvoker -= value; }
        }
        #endregion

        #region 受保护的方法
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
        #endregion

        #region 公共方法
        /// <summary>
        /// 使用原始网格初始化算法。
        /// </summary>
        /// <param name="mesh">网格。</param>
        public abstract void Initialize(Mesh mesh);

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
        public abstract Mesh ToMesh();
        #endregion
    }
}