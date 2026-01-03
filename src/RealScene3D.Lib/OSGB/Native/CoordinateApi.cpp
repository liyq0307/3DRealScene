#include "CoordinateApi.h"
#include "GeoTransform.h"

// C API 实现 - 桥接到 GeoTransform 类

int coord_init_epsg(int epsg_code, double* origin) {
    return GeoTransform::InitFromEPSG(epsg_code, origin) ? 1 : 0;
}

int coord_init_enu(double lon, double lat, double* origin_enu) {
    return GeoTransform::InitFromENU(lon, lat, origin_enu) ? 1 : 0;
}

int coord_init_wkt(const char* wkt, double* origin) {
    return GeoTransform::InitFromWKT(wkt, origin) ? 1 : 0;
}

void coord_cleanup() {
    GeoTransform::Cleanup();
}

const char* coord_get_last_error() {
    return GeoTransform::GetLastError();
}

int coord_is_initialized() {
    return GeoTransform::IsInitialized() ? 1 : 0;
}
