namespace RealScene3D.Domain.Utils;

/// <summary>
/// 扩展方法类，提供集合和字典的扩展功能。
/// </summary>
public static class Extenders
{
    /// <summary>
    /// 将项目添加到集合中并返回该项目的索引。
    /// </summary>
    /// <typeparam name="T">项目的类型。</typeparam>
    /// <param name="collection">要添加项目的集合。</param>
    /// <param name="item">要添加的项目。</param>
    /// <returns>添加项目的索引。</returns>
    public static int AddIndex<T>(this ICollection<T> collection, T item)
    {
        collection.Add(item);
        return collection.Count - 1;
    }

    /// <summary>
    /// 将项目添加到字典中，如果项目已存在则返回现有索引，否则添加并返回新索引。
    /// </summary>
    /// <typeparam name="T">键的类型。</typeparam>
    /// <param name="dictionary">要添加项目的字典。</param>
    /// <param name="item">要添加的键。</param>
    /// <returns>项目的索引。</returns>
    public static int AddIndex<T>(this IDictionary<T, int> dictionary, T item)
    {

        // 如果项目已在字典中，返回索引
        if (dictionary.TryGetValue(item, out var index))
            return index;

        // 如果项目不在字典中，添加它并返回索引
        dictionary.Add(item, dictionary.Count);
        return dictionary.Count - 1;

    }
}