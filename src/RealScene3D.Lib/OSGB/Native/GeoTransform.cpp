#ifdef _WIN32
#include <windows.h>
#include <libloaderapi.h>
#else
#include <unistd.h>
#include <limits.h>
#endif

#include <cstdio>
#include <cmath>
#include <filesystem>
#include <iostream>
#include <iomanip>
#include <sstream>

#include "GeoTransform.h"
#include "OSGBTools.h"

// =================静态成员初始化=================
PJ* GeoTransform::projTransform = nullptr;					// PROJ转换对象
PJ_CONTEXT* GeoTransform::projContext = nullptr;			// PROJ上下文
double GeoTransform::OriginX = 0.0;							// 原点X坐标
double GeoTransform::OriginY = 0.0;							// 原点Y坐标
double GeoTransform::OriginZ = 0.0;							// 原点Z坐标
double GeoTransform::GeoOriginLon = 0.0;					// 地理原点经度
double GeoTransform::GeoOriginLat = 0.0;					// 地理原点纬度
double GeoTransform::GeoOriginHeight = 0.0;					// 地理原点高度
bool GeoTransform::IsENU = false;							// 是否使用ENU坐标系
glm::dmat4 GeoTransform::EcefToEnuMatrix = glm::dmat4(1);	// ECEF到ENU的转换矩阵
std::string GeoTransform::lastError = "";					// 最后的错误信息

// 辅助函数：配置PROJ上下文，设置正确的数据搜索路径
static void ConfigureProjContext(PJ_CONTEXT* ctx)
{
	if (!ctx)
	{
		return;
	}

	// 辅助函数：获取可执行文件所在目录
	auto GetExecutableDirectory = []() -> std::string
	{
#ifdef _WIN32
		char path[MAX_PATH];
		HMODULE hModule = GetModuleHandleA(nullptr);
		if (hModule != nullptr)
		{
			GetModuleFileNameA(hModule, path, MAX_PATH);
			std::filesystem::path exePath(path);
			return exePath.parent_path().string();
		}
#else
		char path[PATH_MAX];
		ssize_t count = readlink("/proc/self/exe", path, PATH_MAX);
		if (count != -1)
		{
			path[count] = '\0';
			std::filesystem::path exePath(path);
			return exePath.parent_path().string();
		}
#endif
		return "";
	};

	// 1. 获取可执行文件目录
	std::string exeDir = GetExecutableDirectory();
	if (exeDir.empty())
	{
		OSGBLog::LOG_W("[PROJ] Cannot determine executable directory");
		return;
	}

	// 2. 构建proj.db预期路径（与可执行文件同目录）
	std::filesystem::path projDbPath = std::filesystem::path(exeDir) / "proj.db";

	// 3. 检查proj.db是否存在
	if (!std::filesystem::exists(projDbPath))
	{
		OSGBLog::LOG_W("[PROJ] proj.db not found at: {}", projDbPath.string());
		OSGBLog::LOG_W("[PROJ] PROJ will use system default search paths (may cause version conflicts)");
		return;
	}

	// 4. 设置PROJ数据搜索路径（优先使用可执行文件目录）
	const char* search_paths[] = { exeDir.c_str() };
	proj_context_set_search_paths(ctx, 1, search_paths);

	OSGBLog::LOG_I("[PROJ] Data directory set to: {}", exeDir);
	OSGBLog::LOG_I("[PROJ] Using proj.db: {}", projDbPath.string());
}

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

	OSGBLog::LOG_I("[GeoTransform] Origin: x={:.8f} y={:.8f} z={:.3f}", originLocal.x, originLocal.y, originLocal.z);

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

			OSGBLog::LOG_I("[GeoTransform] Cartographic: lon={:.10f} lat={:.10f} h={:.3f}", originCartographic.x, originCartographic.y, originCartographic.z);
		}
		else
		{
			OSGBLog::LOG_W("[GeoTransform] Coordinate transformation failed!");
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

	OSGBLog::LOG_I("[GeoTransform] Geographic origin set: lon={:.10f} lat={:.10f} h={:.3f}", lon, lat, height);
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
bool GeoTransform::InitFromEPSG(int epsg_code, double* origin)
{
#ifdef ENABLE_PROJ
	// 清理旧资源，防止重复初始化导致内存泄漏
	if (projContext || projTransform)
	{
		Cleanup();
	}

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

	// 配置PROJ数据搜索路径，避免使用系统PATH中的旧版proj.db
	ConfigureProjContext(ctx);

	// 构建坐标系转换字符串：EPSG:xxxx -> EPSG:4326(WGS84)
	std::string crs_from = "EPSG:" + std::to_string(epsg_code);
	std::string crs_to = "EPSG:4326";

	OSGBLog::LOG_I("[GeoTransform::InitFromEPSG] {} -> {}", crs_from, crs_to);
	OSGBLog::LOG_I("[GeoTransform::InitFromEPSG] Origin: x={:.6f} y={:.6f} z={:.3f}", origin[0], origin[1], origin[2]);

	// 创建坐标系转换对象
	PJ* transform = proj_create_crs_to_crs(ctx, crs_from.c_str(), crs_to.c_str(), nullptr);
	if (!transform)
	{
		std::ostringstream oss;
		oss << "Failed to create transformation from " << crs_from << " to " << crs_to
			<< ": " << proj_errno_string(proj_context_errno(ctx));
		lastError = oss.str();
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

	OSGBLog::LOG_I("[GeoTransform::InitFromEPSG] Initialization successful");
	return true;
#else
	lastError = "PROJ library not enabled";
	return false;
#endif
}

bool GeoTransform::InitFromENU(double lon, double lat, double* origin_enu)
{
#ifdef ENABLE_PROJ
	// 清理旧资源，防止重复初始化导致内存泄漏
	if (projContext || projTransform)
	{
		Cleanup();
	}

	if (!origin_enu)
	{
		lastError = "origin_enu is null";
		return false;
	}

	OSGBLog::LOG_I("[GeoTransform::InitFromENU] ENU: lon={:.7f} lat={:.7f} (offset: {:.3f}, {:.3f}, {:.3f})", lon, lat, origin_enu[0], origin_enu[1], origin_enu[2]);

	// 创建PROJ上下文
	PJ_CONTEXT* ctx = proj_context_create();
	if (!ctx)
	{
		lastError = "Failed to create PROJ context";
		return false;
	}

	// 配置PROJ数据搜索路径，避免使用系统PATH中的旧版proj.db
	ConfigureProjContext(ctx);

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

	OSGBLog::LOG_I("[GeoTransform::InitFromENU] Initialization successful");
	return true;
#else
	lastError = "PROJ library not enabled";
	return false;
#endif
}

bool GeoTransform::InitFromWKT(const char* wkt, double* origin)
{
#ifdef ENABLE_PROJ
	// 清理旧资源，防止重复初始化导致内存泄漏
	if (projContext || projTransform)
	{
		Cleanup();
	}

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

	// 配置PROJ数据搜索路径，避免使用系统PATH中的旧版proj.db
	ConfigureProjContext(ctx);

	OSGBLog::LOG_I("[GeoTransform::InitFromWKT] WKT -> EPSG:4326");
	OSGBLog::LOG_I("[GeoTransform::InitFromWKT] Origin: x={:.6f} y={:.6f} z={:.3f}", origin[0], origin[1], origin[2]);

	// 从WKT创建源坐标系
	PJ* crs_src = proj_create(ctx, wkt);
	if (!crs_src)
	{
		std::ostringstream oss;
		oss << "Failed to parse WKT: " << proj_errno_string(proj_context_errno(ctx));
		lastError = oss.str();
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
		std::ostringstream oss;
		oss << "Failed to create transformation: " << proj_errno_string(proj_context_errno(ctx));
		lastError = oss.str();
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

	OSGBLog::LOG_I("[GeoTransform::InitFromWKT] Initialization successful");
	return true;
#else
	lastError = "PROJ library not enabled";
	return false;
#endif // ENABLE_PROJ
}

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