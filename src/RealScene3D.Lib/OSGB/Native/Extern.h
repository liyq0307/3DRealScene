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

// C++实现的工具函数（替代Rust实现）
inline bool mkdirs(const char* path) {
    try {
        std::filesystem::create_directories(path);
        return true;
    } catch (...) {
        return false;
    }
}

inline bool write_file(const char* filename, const char* buf, unsigned long buf_len) {
    try {
        std::ofstream ofs(filename, std::ios::binary);
        if (!ofs.is_open()) return false;
        ofs.write(buf, buf_len);
        ofs.close();
        return true;
    } catch (...) {
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
	) ;

extern "C" {
	double degree2rad(double val);
	double lati_to_meter(double diff);
	double longti_to_meter(double diff, double lati);
	double meter_to_lati(double m);
	double meter_to_longti(double m, double lati);
}

// Inline implementations for coordinate conversion functions
// Based on WGS84 ellipsoid parameters
inline double degree2rad(double val) {
    const double PI = 3.14159265358979323846;
    return val * PI / 180.0;
}

inline double lati_to_meter(double diff) {
    // Approximately 111,320 meters per degree of latitude
    const double METERS_PER_DEGREE_LAT = 111320.0;
    return diff * METERS_PER_DEGREE_LAT;
}

inline double longti_to_meter(double diff, double lati) {
    // Meters per degree of longitude varies with latitude
    const double METERS_PER_DEGREE_LAT = 111320.0;
    double lat_rad = degree2rad(lati);
    return diff * METERS_PER_DEGREE_LAT * std::cos(lat_rad);
}

inline double meter_to_lati(double m) {
    // Convert meters to degrees of latitude
    const double METERS_PER_DEGREE_LAT = 111320.0;
    return m / METERS_PER_DEGREE_LAT;
}

inline double meter_to_longti(double m, double lati) {
    // Convert meters to degrees of longitude at given latitude
    const double METERS_PER_DEGREE_LAT = 111320.0;
    double lat_rad = degree2rad(lati);
    double cos_lat = std::cos(lat_rad);
    if (cos_lat < 0.0001) cos_lat = 0.0001; // Prevent division by zero near poles
    return m / (METERS_PER_DEGREE_LAT * cos_lat);
}

////////////////////////