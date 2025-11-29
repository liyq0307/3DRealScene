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
    /// 从 MTL 文件读取材质信息
    /// </summary>
    /// <param name="path">MTL 文件路径</param>
    /// <param name="dependencies">输出参数，返回材质依赖的文件路径数组</param>
    /// <returns>材质数组</returns>
    public static Material[] ReadMtl(string path, out string[] dependencies)
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

        foreach (var line in lines)
        {
            if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                continue;

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

        materials.Add(new Material(name, texture, normalMap, ambientColor, diffuseColor, specularColor, specularExponent, dissolve,
            illuminationModel));

        dependencies = deps.ToArray();

        return materials.ToArray();
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
    /// </summary>
    /// <returns>克隆的材质对象</returns>
    public object Clone()
    {
        return new Material(
            Name,
            Texture,
            NormalMap,
            AmbientColor,
            DiffuseColor,
            SpecularColor,
            SpecularExponent,
            Dissolve,
            IlluminationModel);
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