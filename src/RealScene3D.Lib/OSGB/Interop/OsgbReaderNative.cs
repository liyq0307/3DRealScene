using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RealScene3D.Lib.OSGB.Interop
{
    /// <summary>
    /// OSGB读取器C API封装（跨平台）
    /// 使用P/Invoke调用Native DLL
    /// </summary>
    public class OsgbReaderNative : IDisposable
    {
        private const string DllName = "RealScene3D.Lib.OSGB";
        private IntPtr _handle;
        private bool _disposed;

        #region P/Invoke声明

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr osgb_reader_create();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void osgb_reader_destroy(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int osgb_to_glb(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string osgbPath,
            [MarshalAs(UnmanagedType.LPStr)] string glbPath,
            int enableTextureCompress,
            int enableMeshopt,
            int enableDraco);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int osgb_to_glb_buffer(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string osgbPath,
            out IntPtr outBuffer,
            out int outSize,
            int enableTextureCompress,
            int enableMeshopt,
            int enableDraco);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void osgb_free_buffer(IntPtr buffer);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr osgb_get_last_error(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr osgb_get_version();

        #endregion

        #region 构造函数和析构

        public OsgbReaderNative()
        {
            _handle = osgb_reader_create();
            if (_handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create OSGB reader instance");
            }
        }

        ~OsgbReaderNative()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_handle != IntPtr.Zero)
                {
                    osgb_reader_destroy(_handle);
                    _handle = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 将OSGB文件转换为GLB文件
        /// </summary>
        /// <param name="osgbPath">OSGB文件路径</param>
        /// <param name="glbPath">输出GLB文件路径</param>
        /// <param name="enableTextureCompress">是否启用纹理压缩（KTX2）</param>
        /// <param name="enableMeshopt">是否启用网格优化</param>
        /// <param name="enableDraco">是否启用Draco压缩</param>
        /// <returns>成功返回true，失败返回false</returns>
        public bool ConvertToGlb(
            string osgbPath,
            string glbPath,
            bool enableTextureCompress = false,
            bool enableMeshopt = false,
            bool enableDraco = false)
        {
            ThrowIfDisposed();

            int result = osgb_to_glb(
                _handle,
                osgbPath,
                glbPath,
                enableTextureCompress ? 1 : 0,
                enableMeshopt ? 1 : 0,
                enableDraco ? 1 : 0);

            return result != 0;
        }

        /// <summary>
        /// 将OSGB文件转换为GLB内存数据
        /// </summary>
        /// <param name="osgbPath">OSGB文件路径</param>
        /// <param name="enableTextureCompress">是否启用纹理压缩</param>
        /// <param name="enableMeshopt">是否启用网格优化</param>
        /// <param name="enableDraco">是否启用Draco压缩</param>
        /// <returns>GLB二进制数据，失败返回null</returns>
        public byte[]? ConvertToGlbBuffer(
            string osgbPath,
            bool enableTextureCompress = false,
            bool enableMeshopt = false,
            bool enableDraco = false)
        {
            ThrowIfDisposed();

            IntPtr bufferPtr = IntPtr.Zero;
            try
            {
                int size;
                int result = osgb_to_glb_buffer(
                    _handle,
                    osgbPath,
                    out bufferPtr,
                    out size,
                    enableTextureCompress ? 1 : 0,
                    enableMeshopt ? 1 : 0,
                    enableDraco ? 1 : 0);

                if (result == 0 || bufferPtr == IntPtr.Zero || size <= 0)
                {
                    return null;
                }

                // 复制数据到托管内存
                byte[] data = new byte[size];
                Marshal.Copy(bufferPtr, data, 0, size);
                return data;
            }
            finally
            {
                // 释放Native分配的内存
                if (bufferPtr != IntPtr.Zero)
                {
                    osgb_free_buffer(bufferPtr);
                }
            }
        }

        /// <summary>
        /// 获取最后的错误信息
        /// </summary>
        /// <returns>错误信息，无错误返回null</returns>
        public string? GetLastError()
        {
            ThrowIfDisposed();

            IntPtr errorPtr = osgb_get_last_error(_handle);
            if (errorPtr == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.PtrToStringAnsi(errorPtr);
        }

        /// <summary>
        /// 获取库版本
        /// </summary>
        /// <returns>版本字符串</returns>
        public static string GetVersion()
        {
            IntPtr versionPtr = osgb_get_version();
            return Marshal.PtrToStringAnsi(versionPtr) ?? "Unknown";
        }

        #endregion

        #region 辅助方法

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(OsgbReaderNative));
            }
        }

        #endregion
    }
}
