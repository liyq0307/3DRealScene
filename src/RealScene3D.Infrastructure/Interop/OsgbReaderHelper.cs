// ============================================================================
// OSGB 读取器辅助类 - 提供更易用的 C# API
// ============================================================================
// 这个文件提供了对 OSGB23dTiles SWIG 封装的更友好的接口
//
// ============================================================================

using System;
using System.Runtime.InteropServices;
using RealScene3D.Lib.OSGB.Interop;

namespace RealScene3D.Lib.OSGB.Interop
{
    /// <summary>
    /// OSGB 读取器辅助类 - 提供更易用的 C# API
    /// </summary>
    public class OsgbReaderHelper : IDisposable
    {
        private OSGB23dTiles reader;
        private bool disposed = false;

        public OsgbReaderHelper()
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
        /// 将 OSGB 转换为 3D Tiles
        /// </summary>
        /// <param name="inPath">输入OSGB文件路径</param>
        /// <param name="outPath">输出3D Tiles目录路径</param>
        /// <param name="bbox">输出参数：包围盒数组 [maxX, maxY, maxZ, minX, minY, minZ]</param>
        /// <param name="offsetX">X轴偏移（经度）</param>
        /// <param name="offsetY">Y轴偏移（纬度）</param>
        /// <param name="maxLevel">最大层级（0表示不限制）</param>
        /// <param name="enableTextureCompression">是否启用纹理压缩</param>
        /// <param name="enableMeshOptimization">是否启用网格优化</param>
        /// <param name="enableDracoCompression">是否启用Draco压缩</param>
        /// <returns>tileset.json内容</returns>
        public string? ConvertTo3dTiles(
            string inPath,
            string outPath,
            out double[]? bbox,
            double offsetX = 0.0,
            double offsetY = 0.0,
            int maxLevel = 0,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            // 准备输出参数：6个元素的包围盒数组
            double[] bboxArray = new double[6];
            int[] jsonLen = new int[1];

            // 调用SWIG生成的方法
            string result = reader.To3dTile(
                inPath,
                outPath,
                bboxArray,
                jsonLen,
                offsetX,
                offsetY,
                maxLevel,
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);

            // 返回包围盒（如果成功）
            bbox = string.IsNullOrEmpty(result) ? null : bboxArray;

            return string.IsNullOrEmpty(result) ? null : result;
        }

        /// <summary>
        /// 将 OSGB 转换为 3D Tiles（简化版本，不返回bbox）
        /// </summary>
        public string? ConvertTo3dTiles(
            string inPath,
            string outPath,
            double offsetX = 0.0,
            double offsetY = 0.0,
            int maxLevel = 0,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            return ConvertTo3dTiles(
                inPath,
                outPath,
                out _,
                offsetX,
                offsetY,
                maxLevel,
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);
        }

        /// <summary>
        /// 批量处理整个倾斜摄影数据集
        /// </summary>
        /// <param name="dataDir">输入数据目录（包含多个Tile_xxx子目录）</param>
        /// <param name="outputDir">输出3D Tiles目录</param>
        /// <param name="mergedBox">输出参数：合并后的包围盒 [maxX, maxY, maxZ, minX, minY, minZ]</param>
        /// <param name="offsetX">X轴偏移（经度）</param>
        /// <param name="offsetY">Y轴偏移（纬度）</param>
        /// <param name="maxLevel">最大层级（0表示不限制）</param>
        /// <param name="enableTextureCompression">是否启用纹理压缩</param>
        /// <param name="enableMeshOptimization">是否启用网格优化</param>
        /// <param name="enableDracoCompression">是否启用Draco压缩</param>
        /// <returns>tileset.json内容</returns>
        public string? ConvertTo3dTilesBatch(
            string dataDir,
            string outputDir,
            out double[]? mergedBox,
            double offsetX = 0.0,
            double offsetY = 0.0,
            int maxLevel = 0,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            // 准备输出参数
            double[] mergedBoxArray = new double[6];
            int[] jsonLen = new int[1];

            string result = reader.To3dTileBatch(
                dataDir,
                outputDir,
                mergedBoxArray,
                jsonLen,
                offsetX,
                offsetY,
                maxLevel,
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);

            // 返回合并包围盒（如果成功）
            mergedBox = string.IsNullOrEmpty(result) ? null : mergedBoxArray;

            return string.IsNullOrEmpty(result) ? null : result;
        }

        /// <summary>
        /// 批量处理整个倾斜摄影数据集（简化版本，不返回mergedBox）
        /// </summary>
        public string? ConvertTo3dTilesBatch(
            string dataDir,
            string outputDir,
            double offsetX = 0.0,
            double offsetY = 0.0,
            int maxLevel = 0,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            return ConvertTo3dTilesBatch(
                dataDir,
                outputDir,
                out _,
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
                    reader = null!;
                }
                disposed = true;
            }
        }
    }
}