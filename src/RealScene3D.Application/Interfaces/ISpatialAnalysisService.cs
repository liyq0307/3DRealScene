using NetTopologySuite.Geometries;

namespace RealScene3D.Application.Interfaces;

/// <summary>
/// 3D场景空间分析服务接口
/// 基于NetTopologySuite提供几何计算和空间分析功能
/// 支持距离计算、包含关系判断、面积计算等核心空间操作
/// </summary>
public interface ISpatialAnalysisService
{
    /// <summary>
    /// 计算两点之间的欧几里得距离
    /// </summary>
    /// <param name="point1">第一个点，使用WGS84坐标系</param>
    /// <param name="point2">第二个点，使用WGS84坐标系</param>
    /// <returns>两点之间的距离，单位：米</returns>
    double CalculateDistance(Point point1, Point point2);

    /// <summary>
    /// 判断点是否在多边形内部（包含边界）
    /// 使用射线法或包围盒算法进行快速判断
    /// </summary>
    /// <param name="point">待判断的点</param>
    /// <param name="polygon">多边形几何体，必须是封闭的简单多边形</param>
    /// <returns>如果点在多边形内或边界上返回true，否则返回false</returns>
    bool IsPointInPolygon(Point point, Polygon polygon);

    /// <summary>
    /// 计算多边形面积
    /// 使用高斯-博内公式或鞋带公式计算多边形面积
    /// </summary>
    /// <param name="polygon">多边形几何体，支持带洞的多边形</param>
    /// <returns>多边形面积，单位：平方米</returns>
    double CalculateArea(Polygon polygon);

    /// <summary>
    /// 计算多边形的几何中心点
    /// 使用质心算法计算多边形的平衡点，常用于地图标注
    /// </summary>
    /// <param name="polygon">多边形几何体</param>
    /// <returns>多边形的质心点坐标</returns>
    Point GetCentroid(Polygon polygon);

    /// <summary>
    /// 判断两个几何体是否存在相交关系
    /// 支持点、线、多边形等各种几何类型的相交判断
    /// </summary>
    /// <param name="geometry1">第一个几何体</param>
    /// <param name="geometry2">第二个几何体</param>
    /// <returns>如果两个几何体相交或接触返回true，否则返回false</returns>
    bool Intersects(Geometry geometry1, Geometry geometry2);

    /// <summary>
    /// 为几何体创建缓冲区
    /// 在几何体周围创建指定宽度的缓冲区域，常用于空间分析
    /// </summary>
    /// <param name="geometry">原始几何体，支持点、线、多边形</param>
    /// <param name="distance">缓冲距离，单位：米，正数向外缓冲，负数向内收缩</param>
    /// <returns>缓冲后的几何体，通常是多边形</returns>
    Geometry CreateBuffer(Geometry geometry, double distance);
}
