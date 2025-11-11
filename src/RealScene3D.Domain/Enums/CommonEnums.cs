namespace RealScene3D.Domain.Enums;

/// <summary>
/// 3D模型格式枚举
/// 支持通用格式和专业格式
/// </summary>
public enum ModelFormat
{
    /// <summary>
    /// 未知格式
    /// </summary>
    Unknown = 0,

    // ========== 通用格式 (1-100) ==========

    /// <summary>
    /// OBJ格式 - Wavefront OBJ，最广泛使用的3D模型交换格式
    /// 扩展名: .obj
    /// 特点: 文本格式，支持MTL材质，易于编辑
    /// </summary>
    OBJ = 1,

    /// <summary>
    /// glTF格式 - GL Transmission Format，现代3D传输格式
    /// 扩展名: .gltf
    /// 特点: JSON格式，支持PBR材质，Web3D标准
    /// </summary>
    GLTF = 2,

    /// <summary>
    /// GLB格式 - glTF的二进制版本
    /// 扩展名: .glb
    /// 特点: 二进制格式，更高效，包含所有资源
    /// </summary>
    GLB = 3,

    /// <summary>
    /// STL格式 - STereoLithography，3D打印行业标准
    /// 扩展名: .stl
    /// 特点: 仅包含三角形网格，无材质信息
    /// </summary>
    STL = 4,

    /// <summary>
    /// PLY格式 - Polygon File Format，斯坦福多边形格式
    /// 扩展名: .ply
    /// 特点: 支持点云和网格，三维扫描常用
    /// </summary>
    PLY = 5,

    // ========== 专业格式 (101-200) ==========

    /// <summary>
    /// FBX格式 - Autodesk Filmbox，游戏和影视行业标准
    /// 扩展名: .fbx
    /// 特点: 支持动画、骨骼、材质，需要Assimp.NET
    /// </summary>
    FBX = 101,

    /// <summary>
    /// IFC格式 - Industry Foundation Classes，BIM建筑信息模型标准
    /// 扩展名: .ifc
    /// 特点: 包含完整建筑语义信息，需要xBIM
    /// </summary>
    IFC = 102,

    /// <summary>
    /// IFCXML格式 - IFC的XML版本
    /// 扩展名: .ifcxml
    /// 特点: XML格式，便于解析和转换
    /// </summary>
    IFCXML = 103,

    /// <summary>
    /// IFCZIP格式 - 压缩的IFC文件
    /// 扩展名: .ifczip
    /// 特点: 压缩格式，减小文件体积
    /// </summary>
    IFCZIP = 104,

    /// <summary>
    /// OSGB格式 - OpenSceneGraph Binary，倾斜摄影标准格式
    /// 扩展名: .osgb
    /// 特点: 二进制格式，支持LOD，实景三维常用
    /// </summary>
    OSGB = 105,

    /// <summary>
    /// OSG格式 - OpenSceneGraph ASCII格式
    /// 扩展名: .osg
    /// 特点: 文本格式，便于调试
    /// </summary>
    OSG = 106
}

/// <summary>
/// 用户角色枚举
/// </summary>
public enum UserRole
{
    Guest = 0,
    User = 1,
    Admin = 2,
    SuperAdmin = 3
}

/// <summary>
/// 排序方向枚举
/// </summary>
public enum SortDirection
{
    Asc,
    Desc
}

/// <summary>
/// 存储位置类型枚举
/// </summary>
public enum StorageLocationType
{
    /// <summary>
    /// MinIO对象存储
    /// </summary>
    MinIO = 0,
    /// <summary>
    /// 本地文件系统
    /// </summary>
    LocalFileSystem = 1
}
