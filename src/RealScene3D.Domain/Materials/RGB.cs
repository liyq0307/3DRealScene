using System.Globalization;

namespace RealScene3D.Domain.Materials;

public class RGB
{
    public readonly double R;
    public readonly double G;
    public readonly double B;

    public RGB(double r, double g, double b)
    {
        R = r;
        G = g;
        B = b;
    }

    public override string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", R, G, B);
    }

    /// <summary>
    /// 转换为字节数组 (0-255)
    /// </summary>
    public byte[] ToBytes()
    {
        return
        [
            (byte)Math.Clamp(R * 255, 0, 255),
            (byte)Math.Clamp(G * 255, 0, 255),
            (byte)Math.Clamp(B * 255, 0, 255)
        ];
    }

    /// <summary>
    /// 从字节数组创建 (0-255)
    /// </summary>
    public static RGB FromBytes(byte r, byte g, byte b)
    {
        return new RGB(r / 255.0, g / 255.0, b / 255.0);
    }
}