#include <cmath>
#include <vector>
#include <string>
#include <cstring>
#include <cstdio>

#include "extern.h"
#include "GeoTransform.h"

///////////////////////
static const double pi = std::acos(-1);

extern "C" bool
epsg_convert(int insrs, double* val)
{
	// 直接调用GeoTransform::InitFromEPSG
	return GeoTransform::InitFromEPSG(insrs, val);
}

extern "C" bool
enu_init(double lon, double lat, double* origin_enu)
{
	// 直接调用GeoTransform::InitFromENU
	return GeoTransform::InitFromENU(lon, lat, origin_enu);
}

extern "C" bool
wkt_convert(char* wkt, double* val, char* path)
{
	// 直接调用GeoTransform::InitFromWKT
	return GeoTransform::InitFromWKT(wkt, val);
}

extern "C"
{
	double degree2rad(double val)
	{
		return val * pi / 180.0;
	}

	double lati_to_meter(double diff)
	{
		return diff / 0.000000157891;
	}

	double longti_to_meter(double diff, double lati)
	{
		return diff / 0.000000156785 * std::cos(lati);
	}

	double meter_to_lati(double m)
	{
		return m * 0.000000157891;
	}

	double meter_to_longti(double m, double lati)
	{
		return m * 0.000000156785 / std::cos(lati);
	}
}

std::vector<double>
transfrom_xyz(double lon_deg, double lat_deg, double height_min)
{
	// 使用GeoTransform的实现计算ENU->ECEF变换矩阵
	glm::dmat4 matrix = GeoTransform::CalcEnuToEcefMatrix(lon_deg, lat_deg, height_min);

	// 将glm::dmat4转换为std::vector<double>（列主序）
	std::vector<double> result(16);
	for (int col = 0; col < 4; col++)
	{
		for (int row = 0; row < 4; row++)
		{
			result[col * 4 + row] = matrix[col][row];
		}
	}
	return result;
}

extern "C" void
transform_c(double center_x, double center_y, double height_min, double* ptr)
{
	std::vector<double> v = transfrom_xyz(center_x, center_y, height_min);
	fprintf(stderr, "[transform_c] lon=%.10f lat=%.10f h=%.3f -> ECEF translation: x=%.10f y=%.10f z=%.10f\n",
		center_x, center_y, height_min, v[12], v[13], v[14]);
	std::memcpy(ptr, v.data(), v.size() * 8);
}

extern "C" void
transform_c_with_enu_offset(double center_x, double center_y, double height_min,
	double enu_offset_x, double enu_offset_y, double enu_offset_z,
	double* ptr)
{
	std::vector<double> v = transfrom_xyz(center_x, center_y, height_min);
	fprintf(stderr, "[transform_c_with_enu_offset] Base ECEF at lon=%.10f lat=%.10f h=%.3f: x=%.10f y=%.10f z=%.10f\n",
		center_x, center_y, height_min, v[12], v[13], v[14]);

	const double pi = std::acos(-1.0);
	double lat_rad = center_y * pi / 180.0;
	double lon_rad = center_x * pi / 180.0;

	double sinLat = std::sin(lat_rad);
	double cosLat = std::cos(lat_rad);
	double sinLon = std::sin(lon_rad);
	double cosLon = std::cos(lon_rad);

	double ecef_offset_x = -sinLon * enu_offset_x - sinLat * cosLon * enu_offset_y + cosLat * cosLon * enu_offset_z;
	double ecef_offset_y = cosLon * enu_offset_x - sinLat * sinLon * enu_offset_y + cosLat * sinLon * enu_offset_z;
	double ecef_offset_z = cosLat * enu_offset_y + sinLat * enu_offset_z;

	fprintf(stderr, "[transform_c_with_enu_offset] ENU offset (%.3f, %.3f, %.3f) -> ECEF offset (%.10f, %.10f, %.10f)\n",
		enu_offset_x, enu_offset_y, enu_offset_z, ecef_offset_x, ecef_offset_y, ecef_offset_z);

	v[12] += ecef_offset_x;
	v[13] += ecef_offset_y;
	v[14] += ecef_offset_z;

	fprintf(stderr, "[transform_c_with_enu_offset] Final ECEF translation: x=%.10f y=%.10f z=%.10f\n",
		v[12], v[13], v[14]);

	std::memcpy(ptr, v.data(), v.size() * 8);
}

bool
write_tileset_box(Transform* trans, Box& box, double geometricError,
	const char* b3dm_file, const char* json_file)
{
	std::vector<double> matrix;
	if (trans)
	{
		double lon_deg = trans->radian_x * 180.0 / std::acos(-1.0);
		double lat_deg = trans->radian_y * 180.0 / std::acos(-1.0);
		matrix = transfrom_xyz(lon_deg, lat_deg, trans->min_height);
	}
	std::string json_txt = "{\"asset\": {\"version\": \"1.0\",\"gltfUpAxis\": \"Z\"},\"geometricError\":";
	json_txt += std::to_string(geometricError);
	json_txt += ",\"root\": {";
	std::string trans_str = "\"transform\": [";
	if (trans)
	{
		for (int i = 0; i < 15; i++)
		{
			trans_str += std::to_string(matrix[i]);
			trans_str += ",";
		}
		trans_str += "1],";
		json_txt += trans_str;
	}
	json_txt += "\"boundingVolume\": {\"box\": [";
	for (int i = 0; i < 11; i++)
	{
		json_txt += std::to_string(box.matrix[i]);
		json_txt += ",";
	}
	json_txt += std::to_string(box.matrix[11]);

	char last_buf[512];
	sprintf(last_buf, "]},\"geometricError\": %f,\"refine\": \"REPLACE\",\"content\": {\"uri\": \"%s\"}}}", geometricError, b3dm_file);

	json_txt += last_buf;

	bool ret = write_file(json_file, json_txt.data(), (unsigned long)json_txt.size());
	if (!ret)
	{
		LOG_E("write file %s fail", json_file);
	}
	return ret;
}

bool write_tileset_region(
	Transform* trans,
	Region& region,
	double geometricError,
	const char* b3dm_file,
	const char* json_file)
{
	std::vector<double> matrix;
	if (trans)
	{
		double lon_deg = trans->radian_x * 180.0 / std::acos(-1.0);
		double lat_deg = trans->radian_y * 180.0 / std::acos(-1.0);
		matrix = transfrom_xyz(lon_deg, lat_deg, trans->min_height);
	}
	std::string json_txt = "{\"asset\": {\"version\": \"1.0\",\"gltfUpAxis\": \"Z\"},\"geometricError\":";
	json_txt += std::to_string(geometricError);
	json_txt += ",\"root\": {";
	std::string trans_str = "\"transform\": [";
	if (trans)
	{
		for (int i = 0; i < 15; i++)
		{
			trans_str += std::to_string(matrix[i]);
			trans_str += ",";
		}
		trans_str += "1],";
		json_txt += trans_str;
	}
	json_txt += "\"boundingVolume\": {\"region\": [";
	double* pRegion = (double*)&region;
	for (int i = 0; i < 5; i++)
	{
		json_txt += std::to_string(pRegion[i]);
		json_txt += ",";
	}
	json_txt += std::to_string(pRegion[5]);

	char last_buf[512];
	sprintf(last_buf, "]},\"geometricError\": %f,\"refine\": \"REPLACE\",\"content\": {\"uri\": \"%s\"}}}", geometricError, b3dm_file);

	json_txt += last_buf;

	bool ret = write_file(json_file, json_txt.data(), (unsigned long)json_txt.size());
	if (!ret)
	{
		LOG_E("write file %s fail", json_file);
	}
	return ret;
}

bool
write_tileset(double radian_x, double radian_y,
	double tile_w, double tile_h, double height_min, double height_max,
	double geometricError, const char* filename, const char* full_path)
{
	const double pi = std::acos(-1);
	double lon_deg = radian_x * 180.0 / pi;
	double lat_deg = radian_y * 180.0 / pi;

	std::vector<double> matrix = transfrom_xyz(lon_deg, lat_deg, height_min);

	double half_w = tile_w * 0.5;
	double half_h = tile_h * 0.5;
	double half_z = (height_max - height_min) * 0.5;
	double center_z = half_z;

	std::string json_txt = "{\"asset\": {\"version\": \"0.0\",\"gltfUpAxis\": \"Y\"},\"geometricError\":";
	json_txt += std::to_string(geometricError);
	json_txt += ",\"root\": {\"transform\": [";
	for (int i = 0; i < 15; i++)
	{
		json_txt += std::to_string(matrix[i]);
		json_txt += ",";
	}
	json_txt += "1],\"boundingVolume\": {\"box\": [";
	double box_vals[12] = {
		0.0, 0.0, center_z,
		half_w, 0.0, 0.0,
		0.0, half_h, 0.0,
		0.0, 0.0, half_z
	};
	for (int i = 0; i < 11; i++)
	{
		json_txt += std::to_string(box_vals[i]);
		json_txt += ",";
	}
	json_txt += std::to_string(box_vals[11]);

	char last_buf[512];
	sprintf(last_buf, "]},\"geometricError\": %f,\"refine\": \"REPLACE\",\"content\": {\"uri\": \"%s\"}}}", geometricError, filename);

	json_txt += last_buf;

	bool ret = write_file(full_path, json_txt.data(), (unsigned long)json_txt.size());
	if (!ret)
	{
		LOG_E("write file %s fail", filename);
	}
	return ret;
}
