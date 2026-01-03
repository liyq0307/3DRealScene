#include "OsgbReaderCApi.h"
#include <string>
#include <cstring>
#include <vector>
#include <fstream>

// 前向声明MeshInfo结构体（在Osgb23dTile.cpp中定义）
struct MeshInfo {
    std::string name;
    std::vector<double> min;
    std::vector<double> max;
};

// 引入参考代码的核心函数
// 注意：需要在osgb23dtile.cpp中声明为可链接
extern bool osgb2glb_buf(
    std::string path,
    std::string& glb_buff,
    MeshInfo& mesh_info,
    int node_type,
    bool enable_texture_compress,
    bool enable_meshopt,
    bool enable_draco);

/**
 * OSGB读取器内部实现类
 */
class OsgbReaderImpl {
public:
    OsgbReaderImpl() {}
    ~OsgbReaderImpl() {}

    /**
     * 将OSGB转换为GLB
     * 核心逻辑：调用参考代码的osgb2glb_buf函数
     */
    bool ConvertToGlb(
        const std::string& osgb_path,
        std::string& glb_buffer,
        bool enable_texture_compress,
        bool enable_meshopt,
        bool enable_draco)
    {
        try {
            // 调用参考代码的核心函数
            MeshInfo mesh_info;

            bool success = osgb2glb_buf(
                osgb_path,
                glb_buffer,
                mesh_info,
                -1,  // node_type: -1表示自动判断
                enable_texture_compress,
                enable_meshopt,
                enable_draco
            );

            if (!success || glb_buffer.empty()) {
                last_error_ = "osgb2glb_buf() failed to convert OSGB to GLB";
                return false;
            }

            return true;
        }
        catch (const std::exception& ex) {
            last_error_ = std::string("Exception: ") + ex.what();
            return false;
        }
        catch (...) {
            last_error_ = "Unknown exception";
            return false;
        }
    }

    bool SaveGlbToFile(const std::string& glb_buffer, const std::string& output_path) {
        try {
            std::ofstream ofs(output_path, std::ios::binary);
            if (!ofs.is_open()) {
                last_error_ = "Failed to open output file: " + output_path;
                return false;
            }
            ofs.write(glb_buffer.data(), glb_buffer.size());
            ofs.close();
            return true;
        }
        catch (const std::exception& ex) {
            last_error_ = std::string("Exception: ") + ex.what();
            return false;
        }
    }

    const char* GetLastError() const {
        return last_error_.empty() ? nullptr : last_error_.c_str();
    }

private:
    std::string last_error_;
};

//=============================================================================
// C API 实现
//=============================================================================

OSGB_API void* osgb_reader_create() {
    try {
        return new OsgbReaderImpl();
    }
    catch (...) {
        return nullptr;
    }
}

OSGB_API void osgb_reader_destroy(void* handle) {
    if (handle) {
        delete static_cast<OsgbReaderImpl*>(handle);
    }
}

OSGB_API int osgb_to_glb(
    void* handle,
    const char* osgb_path,
    const char* glb_path,
    int enable_texture_compress,
    int enable_meshopt,
    int enable_draco)
{
    if (!handle || !osgb_path || !glb_path) {
        return 0;
    }

    auto* reader = static_cast<OsgbReaderImpl*>(handle);
    std::string glb_buffer;

    // 转换为GLB
    bool success = reader->ConvertToGlb(
        osgb_path,
        glb_buffer,
        enable_texture_compress != 0,
        enable_meshopt != 0,
        enable_draco != 0
    );

    if (!success) {
        return 0;
    }

    // 保存到文件
    success = reader->SaveGlbToFile(glb_buffer, glb_path);
    return success ? 1 : 0;
}

OSGB_API int osgb_to_glb_buffer(
    void* handle,
    const char* osgb_path,
    unsigned char** out_buffer,
    int* out_size,
    int enable_texture_compress,
    int enable_meshopt,
    int enable_draco)
{
    if (!handle || !osgb_path || !out_buffer || !out_size) {
        return 0;
    }

    auto* reader = static_cast<OsgbReaderImpl*>(handle);
    std::string glb_buffer;

    // 转换为GLB
    bool success = reader->ConvertToGlb(
        osgb_path,
        glb_buffer,
        enable_texture_compress != 0,
        enable_meshopt != 0,
        enable_draco != 0
    );

    if (!success || glb_buffer.empty()) {
        return 0;
    }

    // 分配缓冲区并复制数据
    *out_size = static_cast<int>(glb_buffer.size());
    *out_buffer = new unsigned char[*out_size];
    std::memcpy(*out_buffer, glb_buffer.data(), *out_size);

    return 1;
}

OSGB_API void osgb_free_buffer(unsigned char* buffer) {
    if (buffer) {
        delete[] buffer;
    }
}

OSGB_API const char* osgb_get_last_error(void* handle) {
    if (!handle) {
        return "Invalid handle";
    }
    auto* reader = static_cast<OsgbReaderImpl*>(handle);
    return reader->GetLastError();
}

OSGB_API const char* osgb_get_version() {
    return "1.0.0-reference-port";
}
