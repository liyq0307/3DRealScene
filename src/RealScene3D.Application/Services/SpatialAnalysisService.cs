using NetTopologySuite.Geometries;
using RealScene3D.Application.Interfaces;

namespace RealScene3D.Application.Services;

/// <summary>
/// 空间分析服务实现（基于NetTopologySuite）
/// </summary>
public class SpatialAnalysisService : ISpatialAnalysisService
{
    public double CalculateDistance(Point point1, Point point2)
    {
        return point1.Distance(point2);
    }

    public bool IsPointInPolygon(Point point, Polygon polygon)
    {
        return polygon.Contains(point);
    }

    public double CalculateArea(Polygon polygon)
    {
        return polygon.Area;
    }

    public Point GetCentroid(Polygon polygon)
    {
        return (Point)polygon.Centroid;
    }

    public bool Intersects(Geometry geometry1, Geometry geometry2)
    {
        return geometry1.Intersects(geometry2);
    }

    public Geometry CreateBuffer(Geometry geometry, double distance)
    {
        return geometry.Buffer(distance);
    }
}
