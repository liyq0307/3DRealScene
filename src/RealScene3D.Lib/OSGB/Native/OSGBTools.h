#ifndef OSGBTOOLS_H
#define OSGBTOOLS_H

#include <cstdarg>
#include <cstdio>
#include <cmath>
#include <string>
#include <filesystem>
#include <fstream>

#include "MeshProcessor.h"

// ==========简化的日志宏，使用MSVC兼容的可变参数宏语法=============
#define LOG_D(format, ...) printf("[DEBUG] " format "\n", ##__VA_ARGS__)
#define LOG_I(format, ...) printf("[INFO] " format "\n", ##__VA_ARGS__)
#define LOG_W(format, ...) printf("[WARN] " format "\n", ##__VA_ARGS__)
#define LOG_E(format, ...) fprintf(stderr, "[ERROR] " format "\n", ##__VA_ARGS__)

#ifdef max
#undef max
#endif // max
#ifdef min
#undef min
#endif // max

/**
 * @brief 坐标转换结构体
 * */
struct Transform
{
	double dRadianX;
	double dRadianY;
	double dMinHeight;
};

/**
 * @brief 包围盒结构体
 */
struct Box
{
	double dMatrix[12];
};

/**
 * @brief 范围结构体
 */
struct Region
{
	double dMinX;
	double dMinY;
	double dMaxX;
	double dMaxY;
	double dMinHeight;
	double dMaxHeight;
};

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
	static bool MkDirs(const std::string& strPath)
	{
		try
		{
			std::filesystem::create_directories(strPath);
			return true;
		}
		catch (...)
		{
			return false;
		}
	}

	// 写文件函数
	static bool WriteFile(const std::string& strFileName, const char* pszBuf, unsigned long nBufLen)
	{
		try
		{
			std::ofstream ofs(strFileName, std::ios::binary);
			if (!ofs.is_open())
			{
				return false;
			}

			ofs.write(pszBuf, nBufLen);
			ofs.close();

			return true;
		}
		catch (...)
		{
			return false;
		}
	}

	// 判断路径是否为目录
	static bool IsDirectory(const std::string& strPath);

	// 在目录中查找根 OSGB 文件
	static std::string FindRootOSGB(const std::string& strDirPath);

	/**
	 * @brief 扫描目录中所有包含OSGB文件的子目录
	 * @param strDirPath 要扫描的目录路径
	 * @return 返回包含OSGB文件的子目录名称列表
	 */
	static std::vector<std::string> ScanOSGBFolders(const std::string& strDirPath);

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
	 * // levels[0]: 100%精度，无Draco
	 * // levels[1]: 70%精度，有Draco
	 * // levels[2]: 50%精度，有Draco
	 * // levels[3]: 30%精度，有Draco
	 */
	static std::vector<LODLevelSettings> BuildLODLevels(
		const std::vector<float>& dRatios,
		float dBaseError,
		const SimplificationParams& simplifyTemplate,
		const DracoCompressionParams& dracoTemplate,
		bool bDracoForLOD0 = false);

	//========= 3D Tiles Tileset文件写入函数===========
	/**
	 * @brief 写入Tileset区域函数
	 * @param pTrans 坐标转换信息指针
	 * @param region 区域信息
	 * @param dGeometricError 几何误差
	 * @param strB3dmFile b3dm文件路径
	 * @param strJsonFile 输出的tileset json文件路径
	 * @return true=成功, false=失败
	 */
	static bool WriteTilesetRegion(Transform* pTrans, Region& region, double dGeometricError, const std::string& strB3dmFile, const std::string& strJsonFile);

	/**
	 * @brief 写入Tileset包围盒函数
	 * @param pTrans 坐标转换信息指针
	 * @param box 包围盒信息
	 * @param dGeometricError 几何误差
	 * @param strB3dmFile b3dm文件路径
	 * @param strJsonFile 输出的tileset json文件路径
	 * @return true=成功, false=失败
	 */
	static bool WriteTilesetBbox(Transform* pTrans, Box& box, double dGeometricError, const std::string& strB3dmFile, const std::string& strJsonFile);

	/**
	 * @brief 写入Tileset函数
	 * @param dLongti 中心点经度（度）
	 * @param dLati 中心点纬度（度）
	 * @param dTileW 瓦片宽度（米）
	 * @param dTileH 瓦片高度（米）
	 * @param dHeightMin 最小高度（米）
	 * @param dHeightMax 最大高度（米）
	 * @param dGeometricError 几何误差
	 * @param strFileName 输出的tileset json文件路径
	 * @param strFullPath 输出的完整路径（包含文件名）
	 * @return true=成功, false=失败
	 */
	static bool WriteTileset(
		double dLongti, double dLati, double dTileW, double dTileH, double dHeightMin, double dHeightMax,
		double dGeometricError, const std::string& strFileName, const std::string& strFullPath);
};

#endif // !OSGBTOOLS_H
