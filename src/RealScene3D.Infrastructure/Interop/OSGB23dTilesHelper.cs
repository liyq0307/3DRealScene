using System;
using System.Runtime.InteropServices;
using RealScene3D.Lib.OSGB.Interop;

/// <summary>
/// OSGB 读取器辅助类 - 提供更易用的 C# API
/// </summary>
namespace RealScene3D.Lib.OSGB.Interop
{
    /// <summary>
    /// OSGB 读取器辅助类 - 提供更易用的 C# API
    /// </summary>
    public class OSGB23dTilesHelper : IDisposable
    {
        private OSGB23dTiles reader;
        private bool disposed = false;

        public OSGB23dTilesHelper()
        {
            reader = new OSGB23dTiles();
        }

        /// <summary>
        /// 将 OSGB 文件转换为 GLB 文件
        /// </summary>
        public bool ConvertToGlb(
            string osgbPath,
            string glbPath,
            bool bBinary,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            return reader.ToGLB(
                osgbPath,
                glbPath,
                bBinary,
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);
        }

        /// <summary>
        /// 将 OSGB 文件转换为 GLB，返回内存中的字节数组
        /// </summary>
        /// <param name="osgbPath">输入OSGB文件路径</param>
        /// <param name="bBinary">是否生成二进制GLB</param>
        /// <param name="enableTextureCompression">是否启用纹理压缩</param>
        /// <param name="enableMeshOptimization">是否启用网格优化</param>
        /// <param name="enableDracoCompression">是否启用Draco压缩</param>
        /// <returns>GLB文件的字节数组，转换失败返回null</returns>
        /// <remarks>
        /// 该方法将OSGB文件转换为GLB格式，并返回内存中的字节数组。
        /// </remarks>
        /// <returns></returns>
        public byte[]? ConvertToGlbInMemory(
            string osgbPath,
            bool bBinary = true,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {

            byte[] glbBuffer = reader.ToGLBBuf(
                 osgbPath,
                 -1,
                 bBinary,
                 enableTextureCompression,
                 enableMeshOptimization,
                 enableDracoCompression);

            if (glbBuffer == null || glbBuffer.Length <= 0)
            {
                return null;
            }

            return glbBuffer;
        }

        /// <summary>
        /// 将 OSGB 转换为 B3DM
        /// </summary>
        /// <param name="inPath">输入OSGB文件路径</param>
        /// <param name="outPath">输出3D Tiles目录路径</param>
        /// <param name="bbox">输出参数：包围盒数组 [maxX, maxY, maxZ, minX, minY, minZ]</param>
        /// <param name="centerX">中心点X坐标（经度）</param>
        /// <param name="centerY">中心点Y坐标（纬度）</param>
        /// <param name="maxLevel">最大层级（-1表示不限制）</param>
        /// <param name="enableTextureCompression">是否启用纹理压缩</param>
        /// <param name="enableMeshOptimization">是否启用网格优化</param>
        /// <param name="enableDracoCompression">是否启用Draco压缩</param>
        /// <returns>tileset.json内容</returns>
        public string? ConvertToB3DM(
            string inPath,
            string outPath,
            out double[]? bbox,
            double centerX = 0.0,
            double centerY = 0.0,
            int maxLevel = -1,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            // 调用新的 ToB3DM API，返回 B3DMResult 结构体
            B3DMResult result = reader.ToB3DM(
                inPath,
                outPath,
                centerX,
                centerY,
                maxLevel,
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);

            // 检查转换是否成功
            if (!result.success)
            {
                bbox = null;
                return null;
            }

            // 从结构体中提取包围盒数组
            ArrayDouble6 bboxArray = result.boundingBox;
            double[] bboxOutput = new double[6];
            for (int i = 0; i < 6; i++)
            {
                bboxOutput[i] = bboxArray[i];
            }

            bbox = bboxOutput;
            return string.IsNullOrEmpty(result.tilesetJson) ? null : result.tilesetJson;
        }

        /// <summary>
        /// 将 OSGB 转换为 B3DM
        /// </summary>
        public string? ConvertToB3DM(
            string inPath,
            string outPath,
            double centerX = 0.0,
            double centerY = 0.0,
            int maxLevel = -1,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            return ConvertToB3DM(
                inPath,
                outPath,
                out _,
                centerX,
                centerY,
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
        /// <param name="centerX">中心点X坐标（经度）</param>
        /// <param name="centerY">中心点Y坐标（纬度）</param>
        /// <param name="maxLevel">最大层级（0表示不限制）</param>
        /// <param name="enableTextureCompression">是否启用纹理压缩</param>
        /// <param name="enableMeshOptimization">是否启用网格优化</param>
        /// <param name="enableDracoCompression">是否启用Draco压缩</param>
        /// <returns>转换是否成功</returns>
        public bool ConvertToB3DMBatch(
            string dataDir,
            string outputDir,
            double centerX = 0.0,
            double centerY = 0.0,
            int maxLevel = -1,
            bool enableTextureCompression = false,
            bool enableMeshOptimization = false,
            bool enableDracoCompression = false)
        {
            // 调用 ToB3DMBatch API
            bool success = reader.ToB3DMBatch(
                dataDir,
                outputDir,
                centerX,
                centerY,
                maxLevel,
                enableTextureCompression,
                enableMeshOptimization,
                enableDracoCompression);

            return success;
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