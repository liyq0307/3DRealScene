namespace RealScene3D.Infrastructure.Redis;

/// <summary>
/// Redis 缓存键常量
/// </summary>
public static class CacheKeys
{
    // 会话缓存
    public const string SESSION_PREFIX = "session:";
    public static string Session(string sessionId) => $"{SESSION_PREFIX}{sessionId}";

    // 用户缓存
    public const string USER_PREFIX = "user:";
    public static string User(Guid userId) => $"{USER_PREFIX}{userId}";
    public static string UserByEmail(string email) => $"{USER_PREFIX}email:{email}";

    // 场景缓存
    public const string SCENE_PREFIX = "scene:";
    public static string Scene(Guid sceneId) => $"{SCENE_PREFIX}{sceneId}";
    public static string SceneObjects(Guid sceneId) => $"{SCENE_PREFIX}{sceneId}:objects";

    // 热点数据
    public const string HOT_SCENES = "hot:scenes";
    public const string POPULAR_MODELS = "hot:models";

    // 统计计数器
    public const string COUNTER_PREFIX = "counter:";
    public static string SceneViews(Guid sceneId) => $"{COUNTER_PREFIX}scene:{sceneId}:views";
    public static string UserActions(Guid userId) => $"{COUNTER_PREFIX}user:{userId}:actions";

    // 临时数据（文件上传进度等）
    public const string UPLOAD_PREFIX = "upload:";
    public static string UploadProgress(string uploadId) => $"{UPLOAD_PREFIX}{uploadId}:progress";

    // 分布式锁
    public const string LOCK_PREFIX = "lock:";
    public static string SceneLock(Guid sceneId) => $"{LOCK_PREFIX}scene:{sceneId}";
}
