using Rectangle = RealScene3D.MeshTiling.Algos.Model.Rectangle;

namespace RealScene3D.MeshTiling.Algos
{
    /// <summary>
    /// 空闲矩形选择启发式枚举，用于决定如何在空闲矩形列表中放置新矩形。
    /// 参考：https://github.com/juj/RectangleBinPack 中 MaxRectanglesBinPack.cpp实现
    /// </summary>
    public enum FreeRectangleChoiceHeuristic
    {
        /// <summary>
        /// BSSF：将矩形放置在最适合其短边的空闲矩形中。
        /// </summary>
        RectangleBestShortSideFit,
        /// <summary>
        /// BLSF：将矩形放置在最适合其长边的空闲矩形中。
        /// </summary>
        RectangleBestLongSideFit,
        /// <summary>
        /// BAF：将矩形放置在最小的适合空闲矩形中。
        /// </summary>
        RectangleBestAreaFit,
        /// <summary>
        /// BL：执行俄罗斯方块式的放置。
        /// </summary>
        RectangleBottomLeftRule,
        /// <summary>
        /// CP：选择矩形与其他矩形尽可能多接触的放置位置。
        /// </summary>
        RectangleContactPointRule
    };

    /// <summary>
    /// 最大矩形装箱算法类，用于高效地将多个矩形打包到固定大小的容器中。
    /// 支持多种启发式算法来优化放置效果。
    /// </summary>
    public class MaxRectanglesBinPack
    {
        /// <summary>
        /// 容器的宽度。
        /// </summary>
        public int binWidth = 0;

        /// <summary>
        /// 容器的高度。
        /// </summary>
        public int binHeight = 0;

        /// <summary>
        /// 是否允许旋转矩形（90度旋转）。
        /// </summary>
        public bool allowRotations;

        /// <summary>
        /// 已使用的矩形列表。
        /// </summary>
        public readonly List<Rectangle> usedRectangles = [];

        /// <summary>
        /// 空闲矩形列表，用于放置新矩形。
        /// </summary>
        public readonly List<Rectangle> freeRectangles = [];


        /// <summary>
        /// 构造函数，初始化装箱器。
        /// </summary>
        /// <param name="width">容器宽度。</param>
        /// <param name="height">容器高度。</param>
        /// <param name="rotations">是否允许旋转矩形。默认为true。</param>
        public MaxRectanglesBinPack(int width, int height, bool rotations = true)
        {
            Init(width, height, rotations);
        }

        /// <summary>
        /// 初始化装箱器，设置容器尺寸和旋转选项，并重置所有矩形列表。
        /// </summary>
        /// <param name="width">容器宽度。</param>
        /// <param name="height">容器高度。</param>
        /// <param name="rotations">是否允许旋转矩形。默认为true。</param>
        public void Init(int width, int height, bool rotations = true)
        {
            binWidth = width;
            binHeight = height;
            allowRotations = rotations;

            var n = new Rectangle
            {
                X = 0,
                Y = 0,
                Width = width,
                Height = height
            };

            usedRectangles.Clear();

            freeRectangles.Clear();
            freeRectangles.Add(n);
        }

        /// <summary>
        /// 尝试将指定尺寸的矩形插入容器中，使用指定的启发式算法。
        /// </summary>
        /// <param name="width">要插入的矩形宽度。</param>
        /// <param name="height">要插入的矩形高度。</param>
        /// <param name="method">使用的放置启发式算法。</param>
        /// <returns>插入的矩形位置，如果无法插入则返回空的矩形（Height为0）。</returns>
        public Rectangle Insert(int width, int height, FreeRectangleChoiceHeuristic method)
        {
            var newNode = new Rectangle();
            var score2 = 0;
            int score1;
            newNode = method switch
            {
                FreeRectangleChoiceHeuristic.RectangleBestShortSideFit => FindPositionForNewNodeBestShortSideFit(width,
                    height, out score1, ref score2),
                FreeRectangleChoiceHeuristic.RectangleBottomLeftRule => FindPositionForNewNodeBottomLeft(width, height,
                    out score1, ref score2),
                FreeRectangleChoiceHeuristic.RectangleContactPointRule => FindPositionForNewNodeContactPoint(width,
                    height, out score1),
                FreeRectangleChoiceHeuristic.RectangleBestLongSideFit => FindPositionForNewNodeBestLongSideFit(width,
                    height, ref score2, out score1),
                FreeRectangleChoiceHeuristic.RectangleBestAreaFit => FindPositionForNewNodeBestAreaFit(width, height,
                    out score1, ref score2),
                _ => newNode
            };

            if (newNode.Height == 0)
                return newNode;

            var numRectangleanglesToProcess = freeRectangles.Count;
            for (var i = 0; i < numRectangleanglesToProcess; ++i)
            {
                if (SplitFreeNode(freeRectangles[i], ref newNode))
                {
                    freeRectangles.RemoveAt(i);
                    --i;
                    --numRectangleanglesToProcess;
                }
            }

            PruneFreeList();

            usedRectangles.Add(newNode);
            return newNode;
        }

        /// <summary>
        /// 将指定的矩形放置到容器中，并更新空闲矩形列表。
        /// </summary>
        /// <param name="node">要放置的矩形。</param>
        private void PlaceRectangle(Rectangle node)
        {
            var numRectangleanglesToProcess = freeRectangles.Count;
            for (var i = 0; i < numRectangleanglesToProcess; ++i)
            {
                if (SplitFreeNode(freeRectangles[i], ref node))
                {
                    freeRectangles.RemoveAt(i);
                    --i;
                    --numRectangleanglesToProcess;
                }
            }

            PruneFreeList();

            usedRectangles.Add(node);
        }

        /// <summary>
        /// 计算将指定尺寸的矩形插入容器时的得分和位置，但不实际插入。
        /// </summary>
        /// <param name="width">矩形宽度。</param>
        /// <param name="height">矩形高度。</param>
        /// <param name="method">使用的放置启发式算法。</param>
        /// <param name="score1">输出得分1（根据启发式算法而定）。</param>
        /// <param name="score2">输出得分2（根据启发式算法而定）。</param>
        /// <returns>最佳放置位置的矩形，如果无法放置则Height为0。</returns>
        public Rectangle ScoreRectangle(int width, int height, FreeRectangleChoiceHeuristic method, out int score1,
            out int score2)
        {
            var newNode = new Rectangle();
            score1 = int.MaxValue;
            score2 = int.MaxValue;
            switch (method)
            {
                case FreeRectangleChoiceHeuristic.RectangleBestShortSideFit:
                    newNode = FindPositionForNewNodeBestShortSideFit(width, height, out score1, ref score2);
                    break;
                case FreeRectangleChoiceHeuristic.RectangleBottomLeftRule:
                    newNode = FindPositionForNewNodeBottomLeft(width, height, out score1, ref score2);
                    break;
                case FreeRectangleChoiceHeuristic.RectangleContactPointRule:
                    newNode = FindPositionForNewNodeContactPoint(width, height, out score1);
                    score1 = -score1; // Reverse since we are minimizing, but for contact point score bigger is better.
                    break;
                case FreeRectangleChoiceHeuristic.RectangleBestLongSideFit:
                    newNode = FindPositionForNewNodeBestLongSideFit(width, height, ref score2, out score1);
                    break;
                case FreeRectangleChoiceHeuristic.RectangleBestAreaFit:
                    newNode = FindPositionForNewNodeBestAreaFit(width, height, out score1, ref score2);
                    break;
            }

            // 无法放置当前矩形。
            if (newNode.Height == 0)
            {
                score1 = int.MaxValue;
                score2 = int.MaxValue;
            }

            return newNode;
        }

        /// <summary>
        /// 计算已使用表面的比例。
        /// </summary>
        /// <returns>占用率（0.0到1.0之间的浮点数）。</returns>
        public float Occupancy()
        {
            ulong usedSurfaceArea = 0;
            for (var i = 0; i < usedRectangles.Count; ++i)
                usedSurfaceArea += (uint)usedRectangles[i].Width * (uint)usedRectangles[i].Height;

            return (float)usedSurfaceArea / (binWidth * binHeight);
        }

        /// <summary>
        /// 使用底部左边规则寻找新矩形的最佳放置位置。
        /// </summary>
        /// <param name="width">要放置的矩形宽度。</param>
        /// <param name="height">要放置的矩形高度。</param>
        /// <param name="bestY">输出最佳Y坐标。</param>
        /// <param name="bestX">输入/输出最佳X坐标。</param>
        /// <returns>最佳放置位置的矩形。</returns>
        private Rectangle FindPositionForNewNodeBottomLeft(int width, int height, out int bestY, ref int bestX)
        {
            var bestNode = new Rectangle();
            // 将bestNode初始化为0，相当于memset。

            bestY = int.MaxValue;

            for (var i = 0; i < freeRectangles.Count; ++i)
            {
                // 尝试以正立（非翻转）方向放置矩形。
                if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
                {
                    var topSideY = freeRectangles[i].Y + height;
                    if (topSideY < bestY || (topSideY == bestY && freeRectangles[i].X < bestX))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestY = topSideY;
                        bestX = freeRectangles[i].X;
                    }
                }

                if (allowRotations && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
                {
                    var topSideY = freeRectangles[i].Y + width;
                    if (topSideY < bestY || (topSideY == bestY && freeRectangles[i].X < bestX))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestY = topSideY;
                        bestX = freeRectangles[i].X;
                    }
                }
            }

            return bestNode;
        }

        /// <summary>
        /// 使用最佳短边适配规则寻找新矩形的最佳放置位置。
        /// </summary>
        /// <param name="width">要放置的矩形宽度。</param>
        /// <param name="height">要放置的矩形高度。</param>
        /// <param name="bestShortSideFit">输出最佳短边适配值。</param>
        /// <param name="bestLongSideFit">输入/输出最佳长边适配值。</param>
        /// <returns>最佳放置位置的矩形。</returns>
        private Rectangle FindPositionForNewNodeBestShortSideFit(int width, int height, out int bestShortSideFit,
            ref int bestLongSideFit)
        {
            var bestNode = new Rectangle();
            // 将bestNode初始化为0，相当于memset。

            bestShortSideFit = int.MaxValue;

            for (var i = 0; i < freeRectangles.Count; ++i)
            {
                // 尝试以正立（非翻转）方向放置矩形。
                if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
                {
                    var leftoverHoriz = Math.Abs(freeRectangles[i].Width - width);
                    var leftoverVert = Math.Abs(freeRectangles[i].Height - height);
                    var shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
                    var longSideFit = Math.Max(leftoverHoriz, leftoverVert);

                    if (shortSideFit < bestShortSideFit ||
                        (shortSideFit == bestShortSideFit && longSideFit < bestLongSideFit))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }

                if (allowRotations && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
                {
                    var flippedLeftoverHoriz = Math.Abs(freeRectangles[i].Width - height);
                    var flippedLeftoverVert = Math.Abs(freeRectangles[i].Height - width);
                    var flippedShortSideFit = Math.Min(flippedLeftoverHoriz, flippedLeftoverVert);
                    var flippedLongSideFit = Math.Max(flippedLeftoverHoriz, flippedLeftoverVert);

                    if (flippedShortSideFit < bestShortSideFit || (flippedShortSideFit == bestShortSideFit &&
                                                                   flippedLongSideFit < bestLongSideFit))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestShortSideFit = flippedShortSideFit;
                        bestLongSideFit = flippedLongSideFit;
                    }
                }
            }

            return bestNode;
        }

        /// <summary>
        /// 使用最佳长边适配规则寻找新矩形的最佳放置位置。
        /// </summary>
        /// <param name="width">要放置的矩形宽度。</param>
        /// <param name="height">要放置的矩形高度。</param>
        /// <param name="bestShortSideFit">输入/输出最佳短边适配值。</param>
        /// <param name="bestLongSideFit">输出最佳长边适配值。</param>
        /// <returns>最佳放置位置的矩形。</returns>
        private Rectangle FindPositionForNewNodeBestLongSideFit(int width, int height, ref int bestShortSideFit,
            out int bestLongSideFit)
        {
            var bestNode = new Rectangle();
            // 将bestNode初始化为0，相当于memset。

            bestLongSideFit = int.MaxValue;

            for (var i = 0; i < freeRectangles.Count; ++i)
            {
                // 尝试以正立（非翻转）方向放置矩形。
                if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
                {
                    var leftoverHoriz = Math.Abs(freeRectangles[i].Width - width);
                    var leftoverVert = Math.Abs(freeRectangles[i].Height - height);
                    var shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
                    var longSideFit = Math.Max(leftoverHoriz, leftoverVert);

                    if (longSideFit < bestLongSideFit ||
                        (longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }

                if (allowRotations && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
                {
                    var leftoverHoriz = Math.Abs(freeRectangles[i].Width - height);
                    var leftoverVert = Math.Abs(freeRectangles[i].Height - width);
                    var shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
                    var longSideFit = Math.Max(leftoverHoriz, leftoverVert);

                    if (longSideFit < bestLongSideFit ||
                        (longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }
            }

            return bestNode;
        }

        /// <summary>
        /// 使用最佳面积适配规则寻找新矩形的最佳放置位置。
        /// </summary>
        /// <param name="width">要放置的矩形宽度。</param>
        /// <param name="height">要放置的矩形高度。</param>
        /// <param name="bestAreaFit">输出最佳面积适配值。</param>
        /// <param name="bestShortSideFit">输入/输出最佳短边适配值。</param>
        /// <returns>最佳放置位置的矩形。</returns>
        private Rectangle FindPositionForNewNodeBestAreaFit(int width, int height, out int bestAreaFit,
            ref int bestShortSideFit)
        {
            var bestNode = new Rectangle();

            bestAreaFit = int.MaxValue;

            for (var i = 0; i < freeRectangles.Count; ++i)
            {
                var areaFit = freeRectangles[i].Width * freeRectangles[i].Height - width * height;

                // 尝试以正立（非翻转）方向放置矩形。
                if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
                {
                    var leftoverHoriz = Math.Abs(freeRectangles[i].Width - width);
                    var leftoverVert = Math.Abs(freeRectangles[i].Height - height);
                    var shortSideFit = Math.Min(leftoverHoriz, leftoverVert);

                    if (areaFit < bestAreaFit || (areaFit == bestAreaFit && shortSideFit < bestShortSideFit))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestShortSideFit = shortSideFit;
                        bestAreaFit = areaFit;
                    }
                }

                if (allowRotations && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
                {
                    var leftoverHoriz = Math.Abs(freeRectangles[i].Width - height);
                    var leftoverVert = Math.Abs(freeRectangles[i].Height - width);
                    var shortSideFit = Math.Min(leftoverHoriz, leftoverVert);

                    if (areaFit < bestAreaFit || (areaFit == bestAreaFit && shortSideFit < bestShortSideFit))
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestShortSideFit = shortSideFit;
                        bestAreaFit = areaFit;
                    }
                }
            }

            return bestNode;
        }

        /// <summary>
        /// 返回两个区间的重叠长度，如果不重叠则为0
        /// </summary>
        /// <param name="i1start">区间1的起始位置。</param>
        /// <param name="i1end">区间1的结束位置。</param>
        /// <param name="i2start">区间2的起始位置。</param>
        /// <param name="i2end">区间2的结束位置。</param>
        /// <returns>两个区间的重叠长度，如果不重叠则为0。</returns>
        private static int CommonIntervalLength(int i1start, int i1end, int i2start, int i2end)
        {
            if (i1end < i2start || i2end < i1start)
                return 0;
            return Math.Min(i1end, i2end) - Math.Max(i1start, i2start);
        }

        /// <summary>
        /// 计算指定位置放置矩形时的接触点得分。
        /// </summary>
        /// <param name="x">矩形左上角X坐标。</param>
        /// <param name="y">矩形左上角Y坐标。</param>
        /// <param name="width">矩形宽度。</param>
        /// <param name="height">矩形高度。</param>
        /// <returns>接触点得分。</returns>
        private int ContactPointScoreNode(int x, int y, int width, int height)
        {
            var score = 0;

            if (x == 0 || x + width == binWidth)
                score += height;
            if (y == 0 || y + height == binHeight)
                score += width;

            for (var i = 0; i < usedRectangles.Count; ++i)
            {
                if (usedRectangles[i].X == x + width || usedRectangles[i].X + usedRectangles[i].Width == x)
                    score += CommonIntervalLength(usedRectangles[i].Y, usedRectangles[i].Y + usedRectangles[i].Height,
                        y, y + height);
                if (usedRectangles[i].Y == y + height || usedRectangles[i].Y + usedRectangles[i].Height == y)
                    score += CommonIntervalLength(usedRectangles[i].X, usedRectangles[i].X + usedRectangles[i].Width, x,
                        x + width);
            }

            return score;
        }

        /// <summary>
        /// 使用接触点规则寻找新矩形的最佳放置位置。
        /// </summary>
        /// <param name="width">要放置的矩形宽度。</param>
        /// <param name="height">要放置的矩形高度。</param> 
        /// <param name="bestContactScore">返回最佳接触点分数。</param>
        /// <returns>最佳放置位置的矩形。</returns>
        private Rectangle FindPositionForNewNodeContactPoint(int width, int height, out int bestContactScore)
        {
            var bestNode = new Rectangle();

            bestContactScore = -1;

            for (var i = 0; i < freeRectangles.Count; ++i)
            {
                // 尝试以正立（非翻转）方向放置矩形。
                if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
                {
                    var score = ContactPointScoreNode(freeRectangles[i].X, freeRectangles[i].Y, width, height);
                    if (score > bestContactScore)
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestContactScore = score;
                    }
                }

                if (allowRotations && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
                {
                    var score = ContactPointScoreNode(freeRectangles[i].X, freeRectangles[i].Y, height, width);
                    if (score > bestContactScore)
                    {
                        bestNode.X = freeRectangles[i].X;
                        bestNode.Y = freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestContactScore = score;
                    }
                }
            }

            return bestNode;
        }

        /// <summary>
        /// 尝试将已使用的矩形从空闲矩形中分割出来，更新空闲矩形列表。
        /// </summary>
        /// <param name="freeNode">当前空闲矩形。</param>
        /// <param name="usedNode">已使用的矩形。</param>
        /// <returns>是否成功分割。</returns>
        private bool SplitFreeNode(Rectangle freeNode, ref Rectangle usedNode)
        {
            // 如果两个矩形不相交，则无需分割
            if (usedNode.X >= freeNode.X + freeNode.Width || usedNode.X + usedNode.Width <= freeNode.X ||
                usedNode.Y >= freeNode.Y + freeNode.Height || usedNode.Y + usedNode.Height <= freeNode.Y)
                return false;

            if (usedNode.X < freeNode.X + freeNode.Width && usedNode.X + usedNode.Width > freeNode.X)
            {
                // 在已使用矩形的顶部创建新节点。
                if (usedNode.Y > freeNode.Y && usedNode.Y < freeNode.Y + freeNode.Height)
                {
                    var newNode = freeNode.Clone();
                    newNode.Height = usedNode.Y - newNode.Y;
                    freeRectangles.Add(newNode);
                }

                // 在已使用矩形的底部创建新节点。
                if (usedNode.Y + usedNode.Height < freeNode.Y + freeNode.Height)
                {
                    var newNode = freeNode.Clone();
                    newNode.Y = usedNode.Y + usedNode.Height;
                    newNode.Height = freeNode.Y + freeNode.Height - (usedNode.Y + usedNode.Height);
                    freeRectangles.Add(newNode);
                }
            }

            if (usedNode.Y < freeNode.Y + freeNode.Height && usedNode.Y + usedNode.Height > freeNode.Y)
            {
                // 在已使用矩形的左侧创建新节点。
                if (usedNode.X > freeNode.X && usedNode.X < freeNode.X + freeNode.Width)
                {
                    var newNode = freeNode.Clone();
                    newNode.Width = usedNode.X - newNode.X;
                    freeRectangles.Add(newNode);
                }

                // 在已使用矩形的右侧创建新节点。
                if (usedNode.X + usedNode.Width < freeNode.X + freeNode.Width)
                {
                    var newNode = freeNode.Clone();
                    newNode.X = usedNode.X + usedNode.Width;
                    newNode.Width = freeNode.X + freeNode.Width - (usedNode.X + usedNode.Width);
                    freeRectangles.Add(newNode);
                }
            }

            return true;
        }

        /// <summary>
        /// 清理空闲矩形列表，移除被其他矩形完全覆盖的矩形。
        /// </summary>
        private void PruneFreeList()
        {
            var rectanglesToRemove = new SortedSet<int>();

            for (var i = 0; i < freeRectangles.Count; i++)
            {
                for (var j = i + 1; j < freeRectangles.Count; ++j)
                {
                    if (rectanglesToRemove.Contains(j) || rectanglesToRemove.Contains(i))
                    {
                        break;
                    }

                    if (freeRectangles[j].Contains(freeRectangles[i]))
                    {
                        lock (rectanglesToRemove)
                        {
                            rectanglesToRemove.Add(i);
                        }

                        break;
                    }

                    if (freeRectangles[i].Contains(freeRectangles[j]))
                    {
                        lock (rectanglesToRemove)
                        {
                            rectanglesToRemove.Add(j);
                        }
                    }
                }
            }

            foreach (var rectangleToRemove in rectanglesToRemove.Reverse())
            {
                freeRectangles.RemoveAt(rectangleToRemove);
            }
        }
    }
}