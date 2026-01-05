#include "GeoTransform.h"
#include <cstdio>
#include <cmath>

// 静态成员初始化
PJ* GeoTransform::projTransform = nullptr;      // PROJ转换对象
PJ_CONTEXT* GeoTransform::projContext = nullptr; // PROJ上下文
double GeoTransform::OriginX = 0.0;              // 原点X坐标
double GeoTransform::OriginY = 0.0;              // 原点Y坐标
double GeoTransform::OriginZ = 0.0;              // 原点Z坐标
double GeoTransform::GeoOriginLon = 0.0;         // 地理原点经度
double GeoTransform::GeoOriginLat = 0.0;         // 地理原点纬度
double GeoTransform::GeoOriginHeight = 0.0;      // 地理原点高度
bool GeoTransform::IsENU = false;                // 是否使用ENU坐标系
glm::dmat4 GeoTransform::EcefToEnuMatrix = glm::dmat4(1); // ECEF到ENU的转换矩阵
std::string GeoTransform::lastError = "";        // 最后的错误信息

glm::dmat4 GeoTransform::CalcEnuToEcefMatrix(double lnt, double lat, double height_min)
{
	// WGS84椭球参数
	const double pi = std::acos(-1.0);
	const double a = 6378137.0;                  // WGS84椭球长半轴（米）
	const double f = 1.0 / 298.257223563;        // WGS84扁率
	const double e2 = f * (2.0 - f);             // 第一偏心率平方

	// 将经纬度转换为弧度
	double lon = lnt * pi / 180.0;
	double phi = lat * pi / 180.0;

	// 计算三角函数值
	double sinPhi = std::sin(phi), cosPhi = std::cos(phi);
	double sinLon = std::sin(lon), cosLon = std::cos(lon);

	// 计算卯酉圈曲率半径
	double N = a / std::sqrt(1.0 - e2 * sinPhi * sinPhi);

	// 计算原点在ECEF坐标系中的坐标
	double x0 = (N + height_min) * cosPhi * cosLon;
	double y0 = (N + height_min) * cosPhi * sinLon;
	double z0 = (N * (1.0 - e2) + height_min) * sinPhi;

	// ENU基向量在ECEF坐标系中的表示
	glm::dvec3 east(-sinLon, cosLon, 0.0);           // 东方向
	glm::dvec3 north(-sinPhi * cosLon, -sinPhi * sinLon, cosPhi); // 北方向
	glm::dvec3 up(cosPhi * cosLon, cosPhi * sinLon, sinPhi);      // 上方向

	// 构建ENU->ECEF转换矩阵（旋转+平移），列主序
	glm::dmat4 T(1.0);
	T[0] = glm::dvec4(east, 0.0);
	T[1] = glm::dvec4(north, 0.0);
	T[2] = glm::dvec4(up, 0.0);
	T[3] = glm::dvec4(x0, y0, z0, 1.0);
	return T;
}

glm::dvec3 GeoTransform::CartographicToEcef(double lnt, double lat, double height)
{
	// WGS84椭球参数
	const double pi = std::acos(-1.0);
	const double a = 6378137.0;                  // WGS84椭球长半轴（米）
	const double f = 1.0 / 298.257223563;        // WGS84扁率
	const double e2 = f * (2.0 - f);             // 第一偏心率平方

	// 将经纬度转换为弧度
	double lon = lnt * pi / 180.0;
	double phi = lat * pi / 180.0;

	// 计算三角函数值
	double sinPhi = std::sin(phi), cosPhi = std::cos(phi);
	double sinLon = std::sin(lon), cosLon = std::cos(lon);

	// 计算卯酉圈曲率半径
	double N = a / std::sqrt(1.0 - e2 * sinPhi * sinPhi);

	// 将地理坐标（经纬度）转换为ECEF坐标（地心地固坐标系）
	double x = (N + height) * cosPhi * cosLon;
	double y = (N + height) * cosPhi * sinLon;
	double z = (N * (1.0 - e2) + height) * sinPhi;

	return { x, y, z };
}

void GeoTransform::Init(PJ* transform, double* origin)
{
	// 保存转换对象和原点坐标
	GeoTransform::projTransform = transform;
	GeoTransform::OriginX = origin[0];
	GeoTransform::OriginY = origin[1];
	GeoTransform::OriginZ = origin[2];
	GeoTransform::IsENU = false;  // 默认不使用ENU坐标系

	// 执行坐标转换
	glm::dvec3 originLocal = { OriginX, OriginY, OriginZ };
	glm::dvec3 originCartographic = originLocal;

	fprintf(stderr, "[GeoTransform] Origin: x=%.8f y=%.8f z=%.3f\n",
		originLocal.x, originLocal.y, originLocal.z);

	if (projTransform)
	{
		// 使用PROJ进行坐标转换
		PJ_COORD coord;
		coord.xyzt.x = originLocal.x;
		coord.xyzt.y = originLocal.y;
		coord.xyzt.z = originLocal.z;
		coord.xyzt.t = HUGE_VAL;  // 不使用时间维度

		// 执行转换
		PJ_COORD result = proj_trans(projTransform, PJ_FWD, coord);

		if (result.xyzt.x != HUGE_VAL)
		{
			// 保存转换后的地理坐标（经纬度）
			originCartographic.x = result.xyzt.x;
			originCartographic.y = result.xyzt.y;
			originCartographic.z = result.xyzt.z;

			fprintf(stderr, "[GeoTransform] Cartographic: lon=%.10f lat=%.10f h=%.3f\n",
				originCartographic.x, originCartographic.y, originCartographic.z);
		}
		else
		{
			fprintf(stderr, "[GeoTransform] WARNING: Coordinate transformation failed!\n");
		}
	}

	// 保存地理原点
	GeoOriginLon = originCartographic.x;
	GeoOriginLat = originCartographic.y;
	GeoOriginHeight = originCartographic.z;

	// 计算ENU<->ECEF转换矩阵
	glm::dmat4 EnuToEcefMatrix = CalcEnuToEcefMatrix(
		originCartographic.x, originCartographic.y, originCartographic.z);
	EcefToEnuMatrix = glm::inverse(EnuToEcefMatrix);
}

void GeoTransform::SetGeographicOrigin(double lon, double lat, double height)
{
	// 设置ENU系统的地理原点
	GeoOriginLon = lon;
	GeoOriginLat = lat;
	GeoOriginHeight = height;
	IsENU = true;

	// 重新计算ENU<->ECEF转换矩阵
	glm::dmat4 EnuToEcefMatrix = CalcEnuToEcefMatrix(lon, lat, height);
	EcefToEnuMatrix = glm::inverse(EnuToEcefMatrix);

	fprintf(stderr, "[GeoTransform] Geographic origin set: lon=%.10f lat=%.10f h=%.3f\n",
		lon, lat, height);
}

void GeoTransform::Cleanup()
{
	// 清理PROJ资源
	if (projTransform)
	{
		proj_destroy(projTransform);
		projTransform = nullptr;
	}

	if (projContext)
	{
		proj_context_destroy(projContext);
		projContext = nullptr;
	}
}

// ============================================================================
// 外部公开接口实现
// ============================================================================

#ifdef ENABLE_PROJ

bool GeoTransform::InitFromEPSG(int epsg_code, double* origin)
{
	if (!origin)
	{
		lastError = "origin is null";
		return false;
	}

	// 创建PROJ上下文（线程安全）
	PJ_CONTEXT* ctx = proj_context_create();
	if (!ctx)
	{
		lastError = "Failed to create PROJ context";
		return false;
	}

	// 构建坐标系转换字符串：EPSG:xxxx -> EPSG:4326(WGS84)
	char crs_from[64], crs_to[64];
	snprintf(crs_from, sizeof(crs_from), "EPSG:%d", epsg_code);
	snprintf(crs_to, sizeof(crs_to), "EPSG:4326");

	fprintf(stderr, "[GeoTransform::InitFromEPSG] %s -> %s\n", crs_from, crs_to);
	fprintf(stderr, "[GeoTransform::InitFromEPSG] Origin: x=%.6f y=%.6f z=%.3f\n",
		origin[0], origin[1], origin[2]);

	// 创建坐标系转换对象
	PJ* transform = proj_create_crs_to_crs(ctx, crs_from, crs_to, nullptr);
	if (!transform)
	{
		char err_msg[256];
		snprintf(err_msg, sizeof(err_msg),
			"Failed to create transformation from %s to %s: %s",
			crs_from, crs_to, proj_errno_string(proj_context_errno(ctx)));
		lastError = err_msg;
		proj_context_destroy(ctx);
		return false;
	}

	// 确保使用传统GIS顺序（经度在前）
	PJ* transform_normalized = proj_normalize_for_visualization(ctx, transform);
	if (transform_normalized)
	{
		proj_destroy(transform);
		transform = transform_normalized;
	}

	// 调用内部初始化函数
	projContext = ctx;
	Init(transform, origin);

	fprintf(stderr, "[GeoTransform::InitFromEPSG] Initialization successful\n");
	return true;
}

bool GeoTransform::InitFromENU(double lon, double lat, double* origin_enu)
{
	if (!origin_enu)
	{
		lastError = "origin_enu is null";
		return false;
	}

	fprintf(stderr, "[GeoTransform::InitFromENU] ENU: lon=%.7f lat=%.7f (offset: %.3f, %.3f, %.3f)\n",
		lon, lat, origin_enu[0], origin_enu[1], origin_enu[2]);

	// 创建PROJ上下文
	PJ_CONTEXT* ctx = proj_context_create();
	if (!ctx)
	{
		lastError = "Failed to create PROJ context";
		return false;
	}

	// ENU使用恒等变换（已经在正确坐标系中）
	PJ* transform = proj_create_crs_to_crs(ctx, "EPSG:4326", "EPSG:4326", nullptr);
	if (!transform)
	{
		lastError = "Failed to create identity transformation";
		proj_context_destroy(ctx);
		return false;
	}

	// 保存上下文
	projContext = ctx;

	// 初始化GeoTransform
	Init(transform, origin_enu);

	// 设置ENU地理原点
	SetGeographicOrigin(lon, lat, 0.0);

	fprintf(stderr, "[GeoTransform::InitFromENU] Initialization successful\n");
	return true;
}

bool GeoTransform::InitFromWKT(const char* wkt, double* origin)
{
	if (!wkt || !origin)
	{
		lastError = "wkt or origin is null";
		return false;
	}

	// 创建PROJ上下文
	PJ_CONTEXT* ctx = proj_context_create();
	if (!ctx)
	{
		lastError = "Failed to create PROJ context";
		return false;
	}

	fprintf(stderr, "[GeoTransform::InitFromWKT] WKT -> EPSG:4326\n");
	fprintf(stderr, "[GeoTransform::InitFromWKT] Origin: x=%.6f y=%.6f z=%.3f\n",
		origin[0], origin[1], origin[2]);

	// 从WKT创建源坐标系
	PJ* crs_src = proj_create(ctx, wkt);
	if (!crs_src)
	{
		char err_msg[256];
		snprintf(err_msg, sizeof(err_msg),
			"Failed to parse WKT: %s",
			proj_errno_string(proj_context_errno(ctx)));
		lastError = err_msg;
		proj_context_destroy(ctx);
		return false;
	}

	// 创建目标坐标系（WGS84）
	PJ* crs_dst = proj_create(ctx, "EPSG:4326");
	if (!crs_dst)
	{
		lastError = "Failed to create EPSG:4326 CRS";
		proj_destroy(crs_src);
		proj_context_destroy(ctx);
		return false;
	}

	// 创建转换
	PJ* transform = proj_create_crs_to_crs_from_pj(ctx, crs_src, crs_dst, nullptr, nullptr);
	proj_destroy(crs_src);
	proj_destroy(crs_dst);

	if (!transform)
	{
		char err_msg[256];
		snprintf(err_msg, sizeof(err_msg),
			"Failed to create transformation: %s",
			proj_errno_string(proj_context_errno(ctx)));
		lastError = err_msg;
		proj_context_destroy(ctx);
		return false;
	}

	// 标准化转换
	PJ* transform_normalized = proj_normalize_for_visualization(ctx, transform);
	if (transform_normalized)
	{
		proj_destroy(transform);
		transform = transform_normalized;
	}

	// 调用内部初始化
	projContext = ctx;
	Init(transform, origin);

	fprintf(stderr, "[GeoTransform::InitFromWKT] Initialization successful\n");
	return true;
}

#else // !ENABLE_PROJ

// 当PROJ未启用时提供stub实现
bool GeoTransform::InitFromEPSG(int epsg_code, double* origin)
{
	lastError = "PROJ library not enabled";
	return false;
}

bool GeoTransform::InitFromENU(double lon, double lat, double* origin_enu)
{
	lastError = "PROJ library not enabled";
	return false;
}

bool GeoTransform::InitFromWKT(const char* wkt, double* origin)
{
	lastError = "PROJ library not enabled";
	return false;
}

#endif // ENABLE_PROJ

const char* GeoTransform::GetLastError()
{
	// 获取最后的错误信息
	return lastError.empty() ? nullptr : lastError.c_str();
}

bool GeoTransform::IsInitialized()
{
	// 检查坐标转换是否已初始化
	return projTransform != nullptr;
}