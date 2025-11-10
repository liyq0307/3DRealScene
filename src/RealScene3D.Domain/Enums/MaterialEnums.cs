namespace RealScene3D.Domain.Enums;

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
/// 纹理过滤模式
/// </summary>
public enum TextureFilterMode
{
    Nearest = 0,  // 最近邻
    Linear = 1,   // 线性
    Mipmap = 2    // Mipmap
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
