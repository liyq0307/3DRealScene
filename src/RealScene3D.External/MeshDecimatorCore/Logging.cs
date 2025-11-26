#region License
/*
MIT License

Copyright(c) 2017-2018 Mattias Edlund

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
#endregion

namespace MeshDecimatorCore
{
    #region 日志记录器接口
    /// <summary>
    /// 日志记录器。
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// 记录一行详细文本。
        /// </summary>
        /// <param name="text">文本。</param>
        void LogVerbose(string text);

        /// <summary>
        /// 记录一行警告文本。
        /// </summary>
        /// <param name="text">文本。</param>
        void LogWarning(string text);

        /// <summary>
        /// 记录一行错误文本。
        /// </summary>
        /// <param name="text">文本。</param>
        void LogError(string text);
    }
    #endregion

    /// <summary>
    /// 日志记录 API。
    /// </summary>
    public static class Logging
    {
        #region 字段
        private static ILogger logger = null;
        private static object syncObj = new object();
        #endregion

        #region 属性
        /// <summary>
        /// 获取或设置活动的日志记录器。
        /// </summary>
        public static ILogger Logger
        {
            get { return logger; }
            set {
                lock (syncObj)
                {
                    logger = value;
                }
            }
        }
        #endregion

        #region 静态初始化器
        /// <summary>
        /// 静态初始化器。
        /// </summary>
        static Logging()
        {
            logger = new Loggers.ConsoleLogger();
        }
        #endregion

        #region 公共方法
        #region 详细
        /// <summary>
        /// 记录一行详细文本。
        /// </summary>
        /// <param name="text">文本。</param>
        public static void LogVerbose(string text)
        {
            lock (syncObj)
            {
                if (logger != null)
                {
                    logger.LogVerbose(text);
                }
            }
        }

        /// <summary>
        /// 记录一行格式化的详细文本。
        /// </summary>
        /// <param name="format">字符串格式。</param>
        /// <param name="args">字符串参数。</param>
        public static void LogVerbose(string format, params object[] args)
        {
            LogVerbose(string.Format(format, args));
        }
        #endregion

        #region 警告
        /// <summary>
        /// 记录一行警告文本。
        /// </summary>
        /// <param name="text">文本。</param>
        public static void LogWarning(string text)
        {
            lock (syncObj)
            {
                if (logger != null)
                {
                    logger.LogWarning(text);
                }
            }
        }

        /// <summary>
        /// 记录一行格式化的警告文本。
        /// </summary>
        /// <param name="format">字符串格式。</param>
        /// <param name="args">字符串参数。</param>
        public static void LogWarning(string format, params object[] args)
        {
            LogWarning(string.Format(format, args));
        }
        #endregion

        #region 错误
        /// <summary>
        /// 记录一行错误文本。
        /// </summary>
        /// <param name="text">文本。</param>
        public static void LogError(string text)
        {
            lock (syncObj)
            {
                if (logger != null)
                {
                    logger.LogError(text);
                }
            }
        }

        /// <summary>
        /// 记录一行格式化的错误文本。
        /// </summary>
        /// <param name="format">字符串格式。</param>
        /// <param name="args">字符串参数。</param>
        public static void LogError(string format, params object[] args)
        {
            LogError(string.Format(format, args));
        }
        #endregion
        #endregion
    }
}