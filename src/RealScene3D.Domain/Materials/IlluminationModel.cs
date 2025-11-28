namespace RealScene3D.Domain.Materials;

/// <summary>
/// 光照模型枚举
/// </summary>
public enum IlluminationModel
{
    /// <summary>
    /// 颜色开，环境光关
    /// </summary>
    ColorOnAmbientOff = 0,

    /// <summary>
    /// 颜色开，环境光开
    /// </summary>
    ColorOnAmbientOn = 1,

    /// <summary>
    /// 高光开
    /// </summary>
    HighlightOn = 2,

    /// <summary>
    /// 反射开，光线追踪开
    /// </summary>
    ReflectionOnRayTraceOn = 3,

    /// <summary>
    /// 透明玻璃开，反射光线追踪开
    /// </summary>
    TransparencyGlassOnReflectionRayTraceOn = 4,

    /// <summary>
    /// 反射菲涅尔开，光线追踪开
    /// </summary>
    ReflectionFresnelOnRayTraceOn = 5,

    /// <summary>
    /// 透明折射开，反射菲涅尔开，光线追踪开
    /// </summary>
    TransparencyRefractionOnReflectionFresnelOffRayTraceOn = 6,

    /// <summary>
    /// 透明折射开，反射菲涅尔开，光线追踪开
    /// </summary>
    TransparencyRefractionOnReflectionFresnelOnRayTraceOn = 7,

    /// <summary>
    /// 反射开
    /// </summary>
    ReflectionOn = 8,

    /// <summary>
    /// 透明玻璃开，反射光线追踪关
    /// </summary>
    TransparencyGlassOnReflectionRayTraceOff = 9,

    /// <summary>
    /// 投射阴影
    /// </summary>
    CastsShadows = 10
}