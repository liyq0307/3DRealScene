using Microsoft.Extensions.Logging;
using RealScene3D.Infrastructure.MinIO;

namespace RealScene3D.Infrastructure.Services;

/// <summary>
/// 文件清理服务接口
/// </summary>
public interface IFileCleanupService
{
    /// <summary>
    /// 从 URL 中删除文件
    /// </summary>
    Task<bool> DeleteFileFromUrlAsync(string fileUrl);

    /// <summary>
    /// 解析文件 URL 获取 bucket 和 objectName
    /// </summary>
    (string? bucket, string? objectName) ParseFileUrl(string fileUrl);
}

/// <summary>
/// 文件清理服务实现
/// 用于清理旧的上传文件
/// </summary>
public class FileCleanupService : IFileCleanupService
{
    private readonly IMinioStorageService _storageService;
    private readonly ILogger<FileCleanupService> _logger;

    public FileCleanupService(
        IMinioStorageService storageService,
        ILogger<FileCleanupService> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// 从 URL 中删除文件
    /// 例如: http://localhost:5195/api/files/proxy/thumbnails/avatar_xxx.jpg
    /// </summary>
    public async Task<bool> DeleteFileFromUrlAsync(string fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl))
        {
            return false;
        }

        try
        {
            var (bucket, objectName) = ParseFileUrl(fileUrl);

            if (string.IsNullOrEmpty(bucket) || string.IsNullOrEmpty(objectName))
            {
                _logger.LogWarning("无法解析文件 URL: {FileUrl}", fileUrl);
                return false;
            }

            // 检查文件是否存在
            var exists = await _storageService.FileExistsAsync(bucket, objectName);
            if (!exists)
            {
                _logger.LogInformation("文件不存在，无需删除: {Bucket}/{ObjectName}", bucket, objectName);
                return true; // 文件不存在也视为成功
            }

            // 删除文件
            var deleted = await _storageService.DeleteFileAsync(bucket, objectName);

            if (deleted)
            {
                _logger.LogInformation("文件删除成功: {Bucket}/{ObjectName}", bucket, objectName);
            }
            else
            {
                _logger.LogWarning("文件删除失败: {Bucket}/{ObjectName}", bucket, objectName);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除文件失败: {FileUrl}", fileUrl);
            return false;
        }
    }

    /// <summary>
    /// 解析文件 URL
    /// </summary>
    /// <param name="fileUrl">文件 URL</param>
    /// <returns>(bucket, objectName)</returns>
    public (string? bucket, string? objectName) ParseFileUrl(string fileUrl)
    {
        try
        {
            // 解析 URL 格式: http://localhost:5195/api/files/proxy/{bucket}/{objectName}
            // 或: http://localhost:5195/api/files/proxy/{bucket}/path/to/file.jpg

            if (string.IsNullOrEmpty(fileUrl))
            {
                return (null, null);
            }

            var uri = new Uri(fileUrl);
            var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // 查找 "proxy" 关键字的位置
            var proxyIndex = Array.FindIndex(pathSegments, s => s.Equals("proxy", StringComparison.OrdinalIgnoreCase));

            if (proxyIndex < 0 || proxyIndex + 2 >= pathSegments.Length)
            {
                _logger.LogWarning("URL 格式不正确: {FileUrl}", fileUrl);
                return (null, null);
            }

            // proxy 后面的第一个段是 bucket
            var bucket = pathSegments[proxyIndex + 1];

            // 后面的所有段组成 objectName
            var objectName = string.Join("/", pathSegments.Skip(proxyIndex + 2));

            return (bucket, objectName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析文件 URL 失败: {FileUrl}", fileUrl);
            return (null, null);
        }
    }
}
