namespace MeshDecimator.Math
{
    /// <summary>
    /// 数学助手。
    /// </summary>
    public static class MathHelper
    {
        #region 常量
        /// <summary>
        /// Pi 常量。
        /// </summary>
        public const float PI = 3.14159274f;

        /// <summary>
        /// Pi 常量。
        /// </summary>
        public const double PId = 3.1415926535897932384626433832795;

        /// <summary>
        /// 度到弧度的常量。
        /// </summary>
        public const float Deg2Rad = PI / 180f;

        /// <summary>
        /// 度到弧度的常量。
        /// </summary>
        public const double Deg2Radd = PId / 180.0;

        /// <summary>
        /// 弧度到度的常量。
        /// </summary>
        public const float Rad2Deg = 180f / PI;

        /// <summary>
        /// 弧度到度的常量。
        /// </summary>
        public const double Rad2Degd = 180.0 / PId;
        #endregion

        #region 最小值
        /// <summary>
        /// 返回两个值中的最小值。
        /// </summary>
        /// <param name="val1">第一个值。</param>
        /// <param name="val2">第二个值。</param>
        /// <returns>最小值。</returns>
        public static int Min(int val1, int val2)
        {
            return (val1 < val2 ? val1 : val2);
        }

        /// <summary>
        /// 返回三个值中的最小值。
        /// </summary>
        /// <param name="val1">第一个值。</param>
        /// <param name="val2">第二个值。</param>
        /// <param name="val3">第三个值。</param>
        /// <returns>最小值。</returns>
        public static int Min(int val1, int val2, int val3)
        {
            return (val1 < val2 ? (val1 < val3 ? val1 : val3) : (val2 < val3 ? val2 : val3));
        }

        /// <summary>
        /// 返回两个值中的最小值。
        /// </summary>
        /// <param name="val1">第一个值。</param>
        /// <param name="val2">第二个值。</param>
        /// <returns>最小值。</returns>
        public static float Min(float val1, float val2)
        {
            return (val1 < val2 ? val1 : val2);
        }

        /// <summary>
        /// 返回三个值中的最小值。
        /// </summary>
        /// <param name="val1">第一个值。</param>
        /// <param name="val2">第二个值。</param>
        /// <param name="val3">第三个值。</param>
        /// <returns>最小值。</returns>
        public static float Min(float val1, float val2, float val3)
        {
            return (val1 < val2 ? (val1 < val3 ? val1 : val3) : (val2 < val3 ? val2 : val3));
        }

        /// <summary>
        /// 返回两个值中的最小值。
        /// </summary>
        /// <param name="val1">第一个值。</param>
        /// <param name="val2">第二个值。</param>
        /// <returns>最小值。</returns>
        public static double Min(double val1, double val2)
        {
            return (val1 < val2 ? val1 : val2);
        }

        /// <summary>
        /// 返回三个值中的最小值。
        /// </summary>
        /// <param name="val1">第一个值。</param>
        /// <param name="val2">第二个值。</param>
        /// <param name="val3">第三个值。</param>
        /// <returns>最小值。</returns>
        public static double Min(double val1, double val2, double val3)
        {
            return (val1 < val2 ? (val1 < val3 ? val1 : val3) : (val2 < val3 ? val2 : val3));
        }
        #endregion

        #region 最大值
        /// <summary>
        /// 返回两个值中的最大值。
        /// </summary>
        /// <param name="val1">第一个值。</param>
        /// <param name="val2">第二个值。</param>
        /// <returns>最大值。</returns>
        public static int Max(int val1, int val2)
        {
            return (val1 > val2 ? val1 : val2);
        }

        /// <summary>
        /// 返回三个值中的最大值。
        /// </summary>
        /// <param name="val1">第一个值。</param>
        /// <param name="val2">第二个值。</param>
        /// <param name="val3">第三个值。</param>
        /// <returns>最大值。</returns>
        public static int Max(int val1, int val2, int val3)
        {
            return (val1 > val2 ? (val1 > val3 ? val1 : val3) : (val2 > val3 ? val2 : val3));
        }

        /// <summary>
        /// 返回两个值中的最大值。
        /// </summary>
        /// <param name="val1">第一个值。</param>
        /// <param name="val2">第二个值。</param>
        /// <returns>最大值。</returns>
        public static float Max(float val1, float val2)
        {
            return (val1 > val2 ? val1 : val2);
        }

        /// <summary>
        /// 返回三个值中的最大值。
        /// </summary>
        /// <param name="val1">第一个值。</param>
        /// <param name="val2">第二个值。</param>
        /// <param name="val3">第三个值。</param>
        /// <returns>最大值。</returns>
        public static float Max(float val1, float val2, float val3)
        {
            return (val1 > val2 ? (val1 > val3 ? val1 : val3) : (val2 > val3 ? val2 : val3));
        }

        /// <summary>
        /// 返回两个值中的最大值。
        /// </summary>
        /// <param name="val1">第一个值。</param>
        /// <param name="val2">第二个值。</param>
        /// <returns>最大值。</returns>
        public static double Max(double val1, double val2)
        {
            return (val1 > val2 ? val1 : val2);
        }

        /// <summary>
        /// 返回三个值中的最大值。
        /// </summary>
        /// <param name="val1">第一个值。</param>
        /// <param name="val2">第二个值。</param>
        /// <param name="val3">第三个值。</param>
        /// <returns>最大值。</returns>
        public static double Max(double val1, double val2, double val3)
        {
            return (val1 > val2 ? (val1 > val3 ? val1 : val3) : (val2 > val3 ? val2 : val3));
        }
        #endregion

        #region 夹取
        /// <summary>
        /// 将值限制在最小值和最大值之间。
        /// </summary>
        /// <param name="value">要夹取的值。</param>
        /// <param name="min">最小值。</param>
        /// <param name="max">最大值。</param>
        /// <returns>夹取后的值。</returns>
        public static float Clamp(float value, float min, float max)
        {
            return (value >= min ? (value <= max ? value : max) : min);
        }

        /// <summary>
        /// 将值限制在最小值和最大值之间。
        /// </summary>
        /// <param name="value">要夹取的值。</param>
        /// <param name="min">最小值。</param>
        /// <param name="max">最大值。</param>
        /// <returns>夹取后的值。</returns>
        public static double Clamp(double value, double min, double max)
        {
            return (value >= min ? (value <= max ? value : max) : min);
        }

        /// <summary>
        /// 将值限制在0和1之间。
        /// </summary>
        /// <param name="value">要夹取的值。</param>
        /// <returns>夹取后的值。</returns>
        public static float Clamp01(float value)
        {
            return (value > 0f ? (value < 1f ? value : 1f) : 0f);
        }

        /// <summary>
        /// 将值限制在0和1之间。
        /// </summary>
        /// <param name="value">要夹取的值。</param>
        /// <returns>夹取后的值。</returns>
        public static double Clamp01(double value)
        {
            return (value > 0.0 ? (value < 1.0 ? value : 1.0) : 0.0);
        }
        #endregion

        #region 三角形面积
        /// <summary>
        /// 计算三角形的面积。
        /// </summary>
        /// <param name="p0">第一个点。</param>
        /// <param name="p1">第二个点。</param>
        /// <param name="p2">第三个点。</param>
        /// <returns>三角形面积。</returns>
        public static float TriangleArea(ref Vector3 p0, ref Vector3 p1, ref Vector3 p2)
        {
            var dx = p1 - p0;
            var dy = p2 - p0;
            return dx.Magnitude * ((float)System.Math.Sin(Vector3.Angle(ref dx, ref dy) * Deg2Rad) * dy.Magnitude) * 0.5f;
        }

        /// <summary>
        /// 计算三角形的面积。
        /// </summary>
        /// <param name="p0">第一个点。</param>
        /// <param name="p1">第二个点。</param>
        /// <param name="p2">第三个点。</param>
        /// <returns>三角形面积。</returns>
        public static double TriangleArea(ref Vector3d p0, ref Vector3d p1, ref Vector3d p2)
        {
            var dx = p1 - p0;
            var dy = p2 - p0;
            return dx.Magnitude * (System.Math.Sin(Vector3d.Angle(ref dx, ref dy) * Deg2Radd) * dy.Magnitude) * 0.5f;
        }
        #endregion
    }
}