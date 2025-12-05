using System.Collections.Concurrent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RealScene3D.Domain.Materials;

/// <summary>
/// 纹理缓存类，用于缓存和管理纹理图像
/// </summary>
public static class TexturesCache
{
    /// <summary>
    /// 纹理缓存字典，键为纹理名称，值为图像对象
    /// </summary>
    private static readonly ConcurrentDictionary<string, Image<Rgba32>> Textures = new();

    /// <summary>
    /// 获取或加载纹理图像
    /// </summary>
    /// <param name="textureName">纹理文件的路径或名称</param>
    /// <returns>纹理图像，如果文件不存在或加载失败则返回 null</returns>
    public static Image<Rgba32>? GetTexture(string textureName)
    {
        if (Textures.TryGetValue(textureName, out var txout))
            return txout;

        // 检查文件是否存在
        if (!File.Exists(textureName))
        {
            System.Diagnostics.Debug.WriteLine($"纹理文件不存在: {textureName}");
            return null;
        }

        try
        {
            var texture = Image.Load<Rgba32>(textureName);
            Textures.TryAdd(textureName, texture);
            return texture;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载纹理失败 {textureName}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 清空纹理缓存并释放所有纹理资源
    /// </summary>
    public static void Clear()
    {
        foreach(var texture in Textures)
        {
            texture.Value.Dispose();
        }
        Textures.Clear();
    }
}