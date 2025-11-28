namespace RealScene3D.Application.Services.MeshDecimator;

/// <summary>
/// 可调整大小的数组。
/// </summary>
/// <typeparam name="T">项目类型。</typeparam>
internal sealed class ResizableArray<T>
{
    /// <summary>
    /// 数组数据。
    /// </summary>
    private T[] items = null!;

    /// <summary>
    /// 数组长度。
    /// </summary>
    private int length = 0;

    /// <summary>
    /// 空数组。
    /// </summary>
    private static T[] emptyArr = [];

    /// <summary>
    /// 获取此数组的长度。
    /// </summary>
    public int Length
    {
        get { return length; }
    }

    /// <summary>
    /// 获取此数组的内部数据缓冲区。
    /// </summary>
    public T[] Data
    {
        get { return items; }
    }

    /// <summary>
    /// 获取或设置特定索引处的元素值。
    /// </summary>
    /// <param name="index">元素索引。</param>
    /// <returns>元素值。</returns>
    public T this[int index]
    {
        get { return items[index]; }
        set { items[index] = value; }
    }

    /// <summary>
    /// 创建一个新的可调整大小的数组。
    /// </summary>
    /// <param name="capacity">初始数组容量。</param>
    public ResizableArray(int capacity)
        : this(capacity, 0)
    {

    }

    /// <summary>
    /// 创建一个新的可调整大小的数组。
    /// </summary>
    /// <param name="capacity">初始数组容量。</param>
    /// <param name="length">数组的初始长度。</param>
    public ResizableArray(int capacity, int length)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException("capacity");
        else if (length < 0 || length > capacity)
            throw new ArgumentOutOfRangeException("length");

        if (capacity > 0)
            items = new T[capacity];
        else
            items = emptyArr;

        this.length = length;
    }

    private void IncreaseCapacity(int capacity)
    {
        T[] newItems = new T[capacity];
        Array.Copy(items, 0, newItems, 0, System.Math.Min(length, capacity));
        items = newItems;
    }

    /// <summary>
    /// 清除此数组。
    /// </summary>
    public void Clear()
    {
        Array.Clear(items, 0, length);
        length = 0;
    }

    /// <summary>
    /// 调整此数组的大小。
    /// </summary>
    /// <param name="length">新长度。</param>
    /// <param name="trimExess">是否应修剪多余的内存。</param>
    public void Resize(int length, bool trimExess = false)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException("capacity");

        if (length > items.Length)
        {
            IncreaseCapacity(length);
        }
        else if (length < this.length)
        {
            //Array.Clear(items, capacity, length - capacity);
        }

        this.length = length;

        if (trimExess)
        {
            TrimExcess();
        }
    }

    /// <summary>
    /// 修剪此数组的任何多余内存。
    /// </summary>
    public void TrimExcess()
    {
        if (items.Length == length) // Nothing to do
            return;

        T[] newItems = new T[length];
        Array.Copy(items, 0, newItems, 0, length);
        items = newItems;
    }

    /// <summary>
    /// 向此数组的末尾添加一个新项目。
    /// </summary>
    /// <param name="item">新项目。</param>
    public void Add(T item)
    {
        if (length >= items.Length)
        {
            IncreaseCapacity(items.Length << 1);
        }

        items[length++] = item;
    }
}