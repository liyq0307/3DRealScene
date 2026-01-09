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
#include <array>
%}

/* ============================================================================
 * C# 模块配置
 * ============================================================================ */

%include "std_string.i"
%include "std_vector.i"
%include "std_array.i"
%include "typemaps.i"

/* ============================================================================
 * std::vector<uint8_t> 映射 - 用于 ToGLBBuf 返回值
 * ============================================================================ */

// 定义 std::vector<uint8_t> 模板，元素类型为 unsigned char (byte)
namespace std {
    %template(VectorUInt8) vector<unsigned char>;
}

// 将 std::vector<uint8_t> 转换为 C# byte[]
%typemap(cstype) std::vector<uint8_t> "byte[]"
%typemap(csout) std::vector<uint8_t> {
    global::System.IntPtr cPtr = $imcall;
    VectorUInt8 tempVector = new VectorUInt8(cPtr, true);

    byte[] result = new byte[tempVector.Count];
    for (int i = 0; i < tempVector.Count; i++) {
        result[i] = tempVector[i];
    }

    tempVector.Dispose();
    return result;
}

/* ============================================================================
 * std::array 映射 - 用于 B3DMResult.boundingBox
 * ============================================================================ */

// 为 std::array<double, 6> 创建模板
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
%ignore PrimitiveState;
%ignore InfoVisitor;
%ignore OsgBuildState;
%ignore DracoState;

// 忽略 OSGB23dTiles 类的私有方法
%ignore OSGB23dTiles::ToGLBBuf(std::string, std::string&, MeshInfo&, int, bool, bool, bool, bool);
%ignore OSGB23dTiles::ToB3DMBuf;
%ignore OSGB23dTiles::DoTileJob;
%ignore OSGB23dTiles::EncodeTileJSON;
%ignore OSGB23dTiles::GetAllTree;
%ignore OSGB23dTiles::WriteOsgIndecis;
%ignore OSGB23dTiles::WriteVec3Array;
%ignore OSGB23dTiles::WriteVec2Array;
%ignore OSGB23dTiles::WriteElementArrayPrimitive;
%ignore OSGB23dTiles::WriteOsgGeometry;

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
            return reader.ToGLB(
                osgbPath,
                glbPath,
                true,
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);
        }

        /// <summary>
        /// 将 OSGB 文件转换为 GLB 字节数组
        /// </summary>
        public byte[]? ConvertToGlbBuffer(
            string osgbPath,
            bool bBainary = true,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            byte[] glbBytes = reader.ToGLBBuf(
                osgbPath,
                -1,  // node_type: -1 表示自动判断
                bBainary,
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);

            if (glbBytes == null || glbBytes.Length == 0)
            {
                return null;
            }

            return glbBytes;
        }

        /// <summary>
        /// 将 OSGB 转换为 B3DM
        /// </summary>
        public (bool success, string? tilesetJson, double[]? bbox) ConvertToB3DM(
            string inPath,
            string outPath,
            double centerX = 0.0,
            double centerY = 0.0,
            int maxLevel = 0,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            B3DMResult result = reader.ToB3DM(
                inPath,
                outPath,
                centerX,
                centerY,
                maxLevel,
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);

            if (!result.success)
            {
                return (false, null, null);
            }

            // 从结构体中提取值
            string tilesetJson = result.tilesetJson;
            ArrayDouble6 bboxArray = result.boundingBox;

            double[] bbox = new double[6];
            for (int i = 0; i < 6; i++)
            {
                bbox[i] = bboxArray[i];
            }

            return (true, string.IsNullOrEmpty(tilesetJson) ? null : tilesetJson, bbox);
        }

        /// <summary>
        /// 批量转换整个倾斜摄影数据集
        /// </summary>
        public bool ConvertToB3DMBatch(
            string dataDir,
            string outputDir,
            double centerX = 0.0,
            double centerY = 0.0,
            int maxLevel = 0,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            return reader.ToB3DMBatch(
                dataDir,
                outputDir,
                centerX,
                centerY,
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
