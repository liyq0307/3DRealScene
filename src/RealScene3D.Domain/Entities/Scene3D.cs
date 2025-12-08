using NetTopologySuite.Geometries;

namespace RealScene3D.Domain.Entities;

/// <summary>
/// 3D场景领域实体类
/// 表示三维场景的核心业务概念，包含场景的基本信息、地理边界、中心点等
/// 继承自BaseEntity，具备审计字段和软删除功能
/// </summary>
public class Scene3D : BaseEntity
{
    /// <summary>
    /// 场景名称，必填项
    /// 用于标识和区分不同的3D场景，支持中英文字符
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 场景描述，可选项
    /// 用于详细说明场景的内容、用途和特点
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 场景所有者用户ID，必填项
    /// 外键关联到User实体，建立场景与用户的所属关系
    /// </summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// 场景地理边界，可选项
    /// 使用NetTopologySuite的Polygon几何类型存储GIS边界数据
    /// 支持复杂多边形边界定义，常用于场景范围限定和空间查询
    /// </summary>
    public Polygon? Boundary { get; set; }

    /// <summary>
    /// 场景中心点坐标，可选项
    /// 使用WGS84坐标系（EPSG:4326）存储经度、纬度、高度信息
    /// 常用于场景定位、地图显示和导航计算
    /// </summary>
    public Point? CenterPoint { get; set; }

    /// <summary>
    /// 场景元数据，可选项
    /// JSON格式字符串，用于存储场景的扩展属性和配置信息
    /// 如：环境设置、光照参数、渲染选项等
    /// </summary>
    public string Metadata { get; set; } = "{}";

    /// <summary>
    /// 渲染引擎类型，必填项
    /// 指定场景使用的3D渲染引擎
    /// 可选值: "Cesium" - 基于地球的地理空间渲染引擎，支持3D Tiles和地理坐标系统
    ///        "ThreeJS" - 通用3D渲染引擎，支持更多模型格式(OBJ, FBX, GLB等)
    /// 默认值: "Cesium"
    /// 说明: 场景创建后，添加的所有对象必须与此渲染引擎兼容
    /// </summary>
    public string RenderEngine { get; set; } = "Cesium";

    /// <summary>
    /// 场景所有者导航属性
    /// 与User实体建立关联关系，用于获取所有者详细信息
    /// </summary>
    public User Owner { get; set; } = null!;

    /// <summary>
    /// 场景对象集合导航属性
    /// 与SceneObject实体建立一对多关联关系
    /// 表示场景中包含的所有3D对象，如建筑、道路、地形等
    /// </summary>
    public ICollection<SceneObject> SceneObjects { get; set; } = new List<SceneObject>();
}
