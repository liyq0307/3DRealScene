namespace RealScene3D.Infrastructure.Utilities;

/// <summary>
/// 文件验证工具类
/// 提供文件类型、大小、格式等验证功能
/// </summary>
public static class FileValidator
{
    /// <summary>
    /// 允许的图片 MIME 类型
    /// </summary>
    private static readonly HashSet<string> AllowedImageMimeTypes = new()
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
        "image/gif",
        "image/bmp"
    };

    /// <summary>
    /// 图片文件扩展名
    /// </summary>
    private static readonly HashSet<string> ImageExtensions = new()
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"
    };

    /// <summary>
    /// 验证文件是否为有效的图片
    /// </summary>
    /// <param name="stream">文件流</param>
    /// <param name="fileName">文件名</param>
    /// <param name="contentType">内容类型</param>
    /// <returns>验证结果</returns>
    public static FileValidationResult ValidateImage(Stream stream, string fileName, string contentType)
    {
        // 1. 验证 MIME 类型
        if (!AllowedImageMimeTypes.Contains(contentType.ToLower()))
        {
            return FileValidationResult.Failure($"不支持的文件类型: {contentType}。仅支持 JPEG、PNG、WebP、GIF、BMP 格式");
        }

        // 2. 验证文件扩展名
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!ImageExtensions.Contains(extension))
        {
            return FileValidationResult.Failure($"不支持的文件扩展名: {extension}");
        }

        // 3. 验证文件签名（Magic Number）
        if (!IsValidImageSignature(stream))
        {
            return FileValidationResult.Failure("文件签名验证失败，文件可能已损坏或不是有效的图片文件");
        }

        return FileValidationResult.Success();
    }

    /// <summary>
    /// 验证文件大小
    /// </summary>
    /// <param name="fileSize">文件大小（字节）</param>
    /// <param name="maxSize">最大允许大小（字节）</param>
    /// <returns>验证结果</returns>
    public static FileValidationResult ValidateFileSize(long fileSize, long maxSize)
    {
        if (fileSize <= 0)
        {
            return FileValidationResult.Failure("文件不能为空");
        }

        if (fileSize > maxSize)
        {
            var maxSizeMB = maxSize / (1024.0 * 1024.0);
            return FileValidationResult.Failure($"文件大小超过限制。最大允许 {maxSizeMB:F1} MB");
        }

        return FileValidationResult.Success();
    }

    /// <summary>
    /// 验证图片文件签名（Magic Number）
    /// </summary>
    private static bool IsValidImageSignature(Stream stream)
    {
        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            var header = new byte[16]; // 读取前16字节足够识别大多数图片格式
            var bytesRead = stream.Read(header, 0, header.Length);

            if (bytesRead < 4)
            {
                return false;
            }

            // JPG/JPEG: FF D8 FF
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return true;

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                return true;

            // GIF: 47 49 46 38 (GIF8)
            if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
                return true;

            // WebP: 52 49 46 46 ... 57 45 42 50 (RIFF...WEBP)
            if (bytesRead >= 12 &&
                header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                return true;

            // BMP: 42 4D (BM)
            if (header[0] == 0x42 && header[1] == 0x4D)
                return true;

            return false;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    /// <summary>
    /// 获取友好的文件大小字符串
    /// </summary>
    public static string GetFileSizeString(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// 文件验证结果
/// </summary>
public class FileValidationResult
{
    public bool IsValid { get; private set; }
    public string? ErrorMessage { get; private set; }

    private FileValidationResult(bool isValid, string? errorMessage = null)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static FileValidationResult Success() => new(true);
    public static FileValidationResult Failure(string errorMessage) => new(false, errorMessage);
}
