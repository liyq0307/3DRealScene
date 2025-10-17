using NetTopologySuite.Geometries;

namespace RealScene3D.Domain.Entities;

/// <summary>
/// 3D场景对象领域实体类
/// 表示场景中具体3D对象的业务概念，如建筑、道路、车辆等
/// 包含对象的几何变换信息、材质属性和扩展属性
/// </summary>
public class SceneObject : BaseEntity
{
    /// <summary>
    /// 所属场景ID，必填项
    /// 外键关联到Scene3D实体，建立对象与场景的从属关系
    /// </summary>
    public Guid SceneId { get; set; }

    /// <summary>
    /// 对象名称，必填项
    /// 用于标识和区分场景中的不同对象，支持中英文字符
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 对象类型，必填项
    /// 用于分类对象，如：建筑、道路、车辆、地形、水体等
    /// 可用于渲染优化和业务逻辑处理
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 对象3D位置，可选项
    /// 使用WGS84坐标系（EPSG:4326）存储X、Y、Z坐标
    /// 用于精确定位对象在3D空间中的位置
    /// </summary>
    public Point? Position { get; set; }

    /// <summary>
    /// 对象旋转信息，必填项，默认无旋转
    /// JSON格式字符串，支持欧拉角或四元数表示方式
    /// 示例：{"x":0,"y":0,"z":0} 或 {"w":1,"x":0,"y":0,"z":0}
    /// </summary>
    public string Rotation { get; set; } = "{\"x\":0,\"y\":0,\"z\":0}";

    /// <summary>
    /// 对象缩放信息，必填项，默认等比例缩放
    /// JSON格式字符串，定义X、Y、Z三个轴向的缩放比例
    /// 示例：{"x":1,"y":1,"z":1}
    /// </summary>
    public string Scale { get; set; } = "{\"x\":1,\"y\":1,\"z\":1}";

    /// <summary>
    /// 3D模型资源路径，必填项
    /// 指向对象几何模型的文件路径，支持多种3D格式
    /// 如：OBJ、FBX、GLTF、3DS等格式的文件路径
    /// </summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>
    /// 材质数据，可选项，默认空对象
    /// JSON格式字符串，定义对象的视觉外观属性
    /// 包含颜色、纹理、光照参数等材质信息
    /// </summary>
    public string MaterialData { get; set; } = "{}";

    /// <summary>
    /// 自定义属性，可选项，默认空对象
    /// JSON格式字符串，用于存储对象的业务属性和扩展信息
    /// 如：建筑高度、道路等级、车辆颜色等特定于对象类型的属性
    /// </summary>
    public string Properties { get; set; } = "{}";

    /// <summary>
    /// 所属场景导航属性
    /// 与Scene3D实体建立多对一关联关系
    /// 用于获取场景的完整信息和相关对象
    /// </summary>
    public Scene3D Scene { get; set; } = null!;
}
