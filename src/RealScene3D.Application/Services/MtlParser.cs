using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using System.Globalization;
using System.Text;

namespace RealScene3D.Application.Services;

/// <summary>
/// MTL文件解析器 - 解析Wavefront MTL材质文件
/// 支持标准MTL属性和纹理贴图
/// 参考: http://paulbourke.net/dataformats/mtl/
/// </summary>
public class MtlParser
{
    private readonly ILogger<MtlParser> _logger;

    public MtlParser(ILogger<MtlParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 解析MTL文件并提取所有材质定义
    /// </summary>
    /// <param name="mtlPath">MTL文件路径</param>
    /// <param name="basePath">纹理文件的基础路径（通常是MTL文件所在目录）</param>
    /// <returns>材质名称到材质对象的字典</returns>
    public async Task<Dictionary<string, Material>> ParseMtlFileAsync(
        string mtlPath,
        string? basePath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始解析MTL文件: {Path}", mtlPath);

        if (!File.Exists(mtlPath))
        {
            _logger.LogWarning("MTL文件不存在: {Path}", mtlPath);
            return new Dictionary<string, Material>();
        }

        basePath ??= Path.GetDirectoryName(mtlPath) ?? string.Empty;

        var materials = new Dictionary<string, Material>();
        Material? currentMaterial = null;
        int lineNumber = 0;

        try
        {
            using var reader = new StreamReader(mtlPath, Encoding.UTF8);
            string? line;

            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                lineNumber++;
                line = line.Trim();

                // 跳过空行和注释
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                try
                {
                    switch (parts[0].ToLowerInvariant())
                    {
                        case "newmtl": // 新材质定义
                            if (parts.Length > 1)
                            {
                                var materialName = string.Join(" ", parts.Skip(1));
                                currentMaterial = new Material { Name = materialName };
                                materials[materialName] = currentMaterial;
                                _logger.LogDebug("发现材质: {Name}", materialName);
                            }
                            break;

                        case "ka": // 环境光颜色
                            if (currentMaterial != null && parts.Length >= 4)
                            {
                                currentMaterial.AmbientColor = ParseColor(parts);
                            }
                            break;

                        case "kd": // 漫反射颜色
                            if (currentMaterial != null && parts.Length >= 4)
                            {
                                currentMaterial.DiffuseColor = ParseColor(parts);
                            }
                            break;

                        case "ks": // 镜面反射颜色
                            if (currentMaterial != null && parts.Length >= 4)
                            {
                                currentMaterial.SpecularColor = ParseColor(parts);
                            }
                            break;

                        case "ke": // 自发光颜色
                            if (currentMaterial != null && parts.Length >= 4)
                            {
                                currentMaterial.EmissiveColor = ParseColor(parts);
                            }
                            break;

                        case "ns": // 高光指数
                            if (currentMaterial != null && parts.Length >= 2)
                            {
                                currentMaterial.Shininess = double.Parse(parts[1], CultureInfo.InvariantCulture);
                            }
                            break;

                        case "d": // 透明度
                            if (currentMaterial != null && parts.Length >= 2)
                            {
                                currentMaterial.Opacity = double.Parse(parts[1], CultureInfo.InvariantCulture);
                            }
                            break;

                        case "tr": // 透明度（另一种表示）
                            if (currentMaterial != null && parts.Length >= 2)
                            {
                                // Tr = 1 - d
                                currentMaterial.Opacity = 1.0 - double.Parse(parts[1], CultureInfo.InvariantCulture);
                            }
                            break;

                        case "ni": // 折射率
                            if (currentMaterial != null && parts.Length >= 2)
                            {
                                currentMaterial.RefractiveIndex = double.Parse(parts[1], CultureInfo.InvariantCulture);
                            }
                            break;

                        case "map_kd": // 漫反射纹理
                            if (currentMaterial != null && parts.Length >= 2)
                            {
                                var texturePath = ParseTexturePath(parts, basePath);
                                currentMaterial.DiffuseTexture = new TextureInfo
                                {
                                    Type = TextureType.Diffuse,
                                    FilePath = texturePath
                                };
                            }
                            break;

                        case "map_ks": // 镜面反射纹理
                            if (currentMaterial != null && parts.Length >= 2)
                            {
                                var texturePath = ParseTexturePath(parts, basePath);
                                currentMaterial.SpecularTexture = new TextureInfo
                                {
                                    Type = TextureType.Specular,
                                    FilePath = texturePath
                                };
                            }
                            break;

                        case "map_ke": // 自发光纹理
                            if (currentMaterial != null && parts.Length >= 2)
                            {
                                var texturePath = ParseTexturePath(parts, basePath);
                                currentMaterial.EmissiveTexture = new TextureInfo
                                {
                                    Type = TextureType.Emissive,
                                    FilePath = texturePath
                                };
                            }
                            break;

                        case "map_d": // 透明度纹理
                            if (currentMaterial != null && parts.Length >= 2)
                            {
                                var texturePath = ParseTexturePath(parts, basePath);
                                currentMaterial.OpacityTexture = new TextureInfo
                                {
                                    Type = TextureType.Opacity,
                                    FilePath = texturePath
                                };
                            }
                            break;

                        case "map_bump":
                        case "bump": // 法线贴图/凹凸贴图
                            if (currentMaterial != null && parts.Length >= 2)
                            {
                                var texturePath = ParseTexturePath(parts, basePath);
                                currentMaterial.NormalTexture = new TextureInfo
                                {
                                    Type = TextureType.Normal,
                                    FilePath = texturePath
                                };
                            }
                            break;

                        case "map_pm": // 金属度贴图（PBR扩展）
                            if (currentMaterial != null && parts.Length >= 2)
                            {
                                var texturePath = ParseTexturePath(parts, basePath);
                                currentMaterial.MetallicTexture = new TextureInfo
                                {
                                    Type = TextureType.Metallic,
                                    FilePath = texturePath
                                };
                            }
                            break;

                        case "map_pr": // 粗糙度贴图（PBR扩展）
                            if (currentMaterial != null && parts.Length >= 2)
                            {
                                var texturePath = ParseTexturePath(parts, basePath);
                                currentMaterial.RoughnessTexture = new TextureInfo
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
                    _logger.LogWarning("解析第{LineNumber}行时出错: {Error}, 行内容: {Line}",
                        lineNumber, ex.Message, line);
                }
            }

            _logger.LogInformation("MTL文件解析完成: 共{Count}个材质", materials.Count);
            return materials;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析MTL文件失败: {Path}", mtlPath);
            throw;
        }
    }

    /// <summary>
    /// 解析颜色值
    /// </summary>
    private Color3D ParseColor(string[] parts)
    {
        return new Color3D
        {
            R = double.Parse(parts[1], CultureInfo.InvariantCulture),
            G = double.Parse(parts[2], CultureInfo.InvariantCulture),
            B = double.Parse(parts[3], CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// 解析纹理路径
    /// 支持选项参数（-blendu, -blendv, -cc等）
    /// </summary>
    private string ParseTexturePath(string[] parts, string basePath)
    {
        // 跳过选项参数，找到实际的文件名
        string fileName = parts[^1]; // 最后一个参数通常是文件名

        // 处理选项（如 -blendu on -blendv on texture.png）
        for (int i = parts.Length - 1; i >= 1; i--)
        {
            if (!parts[i].StartsWith("-"))
            {
                fileName = parts[i];
                break;
            }
        }

        // 如果是相对路径，与基础路径组合
        if (!Path.IsPathRooted(fileName))
        {
            fileName = Path.Combine(basePath, fileName);
        }

        return fileName;
    }
}
