using Minio;
using Minio.DataModel.Args;
using Microsoft.Extensions.Logging;

namespace RealScene3D.Infrastructure.MinIO;

/// <summary>
/// MinIO 对象存储服务接口
/// </summary>
public interface IMinioStorageService
{
    Task<string> UploadFileAsync(string bucketName, string objectName, Stream data, string contentType);
    Task<Stream> DownloadFileAsync(string bucketName, string objectName);
    Task<bool> DeleteFileAsync(string bucketName, string objectName);
    Task<bool> FileExistsAsync(string bucketName, string objectName);
    Task<string> GetPresignedUrlAsync(string bucketName, string objectName, int expiryInSeconds = 3600);
    Task<List<string>> ListFilesAsync(string bucketName, string prefix = "");
    Task EnsureBucketExistsAsync(string bucketName);
    Task<bool> HealthCheckAsync();
}

/// <summary>
/// MinIO 对象存储服务实现
/// 支持自动重试、健康检查等高级功能
/// </summary>
public class MinioStorageService : IMinioStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioStorageService> _logger;
    private const int MaxRetries = 3;
    private const int BaseDelayMs = 1000;

    public MinioStorageService(IMinioClient minioClient, ILogger<MinioStorageService> logger)
    {
        _minioClient = minioClient;
        _logger = logger;
    }

    public async Task EnsureBucketExistsAsync(string bucketName)
    {
        var bucketExistsArgs = new BucketExistsArgs()
            .WithBucket(bucketName);

        bool found = await _minioClient.BucketExistsAsync(bucketExistsArgs);

        if (!found)
        {
            var makeBucketArgs = new MakeBucketArgs()
                .WithBucket(bucketName);

            await _minioClient.MakeBucketAsync(makeBucketArgs);
        }
    }

    public async Task<string> UploadFileAsync(string bucketName, string objectName, Stream data, string contentType)
    {
        await EnsureBucketExistsAsync(bucketName);

        // 实现重试机制，使用指数退避
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                // 重置流位置
                if (data.CanSeek)
                {
                    data.Position = 0;
                }

                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithStreamData(data)
                    .WithObjectSize(data.Length)
                    .WithContentType(contentType);

                await _minioClient.PutObjectAsync(putObjectArgs);

                _logger.LogInformation("文件上传成功: {Bucket}/{ObjectName}, 尝试次数: {Attempt}",
                    bucketName, objectName, attempt + 1);

                return $"{bucketName}/{objectName}";
            }
            catch (Exception ex) when (attempt < MaxRetries - 1)
            {
                var delayMs = BaseDelayMs * (int)Math.Pow(2, attempt); // 指数退避: 1s, 2s, 4s
                _logger.LogWarning(ex,
                    "文件上传失败，重试 {Attempt}/{MaxRetries}，{Delay}ms 后重试: {Bucket}/{ObjectName}",
                    attempt + 1, MaxRetries, delayMs, bucketName, objectName);

                await Task.Delay(delayMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "文件上传最终失败: {Bucket}/{ObjectName}", bucketName, objectName);
                throw new InvalidOperationException($"上传文件到 MinIO 失败: {ex.Message}", ex);
            }
        }

        throw new InvalidOperationException($"上传文件到 MinIO 失败，已达到最大重试次数 {MaxRetries}");
    }

    public async Task<Stream> DownloadFileAsync(string bucketName, string objectName)
    {
        var memoryStream = new MemoryStream();

        var getObjectArgs = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream((stream) =>
            {
                stream.CopyTo(memoryStream);
            });

        await _minioClient.GetObjectAsync(getObjectArgs);

        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task<bool> DeleteFileAsync(string bucketName, string objectName)
    {
        try
        {
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(removeObjectArgs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> FileExistsAsync(string bucketName, string objectName)
    {
        try
        {
            var statObjectArgs = new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName);

            await _minioClient.StatObjectAsync(statObjectArgs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetPresignedUrlAsync(string bucketName, string objectName, int expiryInSeconds = 3600)
    {
        var presignedGetObjectArgs = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithExpiry(expiryInSeconds);

        return await _minioClient.PresignedGetObjectAsync(presignedGetObjectArgs);
    }

    public async Task<List<string>> ListFilesAsync(string bucketName, string prefix = "")
    {
        var files = new List<string>();

        var listObjectsArgs = new ListObjectsArgs()
            .WithBucket(bucketName)
            .WithPrefix(prefix)
            .WithRecursive(true);

        await foreach (var item in _minioClient.ListObjectsEnumAsync(listObjectsArgs))
        {
            files.Add(item.Key);
        }

        return files;
    }

    /// <summary>
    /// MinIO 健康检查
    /// 尝试列出存储桶以验证连接是否正常
    /// </summary>
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            // 尝试列出所有存储桶来验证连接
            await _minioClient.ListBucketsAsync();
            _logger.LogDebug("MinIO 健康检查通过");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MinIO 健康检查失败");
            return false;
        }
    }
}
