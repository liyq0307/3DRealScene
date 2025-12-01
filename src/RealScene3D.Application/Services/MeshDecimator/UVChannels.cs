namespace RealScene3D.Application.Services.MeshDecimator;
/// <summary>
/// UV 通道集合。
/// </summary>
/// <typeparam name="TVec">UV 向量类型。</typeparam>
internal sealed class UVChannels<TVec>
{
    /// <summary>
    /// 通道集合。
    /// </summary>
    private ResizableArray<TVec>?[] channels = null!;

    /// <summary>
    /// 通道集合数据。
    /// </summary>
    private TVec?[][] channelsData = null!;

    /// <summary>
    /// 获取通道集合数据。
    /// </summary>
    public TVec?[][] Data
    {
        get
        {
            for (int i = 0; i < SimpleMesh.UVChannelCount; i++)
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

    /// <summary>
    /// 创建一个新的 UV 通道集合。
    /// </summary>
    public UVChannels()
    {
        channels = new ResizableArray<TVec>?[SimpleMesh.UVChannelCount];
        channelsData = new TVec?[SimpleMesh.UVChannelCount][];
    }

    /// <summary>
    /// 一次调整所有通道的大小。
    /// </summary>
    /// <param name="capacity">新容量。</param>
    /// <param name="trimExess">是否应修剪多余内存。</param>
    public void Resize(int capacity, bool trimExess = false)
    {
        for (int i = 0; i < SimpleMesh.UVChannelCount; i++)
        {
            if (channels[i] != null)
            {
                channels[i]!.Resize(capacity, trimExess);
            }
        }
    }
}