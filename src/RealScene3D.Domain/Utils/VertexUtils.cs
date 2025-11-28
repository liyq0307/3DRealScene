using System.Diagnostics;
using RealScene3D.Domain.Geometry;

namespace RealScene3D.Domain.Utils;

/// <summary>
/// 轴枚举
/// </summary>
public enum Axis
{
    X,
    Y,
    Z
}

/// <summary>
/// 顶点工具接口
/// </summary>
public interface IVertexUtils
{
    Vertex3 CutEdge(Vertex3 a, Vertex3 b, double q);
    double GetDimension(Vertex3 v);
    
    Axis Axis { get; }   
}

/// <summary>
/// 按X轴处理顶点的工具类
/// </summary>
public class VertexUtilsX : IVertexUtils
{
    public Vertex3 CutEdge(Vertex3 a, Vertex3 b, double q)
    {
        var dx = a.X - b.X;
        var my = (a.Y - b.Y) / dx;
        var mz = (a.Z - b.Z) / dx;
        
        Debug.Assert(double.IsFinite(my));
        Debug.Assert(double.IsFinite(mz));
        
        return new Vertex3(q, my * (q - a.X) + a.Y, mz * (q - a.X) + a.Z);
    }
    
    public double GetDimension(Vertex3 v)
    {
        return v.X;
    }

    public Axis Axis => Axis.X;
}

/// <summary>
/// 按Y轴处理顶点的工具类
/// </summary>
public class VertexUtilsY : IVertexUtils
{

    public Vertex3 CutEdge(Vertex3 a, Vertex3 b, double q)
    {
        var dy = a.Y - b.Y;
        var mx = (a.X - b.X) / dy;
        var mz = (a.Z - b.Z) / dy;
        
        Debug.Assert(double.IsFinite(mx));
        Debug.Assert(double.IsFinite(mz));

        return new Vertex3(mx * (q - a.Y) + a.X, q, mz * (q - a.Y) + a.Z);
    }

    public double GetDimension(Vertex3 v)
    {
        return v.Y;
    }

    public Axis Axis => Axis.Y;

}

/// <summary>
/// 按Z轴处理顶点的工具类
/// </summary>
public class VertexUtilsZ : IVertexUtils
{
    public Vertex3 CutEdge(Vertex3 a, Vertex3 b, double q)
    {
        var dz = a.Z - b.Z;
        var mx = (a.X - b.X) / dz;
        var my = (a.Y - b.Y) / dz;

        Debug.Assert(double.IsFinite(mx));
        Debug.Assert(double.IsFinite(my));

        return new Vertex3(mx * (q - a.Z) + a.X, my * (q - a.Z) + a.Y, q);
    }

    public double GetDimension(Vertex3 v)
    {
        return v.Z;
    }

    public Axis Axis => Axis.Z;
}