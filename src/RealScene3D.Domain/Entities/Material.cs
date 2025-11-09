namespace RealScene3D.Domain.Entities;

/// <summary>
/// 材质定义 - 支持Wavefront MTL和glTF PBR材质
/// 存储材质的所有属性和纹理引用
/// </summary>
public class Material
{
    /// <summary>
    /// 材质名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 环境光颜色 (Ka)
    /// </summary>
    public Color3D? AmbientColor { get; set; }

    /// <summary>
    /// 漫反射颜色 (Kd)
    /// </summary>
    public Color3D? DiffuseColor { get; set; }

    /// <summary>
    /// 镜面反射颜色 (Ks)
    /// </summary>
    public Color3D? SpecularColor { get; set; }

    /// <summary>
    /// 自发光颜色 (Ke)
    /// </summary>
    public Color3D? EmissiveColor { get; set; }

    /// <summary>
    /// 光泽度/高光指数 (Ns) - 范围 [0, 1000]
    /// </summary>
    public double Shininess { get; set; } = 0.0;

    /// <summary>
    /// 透明度 (d 或 Tr) - 范围 [0, 1]，1为完全不透明
    /// </summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// 折射率 (Ni) - 玻璃通常为1.5
    /// </summary>
    public double RefractiveIndex { get; set; } = 1.0;

    /// <summary>
    /// 漫反射纹理 (map_Kd)
    /// </summary>
    public TextureInfo? DiffuseTexture { get; set; }

    /// <summary>
    /// 法线贴图 (map_Bump 或 bump)
    /// </summary>
    public TextureInfo? NormalTexture { get; set; }

    /// <summary>
    /// 镜面反射纹理 (map_Ks)
    /// </summary>
    public TextureInfo? SpecularTexture { get; set; }

    /// <summary>
    /// 自发光纹理 (map_Ke)
    /// </summary>
    public TextureInfo? EmissiveTexture { get; set; }

    /// <summary>
    /// 透明度贴图 (map_d)
    /// </summary>
    public TextureInfo? OpacityTexture { get; set; }

    /// <summary>
    /// 金属度贴图 (map_Pm) - PBR工作流
    /// </summary>
    public TextureInfo? MetallicTexture { get; set; }

    /// <summary>
    /// 粗糙度贴图 (map_Pr) - PBR工作流
    /// </summary>
    public TextureInfo? RoughnessTexture { get; set; }

    /// <summary>
    /// 获取材质的所有纹理
    /// </summary>
    public IEnumerable<TextureInfo> GetAllTextures()
    {
        if (DiffuseTexture != null) yield return DiffuseTexture;
        if (NormalTexture != null) yield return NormalTexture;
        if (SpecularTexture != null) yield return SpecularTexture;
        if (EmissiveTexture != null) yield return EmissiveTexture;
        if (OpacityTexture != null) yield return OpacityTexture;
        if (MetallicTexture != null) yield return MetallicTexture;
        if (RoughnessTexture != null) yield return RoughnessTexture;
    }

    /// <summary>
    /// 检查材质是否包含纹理
    /// </summary>
    public bool HasTextures() => GetAllTextures().Any();
}

/// <summary>
/// 纹理信息 - 存储纹理文件路径和参数
/// </summary>
public class TextureInfo
{
    /// <summary>
    /// 纹理类型
    /// </summary>
    public TextureType Type { get; set; }

    /// <summary>
    /// 纹理文件路径（相对或绝对）
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// U轴纹理包裹模式
    /// </summary>
    public TextureWrapMode WrapU { get; set; } = TextureWrapMode.Repeat;

    /// <summary>
    /// V轴纹理包裹模式
    /// </summary>
    public TextureWrapMode WrapV { get; set; } = TextureWrapMode.Repeat;

    /// <summary>
    /// 纹理过滤模式
    /// </summary>
    public TextureFilterMode FilterMode { get; set; } = TextureFilterMode.Linear;

    /// <summary>
    /// 纹理在图集中的UV偏移（用于纹理图集）
    /// </summary>
    public Vector2D? AtlasOffset { get; set; }

    /// <summary>
    /// 纹理在图集中的UV缩放（用于纹理图集）
    /// </summary>
    public Vector2D? AtlasScale { get; set; }
}

/// <summary>
/// 纹理类型枚举
/// </summary>
public enum TextureType
{
    Diffuse = 0,    // 漫反射/基础颜色
    Normal = 1,     // 法线贴图
    Specular = 2,   // 镜面反射
    Emissive = 3,   // 自发光
    Opacity = 4,    // 透明度
    Metallic = 5,   // 金属度
    Roughness = 6,  // 粗糙度
    Occlusion = 7   // 环境光遮蔽
}

/// <summary>
/// 纹理包裹模式
/// </summary>
public enum TextureWrapMode
{
    Repeat = 0,        // 重复
    ClampToEdge = 1,   // 边缘拉伸
    MirroredRepeat = 2 // 镜像重复
}

/// <summary>
/// 纹理过滤模式
/// </summary>
public enum TextureFilterMode
{
    Nearest = 0,  // 最近邻
    Linear = 1,   // 线性
    Mipmap = 2    // Mipmap
}

/// <summary>
/// 颜色 - RGB三通道
/// </summary>
public class Color3D
{
    public double R { get; set; }
    public double G { get; set; }
    public double B { get; set; }

    public Color3D() { }

    public Color3D(double r, double g, double b)
    {
        R = r;
        G = g;
        B = b;
    }

    /// <summary>
    /// 转换为字节数组 (0-255)
    /// </summary>
    public byte[] ToBytes()
    {
        return new[]
        {
            (byte)Math.Clamp(R * 255, 0, 255),
            (byte)Math.Clamp(G * 255, 0, 255),
            (byte)Math.Clamp(B * 255, 0, 255)
        };
    }

    /// <summary>
    /// 从字节数组创建 (0-255)
    /// </summary>
    public static Color3D FromBytes(byte r, byte g, byte b)
    {
        return new Color3D(r / 255.0, g / 255.0, b / 255.0);
    }
}
