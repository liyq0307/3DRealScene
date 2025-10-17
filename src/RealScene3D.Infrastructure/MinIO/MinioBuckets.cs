namespace RealScene3D.Infrastructure.MinIO;

/// <summary>
/// MinIO 存储桶常量
/// </summary>
public static class MinioBuckets
{
    /// <summary>
    /// 倾斜摄影数据存储桶
    /// </summary>
    public const string TILT_PHOTOGRAPHY = "tilt-photography";

    /// <summary>
    /// BIM 模型存储桶
    /// </summary>
    public const string BIM_MODELS = "bim-models";

    /// <summary>
    /// 视频文件存储桶
    /// </summary>
    public const string VIDEOS = "videos";

    /// <summary>
    /// 3D 模型存储桶
    /// </summary>
    public const string MODELS_3D = "models-3d";

    /// <summary>
    /// 纹理贴图存储桶
    /// </summary>
    public const string TEXTURES = "textures";

    /// <summary>
    /// 缩略图存储桶
    /// </summary>
    public const string THUMBNAILS = "thumbnails";

    /// <summary>
    /// 临时文件存储桶
    /// </summary>
    public const string TEMP = "temp";

    /// <summary>
    /// 文档存储桶
    /// </summary>
    public const string DOCUMENTS = "documents";
}

/// <summary>
/// 文件类型常量
/// </summary>
public static class FileTypes
{
    // 3D 模型格式
    public const string GLTF = "model/gltf+json";
    public const string GLB = "model/gltf-binary";
    public const string OBJ = "model/obj";
    public const string FBX = "model/fbx";
    public const string IFC = "application/x-step";

    // 倾斜摄影格式
    public const string OSGB = "application/octet-stream";
    public const string TILES_3D = "application/json";

    // 视频格式
    public const string MP4 = "video/mp4";
    public const string AVI = "video/x-msvideo";
    public const string MOV = "video/quicktime";

    // 图片格式
    public const string JPEG = "image/jpeg";
    public const string PNG = "image/png";
    public const string WEBP = "image/webp";

    // 文档格式
    public const string PDF = "application/pdf";
    public const string DOC = "application/msword";
    public const string DOCX = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
}
