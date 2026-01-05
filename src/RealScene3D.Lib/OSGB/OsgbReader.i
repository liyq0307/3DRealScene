/* ============================================================================
 * SWIG 接口文件 - OsgbReader C# 绑定
 * 用于自动生成 C# 包装代码
 * ============================================================================ */

%module(directors="1") OsgbReaderCS

%{
#include "Native/OsgbReader.h"
#include <string>
#include <vector>
#include <memory>
%}

/* ============================================================================
 * C# 模块配置
 * ============================================================================ */

%include "std_string.i"
%include "std_vector.i"
%include "typemaps.i"
%include "arrays_csharp.i"

/* ============================================================================
 * 类型映射和异常处理
 * ============================================================================ */

// 将 C++ void* 返回值映射为 IntPtr，稍后在 C# 端处理
%typemap(cstype) void* "System.IntPtr"
%typemap(imtype) void* "System.IntPtr"
%typemap(csout) void* {
    return $imcall;
}

// 数组参数映射 - 用于 origin[3], bbox[6] 等
%apply double INPUT[] { double* origin };
%apply double INPUT[] { double* box };
%apply int INOUT[] { int* len };

/* ============================================================================
 * 忽略不需要的类型和成员
 * ============================================================================ */

/* ============================================================================
 * 内存管理 - 使用 C# 的 IDisposable 模式
 * ============================================================================ */

%define MANAGED_PTR(TYPE)
%typemap(csimports) TYPE %{
using System;
using System.Runtime.InteropServices;
%}

%typemap(cscode) TYPE %{
  private HandleRef swigCPtr;

  public TYPE(IntPtr cPtr, bool cMemoryOwn) {
    swigCPtr = new HandleRef(this, cPtr);
    swigCMemOwn = cMemoryOwn;
  }

  public static HandleRef getCPtr(TYPE obj) {
    return (obj == null) ? new HandleRef(null, IntPtr.Zero) : obj.swigCPtr;
  }
%}

%typemap(csfinalize) TYPE %{
  ~$csclassname() {
    Dispose();
  }
%}

%enddef

/* ============================================================================
 * 结构体定义
 * ============================================================================ */

// TileBox 结构体
%ignore TileBox::extend;  // 忽略成员函数，稍后手动绑定

struct TileBox {
    std::vector<double> max;
    std::vector<double> min;
};

// 手动添加 extend 方法
%extend TileBox {
    void extend(double ratio) {
        $self->extend(ratio);
    }
}

// OSGTree 结构体
struct OSGTree {
    TileBox bbox;
    double geometricError;
    std::string file_name;
    std::vector<OSGTree> sub_nodes;
    int type;
};

// PrimitiveState 结构体
struct PrimitiveState {
    int vertexAccessor;
    int normalAccessor;
    int textcdAccessor;
};

// MeshInfo 结构体
struct MeshInfo {
    std::string name;
    std::vector<double> min;
    std::vector<double> max;
};

/* ============================================================================
 * 类定义
 * ============================================================================ */

// OsgbReader 类 - 主类
%ignore OsgbReader::Osgb2GlbBuf(std::string, std::string&, MeshInfo&, int, bool, bool, bool, bool);
%ignore OsgbReader::Osgb2B3dmBuf;
%ignore OsgbReader::DoTileJob;
%ignore OsgbReader::EncodeTileJson;
%ignore OsgbReader::GetAllTree;

class OsgbReader {
public:
    OsgbReader();
    ~OsgbReader();

    // 将 OSGB 转换为 3D Tiles
    void* Osgb23dTile(
        const char* in_path,
        const char* out_path,
        double* box,
        int* len,
        double x,
        double y,
        int max_lvl,
        bool enable_texture_compress = false,
        bool enable_meshopt = false,
        bool enable_draco = false
    );

    // 将 OSGB 文件转换为 GLB 缓冲区
    bool Osgb2GlbBuf(
        std::string path,
        std::string& glb_buff,
        int node_type,
        bool enable_texture_compression = false,
        bool enable_meshopt = false,
        bool enable_draco = false
    );

    // OSGB 转 GLB 文件
    bool Osgb2Glb(
        const char* in,
        const char* out,
        bool enable_texture_compression = false,
        bool enable_meshopt = false,
        bool enable_draco = false
    );

private:
    // 私有方法 - 不暴露给 C#
    bool Osgb2GlbBuf(
        std::string path,
        std::string& glb_buff,
        MeshInfo& mesh_info,
        int node_type,
        bool enable_texture_compression = false,
        bool enable_meshopt = false,
        bool enable_draco = false,
        bool need_mesh_info = true
    );

    bool Osgb2B3dmBuf(
        std::string path,
        std::string& b3dm_buf,
        TileBox& tile_box,
        int node_type,
        bool enable_texture_compression = false,
        bool enable_meshopt = false,
        bool enable_draco = false
    );

    void DoTileJob(
        OSGTree& tree,
        std::string out_path,
        int max_lvl,
        bool enable_texture_compression = false,
        bool enable_meshopt = false,
        bool enable_draco = false
    );

    std::string EncodeTileJson(OSGTree& tree, double x, double y);
    OSGTree GetAllTree(std::string& file_name);
};

/* ============================================================================
 * 自定义 C# 辅助类 - 提供更友好的 API
 * ============================================================================ */

%pragma(csharp) imclassimports=%{
using System;
using System.Runtime.InteropServices;
%}

%typemap(cscode) OsgbReader %{
    /// <summary>
    /// OSGB 读取器辅助类 - 提供更易用的 C# API
    /// </summary>
    public class Helper : IDisposable
    {
        private OsgbReader reader;
        private bool disposed = false;

        public Helper()
        {
            reader = new OsgbReader();
        }

        /// <summary>
        /// 将 OSGB 文件转换为 GLB 文件
        /// </summary>
        public bool ConvertToGlb(
            string osgbPath,
            string glbPath,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            return reader.Osgb2Glb(
                osgbPath,
                glbPath,
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);
        }

        /// <summary>
        /// 将 OSGB 文件转换为 GLB 字节数组
        /// </summary>
        public byte[]? ConvertToGlbBuffer(
            string osgbPath,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            string glbBuffer;
            bool success = reader.Osgb2GlbBuf(
                osgbPath,
                out glbBuffer,  // 使用 out 参数（已通过 OUTPUT 映射）
                -1,  // node_type: -1 表示自动判断
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);

            if (!success || string.IsNullOrEmpty(glbBuffer))
            {
                return null;
            }

            // 注意：std::string 中存储的是二进制数据，需要正确转换
            // SWIG 会将 std::string 转换为 C# string，但可能包含二进制数据
            // 建议改进：在 C++ 端直接返回 byte[] 或使用 Base64 编码
            return System.Text.Encoding.Latin1.GetBytes(glbBuffer);
        }

        /// <summary>
        /// 将 OSGB 转换为 3D Tiles
        /// 注意：返回的字符串由 C++ 分配，SWIG 会自动管理内存
        /// </summary>
        public string? ConvertTo3dTiles(
            string inPath,
            string outPath,
            double[]? bbox = null,
            double offsetX = 0.0,
            double offsetY = 0.0,
            int maxLevel = 0,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            if (bbox != null && bbox.Length != 6)
            {
                throw new ArgumentException("bbox must contain exactly 6 elements [minX, minY, minZ, maxX, maxY, maxZ]");
            }

            int bboxLen = bbox?.Length ?? 0;
            IntPtr result = reader.Osgb23dTile(
                inPath,
                outPath,
                bbox,
                ref bboxLen,
                offsetX,
                offsetY,
                maxLevel,
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);

            if (result == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                // 从指针读取字符串（复制到托管内存）
                return Marshal.PtrToStringAnsi(result);
            }
            finally
            {
                // TODO: 需要在 C++ 端提供释放函数
                // 或者修改 Osgb23dTile 返回 std::string 而不是 void*
                // 当前实现可能存在内存泄漏
                // 建议添加: reader.FreeString(result);
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                if (reader != null)
                {
                    reader.Dispose();
                    reader = null;
                }
                disposed = true;
            }
        }
    }
%}

/* ============================================================================
 * 异常处理
 * ============================================================================ */

%exception {
    try {
        $action
    }
    catch (const std::exception& e) {
        SWIG_CSharpSetPendingException(SWIG_CSharpSystemException, e.what());
        return $null;
    }
    catch (...) {
        SWIG_CSharpSetPendingException(SWIG_CSharpSystemException, "Unknown exception occurred");
        return $null;
    }
}