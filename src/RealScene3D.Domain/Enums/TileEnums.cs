namespace RealScene3D.Domain.Enums;

/// <summary>
/// 3D Tiles瓦片格式枚举 - 支持的输出格式类型
/// 定义Cesium 3D Tiles规范支持的各种瓦片格式
/// </summary>
public enum TileFormat
{
    /// <summary>
    /// Batched 3D Model（批量3D模型）
    /// 用途：建筑模型批量渲染、城市3D模型、BIM模型可视化
    /// 特点：支持批量渲染,性能高，兼容性好
    /// </summary>
    B3DM = 0,

    /// <summary>
    /// Instanced 3D Model（实例化3D模型）
    /// 用途：树木、路灯、标志牌等重复对象批量渲染
    /// 特点：GPU实例化加速，内存占用小，渲染效率极高
    /// </summary>
    I3DM = 1,

    /// <summary>
    /// Standard glTF 2.0（标准glTF格式）
    /// 用途：跨平台3D模型交换、Web 3D可视化
    /// 特点：通用性强，支持Three.js、Babylon.js等WebGL库
    /// </summary>
    GLTF = 2,

    /// <summary>
    /// Point Cloud（点云）
    /// 用途：激光扫描数据、地形高程点、LiDAR数据
    /// 特点：支持大规模点云渲染，多种采样策略
    /// </summary>
    PNTS = 3,

    /// <summary>
    /// Composite Tile（复合瓦片）
    /// 用途：混合场景（建筑+点云、网格+实例）
    /// 特点：组合多种格式，减少HTTP请求，优化加载性能
    /// </summary>
    CMPT = 4
}

/// <summary>
/// GLTF输出格式枚举
/// </summary>
public enum GltfFormat
{
    /// <summary>
    /// GLB格式 - 二进制，单文件包含所有数据
    /// </summary>
    GLB,

    /// <summary>
    /// GLTF格式 - JSON文本+外部.bin文件
    /// </summary>
    GLTF
}

/// <summary>
/// 点云采样策略枚举
/// </summary>
public enum SamplingStrategy
{
    /// <summary>
    /// 仅使用顶点 - 最快，点数最少
    /// </summary>
    VerticesOnly,

    /// <summary>
    /// 表面均匀采样 - 质量好，点数适中
    /// </summary>
    UniformSampling,

    /// <summary>
    /// 密集采样 - 最高质量，点数最多
    /// </summary>
    DenseSampling
}
