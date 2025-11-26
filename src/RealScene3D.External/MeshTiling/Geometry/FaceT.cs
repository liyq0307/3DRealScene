namespace RealScene3D.MeshTiling.Geometry;

/// <summary>
/// 带有纹理坐标的面类，继承自Face
/// </summary>
public class FaceT : Face
{
    /// <summary>
    /// 第一个顶点的纹理坐标索引
    /// </summary>
    public int TextureIndexA;
    /// <summary>
    /// 第二个顶点的纹理坐标索引
    /// </summary>
    public int TextureIndexB;
    /// <summary>
    /// 第三个顶点的纹理坐标索引
    /// </summary>
    public int TextureIndexC;

    /// <summary>
    /// 材质索引
    /// </summary>
    public int MaterialIndex;

    /// <summary>
    /// 将带有纹理的面转换为字符串表示
    /// </summary>
    /// <returns>字符串表示</returns>
    public override string ToString()
    {
        return $"{IndexA} {IndexB} {IndexC} | {TextureIndexA} {TextureIndexB} {TextureIndexC} | {MaterialIndex}";
    }

    /// <summary>
    /// 使用顶点索引、纹理坐标索引和材质索引构造带有纹理的面
    /// </summary>
    /// <param name="indexA">第一个顶点的索引</param>
    /// <param name="indexB">第二个顶点的索引</param>
    /// <param name="indexC">第三个顶点的索引</param>
    /// <param name="textureIndexA">第一个顶点的纹理坐标索引</param>
    /// <param name="textureIndexB">第二个顶点的纹理坐标索引</param>
    /// <param name="textureIndexC">第三个顶点的纹理坐标索引</param>
    /// <param name="materialIndex">材质索引</param>
    public FaceT(int indexA, int indexB, int indexC, int textureIndexA, int textureIndexB,
        int textureIndexC, int materialIndex) : base(indexA, indexB, indexC)
    {

        TextureIndexA = textureIndexA;
        TextureIndexB = textureIndexB;
        TextureIndexC = textureIndexC;

        MaterialIndex = materialIndex;
    }

    /// <summary>
    /// 将带有纹理的面转换为OBJ格式字符串
    /// </summary>
    /// <returns>OBJ格式字符串</returns>
    public override string ToObj()
    {
        return $"f {IndexA + 1}/{TextureIndexA + 1} {IndexB + 1}/{TextureIndexB + 1} {IndexC + 1}/{TextureIndexC + 1}";
    }
}