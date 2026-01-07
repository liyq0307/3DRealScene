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
#include <tuple>
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
 * std::vector<uint8_t> 映射 - 用于 ToGlbBufBytes 返回值
 * ============================================================================ */

%template(VectorUInt8) std::vector<uint8_t>;

// 将 std::vector<uint8_t> 转换为 C# byte[]
%typemap(cstype) std::vector<uint8_t> "byte[]"
%typemap(csout) std::vector<uint8_t> {
    global::System.IntPtr cPtr = $imcall;
    VectorUInt8 tempVector = new VectorUInt8(cPtr, true);

    byte[] result = new byte[tempVector.Count];
    for (int i = 0; i < tempVector.Count; i++) {
        result[i] = (byte)tempVector[i];
    }

    tempVector.Dispose();
    return result;
}

/* ============================================================================
 * std::tuple 映射 - 用于 To3dTile 返回值
 * ============================================================================ */

// 定义 tuple 返回类型的包装结构
%inline %{
struct TileConversionResult {
    bool success;
    std::string tilesetJson;
    std::array<double, 6> bbox;

    TileConversionResult() : success(false), bbox{} {}
    TileConversionResult(bool s, const std::string& json, const std::array<double, 6>& box)
        : success(s), tilesetJson(json), bbox(box) {}
};
%}

// 为 std::array<double, 6> 创建模板
%template(ArrayDouble6) std::array<double, 6>;

// 扩展 OSGB23dTiles 类，添加返回包装结构的方法
%extend OSGB23dTiles {
    TileConversionResult To3dTileWrapped(
        const std::string& strInPath, const std::string& strOutPath,
        double dCenterX, double dCenterY, int nMaxLevel,
        bool bEnableTextureCompress = false, bool bEnableMeshOpt = false, bool bEnableDraco = false)
    {
        auto result = $self->To3dTile(strInPath, strOutPath, dCenterX, dCenterY, nMaxLevel,
                                      bEnableTextureCompress, bEnableMeshOpt, bEnableDraco);
        return TileConversionResult(std::get<0>(result), std::get<1>(result), std::get<2>(result));
    }
}

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

// 忽略 OSGB23dTiles 类的私有方法和原始tuple返回方法
%ignore OSGB23dTiles::ToGlbBuf(std::string, std::string&, MeshInfo&, int, bool, bool, bool, bool);
%ignore OSGB23dTiles::ToB3dmBuf;
%ignore OSGB23dTiles::DoTileJob;
%ignore OSGB23dTiles::EncodeTileJSON;
%ignore OSGB23dTiles::GetAllTree;
%ignore OSGB23dTiles::To3dTile;  // 忽略原始tuple返回方法，使用包装方法

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
            byte[] glbBytes = reader.ToGlbBufBytes(
                osgbPath,
                -1,  // node_type: -1 表示自动判断
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
            TileConversionResult result = reader.To3dTileWrapped(
                inPath,
                outPath,
                offsetX,
                offsetY,
                maxLevel,
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);

            if (!result.success)
            {
                return (false, null, null);
            }

            // 从包装结构中提取值
            string tilesetJson = result.tilesetJson;
            ArrayDouble6 bboxArray = result.bbox;

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
        public bool ConvertTo3dTilesBatch(
            string dataDir,
            string outputDir,
            double offsetX = 0.0,
            double offsetY = 0.0,
            int maxLevel = 0,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            return reader.To3dTileBatch(
                dataDir,
                outputDir,
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
