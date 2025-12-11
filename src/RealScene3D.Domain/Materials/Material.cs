using System.Diagnostics;
using System.Text;
using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RealScene3D.Domain.Materials;

/// <summary>
/// 材质类
/// </summary>
public class Material : ICloneable
{
    /// <summary>
    /// 材质名称
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// 纹理文件路径
    /// </summary>
    public string? Texture;

    /// <summary>
    /// 法线贴图文件路径
    /// </summary>
    public string? NormalMap;

    /// <summary>
    /// 纹理图像数据（内存中）
    /// 如果不为 null，优先使用此数据而不是从 Texture 路径加载
    /// </summary>
    public Image<Rgba32>? TextureImage;

    /// <summary>
    /// 法线贴图图像数据（内存中）
    /// 如果不为 null，优先使用此数据而不是从 NormalMap 路径加载
    /// </summary>
    public Image<Rgba32>? NormalMapImage;

    /// <summary>
    /// 纹理是否已经过JPEG压缩
    /// true = JPEG压缩，false = PNG或原始格式
    /// </summary>
    public bool IsTextureCompressed { get; set; }

    /// <summary>
    /// Ka - 环境光颜色
    /// </summary>
    public readonly RGB? AmbientColor;

    /// <summary>
    /// Kd - 漫反射颜色
    /// </summary>
    public readonly RGB? DiffuseColor;

    /// <summary>
    /// Ks - 镜面反射颜色
    /// </summary>
    public readonly RGB? SpecularColor;

    /// <summary>
    /// Ns - 镜面反射指数
    /// </summary>
    public readonly double? SpecularExponent;

    /// <summary>
    /// d - 溶解度 / 透明度 (Tr = 1 - d)
    /// </summary>
    public readonly double? Dissolve;

    /// <summary>
    /// 光照模型
    /// </summary>
    public readonly IlluminationModel? IlluminationModel;

    /// <summary>
    /// 初始化材质对象
    /// </summary>
    /// <param name="name">材质名称</param>
    /// <param name="texture">纹理文件路径</param>
    /// <param name="normalMap">法线贴图文件路径</param>
    /// <param name="ambientColor">环境光颜色</param>
    /// <param name="diffuseColor">漫反射颜色</param>
    /// <param name="specularColor">镜面反射颜色</param>
    /// <param name="specularExponent">镜面反射指数</param>
    /// <param name="dissolve">溶解度/透明度</param>
    /// <param name="illuminationModel">光照模型</param>
    public Material(string name, string? texture = null, string? normalMap = null, RGB? ambientColor = null, RGB? diffuseColor = null,
        RGB? specularColor = null, double? specularExponent = null, double? dissolve = null,
        IlluminationModel? illuminationModel = null)
    {
        Name = name;
        Texture = texture;
        NormalMap = normalMap;
        AmbientColor = ambientColor;
        DiffuseColor = diffuseColor;
        SpecularColor = specularColor;
        SpecularExponent = specularExponent;
        Dissolve = dissolve;
        IlluminationModel = illuminationModel;
    }

    /// <summary>
    /// 从MTL文件异步读取材质列表
    /// </summary>
    /// <param name="path">MTL文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>材质数组和依赖的纹理文件路径数组</returns>
    /// <remarks>
    /// 支持解析以下MTL属性:
    ///     newmtl - 新材质名称
    ///     map_Kd - 漫反射纹理贴图
    ///     norm   - 法线贴图
    ///     Ka     - 环境光颜色
    ///     Kd     - 漫反射颜色
    ///     Ks     - 镜面反射颜色
    ///     Ns     - 镜面反射指数
    ///     d      - 溶解度/透明度
    ///     Tr     - 透明度（与d互补）
    ///     illum  - 光照模型
    /// </remarks>    
    public static async Task<(List<Material>, string[])> ReadMtlAsync(string path, CancellationToken cancellationToken = default)
    {
        var lines = File.ReadAllLines(path);
        var materials = new List<Material>();
        var deps = new List<string>();

        string? texture = null;
        string? normalMap = null;
        var name = string.Empty;
        RGB? ambientColor = null, diffuseColor = null, specularColor = null;
        double? specularExponent = null, dissolve = null;
        IlluminationModel? illuminationModel = null;

        string? line;
        int lineNumber = 0;
        using var reader = new StreamReader(path, Encoding.UTF8);

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            lineNumber++;
            if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var lineTrimmed = line.Trim();
                var parts = lineTrimmed.Split(' ');
                switch (parts[0])
                {
                    case "newmtl":

                        if (name.Length > 0)
                            materials.Add(new Material(name, texture, normalMap, ambientColor, diffuseColor, specularColor,
                                specularExponent, dissolve, illuminationModel));

                        name = parts[1];

                        break;
                    case "map_Kd":
                        texture = Path.IsPathRooted(parts[1])
                            ? parts[1]
                            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, parts[1]));

                        deps.Add(texture);

                        break;
                    case "norm":
                        normalMap = Path.IsPathRooted(parts[1])
                            ? parts[1]
                            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, parts[1]));

                        deps.Add(normalMap);

                        break;
                    case "Ka":
                        if (parts.Length >= 4)
                        {
                            ambientColor = ParseColor(parts);
                        }
                        break;
                    case "Kd":
                        if (parts.Length >= 4)
                        {
                            diffuseColor = ParseColor(parts);
                        }
                        break;
                    case "Ks":
                        if (parts.Length >= 4)
                        {
                            specularColor = ParseColor(parts);
                        }
                        break;
                    case "Ns":
                        specularExponent = double.Parse(parts[1], CultureInfo.InvariantCulture);
                        break;
                    case "d":
                        dissolve = double.Parse(parts[1], CultureInfo.InvariantCulture);
                        break;
                    case "Tr":
                        dissolve = 1 - double.Parse(parts[1], CultureInfo.InvariantCulture);
                        break;
                    case "illum":
                        illuminationModel = (IlluminationModel)int.Parse(parts[1]);
                        break;
                    default:
                        Debug.WriteLine($"Unknown line: '{line}'");
                        break;
                }
            }
            catch (Exception ex)
            {
                // 解析错误时静默忽略，继续处理后续行
                Debug.WriteLine($"解析MTL文件时第{lineNumber}出错: {ex.Message}, 行内容: {line}");
            }
        }

        materials.Add(new Material(
            name, texture, normalMap, ambientColor, diffuseColor,
            specularColor, specularExponent, dissolve, illuminationModel));

        return (materials, deps.ToArray());
    }

    /// <summary>
    /// 将材质转换为 MTL 格式字符串
    /// </summary>
    /// <returns>MTL 格式的材质定义字符串</returns>
    public string ToMtl()
    {
        var builder = new StringBuilder();

        builder.Append("newmtl ");
        builder.AppendLine(Name);

        if (Texture != null)
        {
            builder.Append("map_Kd ");
            builder.AppendLine(Texture.Replace('\\', '/'));
        }
        if (NormalMap != null)
        {
            builder.Append("norm ");
            builder.AppendLine(NormalMap.Replace('\\', '/'));
        }

        if (AmbientColor != null)
        {
            builder.Append("Ka ");
            builder.AppendLine(AmbientColor.ToString());
        }

        if (DiffuseColor != null)
        {
            builder.Append("Kd ");
            builder.AppendLine(DiffuseColor.ToString());
        }

        if (SpecularColor != null)
        {
            builder.Append("Ks ");
            builder.AppendLine(SpecularColor.ToString());
        }

        if (SpecularExponent != null)
        {
            builder.Append("Ns ");
            builder.AppendLine(SpecularExponent.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (Dissolve != null)
        {
            builder.Append("d ");
            builder.AppendLine(Dissolve.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (IlluminationModel != null)
        {
            builder.Append("illum ");
            builder.AppendLine(((int)IlluminationModel).ToString());
        }

        return builder.ToString();
    }

    /// <summary>
    /// 克隆材质对象
    /// 注意：会复制TextureImage和NormalMapImage的引用（浅拷贝），因为图像数据是只读的
    /// </summary>
    /// <returns>克隆的材质对象</returns>
    public object Clone()
    {
        var cloned = new Material(
            Name,
            Texture,
            NormalMap,
            AmbientColor,
            DiffuseColor,
            SpecularColor,
            SpecularExponent,
            Dissolve,
            IlluminationModel);

        // 关键修复：复制内存中的纹理图像引用
        // 使用浅拷贝是安全的，因为图像数据在切片过程中是只读的
        cloned.TextureImage = this.TextureImage;
        cloned.NormalMapImage = this.NormalMapImage;
        cloned.IsTextureCompressed = this.IsTextureCompressed;

        return cloned;
    }

    /// <summary>
    /// 解析颜色值
    /// </summary>
    public static RGB ParseColor(string[] parts)
    {
        return new RGB(
            double.Parse(parts[1], CultureInfo.InvariantCulture),
            double.Parse(parts[2], CultureInfo.InvariantCulture),
            double.Parse(parts[3], CultureInfo.InvariantCulture));
    }
}