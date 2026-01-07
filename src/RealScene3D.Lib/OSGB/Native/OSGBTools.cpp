#include <vector>
#include <cstring>
#include <cstdio>
#include <sstream>
#include <algorithm>
#include <iostream>
#include <iomanip>

#ifdef _WIN32
#include <Windows.h>
#else
#include <dirent.h>
#include <sys/stat.h>
#endif

#include <osgDB/ConvertUTF>

#include "OSGBTools.h"
#include "GeoTransform.h"

static const double dPI = std::acos(-1);

bool OSGBTools::MkDirs(const std::string& strPath)
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

bool OSGBTools::WriteFile(const std::string& strFileName, const char* pszBuf, unsigned long nBufLen)
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

bool OSGBTools::IsDirectory(const std::string& strPath)
{
#ifdef _WIN32
	DWORD attrs = GetFileAttributesA(strPath.c_str());
	return (attrs != INVALID_FILE_ATTRIBUTES) && (attrs & FILE_ATTRIBUTE_DIRECTORY);
#else
	struct stat st;
	return (stat(strPath.c_str(), &st) == 0) && S_ISDIR(st.st_mode);
#endif
}

bool OSGBTools::IsRegularFile(const std::string& strPath)
{
#ifdef _WIN32
	DWORD attrs = GetFileAttributesA(strPath.c_str());
	return (attrs != INVALID_FILE_ATTRIBUTES) && !(attrs & FILE_ATTRIBUTE_DIRECTORY);
#else
	struct stat st;
	return (stat(strPath.c_str(), &st) == 0) && S_ISREG(st.st_mode);
#endif
}

bool OSGBTools::ForEachEntry(const std::string& strDirPath,
	std::function<bool(const DirectoryEntry&)> callback)
{
	// 标准化路径分隔符
	std::string strNormalizedPath = strDirPath;
	for (char& c : strNormalizedPath)
	{
		if (c == '\\') c = '/';
	}

	// 移除尾部斜杠
	if (!strNormalizedPath.empty() && strNormalizedPath.back() == '/')
	{
		strNormalizedPath.pop_back();
	}

#ifdef _WIN32
	WIN32_FIND_DATAA findData;
	std::string search_pattern = strNormalizedPath + "/*";
	HANDLE hFind = FindFirstFileA(search_pattern.c_str(), &findData);

	if (hFind == INVALID_HANDLE_VALUE)
	{
		return false;
	}

	do
	{
		DirectoryEntry entry;
		entry.name = findData.cFileName;
		entry.is_directory = (findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
		entry.is_regular_file = !entry.is_directory;

		// 跳过 . 和 ..
		if (entry.name == "." || entry.name == "..")
		{
			continue;
		}

		// 调用回调，如果返回 false 则提前终止
		if (!callback(entry))
		{
			FindClose(hFind);
			return true;
		}
	} while (FindNextFileA(hFind, &findData));

	FindClose(hFind);
	return true;
#else
	// Linux/macOS 实现
	DIR* dir = opendir(strNormalizedPath.c_str());
	if (dir == nullptr)
	{
		return false;
	}

	struct dirent* dirent_entry;
	while ((dirent_entry = readdir(dir)) != nullptr)
	{
		DirectoryEntry entry;
		entry.name = dirent_entry->d_name;

		// 跳过 . 和 ..
		if (entry.name == "." || entry.name == "..")
		{
			continue;
		}

		// 获取文件类型
		entry.is_directory = (dirent_entry->d_type == DT_DIR);
		entry.is_regular_file = (dirent_entry->d_type == DT_REG);

		// 如果 d_type 不可用（某些文件系统），使用 stat
		if (dirent_entry->d_type == DT_UNKNOWN)
		{
			std::string full_path = strNormalizedPath + "/" + entry.name;
			struct stat st;
			if (stat(full_path.c_str(), &st) == 0)
			{
				entry.is_directory = S_ISDIR(st.st_mode);
				entry.is_regular_file = S_ISREG(st.st_mode);
			}
		}

		// 调用回调，如果返回 false 则提前终止
		if (!callback(entry))
		{
			closedir(dir);
			return true;
		}
	}

	closedir(dir);
	return true;
#endif
}

std::string OSGBTools::FindRootOSGB(const std::string& strDirPath)
{
	// 标准化路径分隔符
	std::string strNormalizedPath = strDirPath;
	for (char& c : strNormalizedPath)
	{
		if (c == '\\') c = '/';
	}
	// 移除尾部斜杠
	if (!strNormalizedPath.empty() && strNormalizedPath.back() == '/')
	{
		strNormalizedPath.pop_back();
	}

	// 在目录中搜索根 OSGB 的辅助函数
	auto search_dir = [](const std::string& strSearchPath) -> std::string
		{
			std::string result;

			ForEachEntry(strSearchPath, [&](const DirectoryEntry& entry) -> bool
				{
					if (!entry.is_directory)
					{
						return true; // 继续遍历
					}

					// 在子目录中搜索 OSGB 文件
					std::string subdir_path = strSearchPath + "/" + entry.name;

					ForEachEntry(subdir_path, [&](const DirectoryEntry& subEntry) -> bool
						{
							if (!subEntry.is_regular_file)
							{
								return true;
							}

							// 检查是否为 OSGB 文件且不包含 "_L" 级别指示符
							if (subEntry.name.length() > 5 &&
								subEntry.name.substr(subEntry.name.length() - 5) == ".osgb" &&
								subEntry.name.find("_L") == std::string::npos)
							{
								result = subdir_path + "/" + subEntry.name;
								return false;  // 找到了，提前终止
							}

							return true;
						});

					// 如果找到了，提前终止外层遍历
					return result.empty();
				});

			return result;
		};

	// 检查输入路径本身是否看起来像 Data 目录（包含带有 OSGB 文件的子目录）
	std::string result = search_dir(strNormalizedPath);
	if (!result.empty())
	{
		return result;
	}

	// 尝试 Data 子目录
	std::string strDataDir = strNormalizedPath + "/Data";
	if (IsDirectory(strDataDir))
	{
		result = search_dir(strDataDir);
		if (!result.empty())
		{
			return result;
		}
	}

	return "";  // Not found
}

std::vector<std::string> OSGBTools::ScanOSGBFolders(const std::string& strDirPath)
{
	std::vector<std::string> folders;

	// 标准化路径分隔符
	std::string strNormalizedPath = strDirPath;
	for (char& c : strNormalizedPath)
	{
		if (c == '\\') c = '/';
	}

	// 移除尾部斜杠
	if (!strNormalizedPath.empty() && strNormalizedPath.back() == '/')
	{
		strNormalizedPath.pop_back();
	}

	ForEachEntry(strNormalizedPath, [&](const DirectoryEntry& entry) -> bool
		{
			if (!entry.is_directory)
			{
				return true;
			}

			// 检查子目录是否包含OSGB文件
			std::string subdir_path = strNormalizedPath + "/" + entry.name;
			bool has_osgb = false;

			ForEachEntry(subdir_path, [&](const DirectoryEntry& subEntry) -> bool
				{
					if (subEntry.is_regular_file &&
						subEntry.name.length() > 5 &&
						subEntry.name.substr(subEntry.name.length() - 5) == ".osgb")
					{
						has_osgb = true;
						return false;  // 找到了，提前终止
					}

					return true;
				});

			if (has_osgb)
			{
				folders.emplace_back(entry.name);
			}

			return true;  // 继续遍历
		});

	return folders;
}

std::vector<std::string> OSGBTools::ScanTileDirectories(const std::string& strDirPath)
{
	std::vector<std::string> tiles;

	// 标准化路径分隔符
	std::string strNormalizedPath = strDirPath;
	for (char& c : strNormalizedPath)
	{
		if (c == '\\') c = '/';
	}

	// 移除尾部斜杠
	if (!strNormalizedPath.empty() && strNormalizedPath.back() == '/')
	{
		strNormalizedPath.pop_back();
	}

	ForEachEntry(strNormalizedPath, [&](const DirectoryEntry& entry) -> bool
		{
			if (!entry.is_directory)
			{
				return true;
			}

			// 检查是否为 Tile_ 开头的目录
			if (entry.name.find("Tile_") != 0)
			{
				return true;
			}

			// 检查是否存在对应的 OSGB 文件
			std::string tile_dir = strNormalizedPath + "/" + entry.name;
			std::string osgb_file = tile_dir + "/" + entry.name + ".osgb";

			if (IsRegularFile(osgb_file))
			{
				tiles.emplace_back(entry.name);
			}

			return true;  // 继续遍历
		});

	return tiles;
}

std::vector<std::string> OSGBTools::ScanOSGBFiles(const std::string& strDirPath, bool bRecursive)
{
	std::vector<std::string> files;

	// 标准化路径分隔符
	std::string strNormalizedPath = strDirPath;
	for (char& c : strNormalizedPath)
	{
		if (c == '\\') c = '/';
	}

	// 移除尾部斜杠
	if (!strNormalizedPath.empty() && strNormalizedPath.back() == '/')
	{
		strNormalizedPath.pop_back();
	}

	ForEachEntry(strNormalizedPath, [&](const DirectoryEntry& entry) -> bool
		{
			if (entry.is_regular_file)
			{
				// 检查是否为 OSGB 文件
				if (entry.name.length() > 5 &&
					entry.name.substr(entry.name.length() - 5) == ".osgb")
				{
					files.emplace_back(strNormalizedPath + "/" + entry.name);
				}
			}
			else if (bRecursive && entry.is_directory)
			{
				// 递归扫描子目录
				std::string subdir_path = strNormalizedPath + "/" + entry.name;
				auto subFiles = ScanOSGBFiles(subdir_path, true);
				files.insert(files.end(), subFiles.begin(), subFiles.end());
			}

			return true;  // 继续遍历
		});

	return files;
}

std::string OSGBTools::GetParent(std::string str)
{
	auto p0 = str.find_last_of("/\\");
	if (p0 != std::string::npos)
	{
		return str.substr(0, p0);
	}
	else
	{
		return "";
	}
}

std::string OSGBTools::GetFileName(std::string strPath)
{
	auto p0 = strPath.find_last_of("/\\");
	if (p0 == std::string::npos)
	{
		return strPath;
	}

	return strPath.substr(p0 + 1);
}

std::string OSGBTools::Replace(std::string str, std::string s0, std::string s1)
{
	auto p0 = str.find(s0);
	if (p0 == std::string::npos)
	{
		return str;
	}
	return str.replace(p0, s0.length(), s1);
}

std::string OSGBTools::OSGString(const std::string& strPath)
{
#ifdef WIN32
	std::string root_path = osgDB::convertStringFromUTF8toCurrentCodePage(strPath);
#else
	std::string root_path = strPath;
#endif // WIN32
	return root_path;
}

std::string OSGBTools::Utf8String(const std::string& strPath)
{
#ifdef WIN32
	std::string utf8 = osgDB::convertStringFromCurrentCodePageToUTF8(strPath);
#else
	std::string utf8 = strPath;
#endif // WIN32
	return utf8;
}

int OSGBTools::GetLvlNum(std::string strFileName)
{
	std::string stem = GetFileName(strFileName);
	auto p0 = stem.find("_L");
	auto p1 = stem.find("_", p0 + 2);
	if (p0 != std::string::npos && p1 != std::string::npos)
	{
		std::string substr = stem.substr(p0 + 2, p1 - p0 - 2);
		try
		{
			return std::stol(substr);
		}
		catch (...)
		{
			return -1;
		}
	}
	else if (p0 != std::string::npos)
	{
		int end = p0 + 2;
		while (true)
		{
			if (isdigit(stem[end]))
				end++;
			else
				break;
		}

		std::string substr = stem.substr(p0 + 2, end - p0 - 2);
		try
		{
			return std::stol(substr);
		}
		catch (...)
		{
			return -1;
		}
	}

	return -1;
}

double OSGBTools::Degree2Rad(double dVal)
{
	return dVal * dPI / 180.0;
}

double OSGBTools::Lati2Meter(double dDiff)
{
	return dDiff / 0.000000157891;
}

double OSGBTools::Longti2Meter(double dDiff, double dLati)
{
	return dDiff / 0.000000156785 * std::cos(dLati);
}

double OSGBTools::Meter2Lati(double dM)
{
	return dM * 0.000000157891;
}

double OSGBTools::Meter2Longti(double dM, double dLati)
{
	return dM * 0.000000156785 / std::cos(dLati);
}

// 计算ENU->ECEF变换矩阵的辅助函数
std::vector<double> transfrom_xyz(double dLonDeg, double dLatDeg, double dHeightMin)
{
	// 使用GeoTransform的实现计算ENU->ECEF变换矩阵
	glm::dmat4 dMatrix = GeoTransform::CalcEnuToEcefMatrix(dLonDeg, dLatDeg, dHeightMin);

	// 将glm::dmat4转换为std::vector<double>（列主序）
	std::vector<double> dResult(16);
	for (int nCol = 0; nCol < 4; nCol++)
	{
		for (int nRow = 0; nRow < 4; nRow++)
		{
			dResult[nCol * 4 + nRow] = dMatrix[nCol][nRow];
		}
	}

	return dResult;
}

void OSGBTools::TransformC(double dCenterX, double dCenterY, double dHeightMin, double* pPtr)
{
	std::vector<double> v = transfrom_xyz(dCenterX, dCenterY, dHeightMin);
	std::cerr << "[TransformC] lon=" << std::fixed << std::setprecision(10) << dCenterX
		<< " lat=" << dCenterY << " h=" << std::setprecision(3) << dHeightMin
		<< " -> ECEF translation: x=" << std::setprecision(10) << v[12]
		<< " y=" << v[13] << " z=" << v[14] << std::endl;

	std::copy(v.begin(), v.end(), pPtr);
}

void OSGBTools::TransformCWithEnuOffset(double dCenterX, double dCenterY, double dHeightMin, double dEnuOffsetX, double dEnuOffsetY, double dEnuOffsetZ, double* pPtr)
{
	std::vector<double> v = transfrom_xyz(dCenterX, dCenterY, dHeightMin);
	std::cerr << "[TransformCWithEnuOffset] Base ECEF at lon=" << std::fixed << std::setprecision(10) << dCenterX
		<< " lat=" << dCenterY << " h=" << std::setprecision(3) << dHeightMin
		<< ": x=" << std::setprecision(10) << v[12]
		<< " y=" << v[13] << " z=" << v[14] << std::endl;

	double dLatRad = dCenterY * dPI / 180.0;
	double dLonRad = dCenterX * dPI / 180.0;

	double dSinLat = std::sin(dLatRad);
	double dCosLat = std::cos(dLatRad);
	double dSinLon = std::sin(dLonRad);
	double dCosLon = std::cos(dLonRad);

	double dEcefOffsetX = -dSinLon * dEnuOffsetX - dSinLat * dCosLon * dEnuOffsetY + dCosLat * dCosLon * dEnuOffsetZ;
	double dEcefOffsetY = dCosLon * dEnuOffsetX - dSinLat * dSinLon * dEnuOffsetY + dCosLat * dSinLon * dEnuOffsetZ;
	double dEcefOffsetZ = dCosLat * dEnuOffsetY + dSinLat * dEnuOffsetZ;
	std::cerr << "[TransformCWithEnuOffset] ENU offset (" << std::setprecision(3) << dEnuOffsetX
		<< ", " << dEnuOffsetY << ", " << dEnuOffsetZ
		<< ") -> ECEF offset (" << std::setprecision(10) << dEcefOffsetX
		<< ", " << dEcefOffsetY << ", " << dEcefOffsetZ << ")" << std::endl;

	v[12] += dEcefOffsetX;
	v[13] += dEcefOffsetY;
	v[14] += dEcefOffsetZ;

	std::cerr << "[TransformCWithEnuOffset] Final ECEF translation: x=" << v[12]
		<< " y=" << v[13] << " z=" << v[14] << std::endl;

	std::copy(v.begin(), v.end(), pPtr);
}

// 简单的 XML 标签提取函数
std::string extractXmlTag(const std::string& xml, const std::string& tag)
{
	std::string startTag = "<" + tag + ">";
	std::string endTag = "</" + tag + ">";

	size_t start = xml.find(startTag);
	if (start == std::string::npos)
	{
		return "";
	}

	start += startTag.length();
	size_t end = xml.find(endTag, start);
	if (end == std::string::npos)
	{
		return "";
	}

	return xml.substr(start, end - start);
}

// 字符串分割函数
std::vector<std::string> split(const std::string& str, char delimiter)
{
	std::vector<std::string> tokens;
	std::stringstream ss(str);
	std::string token;
	while (std::getline(ss, token, delimiter))
	{
		tokens.emplace_back(token);
	}

	return tokens;
}

// 去除字符串前后空白
std::string trim(const std::string& str)
{
	size_t start = str.find_first_not_of(" \t\r\n");
	size_t end = str.find_last_not_of(" \t\r\n");
	if (start == std::string::npos || end == std::string::npos)
	{
		return "";
	}

	return str.substr(start, end - start + 1);
}

bool OSGBTools::ParseMetadataXml(const std::string& strXmlPath, OSGBMetadata& outMetadata)
{
	// 读取文件
	std::ifstream file(strXmlPath);
	if (!file.is_open())
	{
		LOG_E("Failed to open metadata.xml: %s", strXmlPath.c_str());
		return false;
	}

	std::stringstream buffer;
	buffer << file.rdbuf();
	std::string strXmlContent = buffer.str();
	file.close();

	// 解析 version
	outMetadata.strVersion = extractXmlTag(strXmlContent, "ModelMetadata version");
	if (outMetadata.strVersion.empty())
	{
		outMetadata.strVersion = "1";
	}

	// 解析 SRS
	outMetadata.strSrs = extractXmlTag(strXmlContent, "SRS");
	if (outMetadata.strSrs.empty())
	{
		LOG_E("SRS tag not found in metadata.xml");
		return false;
	}

	// 解析 SRSOrigin
	outMetadata.strSrsOrigin = extractXmlTag(strXmlContent, "SRSOrigin");
	if (outMetadata.strSrsOrigin.empty())
	{
		LOG_E("SRSOrigin tag not found in metadata.xml");
		return false;
	}

	// 解析 SRS 内容
	std::vector<std::string> srsParts = split(outMetadata.strSrs, ':');
	if (srsParts.size() >= 2)
	{
		std::string srsType = trim(srsParts[0]);

		if (srsType == "ENU")
		{
			// ENU:纬度,经度
			outMetadata.bIsENU = true;
			std::vector<std::string> coords = split(srsParts[1], ',');
			if (coords.size() >= 2)
			{
				try
				{
					outMetadata.dCenterLat = std::stod(trim(coords[0]));
					outMetadata.dCenterLon = std::stod(trim(coords[1]));
				}
				catch (...)
				{
					LOG_E("Failed to parse ENU coordinates");
					return false;
				}
			}
			else
			{
				LOG_E("ENU coordinates format invalid");
				return false;
			}
		}
		else if (srsType == "EPSG")
		{
			// EPSG:4547
			outMetadata.bIsEPSG = true;
			try
			{
				outMetadata.nEpsgCode = std::stoi(trim(srsParts[1]));
			}
			catch (...)
			{
				LOG_E("Failed to parse EPSG code");
				return false;
			}
		}
		else
		{
			// 不是 ENU 或 EPSG，可能是其他格式，当作 WKT 处理
			LOG_W("Unknown SRS type: %s, treating as WKT format", srsType.c_str());
			outMetadata.bIsWKT = true;
		}
	}
	else
	{
		// 没有冒号分隔符，当作 WKT 格式处理
		LOG_I("SRS format without colon separator, treating as WKT projection");
		outMetadata.bIsWKT = true;
	}

	// 解析 SRSOrigin (x,y,z)
	std::vector<std::string> originParts = split(outMetadata.strSrsOrigin, ',');
	if (originParts.size() >= 2)
	{
		try
		{
			outMetadata.dOffsetX = std::stod(trim(originParts[0]));
			outMetadata.dOffsetY = std::stod(trim(originParts[1]));
			if (originParts.size() >= 3)
			{
				outMetadata.dOffsetZ = std::stod(trim(originParts[2]));
			}
		}
		catch (...)
		{
			LOG_E("Failed to parse SRSOrigin coordinates");
			return false;
		}
	}
	else
	{
		LOG_E("SRSOrigin format invalid (expected x,y,z)");
		return false;
	}

	LOG_I("Parsed metadata.xml successfully:");
	LOG_I("  SRS: %s", outMetadata.strSrs.c_str());
	LOG_I("  SRSOrigin: %s", outMetadata.strSrsOrigin.c_str());
	if (outMetadata.bIsENU)
	{
		LOG_I("  ENU Center: lat=%.6f, lon=%.6f", outMetadata.dCenterLat, outMetadata.dCenterLon);
	}
	else if (outMetadata.bIsEPSG)
	{
		LOG_I("  EPSG Code: %d", outMetadata.nEpsgCode);
	}
	else if (outMetadata.bIsWKT)
	{
		LOG_I("  WKT Projection (will be converted using GDAL)");
	}

	LOG_I("  Offset: x=%.3f, y=%.3f, z=%.3f", outMetadata.dOffsetX, outMetadata.dOffsetY, outMetadata.dOffsetZ);

	return true;
}

std::vector<LODLevelSettings> OSGBTools::BuildLODLevels(
	const std::vector<float>& dRatios,
	float dBaseError,
	const SimplificationParams& simplifyTemplate,
	const DracoCompressionParams& dracoTemplate,
	bool bDracoForLOD0)
{
	std::vector<LODLevelSettings> levels;
	levels.reserve(dRatios.size());

	for (size_t i = 0; i < dRatios.size(); ++i)
	{
		LODLevelSettings lvl;
		lvl.dTargetRatio = dRatios[i];
		lvl.dTargetError = dBaseError;

		// 使用用户的 enable_simplification 设置而不是强制为 true
		// 这允许用户控制是否对特定级别启用简化
		lvl.bEnableSimplification = simplifyTemplate.bEnableSimplification;
		lvl.simplify = simplifyTemplate;
		lvl.simplify.dTargetRatio = dRatios[i];
		lvl.simplify.dTargetError = dBaseError;

		// 应用Draco压缩设置
		lvl.bEnableDraco = dracoTemplate.bEnableCompression;

		// 特殊处理：LOD0 通常保持未压缩以便快速加载
		if (i == 0 && !bDracoForLOD0)
		{
			lvl.bEnableDraco = false;
		}

		lvl.draco = dracoTemplate;

		levels.emplace_back(lvl);
	}

	return levels;
}