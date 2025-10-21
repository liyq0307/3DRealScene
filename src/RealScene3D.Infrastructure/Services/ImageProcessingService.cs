using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using Microsoft.Extensions.Logging;

namespace RealScene3D.Infrastructure.Services;

/// <summary>
/// 图片处理服务接口
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// 生成缩略图
    /// </summary>
    Task<Stream> GenerateThumbnailAsync(Stream originalImage, int maxSize = 200, int quality = 85);

    /// <summary>
    /// 调整图片大小
    /// </summary>
    Task<Stream> ResizeImageAsync(Stream originalImage, int width, int height, bool maintainAspectRatio = true);

    /// <summary>
    /// 压缩图片
    /// </summary>
    Task<Stream> CompressImageAsync(Stream originalImage, int quality = 85);
}

/// <summary>
/// 图片处理服务实现
/// 使用 ImageSharp 库进行图片处理
/// </summary>
public class ImageProcessingService : IImageProcessingService
{
    private readonly ILogger<ImageProcessingService> _logger;

    public ImageProcessingService(ILogger<ImageProcessingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 生成缩略图
    /// </summary>
    /// <param name="originalImage">原始图片流</param>
    /// <param name="maxSize">最大尺寸（宽或高）</param>
    /// <param name="quality">JPEG 质量 (1-100)</param>
    /// <returns>缩略图流</returns>
    public async Task<Stream> GenerateThumbnailAsync(Stream originalImage, int maxSize = 200, int quality = 85)
    {
        try
        {
            originalImage.Position = 0;

            using var image = await Image.LoadAsync(originalImage);

            _logger.LogDebug("生成缩略图: 原始尺寸 {Width}x{Height}, 目标最大尺寸 {MaxSize}",
                image.Width, image.Height, maxSize);

            // 按比例缩放，保持宽高比
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(maxSize, maxSize),
                Mode = ResizeMode.Max, // 保持宽高比，不超过最大尺寸
                Sampler = KnownResamplers.Lanczos3 // 高质量重采样
            }));

            var thumbnail = new MemoryStream();

            // 保存为 JPEG 格式
            var encoder = new JpegEncoder
            {
                Quality = quality
            };

            await image.SaveAsync(thumbnail, encoder);
            thumbnail.Position = 0;

            _logger.LogInformation("缩略图生成成功: 新尺寸 {Width}x{Height}, 大小 {Size} bytes",
                image.Width, image.Height, thumbnail.Length);

            return thumbnail;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成缩略图失败");
            throw new InvalidOperationException("生成缩略图失败", ex);
        }
    }

    /// <summary>
    /// 调整图片大小
    /// </summary>
    public async Task<Stream> ResizeImageAsync(Stream originalImage, int width, int height, bool maintainAspectRatio = true)
    {
        try
        {
            originalImage.Position = 0;

            using var image = await Image.LoadAsync(originalImage);

            var resizeMode = maintainAspectRatio ? ResizeMode.Max : ResizeMode.Stretch;

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = resizeMode,
                Sampler = KnownResamplers.Lanczos3
            }));

            var resized = new MemoryStream();
            await image.SaveAsJpegAsync(resized);
            resized.Position = 0;

            return resized;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调整图片大小失败");
            throw new InvalidOperationException("调整图片大小失败", ex);
        }
    }

    /// <summary>
    /// 压缩图片
    /// </summary>
    public async Task<Stream> CompressImageAsync(Stream originalImage, int quality = 85)
    {
        try
        {
            originalImage.Position = 0;

            using var image = await Image.LoadAsync(originalImage);

            var compressed = new MemoryStream();
            var encoder = new JpegEncoder
            {
                Quality = quality
            };

            await image.SaveAsync(compressed, encoder);
            compressed.Position = 0;

            _logger.LogInformation("图片压缩成功: 质量 {Quality}, 大小 {Size} bytes",
                quality, compressed.Length);

            return compressed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "压缩图片失败");
            throw new InvalidOperationException("压缩图片失败", ex);
        }
    }
}
