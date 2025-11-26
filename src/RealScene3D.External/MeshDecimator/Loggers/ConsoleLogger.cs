namespace MeshDecimator.Loggers
{
    /// <summary>
    /// 默认控制台日志记录器。
    /// </summary>
    public sealed class ConsoleLogger : ILogger
    {
        /// <summary>
        /// 记录一行详细文本。
        /// </summary>
        /// <param name="text">文本。</param>
        public void LogVerbose(string text)
        {
            Console.WriteLine(text);
        }

        /// <summary>
        /// 记录一行警告文本。
        /// </summary>
        /// <param name="text">文本。</param>
        public void LogWarning(string text)
        {
            Console.WriteLine(text);
        }

        /// <summary>
        /// 记录一行错误文本。
        /// </summary>
        /// <param name="text">文本。</param>
        public void LogError(string text)
        {
            Console.Error.WriteLine(text);
        }
    }
}