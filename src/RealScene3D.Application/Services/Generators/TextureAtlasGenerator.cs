using Microsoft.Extensions.Logging;
using RealScene3D.Domain.Entities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace RealScene3D.Application.Services.Generators;

/// <summary>
/// 纹理图集生成器 - 将多个纹理图片打包到单个图集中
/// 使用MaxRects矩形装箱算法进行高效布局
/// 参考: http://clb.confined.space/MagicTex/max_rects_bin_pack.pdf
/// </summary>
public class TextureAtlasGenerator
{
    private readonly ILogger<TextureAtlasGenerator> _logger;

    /// <summary>
    /// JPEG编码器 - 用于纹理图集压缩
    /// 质量75提供了良好的大小/质量平衡
    /// </summary>
    private static readonly JpegEncoder JpegEncoder = new() { Quality = 75 };

    public TextureAtlasGenerator(ILogger<TextureAtlasGenerator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 纹理图集结果
    /// </summary>
    public class AtlasResult
    {
        /// <summary>
        /// 图集图像数据（RGBA格式）
        /// </summary>
        public byte[] ImageData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// 图集宽度
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 图集高度
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 纹理映射：原始纹理路径 → 图集UV坐标
        /// </summary>
        public Dictionary<string, TextureRegion> TextureRegions { get; set; } = new();

        /// <summary>
        /// 空间利用率 (0.0-1.0)
        /// </summary>
        public double Utilization { get; set; }
    }

    /// <summary>
    /// 纹理区域 - 纹理在图集中的位置和UV坐标
    /// </summary>
    public class TextureRegion
    {
        /// <summary>
        /// 在图集中的X坐标（像素）
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// 在图集中的Y坐标（像素）
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// 纹理宽度（像素）
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 纹理高度（像素）
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 归一化UV最小坐标
        /// </summary>
        public Vector2D UVMin { get; set; } = new(0, 0);

        /// <summary>
        /// 归一化UV最大坐标
        /// </summary>
        public Vector2D UVMax { get; set; } = new(1, 1);
    }

    /// <summary>
    /// 生成纹理图集
    /// </summary>
    /// <param name="texturePaths">纹理文件路径列表</param>
    /// <param name="maxAtlasSize">图集最大尺寸（默认2048）</param>
    /// <param name="padding">纹理间距（像素，防止纹理渗透）</param>
    /// <param name="downsampleFactor">降采样因子（默认1.0，无缩放）。值越小，纹理越小</param>
    /// <param name="useJpegCompression">是否使用JPEG压缩（默认true）</param>
    /// <returns>纹理图集结果</returns>
    public async Task<AtlasResult> GenerateAtlasAsync(
        IEnumerable<string> texturePaths,
        int maxAtlasSize = 2048,
        int padding = 2,
        double downsampleFactor = 1.0,
        bool useJpegCompression = true)
    {
        var startTime = DateTime.UtcNow;
        var validTextures = texturePaths.Where(File.Exists).Distinct().ToList();

        _logger.LogInformation("开始生成纹理图集: {Count}张纹理, 最大尺寸={Size}x{Size}",
            validTextures.Count, maxAtlasSize, maxAtlasSize);

        if (validTextures.Count == 0)
        {
            _logger.LogWarning("没有有效的纹理文件");
            return CreateEmptyAtlas();
        }

        try
        {
            // 1. 加载所有纹理图像（应用降采样）
            var loadedTextures = new List<LoadedTexture>();
            foreach (var path in validTextures)
            {
                try
                {
                    var image = await Image.LoadAsync<Rgba32>(path);

                    // 应用降采样
                    if (downsampleFactor < 1.0 && downsampleFactor > 0.0)
                    {
                        int newWidth = Math.Max(1, (int)(image.Width * downsampleFactor));
                        int newHeight = Math.Max(1, (int)(image.Height * downsampleFactor));

                        if (newWidth != image.Width || newHeight != image.Height)
                        {
                            image.Mutate(x => x.Resize(newWidth, newHeight));
                            _logger.LogDebug("纹理降采样: {Path}, {OldW}x{OldH} → {NewW}x{NewH} (因子={Factor:F2})",
                                path, image.Width, image.Height, newWidth, newHeight, downsampleFactor);
                        }
                    }

                    loadedTextures.Add(new LoadedTexture
                    {
                        Path = path,
                        Image = image,
                        Width = image.Width,
                        Height = image.Height
                    });
                    _logger.LogDebug("加载纹理: {Path}, {Width}x{Height}", path, image.Width, image.Height);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("加载纹理失败: {Path}, 错误: {Error}", path, ex.Message);
                }
            }

            if (loadedTextures.Count == 0)
            {
                _logger.LogWarning("所有纹理加载失败");
                return CreateEmptyAtlas();
            }

            // 2. 矩形装箱 - MaxRects算法
            var packer = new MaxRectsPacker(maxAtlasSize, maxAtlasSize);
            var packedRects = packer.Pack(loadedTextures.Select(t => new Rectangle
            {
                Width = t.Width + padding * 2,
                Height = t.Height + padding * 2
            }).ToList());

            if (packedRects == null || packedRects.Count != loadedTextures.Count)
            {
                _logger.LogError("纹理装箱失败，尺寸超出限制");
                throw new InvalidOperationException("纹理装箱失败，请增大maxAtlasSize或减少纹理数量");
            }

            // 3. 计算实际需要的图集尺寸（2的幂次方）
            int requiredWidth = packedRects.Max(r => r.X + r.Width);
            int requiredHeight = packedRects.Max(r => r.Y + r.Height);
            int atlasWidth = NextPowerOfTwo(requiredWidth);
            int atlasHeight = NextPowerOfTwo(requiredHeight);

            _logger.LogInformation("图集尺寸: {Width}x{Height} (实际使用: {UsedWidth}x{UsedHeight})",
                atlasWidth, atlasHeight, requiredWidth, requiredHeight);

            // 4. 创建图集图像并复制纹理
            using var atlasImage = new Image<Rgba32>(atlasWidth, atlasHeight);
            atlasImage.Mutate(ctx => ctx.BackgroundColor(Color.Transparent));

            var textureRegions = new Dictionary<string, TextureRegion>();
            double totalArea = 0;
            double usedArea = 0;

            for (int i = 0; i < loadedTextures.Count; i++)
            {
                var texture = loadedTextures[i];
                var rect = packedRects[i];

                // 应用边缘像素扩展
                // 防止纹理采样时的 seaming artifacts
                var paddedTexture = BuildPaddedImage(texture.Image, padding);

                // 复制带padding的纹理到图集
                int destX = rect.X;
                int destY = rect.Y;

                atlasImage.Mutate(ctx => ctx.DrawImage(
                    paddedTexture,
                    new Point(destX, destY),
                    1f));

                paddedTexture.Dispose();

                // 计算归一化UV坐标（不包括padding）
                var uvMin = new Vector2D(
                    (double)(destX + padding) / atlasWidth,
                    (double)(destY + padding) / atlasHeight);

                var uvMax = new Vector2D(
                    (double)(destX + padding + texture.Width) / atlasWidth,
                    (double)(destY + padding + texture.Height) / atlasHeight);

                textureRegions[texture.Path] = new TextureRegion
                {
                    X = destX + padding,
                    Y = destY + padding,
                    Width = texture.Width,
                    Height = texture.Height,
                    UVMin = uvMin,
                    UVMax = uvMax
                };

                usedArea += texture.Width * texture.Height;
                totalArea = atlasWidth * atlasHeight;

                _logger.LogDebug("纹理映射: {Path} → [{UVMinU:F4},{UVMinV:F4}]-[{UVMaxU:F4},{UVMaxV:F4}]",
                    texture.Path, uvMin.U, uvMin.V, uvMax.U, uvMax.V);
            }

            // 5. 导出图集为图像字节数组（根据压缩策略选择格式）
            using var ms = new MemoryStream();
            if (useJpegCompression)
            {
                await atlasImage.SaveAsJpegAsync(ms, JpegEncoder);
            }
            else
            {
                await atlasImage.SaveAsPngAsync(ms);
            }
            var imageData = ms.ToArray();

            var utilization = usedArea / totalArea;
            var elapsed = DateTime.UtcNow - startTime;
            var formatName = useJpegCompression ? "JPEG (质量75)" : "PNG";

            _logger.LogInformation("纹理图集生成完成: {Width}x{Height}, {Count}张纹理, " +
                "利用率={Utilization:P2}, 大小={Size}KB, 格式={Format}, 耗时={Elapsed:F2}秒",
                atlasWidth, atlasHeight, loadedTextures.Count,
                utilization, imageData.Length / 1024.0, formatName, elapsed.TotalSeconds);

            // 清理加载的图像
            foreach (var texture in loadedTextures)
            {
                texture.Image.Dispose();
            }

            return new AtlasResult
            {
                ImageData = imageData,
                Width = atlasWidth,
                Height = atlasHeight,
                TextureRegions = textureRegions,
                Utilization = utilization
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成纹理图集失败");
            throw;
        }
    }

    /// <summary>
    /// 计算大于等于n的最小2的幂次方
    /// </summary>
    private int NextPowerOfTwo(int n)
    {
        if (n <= 0) return 1;
        n--;
        n |= n >> 1;
        n |= n >> 2;
        n |= n >> 4;
        n |= n >> 8;
        n |= n >> 16;
        return n + 1;
    }

    /// <summary>
    /// 构建带边缘像素扩展的纹理图像
    /// 通过复制边缘像素到padding区域，防止纹理采样时的seaming artifacts
    /// </summary>
    /// <param name="source">源纹理图像</param>
    /// <param name="padding">padding宽度（像素）</param>
    /// <returns>带padding的新图像</returns>
    private Image<Rgba32> BuildPaddedImage(Image<Rgba32> source, int padding)
    {
        if (padding <= 0)
            return source.Clone();

        int paddedWidth = source.Width + padding * 2;
        int paddedHeight = source.Height + padding * 2;
        var padded = new Image<Rgba32>(paddedWidth, paddedHeight);

        // 1. 复制中心内容（原始纹理）
        padded.Mutate(ctx => ctx.DrawImage(source, new Point(padding, padding), 1f));

        // 2. 扩展边缘像素到padding区域
        for (int p = 0; p < padding; p++)
        {
            // 上边缘：复制第一行像素
            for (int x = 0; x < source.Width; x++)
            {
                padded[padding + x, p] = source[x, 0];
            }

            // 下边缘：复制最后一行像素
            for (int x = 0; x < source.Width; x++)
            {
                padded[padding + x, paddedHeight - 1 - p] = source[x, source.Height - 1];
            }

            // 左边缘：复制第一列像素
            for (int y = 0; y < source.Height; y++)
            {
                padded[p, padding + y] = source[0, y];
            }

            // 右边缘：复制最后一列像素
            for (int y = 0; y < source.Height; y++)
            {
                padded[paddedWidth - 1 - p, padding + y] = source[source.Width - 1, y];
            }
        }

        // 3. 填充四个角落（使用角落像素填充）
        for (int py = 0; py < padding; py++)
        {
            for (int px = 0; px < padding; px++)
            {
                // 左上角
                padded[px, py] = source[0, 0];
                // 右上角
                padded[paddedWidth - 1 - px, py] = source[source.Width - 1, 0];
                // 左下角
                padded[px, paddedHeight - 1 - py] = source[0, source.Height - 1];
                // 右下角
                padded[paddedWidth - 1 - px, paddedHeight - 1 - py] = source[source.Width - 1, source.Height - 1];
            }
        }

        return padded;
    }

    /// <summary>
    /// 创建空图集
    /// </summary>
    private AtlasResult CreateEmptyAtlas()
    {
        using var emptyImage = new Image<Rgba32>(1, 1);
        using var ms = new MemoryStream();
        emptyImage.SaveAsJpeg(ms, JpegEncoder);

        return new AtlasResult
        {
            ImageData = ms.ToArray(),
            Width = 1,
            Height = 1,
            TextureRegions = new Dictionary<string, TextureRegion>(),
            Utilization = 0.0
        };
    }

    /// <summary>
    /// 加载的纹理
    /// </summary>
    private class LoadedTexture
    {
        public string Path { get; set; } = string.Empty;
        public Image<Rgba32> Image { get; set; } = null!;
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// 矩形 - 用于装箱算法
    /// </summary>
    private class Rectangle
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// MaxRects矩形装箱算法
    /// 基于 "A Thousand Ways to Pack the Bin" by Jukka Jylänki
    /// </summary>
    private class MaxRectsPacker
    {
        private readonly int _binWidth;
        private readonly int _binHeight;
        private readonly List<Rectangle> _freeRectangles = new();

        public MaxRectsPacker(int width, int height)
        {
            _binWidth = width;
            _binHeight = height;
            _freeRectangles.Add(new Rectangle { X = 0, Y = 0, Width = width, Height = height });
        }

        /// <summary>
        /// 装箱多个矩形
        /// </summary>
        public List<Rectangle>? Pack(List<Rectangle> rectangles)
        {
            // 按面积从大到小排序（贪心策略）
            var sorted = rectangles.OrderByDescending(r => r.Width * r.Height).ToList();
            var packed = new List<Rectangle>();

            foreach (var rect in sorted)
            {
                var position = FindPositionForNewNode(rect.Width, rect.Height);
                if (position == null)
                {
                    return null; // 装箱失败
                }

                packed.Add(new Rectangle
                {
                    X = position.Value.X,
                    Y = position.Value.Y,
                    Width = rect.Width,
                    Height = rect.Height
                });

                PlaceRectangle(position.Value.X, position.Value.Y, rect.Width, rect.Height);
            }

            return packed;
        }

        /// <summary>
        /// 为新节点寻找最佳位置 - 使用 Best Area Fit (BAF) 启发式
        /// Best Area Fit: 选择剩余面积最小的自由矩形
        /// </summary>
        private (int X, int Y)? FindPositionForNewNode(int width, int height)
        {
            int bestAreaFit = int.MaxValue;
            int bestShortSideFit = int.MaxValue;
            int bestX = 0;
            int bestY = 0;
            bool found = false;

            foreach (var free in _freeRectangles)
            {
                if (free.Width >= width && free.Height >= height)
                {
                    // 计算剩余面积（Best Area Fit的核心指标）
                    int areaFit = free.Width * free.Height - width * height;
                    int leftoverHoriz = free.Width - width;
                    int leftoverVert = free.Height - height;
                    int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);

                    // 优先选择剩余面积最小的，其次是短边剩余最小的
                    if (areaFit < bestAreaFit ||
                        (areaFit == bestAreaFit && shortSideFit < bestShortSideFit))
                    {
                        bestX = free.X;
                        bestY = free.Y;
                        bestAreaFit = areaFit;
                        bestShortSideFit = shortSideFit;
                        found = true;
                    }
                }
            }

            return found ? (bestX, bestY) : null;
        }

        /// <summary>
        /// 放置矩形并分割自由空间
        /// </summary>
        private void PlaceRectangle(int x, int y, int width, int height)
        {
            var usedRect = new Rectangle { X = x, Y = y, Width = width, Height = height };
            var newFreeRectangles = new List<Rectangle>();

            foreach (var free in _freeRectangles)
            {
                if (SplitFreeNode(free, usedRect, out var splits))
                {
                    newFreeRectangles.AddRange(splits);
                }
                else
                {
                    newFreeRectangles.Add(free);
                }
            }

            _freeRectangles.Clear();
            _freeRectangles.AddRange(newFreeRectangles);

            // 删除被包含的矩形
            PruneFreeList();
        }

        /// <summary>
        /// 分割自由节点
        /// </summary>
        private bool SplitFreeNode(Rectangle freeNode, Rectangle usedNode, out List<Rectangle> splits)
        {
            splits = new List<Rectangle>();

            // 检查是否相交
            if (!Intersects(freeNode, usedNode))
                return false;

            // 生成最多4个分割矩形
            if (usedNode.X < freeNode.X + freeNode.Width && usedNode.X + usedNode.Width > freeNode.X)
            {
                // 上方分割
                if (usedNode.Y > freeNode.Y && usedNode.Y < freeNode.Y + freeNode.Height)
                {
                    splits.Add(new Rectangle
                    {
                        X = freeNode.X,
                        Y = freeNode.Y,
                        Width = freeNode.Width,
                        Height = usedNode.Y - freeNode.Y
                    });
                }

                // 下方分割
                if (usedNode.Y + usedNode.Height < freeNode.Y + freeNode.Height)
                {
                    splits.Add(new Rectangle
                    {
                        X = freeNode.X,
                        Y = usedNode.Y + usedNode.Height,
                        Width = freeNode.Width,
                        Height = freeNode.Y + freeNode.Height - (usedNode.Y + usedNode.Height)
                    });
                }
            }

            if (usedNode.Y < freeNode.Y + freeNode.Height && usedNode.Y + usedNode.Height > freeNode.Y)
            {
                // 左侧分割
                if (usedNode.X > freeNode.X && usedNode.X < freeNode.X + freeNode.Width)
                {
                    splits.Add(new Rectangle
                    {
                        X = freeNode.X,
                        Y = freeNode.Y,
                        Width = usedNode.X - freeNode.X,
                        Height = freeNode.Height
                    });
                }

                // 右侧分割
                if (usedNode.X + usedNode.Width < freeNode.X + freeNode.Width)
                {
                    splits.Add(new Rectangle
                    {
                        X = usedNode.X + usedNode.Width,
                        Y = freeNode.Y,
                        Width = freeNode.X + freeNode.Width - (usedNode.X + usedNode.Width),
                        Height = freeNode.Height
                    });
                }
            }

            return splits.Count > 0;
        }

        /// <summary>
        /// 检查两个矩形是否相交
        /// </summary>
        private bool Intersects(Rectangle a, Rectangle b)
        {
            return a.X < b.X + b.Width &&
                   a.X + a.Width > b.X &&
                   a.Y < b.Y + b.Height &&
                   a.Y + a.Height > b.Y;
        }

        /// <summary>
        /// 删除被其他矩形完全包含的自由矩形
        /// </summary>
        private void PruneFreeList()
        {
            for (int i = 0; i < _freeRectangles.Count; i++)
            {
                for (int j = i + 1; j < _freeRectangles.Count; j++)
                {
                    if (IsContainedIn(_freeRectangles[i], _freeRectangles[j]))
                    {
                        _freeRectangles.RemoveAt(i);
                        i--;
                        break;
                    }
                    else if (IsContainedIn(_freeRectangles[j], _freeRectangles[i]))
                    {
                        _freeRectangles.RemoveAt(j);
                        j--;
                    }
                }
            }
        }

        /// <summary>
        /// 检查矩形a是否被矩形b包含
        /// </summary>
        private bool IsContainedIn(Rectangle a, Rectangle b)
        {
            return a.X >= b.X && a.Y >= b.Y &&
                   a.X + a.Width <= b.X + b.Width &&
                   a.Y + a.Height <= b.Y + b.Height;
        }
    }
}
