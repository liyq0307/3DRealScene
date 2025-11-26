using RealScene3D.MeshTiling.Geometry;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RealScene3D.MeshTiling;

public static class Common
{
    public static readonly double Epsilon = double.Epsilon * 10;
    
    /// <summary>
    /// 从源图像复制指定区域到目标图像，支持循环复制
    /// </summary>
    /// <param name="sourceImage">源图像</param>
    /// <param name="dest">目标图像</param>
    /// <param name="sourceX">源图像起始X坐标</param>
    /// <param name="sourceY">源图像起始Y坐标</param>
    /// <param name="sourceWidth">复制区域宽度</param>
    /// <param name="sourceHeight">复制区域高度</param>
    /// <param name="destX">目标图像起始X坐标</param>
    /// <param name="destY">目标图像起始Y坐标</param>
    public static void CopyImage(Image<Rgba32> sourceImage, Image<Rgba32> dest, int sourceX, int sourceY, int sourceWidth, int sourceHeight, int destX, int destY)
    {
        var height = sourceHeight;

        sourceImage.ProcessPixelRows(dest, (sourceAccessor, targetAccessor) =>
        {
            var shouldCulaSourceAccessorIndex = sourceY + height > sourceAccessor.Height;

            for (var i = 0; i < height; i++)
            {
                var sourceAccessorIndex = sourceY + i;
                if (shouldCulaSourceAccessorIndex && sourceAccessorIndex >= sourceAccessor.Height)
                {
                    sourceAccessorIndex %= sourceAccessor.Height;
                }
                var sourceRow = sourceAccessor.GetRowSpan(sourceAccessorIndex);
                var targetRow = targetAccessor.GetRowSpan(i + destY);

                var shouldCulaSourceRowIndex = sourceX + sourceWidth > sourceRow.Length;
                for (var x = 0; x < sourceWidth; x++)
                {
                    var sourceRowIndex = x + sourceX;
                    if (shouldCulaSourceRowIndex && sourceRowIndex >= sourceRow.Length)
                    {
                        sourceRowIndex %= sourceRow.Length;
                    }
                    targetRow[x + destX] = sourceRow[sourceRowIndex];
                }
            }
        });
    }
    
    /// <summary>
    /// 计算由三个2D顶点组成的三角形的面积
    /// </summary>
    /// <param name="a">第一个顶点</param>
    /// <param name="b">第二个顶点</param>
    /// <param name="c">第三个顶点</param>
    /// <returns>三角形的面积</returns>
    public static double Area(Vertex2 a, Vertex2 b, Vertex2 c)
    {
        return Math.Abs(
            (a.X - c.X) * (b.Y - a.Y) -
            (a.X - b.X) * (c.Y - a.Y)
        ) / 2;
    }

    /// <summary>
    /// 计算三个3D顶点形成的三角形的法线向量（用于确定三角形朝向）
    /// </summary>
    /// <param name="a">第一个顶点</param>
    /// <param name="b">第二个顶点</param>
    /// <param name="c">第三个顶点</param>
    /// <returns>法线向量</returns>
    public static Vertex3 Orientation(Vertex3 a, Vertex3 b, Vertex3 c)
    {
        // 计算三角形朝向
        var v0 = b - a;
        var v1 = c - a;
        var v2 = v0.Cross(v1);
        return v2;

    }

    /// <summary>
    /// 计算大于或等于给定整数的最小2的幂
    /// </summary>
    /// <param name="x"></param>
    /// <returns></returns>
    public static int NextPowerOfTwo(int x)
    {
        x--;
        x |= (x >> 1);
        x |= (x >> 2);
        x |= (x >> 4);
        x |= (x >> 8);
        x |= (x >> 16);
        return (x + 1);
    }

    /// <summary>
    /// Gets the distance of P from A (in percent) relative to segment AB
    /// </summary>
    /// <param name="a">Edge start</param>
    /// <param name="b">Edge end</param>
    /// <param name="p">Point on the segment</param>
    /// <returns></returns>
    public static double GetIntersectionPerc(Vertex3 a, Vertex3 b, Vertex3 p)
    {
        var edge1Length = a.Distance(b);
        var subEdge1Length = a.Distance(p);
        return subEdge1Length / edge1Length;
    }

}

/// <summary>
/// 格式化流写入器，用于指定格式提供程序的流写入
/// </summary>
public class FormattingStreamWriter : StreamWriter
{
    /// <summary>
    /// 使用指定的格式提供程序初始化 FormattingStreamWriter 类的新实例
    /// </summary>
    /// <param name="path">要写入的文件路径</param>
    /// <param name="formatProvider">格式提供程序</param>
    public FormattingStreamWriter(string path, IFormatProvider formatProvider)
        : base(path)
    {
        FormatProvider = formatProvider;
    }

    /// <summary>
    /// 获取格式提供程序
    /// </summary>
    public override IFormatProvider FormatProvider { get; }
}