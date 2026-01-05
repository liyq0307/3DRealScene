#pragma once
#include <cstdarg>
#include <cstdio>
#include <cmath>
#include <string>
#include <filesystem>
#include <fstream>

/////////////////////////
// 简化的日志宏（不依赖spdlog）
// 使用MSVC兼容的可变参数宏语法
#define LOG_D(format, ...) printf("[DEBUG] " format "\n", ##__VA_ARGS__)
#define LOG_I(format, ...) printf("[INFO] " format "\n", ##__VA_ARGS__)
#define LOG_W(format, ...) printf("[WARN] " format "\n", ##__VA_ARGS__)
#define LOG_E(format, ...) fprintf(stderr, "[ERROR] " format "\n", ##__VA_ARGS__)

// C++实现的工具函数
inline bool mkdirs(const char* path)
{
	try
	{
		std::filesystem::create_directories(path);
		return true;
	}
	catch (...)
	{
		return false;
	}
}

inline bool write_file(const char* filename, const char* buf, unsigned long buf_len)
{
	try
	{
		std::ofstream ofs(filename, std::ios::binary);
		if (!ofs.is_open()) return false;
		ofs.write(buf, buf_len);
		ofs.close();
		return true;
	}
	catch (...)
	{
		return false;
	}
}

//// -- others
struct Transform
{
	double radian_x;
	double radian_y;
	double min_height;
};

struct Box
{
	double matrix[12];
};

struct Region
{
	double min_x;
	double min_y;
	double max_x;
	double max_y;
	double min_height;
	double max_height;
};

bool write_tileset_region(
	Transform* trans,
	Region& region,
	double geometricError,
	const char* b3dm_file,
	const char* json_file);

bool write_tileset_box(
	Transform* trans, Box& box,
	double geometricError,
	const char* b3dm_file,
	const char* json_file);

bool write_tileset(
	double longti, double lati,
	double tile_w, double tile_h,
	double height_min, double height_max,
	double geometricError,
	const char* filename, const char* full_path
);

extern "C" {
	double degree2rad(double val);
	double lati_to_meter(double diff);
	double longti_to_meter(double diff, double lati);
	double meter_to_lati(double m);
	double meter_to_longti(double m, double lati);

	// 坐标转换矩阵计算函数
	void transform_c(double center_x, double center_y, double height_min, double* ptr);
	void transform_c_with_enu_offset(double center_x, double center_y, double height_min,
	                                  double enu_offset_x, double enu_offset_y, double enu_offset_z,
	                                  double* ptr);
}
////////////////////////