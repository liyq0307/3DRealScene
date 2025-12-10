namespace RealScene3D.Application.DTOs;
/// <summary>
/// 3D场景数据传输对象集合
/// 包含场景和场景对象的创建请求和响应DTO
/// </summary>
public class SceneDtos
{
    /// <summary>
    /// 3D场景创建请求DTO
    /// 用于接收客户端创建3D场景的请求数据
    /// </summary>
    public class CreateSceneRequest
    {
        /// <summary>
        /// 场景名称，必填项
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 场景描述，可选项
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 场景边界GeoJSON格式数据，可选项
        /// 用于定义场景的地理边界范围，支持Polygon、MultiPolygon等几何类型
        /// 格式示例：
        /// {
        ///   "type": "Polygon",
        ///   "coordinates": [[[lng1, lat1], [lng2, lat2], [lng3, lat3], [lng1, lat1]]]
        /// }
        /// 用于空间查询、视图裁剪、碰撞检测等地理计算
        /// </summary>
        public string? BoundaryGeoJson { get; set; }

        /// <summary>
        /// 场景中心点坐标，经度、纬度、高度
        /// 数组格式：[经度, 纬度, 高度]
        /// - 经度范围：-180 到 180
        /// - 纬度范围：-90 到 90
        /// - 高度单位：米，可为负值（地下）
        /// 用于相机定位、导航计算、距离测量等操作
        /// </summary>
        public double[]? CenterPoint { get; set; } // [lon, lat, alt]

        /// <summary>
        /// 场景元数据JSON字符串，默认空对象
        /// 用于存储场景的扩展属性和配置信息
        /// 支持嵌套对象和数组结构，可扩展性强
        /// 常用字段：相机配置、渲染设置、权限策略等
        /// 格式示例：{"camera": {"fov": 60, "near": 1, "far": 10000}, "rendering": {"quality": "high"}}
        /// </summary>
        public string Metadata { get; set; } = "{}";

        /// <summary>
        /// 渲染引擎类型
        /// 可选值: "Cesium" - 基于地球的地理空间渲染引擎，支持3D Tiles和地理坐标系统
        ///        "ThreeJS" - 通用3D渲染引擎，支持更多模型格式(OBJ, FBX, GLB等)
        /// 默认值: "Cesium"
        /// </summary>
        public string RenderEngine { get; set; } = "Cesium";

        /// <summary>
        /// 场景对象集合，用于在创建场景时一并创建场景对象
        /// </summary>
        public ICollection<CreateSceneObjectRequest> SceneObjects { get; set; } = new List<CreateSceneObjectRequest>();
    }

    /// <summary>
    /// 3D场景更新请求DTO
    /// 用于接收客户端更新3D场景的请求数据
    /// </summary>
    public class UpdateSceneRequest
    {
        /// <summary>
        /// 场景名称，可选项
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 场景描述，可选项
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 场景边界GeoJSON格式数据，可选项
        /// 用于定义场景的地理边界范围，支持Polygon、MultiPolygon等几何类型
        /// </summary>
        public string? BoundaryGeoJson { get; set; }

        /// <summary>
        /// 场景中心点坐标，经度、纬度、高度
        /// 数组格式：[经度, 纬度, 高度]
        /// </summary>
        public double[]? CenterPoint { get; set; }

        /// <summary>
        /// 场景元数据JSON字符串
        /// 用于存储场景的扩展属性和配置信息
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// 渲染引擎类型
        /// 可选值: "Cesium" 或 "ThreeJS"
        /// </summary>
        public string? RenderEngine { get; set; }
    }

    /// <summary>
    /// 3D场景响应DTO
    /// 用于向前端返回3D场景的完整信息
    /// </summary>
    public class SceneDto
    {
        /// <summary>
        /// 场景唯一标识符
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 场景名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 场景描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 场景边界GeoJSON格式数据
        /// </summary>
        public string? BoundaryGeoJson { get; set; }

        /// <summary>
        /// 场景中心点坐标
        /// </summary>
        public double[]? CenterPoint { get; set; }

        /// <summary>
        /// 场景元数据JSON字符串
        /// </summary>
        public string Metadata { get; set; } = "{}";

        /// <summary>
        /// 渲染引擎类型
        /// 可选值: "Cesium" 或 "ThreeJS"
        /// </summary>
        public string RenderEngine { get; set; } = "Cesium";

        /// <summary>
        /// 场景所有者用户ID
        /// </summary>
        public Guid OwnerId { get; set; }

        /// <summary>
        /// 场景创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 场景对象集合
        /// </summary>
        public ICollection<SceneObjectDto> SceneObjects { get; set; } = new List<SceneObjectDto>();
    }

    /// <summary>
    /// 场景对象创建请求DTO
    /// 用于接收客户端在场景中创建3D对象的请求数据
    /// </summary>
    public class CreateSceneObjectRequest
    {
        /// <summary>
        /// 所属场景ID，必填项
        /// </summary>
        public Guid SceneId { get; set; }

        /// <summary>
        /// 对象名称，必填项
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 对象类型，必填项，如：建筑、道路、车辆等
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 对象3D位置坐标，X、Y、Z轴坐标
        /// 数组格式：[x, y, z]，对应三维空间坐标系
        /// - X轴：水平方向，增大表示向右
        /// - Y轴：垂直方向，增大表示向上
        /// - Z轴：深度方向，增大表示向前（相机视角）
        /// 坐标系：右手坐标系，原点通常位于场景中心或地面
        /// </summary>
        public double[] Position { get; set; } = Array.Empty<double>(); // [x, y, z]

        /// <summary>
        /// 对象旋转信息JSON格式，包含X、Y、Z轴旋转角度（弧度）
        /// 格式示例：{"x": 0, "y": 1.57, "z": 0}（Y轴旋转90度）
        /// - X轴旋转：俯仰角，围绕X轴旋转
        /// - Y轴旋转：偏航角，围绕Y轴旋转
        /// - Z轴旋转：翻滚角，围绕Z轴旋转
        /// 旋转顺序：通常为Y->X->Z（偏航->俯仰->翻滚）
        /// </summary>
        public string Rotation { get; set; } = "{\"x\":0,\"y\":0,\"z\":0}";

        /// <summary>
        /// 对象缩放信息JSON格式，包含X、Y、Z轴缩放比例
        /// 格式示例：{"x": 1.0, "y": 1.0, "z": 1.0}（原始大小）
        /// - X轴缩放：水平方向缩放比例
        /// - Y轴缩放：垂直方向缩放比例
        /// - Z轴缩放：深度方向缩放比例
        /// 注意：负值会产生镜像效果，通常用于特殊视觉效果
        /// </summary>
        public string Scale { get; set; } = "{\"x\":1,\"y\":1,\"z\":1}";

        /// <summary>
        /// 3D模型文件路径，必填项
        /// 支持多种3D模型格式：
        /// - glTF/glb：标准格式，推荐用于Web渲染
        /// - OBJ：传统格式，支持纹理映射
        /// - FBX：专业格式，支持动画和骨骼
        /// - BIM格式：IFC、RVT等建筑信息模型
        /// 路径格式：相对于存储桶根目录的路径，如："models/building.glb"
        /// </summary>
        public string ModelPath { get; set; } = string.Empty;

        /// <summary>
        /// 材质数据JSON格式，定义对象的视觉外观
        /// 支持多种材质属性：
        /// - color：漫反射颜色，RGB格式，如：[1.0, 1.0, 1.0]
        /// - metallic：金属度，0-1之间，0为非金属，1为纯金属
        /// - roughness：粗糙度，0-1之间，0为光滑镜面，1为完全粗糙
        /// - texture：纹理贴图路径，如："textures/diffuse.jpg"
        /// - transparent：透明度，0-1之间，0为完全透明，1为完全不透明
        /// - emissive：自发光颜色，用于光源效果
        /// </summary>
        public string MaterialData { get; set; } = "{}";

        /// <summary>
        /// 对象属性数据JSON格式，自定义属性集合
        /// 用于存储对象的业务属性和扩展信息，支持嵌套结构
        /// 常用字段：
        /// - category：对象分类，如："建筑"、"道路"、"车辆"
        /// - tags：标签数组，如：["重要", "临时", "可移动"]
        /// - description：对象描述，如："主教学楼A栋"
        /// - customData：自定义数据，灵活扩展
        /// 示例：{"category": "建筑", "tags": ["教学楼"], "description": "主教学楼"}
        /// </summary>
        public string Properties { get; set; } = "{}";
    }

    /// <summary>
    /// 场景对象更新请求DTO
    /// 用于接收客户端更新场景对象的请求数据
    /// </summary>
    public class UpdateSceneObjectRequest
    {
        /// <summary>
        /// 对象名称，可选项
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 对象类型，可选项
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// 对象3D位置坐标，可选项
        /// </summary>
        public double[]? Position { get; set; }

        /// <summary>
        /// 对象旋转信息JSON格式，可选项
        /// </summary>
        public string? Rotation { get; set; }

        /// <summary>
        /// 对象缩放信息JSON格式，可选项
        /// </summary>
        public string? Scale { get; set; }

        /// <summary>
        /// 3D模型文件路径，可选项
        /// </summary>
        public string? ModelPath { get; set; }

        /// <summary>
        /// 材质数据JSON格式，可选项
        /// </summary>
        public string? MaterialData { get; set; }

        /// <summary>
        /// 对象属性数据JSON格式，可选项
        /// </summary>
        public string? Properties { get; set; }
    }

    /// <summary>
    /// 场景对象响应DTO
    /// 用于向前端返回场景中3D对象的完整信息
    /// </summary>
    public class SceneObjectDto
    {
        /// <summary>
        /// 对象唯一标识符
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 所属场景ID
        /// </summary>
        public Guid SceneId { get; set; }

        /// <summary>
        /// 对象名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 对象类型，如：建筑、道路、车辆等
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 对象3D位置坐标数组
        /// </summary>
        public double[] Position { get; set; } = Array.Empty<double>();

        /// <summary>
        /// 对象旋转信息JSON字符串
        /// </summary>
        public string Rotation { get; set; } = string.Empty;

        /// <summary>
        /// 对象缩放信息JSON字符串
        /// </summary>
        public string Scale { get; set; } = string.Empty;

        /// <summary>
        /// 3D模型文件路径
        /// </summary>
        public string ModelPath { get; set; } = string.Empty;

        /// <summary>
        /// 材质数据JSON字符串
        /// </summary>
        public string MaterialData { get; set; } = "{}";

        /// <summary>
        /// 对象属性数据JSON字符串
        /// </summary>
        public string Properties { get; set; } = "{}";

        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 关联的切片任务ID，可选项
        /// </summary>
        public Guid? SlicingTaskId { get; set; }

        /// <summary>
        /// 关联的切片任务状态，可选项
        /// </summary>
        public string? SlicingTaskStatus { get; set; }

        /// <summary>
        /// 切片输出路径
        /// </summary>
        public string? SlicingOutputPath { get; set; }

        /// <summary>
        /// 显示路径
        /// </summary>
        public string? DisplayPath { get; set; }
    }
}
