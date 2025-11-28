using RealScene3D.Domain.Enums;
using RealScene3D.Domain.Geometry;
using System.Globalization;
using System.Text;

namespace RealScene3D.Domain.Materials;

/// <summary>
/// 材质扩展类 - 支持Wavefront MTL和glTF PBR材质
/// 继承自 Material，添加更多高级属性和纹理引用
/// 参考：https://paulbourke.net/dataformats/mtl/
/// </summary>
public class MaterialEx : Material
{
    /// <summary>
    /// Ke - 自发光颜色
    /// </summary>
    public RGB? EmissiveColor { get; set; }

    /// <summary>
    /// Ni - 折射率
    /// </summary>
    public double? RefractiveIndex { get; set; }

    /// <summary>
    /// 漫反射纹理 (map_Kd) - 映射到基类的 Texture 属性
    /// </summary>
    public TextureInfo? DiffuseTexture { get; set; }

    /// <summary>
    /// 法线贴图 (map_Bump 或 bump) - 映射到基类的 NormalMap 属性
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
    /// 光泽度/高光指数 (Ns) - 映射到基类的 SpecularExponent
    /// </summary>
    public double Shininess
    {
        get => SpecularExponent ?? 0.0;
        set { }  // SpecularExponent 是 readonly，此处仅提供兼容性
    }

    /// <summary>
    /// 透明度 (d) - 映射到基类的 Dissolve
    /// </summary>
    public double Opacity
    {
        get => Dissolve ?? 1.0;
        set { }  // Dissolve 是 readonly，此处仅提供兼容性
    }

    /// <summary>
    /// 初始化扩展材质对象
    /// </summary>
    public MaterialEx(
        string name,
        string? texture = null,
        string? normalMap = null,
        RGB? ambientColor = null,
        RGB? diffuseColor = null,
        RGB? specularColor = null,
        RGB? emissiveColor = null,
        double? specularExponent = null,
        double? dissolve = null,
        double? refractiveIndex = null,
        IlluminationModel? illuminationModel = null)
        : base(name, texture, normalMap, ambientColor, diffuseColor, specularColor, specularExponent, dissolve, illuminationModel)
    {
        EmissiveColor = emissiveColor;
        RefractiveIndex = refractiveIndex;
    }

    /// <summary>
    /// 默认构造函数（用于对象初始化器）
    /// </summary>
    public MaterialEx() : base(string.Empty)
    {
    }

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

    /// <summary>
    /// 从 MTL 文件异步读取材质信息
    /// </summary>
    /// <param name="path">MTL 文件路径</param>
    /// <param name="basePath">纹理文件的基础路径（通常是MTL文件所在目录）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>材质名称到材质对象的字典</returns>
    public static async Task<Dictionary<string, MaterialEx>> ReadMtlAsync(
        string path,
        string? basePath = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, MaterialEx>();
        }

        basePath ??= Path.GetDirectoryName(path) ?? string.Empty;

        var materials = new Dictionary<string, MaterialEx>();

        // 临时存储当前材质的属性
        string? materialName = null;
        string? texture = null;
        string? normalMap = null;
        RGB? ambientColor = null, diffuseColor = null, specularColor = null, emissiveColor = null;
        double? specularExponent = null, dissolve = null, refractiveIndex = null;
        IlluminationModel? illuminationModel = null;
        TextureInfo? diffuseTexture = null, normalTexture = null, specularTexture = null;
        TextureInfo? emissiveTexture = null, opacityTexture = null, metallicTexture = null, roughnessTexture = null;

        using var reader = new StreamReader(path, Encoding.UTF8);
        string? line;
        int lineNumber = 0;

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            lineNumber++;
            line = line.Trim();

            // 跳过空行和注释
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            try
            {
                switch (parts[0].ToLowerInvariant())
                {
                    case "newmtl": // 新材质定义
                        // 保存之前的材质
                        if (materialName != null)
                        {
                            materials.Add(materialName, new MaterialEx(
                                materialName, texture, normalMap, ambientColor, diffuseColor,
                                specularColor, emissiveColor, specularExponent, dissolve,
                                refractiveIndex, illuminationModel));
                        }

                        // 重置属性准备解析新材质
                        if (parts.Length > 1)
                        {
                            materialName = string.Join(" ", parts.Skip(1));
                            texture = null;
                            normalMap = null;
                            ambientColor = null;
                            diffuseColor = null;
                            specularColor = null;
                            emissiveColor = null;
                            specularExponent = null;
                            dissolve = null;
                            refractiveIndex = null;
                            illuminationModel = null;
                            diffuseTexture = null;
                            normalTexture = null;
                            specularTexture = null;
                            emissiveTexture = null;
                            opacityTexture = null;
                            metallicTexture = null;
                            roughnessTexture = null;
                        }
                        break;

                    case "ka": // 环境光颜色
                        if (parts.Length >= 4)
                        {
                            ambientColor = ParseColor(parts);
                        }
                        break;

                    case "kd": // 漫反射颜色
                        if (parts.Length >= 4)
                        {
                            diffuseColor = ParseColor(parts);
                        }
                        break;

                    case "ks": // 镜面反射颜色
                        if (parts.Length >= 4)
                        {
                            specularColor = ParseColor(parts);
                        }
                        break;

                    case "ke": // 自发光颜色
                        if (parts.Length >= 4)
                        {
                            emissiveColor = ParseColor(parts);
                        }
                        break;

                    case "ns": // 高光指数
                        if (parts.Length >= 2)
                        {
                            specularExponent = double.Parse(parts[1], CultureInfo.InvariantCulture);
                        }
                        break;

                    case "d": // 透明度
                        if (parts.Length >= 2)
                        {
                            dissolve = double.Parse(parts[1], CultureInfo.InvariantCulture);
                        }
                        break;

                    case "tr": // 透明度（另一种表示）
                        if (parts.Length >= 2)
                        {
                            // Tr = 1 - d
                            dissolve = 1.0 - double.Parse(parts[1], CultureInfo.InvariantCulture);
                        }
                        break;

                    case "ni": // 折射率
                        if (parts.Length >= 2)
                        {
                            refractiveIndex = double.Parse(parts[1], CultureInfo.InvariantCulture);
                        }
                        break;

                    case "illum": // 光照模型
                        if (parts.Length >= 2)
                        {
                            illuminationModel = (IlluminationModel)int.Parse(parts[1]);
                        }
                        break;

                    case "map_kd": // 漫反射纹理
                        if (parts.Length >= 2)
                        {
                            var texturePath = ParseTexturePath(parts, basePath);
                            texture = texturePath;
                            diffuseTexture = new TextureInfo
                            {
                                Type = TextureType.Diffuse,
                                FilePath = texturePath
                            };
                        }
                        break;

                    case "map_ks": // 镜面反射纹理
                        if (parts.Length >= 2)
                        {
                            var texturePath = ParseTexturePath(parts, basePath);
                            specularTexture = new TextureInfo
                            {
                                Type = TextureType.Specular,
                                FilePath = texturePath
                            };
                        }
                        break;

                    case "map_ke": // 自发光纹理
                        if (parts.Length >= 2)
                        {
                            var texturePath = ParseTexturePath(parts, basePath);
                            emissiveTexture = new TextureInfo
                            {
                                Type = TextureType.Emissive,
                                FilePath = texturePath
                            };
                        }
                        break;

                    case "map_d": // 透明度纹理
                        if (parts.Length >= 2)
                        {
                            var texturePath = ParseTexturePath(parts, basePath);
                            opacityTexture = new TextureInfo
                            {
                                Type = TextureType.Opacity,
                                FilePath = texturePath
                            };
                        }
                        break;

                    case "map_bump":
                    case "bump":
                    case "norm": // 法线贴图/凹凸贴图
                        if (parts.Length >= 2)
                        {
                            var texturePath = ParseTexturePath(parts, basePath);
                            normalMap = texturePath;
                            normalTexture = new TextureInfo
                            {
                                Type = TextureType.Normal,
                                FilePath = texturePath
                            };
                        }
                        break;

                    case "map_pm": // 金属度贴图（PBR扩展）
                        if (parts.Length >= 2)
                        {
                            var texturePath = ParseTexturePath(parts, basePath);
                            metallicTexture = new TextureInfo
                            {
                                Type = TextureType.Metallic,
                                FilePath = texturePath
                            };
                        }
                        break;

                    case "map_pr": // 粗糙度贴图（PBR扩展）
                        if (parts.Length >= 2)
                        {
                            var texturePath = ParseTexturePath(parts, basePath);
                            roughnessTexture = new TextureInfo
                            {
                                Type = TextureType.Roughness,
                                FilePath = texturePath
                            };
                        }
                        break;

                    default:
                        // 其他属性暂时忽略
                        break;
                }
            }
            catch (Exception ex)
            {
                // 解析错误时静默忽略，继续处理后续行
                System.Diagnostics.Debug.WriteLine($"解析第{lineNumber}行时出错: {ex.Message}, 行内容: {line}");
            }
        }

        // 保存最后一个材质
        if (materialName != null)
        {
            var mat = new MaterialEx(
                materialName, texture, normalMap, ambientColor, diffuseColor,
                specularColor, emissiveColor, specularExponent, dissolve,
                refractiveIndex, illuminationModel)
            {
                DiffuseTexture = diffuseTexture,
                NormalTexture = normalTexture,
                SpecularTexture = specularTexture,
                EmissiveTexture = emissiveTexture,
                OpacityTexture = opacityTexture,
                MetallicTexture = metallicTexture,
                RoughnessTexture = roughnessTexture
            };
            materials[materialName] = mat;
        }

        return materials;
    }

    /// <summary>
    /// 解析纹理路径
    /// 支持选项参数（-blendu, -blendv, -cc等）
    /// 返回规范化的绝对路径
    /// </summary>
    private static string ParseTexturePath(string[] parts, string basePath)
    {
        // 跳过选项参数，找到实际的文件名
        string fileName = parts[^1]; // 最后一个参数通常是文件名

        // 处理选项（如 -blendu on -blendv on texture.png）
        for (int i = parts.Length - 1; i >= 1; i--)
        {
            if (!parts[i].StartsWith('-'))
            {
                fileName = parts[i];
                break;
            }
        }

        // 规范化路径：统一使用正斜杠
        fileName = fileName.Replace('\\', '/');

        // 如果是相对路径，与基础路径组合生成绝对路径
        string absolutePath;
        if (!Path.IsPathRooted(fileName))
        {
            absolutePath = Path.Combine(basePath, fileName);
        }
        else
        {
            absolutePath = fileName;
        }

        // 规范化路径（解析 .. 和 .）
        absolutePath = Path.GetFullPath(absolutePath);

        return absolutePath;
    }
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
    public Vector2d? AtlasOffset { get; set; }

    /// <summary>
    /// 纹理在图集中的UV缩放（用于纹理图集）
    /// </summary>
    public Vector2d? AtlasScale { get; set; }
}