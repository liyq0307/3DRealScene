namespace RealScene3D.Domain.Geometry;

/// <summary>
/// 面类，支持带纹理和不带纹理两种模式
/// </summary>
public class Face
{
    /// <summary>
    /// 第一个顶点的索引
    /// </summary>
    public int IndexA;

    /// <summary>
    /// 第二个顶点的索引
    /// </summary>
    public int IndexB;

    /// <summary>
    /// 第三个顶点的索引
    /// </summary>
    public int IndexC;

    /// <summary>
    /// 第一个顶点的纹理坐标索引（默认为0）
    /// </summary>
    public int TextureIndexA;

    /// <summary>
    /// 第二个顶点的纹理坐标索引（默认为0）
    /// </summary>
    public int TextureIndexB;

    /// <summary>
    /// 第三个顶点的纹理坐标索引（默认为0）
    /// </summary>
    public int TextureIndexC;

    /// <summary>
    /// 材质索引（默认为0）
    /// </summary>
    public int MaterialIndex;

    /// <summary>
    /// 判断是否包含纹理信息（通过检查是否使用了带纹理的构造函数）
    /// </summary>
    private bool _hasTexture;

    /// <summary>
    /// 判断是否包含纹理信息
    /// </summary>
    public bool HasTexture => _hasTexture;

    /// <summary>
    /// 构造不带纹理的面
    /// </summary>
    /// <param name="indexA">第一个顶点的索引</param>
    /// <param name="indexB">第二个顶点的索引</param>
    /// <param name="indexC">第三个顶点的索引</param>
    public Face(int indexA, int indexB, int indexC)
    {
        IndexA = indexA;
        IndexB = indexB;
        IndexC = indexC;
        TextureIndexA = -1;
        TextureIndexB = -1;
        TextureIndexC = -1;
        MaterialIndex = -1;
        _hasTexture = false;
    }

    /// <summary>
    /// 构造带纹理的面
    /// </summary>
    /// <param name="indexA">第一个顶点的索引</param>
    /// <param name="indexB">第二个顶点的索引</param>
    /// <param name="indexC">第三个顶点的索引</param>
    /// <param name="textureIndexA">第一个顶点的纹理坐标索引</param>
    /// <param name="textureIndexB">第二个顶点的纹理坐标索引</param>
    /// <param name="textureIndexC">第三个顶点的纹理坐标索引</param>
    /// <param name="materialIndex">材质索引</param>
    public Face(int indexA, int indexB, int indexC,
        int textureIndexA, int textureIndexB, int textureIndexC, int materialIndex)
    {
        IndexA = indexA;
        IndexB = indexB;
        IndexC = indexC;
        TextureIndexA = textureIndexA;
        TextureIndexB = textureIndexB;
        TextureIndexC = textureIndexC;
        MaterialIndex = materialIndex;
        _hasTexture = true;
    }

    /// <summary>
    /// 将面转换为字符串表示
    /// </summary>
    /// <returns>字符串表示</returns>
    public override string ToString()
    {
        if (HasTexture)
        {
            return $"{IndexA} {IndexB} {IndexC} | {TextureIndexA} {TextureIndexB} {TextureIndexC} | {MaterialIndex}";
        }

        return $"{IndexA} {IndexB} {IndexC}";
    }

    /// <summary>
    /// 将面转换为OBJ格式字符串
    /// </summary>
    /// <returns>OBJ格式字符串</returns>
    public string ToObj()
    {
        if (HasTexture)
        {
            return $"f {IndexA + 1}/{TextureIndexA + 1} {IndexB + 1}/{TextureIndexB + 1} {IndexC + 1}/{TextureIndexC + 1}";
        }

        return $"f {IndexA + 1} {IndexB + 1} {IndexC + 1}";
    }
}