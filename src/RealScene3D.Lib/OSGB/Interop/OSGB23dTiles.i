/* ============================================================================
 * SWIG 接口文件 - OSGB23dTiles C# 绑定
 * 用于自动生成 C# 包装代码
 * ============================================================================ */

%module(directors="1") OSGB23dTilesCS

%{
#include "Native/OSGB23dTiles.h"
#include "Native/Tileset.h"
#include <string>
#include <vector>
#include <memory>
%}

/* ============================================================================
 * C# 模块配置
 * ============================================================================ */

%include "std_string.i"
%include "std_vector.i"
%include "std_array.i"
%include "typemaps.i"
%include "arrays_csharp.i"

/* ============================================================================
 * 类型映射和异常处理
 * ============================================================================ */

// 数组参数映射 - 用于 origin[3], bbox[6] 等
%apply double INPUT[] { double* origin };
%apply double INPUT[] { double* pBox };
%apply int INOUT[] { int* pLen };
%apply double INPUT[] { double* pMergedBox };
%apply int INOUT[] { int* pJsonLen };

// std::vector<uint8_t> 映射 - 用于 ToGlbBufBytes 返回值
%include "std_vector.i"
%template(VectorUInt8) std::vector<uint8_t>;

// std::array<double, 6> 映射 - 用于 To3dTile 返回值
%include "std_array.i"
%template(ArrayDouble6) std::array<double, 6>;

/* ============================================================================
 * 忽略不需要的类型和成员
 * ============================================================================ */

// 从 Tileset.h 中忽略不需要导出的类型
%ignore BoundingVolumeType;
%ignore BoundingVolume;
%ignore TilesetNode;
%ignore Box;
%ignore Region;
%ignore BoundingVolumeFromTileBox;

// 从 OSGB23dTiles.h 中忽略内部实现类
%ignore InfoVisitor;
%ignore OsgBuildState;

// 忽略 OSGB23dTiles 类的私有方法
%ignore OSGB23dTiles::ToGlbBuf;
%ignore OSGB23dTiles::ToGlbBuf(std::string, std::string&, MeshInfo&, int, bool, bool, bool, bool);
%ignore OSGB23dTiles::ToB3dmBuf;
%ignore OSGB23dTiles::DoTileJob;
%ignore OSGB23dTiles::EncodeTileJSON;
%ignore OSGB23dTiles::GetAllTree;

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
 * 包含C++头文件
 * ============================================================================ */

// 包含 Tileset.h（获取 TileBox 等定义）
%include "Native/Tileset.h"

// 包含 OSGB23dTiles.h（获取所有结构体和类定义）
%include "Native/OSGB23dTiles.h"

/* ============================================================================
 * 自定义 C# 辅助类 - 提供更友好的 API
 * ============================================================================ */

%pragma(csharp) imclassimports=%{
using System;
using System.Runtime.InteropServices;
%}

%typemap(cscode) OSGB23dTiles %{
    /// <summary>
    /// OSGB 读取器辅助类 - 提供更易用的 C# API
    /// </summary>
    public class Helper : IDisposable
    {
        private OSGB23dTiles reader;
        private bool disposed = false;

        public Helper()
        {
            reader = new OSGB23dTiles();
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
            return reader.ToGlb(
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
            SWIGTYPE_p_std__vectorT_unsigned_char_t glbBytes = reader.ToGlbBufBytes(
                osgbPath,
                -1,  // node_type: -1 表示自动判断
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);

            if (glbBytes == null)
            {
                return null;
            }

            // 将 SWIG 生成的 vector<uint8_t> 转换为 byte[]
            return OSGB23dTilesCS.VectorUInt8ToByteArray(glbBytes);
        }

        /// <summary>
        /// 将 OSGB 转换为 3D Tiles（返回tileset.json字符串）
        /// </summary>
        public (bool success, string? tilesetJson, double[]? bbox) ConvertTo3dTiles(
            string inPath,
            string outPath,
            double offsetX = 0.0,
            double offsetY = 0.0,
            int maxLevel = 0,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            SWIGTYPE_p_std__tupleT_bool_std__string_std__arrayT_double_6_t result = reader.To3dTile(
                inPath,
                outPath,
                offsetX,
                offsetY,
                maxLevel,
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);

            if (result == null)
            {
                return (false, null, null);
            }

            // 从 tuple 中提取值
            bool success = OSGB23dTilesCS.TupleGetBool(result);
            string tilesetJson = OSGB23dTilesCS.TupleGetString(result);
            double[] bbox = OSGB23dTilesCS.TupleGetArrayDouble6(result);

            return (success, string.IsNullOrEmpty(tilesetJson) ? null : tilesetJson, bbox);
        }

        /// <summary>
        /// 批量转换整个倾斜摄影数据集
        /// </summary>
        public bool ConvertTo3dTilesBatch(
            string dataDir,
            string outputDir,
            double[]? mergedBox = null,
            double offsetX = 0.0,
            double offsetY = 0.0,
            int maxLevel = 0,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            if (mergedBox != null && mergedBox.Length != 6)
            {
                throw new ArgumentException("mergedBox must contain exactly 6 elements [minX, minY, minZ, maxX, maxY, maxZ]");
            }

            int jsonLen = 0;
            return reader.To3dTileBatch(
                dataDir,
                outputDir,
                mergedBox,
                ref jsonLen,
                offsetX,
                offsetY,
                maxLevel,
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);
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