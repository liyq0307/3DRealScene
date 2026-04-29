#ifndef GEOTRANSFORM_H
#define GEOTRANSFORM_H	

#include "glm/glm.hpp"
#include <string>
#include <memory>
#include <proj.h>

/**
 * @brief 坐标转换工具类, 使用PROJ库实现,支持EPSG/WKT/ENU坐标系转换
 */
class GeoTransform
{
private:
	// 自定义删除器用于智能指针管理PROJ资源
	struct ProjDeleter 
	{
		void operator()(PJ* p) const 
		{
			if (p) 
			{
				proj_destroy(p);
			}
		}
	};
	
	struct ProjContextDeleter 
	{
		void operator()(PJ_CONTEXT* p) const 
		{
			if (p) 
			{
				proj_context_destroy(p);
			}
		}
	};

public:
	// PROJ转换对象（坐标转换的核心）- 使用智能指针自动管理
	static inline std::unique_ptr<PJ, ProjDeleter> projTransform = nullptr;
	static inline std::unique_ptr<PJ_CONTEXT, ProjContextDeleter> projContext = nullptr;

	// 原点坐标（局部坐标系原点）
	static inline double OriginX = 0.0;
	static inline double OriginY = 0.0;
	static inline double OriginZ = 0.0;

	// ENU地理原点（经纬度）
	static inline double GeoOriginLon = 0.0;
	static inline double GeoOriginLat = 0.0;
	static inline double GeoOriginHeight = 0.0;

	// ENU标志（是否使用ENU坐标系）
	static inline bool IsENU = false;

	// ECEF<->ENU转换矩阵
	static inline glm::dmat4 EcefToEnuMatrix = glm::dmat4(1.0);

	// 最后的错误信息
	static inline std::string lastError = "";

	// ========================================================================
	// Core coordinate transformation methods
	// ========================================================================

	/**
	 * @brief 计算ENU到ECEF的转换矩阵
	 */
	static glm::dmat4 CalcEnuToEcefMatrix(double lnt, double lat, double height_min);

	/**
	 * @brief 经纬度转ECEF
	 */
	static glm::dvec3 CartographicToEcef(double lnt, double lat, double height);

	/**
	 * @brief 初始化坐标转换器（内部使用）
	 * @param transform PROJ转换对象
	 * @param origin 原点坐标[x, y, z]
	 *
	 * 该方法会执行以下操作：
	 * 1. 保存PROJ转换对象和原点坐标
	 * 2. 将原点坐标转换为地理坐标（经纬度）
	 * 3. 计算ENU<->ECEF转换矩阵
	 */
	static void Init(PJ* transform, double* origin);

	/**
	 * @brief 设置ENU系统的地理原点
	 */
	static void SetGeographicOrigin(double lon, double lat, double height);

	/**
	 * @brief 清理资源
	 */
	static void Cleanup();

	// ========================================================================
	// Public API (convenience initialization methods)
	// ========================================================================

	/**
	 * @brief EPSG坐标系转换初始化
	 *
	 * @param epsg_code 输入坐标系EPSG代码（如4490=CGCS2000, 4547=CGCS2000 3度带等）
	 * @param origin 原点坐标[x, y, z]
	 * @return true=成功, false=失败
	 *
	 * @example
	 * double origin[3] = {39500000.0, 3450000.0, 0.0};
	 * GeoTransform::InitFromEPSG(4547, origin);
	 */
	static bool InitFromEPSG(int epsg_code, double* origin);

	/**
	 * @brief ENU局部坐标系初始化
	 *
	 * @param lon 原点经度（度）
	 * @param lat 原点纬度（度）
	 * @param origin_enu ENU原点偏移[x, y, z]（米）
	 * @return true=成功, false=失败
	 */
	static bool InitFromENU(double lon, double lat, double* origin_enu);

	/**
	 * @brief WKT坐标系定义初始化
	 *
	 * @param wkt WKT格式坐标系定义字符串
	 * @param origin 原点坐标[x, y, z]
	 * @return true=成功, false=失败
	 */
	static bool InitFromWKT(const char* wkt, double* origin);

	/**
	 * @brief 获取最后的错误信息
	 */
	static const char* GetLastError();

	/**
	 * @brief 检查坐标转换是否已初始化
	 */
	static bool IsInitialized();
};

#endif // GEOTRANSFORM_H