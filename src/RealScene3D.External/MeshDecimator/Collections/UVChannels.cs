namespace MeshDecimator.Collections
{
    /// <summary>
    /// UV 通道集合。
    /// </summary>
    /// <typeparam name="TVec">UV 向量类型。</typeparam>
    internal sealed class UVChannels<TVec>
    {
        #region 字段
        private ResizableArray<TVec>?[] channels = null!;
        private TVec?[][] channelsData = null!;
        #endregion

        #region 属性
        /// <summary>
        /// 获取通道集合数据。
        /// </summary>
        public TVec?[][] Data
        {
            get
            {
                for (int i = 0; i < Mesh.UVChannelCount; i++)
                {
                    if (channels[i] != null)
                    {
                        channelsData[i] = channels[i]!.Data;
                    }
                    else
                    {
                        channelsData[i] = null!;
                    }
                }
                return channelsData;
            }
        }

        /// <summary>
        /// 获取或设置特定索引处的通道。
        /// </summary>
        /// <param name="index">通道索引。</param>
        public ResizableArray<TVec>? this[int index]
        {
            get { return channels[index]; }
            set { channels[index] = value; }
        }
        #endregion

        #region 构造函数
        /// <summary>
        /// 创建一个新的 UV 通道集合。
        /// </summary>
        public UVChannels()
        {
            channels = new ResizableArray<TVec>?[Mesh.UVChannelCount];
            channelsData = new TVec?[Mesh.UVChannelCount][];
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 一次调整所有通道的大小。
        /// </summary>
        /// <param name="capacity">新容量。</param>
        /// <param name="trimExess">是否应修剪多余内存。</param>
        public void Resize(int capacity, bool trimExess = false)
        {
            for (int i = 0; i < Mesh.UVChannelCount; i++)
            {
                if (channels[i] != null)
                {
                    channels[i]!.Resize(capacity, trimExess);
                }
            }
        }
        #endregion
    }
}
