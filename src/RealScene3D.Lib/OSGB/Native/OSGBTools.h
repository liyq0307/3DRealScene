#ifndef OSGBTOOLS_H
#define OSGBTOOLS_H

#include <cstdarg>
#include <iostream>
#include <cstdio>
#include <cmath>
#include <string>
#include <vector>
#include <functional>
#include <filesystem>
#include <fstream>

#include "MeshProcessor.h"
#include "Tileset.h"  

namespace OSGBLog
{
	// 辅助函数：格式化输出到 stdout
	template<typename... Args>
	inline void PrintInfo(const char* level, const char* format, Args&&... args)
	{
		std::printf("[%s] ", level);
		std::printf(format, std::forward<Args>(args)...);
		std::printf("\n");
	}

	// 辅助函数：格式化输出到 stderr
	template<typename... Args>
	inline void PrintError(const char* level, const char* format, Args&&... args)
	{
		std::fprintf(stderr, "[%s] ", level);
		std::fprintf(stderr, format, std::forward<Args>(args)...);
		std::fprintf(stderr, "\n");
	}

	// 辅助函数：格式化字符串
	template < typename... Args>
	std::string FormatString(const char* format, Args&&... args)
	{
		// 计算所需缓冲区大小

		int size = std::snprintf(nullptr, 0, format, std::forward<Args>(args)...);
		if (size <= 0)
		{
			return "";
		}

		// 分配缓冲区并格式化
		std::snprintf(&result[0], size + 1, format, std::forward<Args>(args)...);
		result.resize(size);  // 移除末尾的 '\0'

		return result;
	}
}

#define LOG_D(format, ...) OSGBLog::PrintInfo("DEBUG", format, ##__VA_ARGS__)
#define LOG_I(format, ...) OSGBLog::PrintInfo("INFO", format, ##__VA_ARGS__)
#define LOG_W(format, ...) OSGBLog::PrintInfo("WARN", format, ##__VA_ARGS__)
#define LOG_E(format, ...) OSGBLog::PrintError("ERROR", format, ##__VA_ARGS__)

/**
 * @brief metadata.xml 元数据结构
 */
struct OSGBMetadata
{
	// 版本
	std::string strVersion;

	// 坐标系统 (例如 "ENU:31.15152,114.51554" 或 "EPSG:4547" 或 WKT字符串)
	std::string strSrs;

	// 原点偏移 (例如 "-0,-0,0")
	std::string strSrsOrigin;

	// =====解析后的值=======
	// 是否为ENU坐标系
	bool bIsENU = false;

	// 是否为EPSG代码
	bool bIsEPSG = false;

	// WKT 格式投影
	bool bIsWKT = false;

	// EPSG代码
	int nEpsgCode = 0;

	// 纬度
	double dCenterLat = 0.0;

	// 经度
	double dCenterLon = 0.0;

	// SRSOrigin X偏移
	double dOffsetX = 0.0;

	// SRSOrigin Y偏移
	double dOffsetY = 0.0;

	// SRSOrigin Z偏移
	double dOffsetZ = 0.0;
};

/**
 * @brief 单个LOD级别的配置
 *
 * 每个LOD级别对应一个不同精度的GLB输出文件
 */
struct LODLevelSettings
{
	// 目标简化比例：1.0 = 完整精度；0.5 = 保留50%三角面
	float dTargetRatio = 1.0f;

	// 简化误差预算（匹配 SimplificationParams）
	float dTargetError = 0.01f;

	// 是否对此LOD级别启用网格简化
	bool bEnableSimplification = false;

	// 是否对此LOD级别应用Draco压缩
	bool bEnableDraco = false;

	// 基础简化参数（ratio/error会被上面的值覆盖）
	SimplificationParams simplify;

	// 基础Draco压缩参数
	DracoCompressionParams draco;
};

/**
 * @brief LOD流水线配置
 *
 * 管理整个LOD层级生成过程
 */
struct LODPipelineSettings
{
	// 主开关：false时只生成LOD0
	bool bEnableLOD = false;

	// LOD级别列表（从粗到细或从细到粗，取决于使用场景）            
	std::vector<LODLevelSettings> levels;
};

/**
 * @brief 工具类，主要用于坐标转换和矩阵计算等
 */
class OSGBTools
{
public:
	// 构造函数
	OSGBTools() = default;

	// 析构函数
	~OSGBTools() = default;

	// ==========文件操作辅助函数===========
	// 创建多级目录
	static bool MkDirs(const std::string& strPath);

	// 写文件函数
	static bool WriteFile(const std::string& strFileName, const char* pszBuf, unsigned long nBufLen);

	// 判断路径是否为目录
	static bool IsDirectory(const std::string& strPath);

	// 判断路径是否为常规文件
	static bool IsRegularFile(const std::string& strPath);

	/**
	 * @brief 目录条目结构
	 */
	struct DirectoryEntry
	{
		// 文件/目录名（不含路径）
		std::string name;

		// 是否为目录
		bool is_directory = false;

		// 是否为常规文件
		bool is_regular_file = false;
	};

	/**
	 * @brief 遍历目录中的所有条目
	 * @param strDirPath 目录路径
	 * @param callback 回调函数，参数为 DirectoryEntry，返回 false 可提前终止遍历
	 * @return 成功返回 true，失败返回 false
	 */
	static bool ForEachEntry(const std::string& strDirPath, std::function<bool(const DirectoryEntry&)> callback);

	// 在目录中查找根 OSGB 文件
	static std::string FindRootOSGB(const std::string& strDirPath);

	/**
	 * @brief 扫描目录中所有包含OSGB文件的子目录
	 * @param strDirPath 要扫描的目录路径
	 * @return 返回包含OSGB文件的子目录名称列表
	 */
	static std::vector<std::string> ScanOSGBFolders(const std::string& strDirPath);

	/**
	 * @brief 扫描倾斜摄影数据的 Tile_* 目录
	 *
	 * 此函数专门用于扫描倾斜摄影数据目录结构，查找所有 Tile_* 格式的子目录，
	 * 并验证每个目录中是否存在对应的 <tile_name>.osgb 文件。
	 *
	 * @param strDirPath 要扫描的目录路径（通常是 Data 目录）
	 * @return 返回符合条件的 Tile 目录名称列表（如 "Tile_+004_+012"）
	 *
	 * @example
	 * // 扫描倾斜摄影数据目录
	 * auto tiles = OSGBTools::ScanTileDirectories("E:/Data/3D/Data");
	 * // 返回: ["Tile_+004_+012", "Tile_+005_+013", ...]
	 */
	static std::vector<std::string> ScanTileDirectories(const std::string& strDirPath);

	/**
	 * @brief 扫描目录中所有OSGB文件
	 * @param strDirPath 要扫描的目录路径
	 * @param bRecursive 是否递归扫描子目录
	 * @return 返回OSGB文件路径列表
	 */
	static std::vector<std::string> ScanOSGBFiles(const std::string& strDirPath, bool bRecursive = false);

	// 获取父目录路径
	static std::string GetParent(std::string str);

	// 获取文件名
	static std::string GetFileName(std::string strPath);

	// 字符串替换辅助函数
	static std::string Replace(std::string str, std::string s0, std::string s1);

	// 转换为 OSG 字符串格式
	static std::string OSGString(const std::string& strPath);

	// 转换为 UTF8 字符串格式
	static std::string Utf8String(const std::string& strPath);

	// 从文件名获取级别编号
	static int GetLvlNum(std::string strFileName);

	// ==========坐标转换辅助函数===========
	// 弧度与角度转换函数
	static double Degree2Rad(double dVal);

	// 纬度/经度与米制转换函数
	static double Lati2Meter(double dDiff);

	// 经度差转换为米制，需提供纬度用于计算
	static double Longti2Meter(double dDiff, double dLati);

	// 米制转换为纬度/经度差函数
	static double Meter2Lati(double dM);

	// 米制转换为经度差函数，需提供纬度用于计算
	static double Meter2Longti(double dM, double dLati);

	//========= 坐标转换矩阵计算函数===========
	/**
	 * @brief 计算从ENU坐标系到ECEF坐标系的转换矩阵
	 * @param dCenterX 中心点经度（度）
	 * @param dCenterY 中心点纬度（度）
	 * @param dHeightMin 最小高度（米）
	 * @param pPtr 输出的转换矩阵指针，指向一个包含16个double元素的数组
	 * @return void
	 */
	static void TransformC(double dCenterX, double dCenterY, double dHeightMin, double* pPtr);

	/**
	 * @brief 计算从带ENU偏移的坐标系到ECEF坐标系的转换矩阵
	 * @param dCenterX 中心点经度（度）
	 * @param dCenterY 中心点纬度（度）
	 * @param dHeightMin 最小高度（米）
	 * @param dEnuOffsetX ENU坐标系X轴偏移（米）
	 * @param dEnuOffsetY ENU坐标系Y轴偏移（米）
	 * @param dEnuOffsetZ ENU坐标系Z轴偏移（米）
	 * @param pPtr 输出的转换矩阵指针，指向一个包含16个double元素的数组
	 * @return void
	 */
	static void TransformCWithEnuOffset(
		double dCenterX, double dCenterY, double dHeightMin,
		double dEnuOffsetX, double dEnuOffsetY, double dEnuOffsetZ, double* pPtr);

	// ========= Metadata解析函数 ===========
	/**
	 * @brief 解析 metadata.xml 文件
	 * @param strXmlPath metadata.xml 文件路径
	 * @param outMetadata 输出解析后的元数据
	 * @return true=成功, false=失败
	 */
	static bool ParseMetadataXml(const std::string& strXmlPath, OSGBMetadata& outMetadata);

	// ======== LOD级别配置生成函数 ===========
	/**
	 * @brief 从简化比例数组构建LOD级别配置
	 *
	 * 使用提供的比例数组和模板参数自动生成多个LOD级别配置。
	 *
	 * @param dRatios 简化比例数组，按顺序应用到各级别（如 {1.0f, 0.7f, 0.5f, 0.3f}）
	 * @param dBaseError 所有级别使用的基础简化误差
	 * @param simplifyTemplate 网格简化模板参数
	 * @param dracoTemplate Draco压缩模板参数
	 * @param bDracoForLOD0 是否对LOD0应用Draco压缩（默认false，LOD0通常保持未压缩以便快速加载）
	 * @return 配置好的LOD级别数组
	 *
	 * @example
	 * // 创建4级LOD，精度递减
	 * SimplificationParams simplify = {.bEnableSimplification = true, .dTargetError = 0.01f};
	 * DracoCompressionParams draco = {.bEnableCompression = true};
	 * auto levels = BuildLODLevels({1.0f, 0.7f, 0.5f, 0.3f}, 0.01f, simplify, draco, false);
	 * levels[0]: 100%精度，无Draco
	 * levels[1]: 70%精度，有Draco
	 * levels[2]: 50%精度，有Draco
	 * levels[3]: 30%精度，有Draco
	 */
	static std::vector<LODLevelSettings> BuildLODLevels(
		const std::vector<float>& dRatios,
		float dBaseError,
		const SimplificationParams& simplifyTemplate,
		const DracoCompressionParams& dracoTemplate,
		bool bDracoForLOD0 = false);
};

#endif // !OSGBTOOLS_H
