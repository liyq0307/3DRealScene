#ifndef OSGB_READER_C_API_H
#define OSGB_READER_C_API_H

#ifdef _WIN32
    #ifdef OSGB_EXPORTS
        #define OSGB_API __declspec(dllexport)
    #else
        #define OSGB_API __declspec(dllimport)
    #endif
#else
    #define OSGB_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

/**
 * 创建OSGB读取器实例
 * @return 读取器句柄，失败返回NULL
 */
OSGB_API void* osgb_reader_create();

/**
 * 销毁OSGB读取器实例
 * @param handle 读取器句柄
 */
OSGB_API void osgb_reader_destroy(void* handle);

/**
 * 将OSGB文件转换为GLB文件
 * @param handle 读取器句柄
 * @param osgb_path OSGB文件路径（UTF-8编码）
 * @param glb_path 输出GLB文件路径（UTF-8编码）
 * @param enable_texture_compress 是否启用纹理压缩（0=否，1=是）
 * @param enable_meshopt 是否启用网格优化（0=否，1=是）
 * @param enable_draco 是否启用Draco压缩（0=否，1=是）
 * @return 成功返回1，失败返回0
 */
OSGB_API int osgb_to_glb(
    void* handle,
    const char* osgb_path,
    const char* glb_path,
    int enable_texture_compress,
    int enable_meshopt,
    int enable_draco
);

/**
 * 将OSGB转换为GLB内存数据
 * @param handle 读取器句柄
 * @param osgb_path OSGB文件路径
 * @param out_buffer 输出缓冲区指针（需要调用osgb_free_buffer释放）
 * @param out_size 输出数据大小
 * @param enable_texture_compress 是否启用纹理压缩
 * @param enable_meshopt 是否启用网格优化
 * @param enable_draco 是否启用Draco压缩
 * @return 成功返回1，失败返回0
 */
OSGB_API int osgb_to_glb_buffer(
    void* handle,
    const char* osgb_path,
    unsigned char** out_buffer,
    int* out_size,
    int enable_texture_compress,
    int enable_meshopt,
    int enable_draco
);

/**
 * 释放由osgb_to_glb_buffer分配的缓冲区
 * @param buffer 缓冲区指针
 */
OSGB_API void osgb_free_buffer(unsigned char* buffer);

/**
 * 获取最后的错误信息
 * @param handle 读取器句柄
 * @return 错误信息字符串（UTF-8编码），无错误返回NULL
 */
OSGB_API const char* osgb_get_last_error(void* handle);

/**
 * 获取库版本信息
 * @return 版本字符串
 */
OSGB_API const char* osgb_get_version();

#ifdef __cplusplus
}
#endif

#endif // OSGB_READER_C_API_H
