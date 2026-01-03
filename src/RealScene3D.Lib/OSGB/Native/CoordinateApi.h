#pragma once

#ifdef _WIN32
    #ifdef OSGB_EXPORTS
        #define COORD_API __declspec(dllexport)
    #else
        #define COORD_API __declspec(dllimport)
    #endif
#else
    #define COORD_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief EPSG坐标系转换初始化
 * @param epsg_code 输入坐标系EPSG代码（如4490=CGCS2000, 4547=CGCS2000_3DegreeGKCMZone39等）
 * @param origin 原点坐标[x, y, z]
 * @return 成功返回1，失败返回0
 */
COORD_API int coord_init_epsg(int epsg_code, double* origin);

/**
 * @brief ENU局部坐标系初始化
 * @param lon 经度（度）
 * @param lat 纬度（度）
 * @param origin_enu ENU原点偏移[x, y, z]
 * @return 成功返回1，失败返回0
 */
COORD_API int coord_init_enu(double lon, double lat, double* origin_enu);

/**
 * @brief WKT坐标系定义初始化
 * @param wkt WKT字符串
 * @param origin 原点坐标[x, y, z]
 * @return 成功返回1，失败返回0
 */
COORD_API int coord_init_wkt(const char* wkt, double* origin);

/**
 * @brief 清理坐标转换资源
 */
COORD_API void coord_cleanup();

/**
 * @brief 获取最后的错误信息
 * @return 错误信息字符串，无错误时返回NULL
 */
COORD_API const char* coord_get_last_error();

/**
 * @brief 检查坐标转换是否已初始化
 * @return 已初始化返回1，未初始化返回0
 */
COORD_API int coord_is_initialized();

#ifdef __cplusplus
}
#endif
