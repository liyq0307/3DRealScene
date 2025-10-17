using Minio;
using Minio.DataModel.Args;

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
}

/// <summary>
/// MinIO 对象存储服务实现
/// </summary>
public class MinioStorageService : IMinioStorageService
{
    private readonly IMinioClient _minioClient;

    public MinioStorageService(IMinioClient minioClient)
    {
        _minioClient = minioClient;
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

        var putObjectArgs = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(data)
            .WithObjectSize(data.Length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(putObjectArgs);

        return $"{bucketName}/{objectName}";
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
}
