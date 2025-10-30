using System.Text.Json;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.MinIO;
using Microsoft.Extensions.Logging;

namespace RealScene3D.Application.Services;

/// <summary>
/// 切片工具类 - 提供通用的切片相关功能，避免代码重复
/// </summary>
public static class SlicingUtilities
{
    /// <summary>
    /// 解析切片配置JSON字符串
    /// </summary>
    /// <param name="configJson">配置JSON字符串</param>
    /// <returns>切片配置对象</returns>
    public static SlicingConfig ParseSlicingConfig(string configJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize<SlicingConfig>(configJson);
            return config ?? new SlicingConfig();
        }
        catch
        {
            return new SlicingConfig();
        }
    }

    /// <summary>
    /// 根据存储位置删除切片文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="outputPath">任务输出路径</param>
    /// <param name="storageLocation">存储位置类型</param>
    /// <param name="minioService">MinIO存储服务</param>
    /// <param name="logger">日志记录器</param>
    public static async Task DeleteSliceFileAsync(
        string? filePath,
        string? outputPath,
        StorageLocationType storageLocation,
        IMinioStorageService? minioService,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            logger.LogDebug("文件路径为空，跳过文件删除");
            return;
        }

        try
        {
            if (storageLocation == StorageLocationType.LocalFileSystem)
            {
                logger.LogDebug("尝试删除本地切片文件：{FilePath}", filePath);

                // 本地文件系统：拼接完整路径
                var fullPath = Path.IsPathRooted(filePath)
                    ? filePath
                    : Path.Combine(outputPath ?? "", filePath);

                if (File.Exists(fullPath))
                {
                    logger.LogDebug("本地切片文件存在：{FilePath}", fullPath);
                    try
                    {
                        File.Delete(fullPath);
                        logger.LogInformation("✓ 成功删除本地切片文件: {FilePath}", fullPath);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "删除本地切片文件失败：{FilePath}", fullPath);
                    }
                }
                else
                {
                    logger.LogWarning("本地切片文件不存在，无法删除：{FilePath}", fullPath);
                }
            }
            else // MinIO
            {
                // MinIO：直接使用相对路径
                if (minioService == null)
                {
                    logger.LogWarning("MinIO存储服务未注入，无法删除切片文件: {FilePath}", filePath);
                    return;
                }

                var deleted = await minioService.DeleteFileAsync("slices", filePath);
                if (deleted)
                {
                    logger.LogInformation("✓ 成功删除MinIO切片文件: {FilePath}", filePath);
                }
                else
                {
                    logger.LogWarning("✗ MinIO切片文件删除失败或文件不存在: {FilePath}", filePath);
                }
            }
        }
        catch (Exception ex)
        {
            // 不抛出异常，只记录日志，避免影响主流程
            logger.LogError(ex, "删除切片文件时发生错误: {FilePath}", filePath);
        }
    }


    /// <summary>
    /// 删除切片索引和Tileset
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="storageLocation">存储位置类型</param>
    /// <param name="minioService">MinIO存储服务</param>
    /// <param name="logger">日志记录</param>
    /// <returns></returns>
    public static async Task DeleteSliceIndexAndTilesetAsync(
        string? filePath,
        StorageLocationType storageLocation,
        IMinioStorageService? minioService,
        ILogger logger)
    {
        if (!string.IsNullOrEmpty(filePath))
        {
            if (storageLocation == StorageLocationType.LocalFileSystem)
            {
                var indexPath = Path.Combine(filePath, "index.json");
                if (File.Exists(indexPath))
                {
                    File.Delete(indexPath);
                    logger.LogDebug("本地切片索引文件已删除：{FilePath}", indexPath);
                }

                var tilesetPath = Path.Combine(filePath, "tileset.json");
                if (File.Exists(tilesetPath))
                {
                    File.Delete(tilesetPath);
                    logger.LogDebug("本地tileset.json文件已删除：{FilePath}", tilesetPath);
                }

                // 删除增量更新索引文件
                var incrementalIndexPath = Path.Combine(filePath, "incremental_index.json");
                if (File.Exists(incrementalIndexPath))
                {
                    File.Delete(incrementalIndexPath);
                    logger.LogDebug("本地增量更新索引文件已删除：{FilePath}", incrementalIndexPath);
                }

                // 尝试删除输出目录，如果为空
                if (Directory.Exists(filePath))
                {
                    // 递归删除所有空子目录
                    foreach (var dir in Directory.EnumerateDirectories(
                        filePath, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                    {
                        try
                        {
                            if (!Directory.EnumerateFileSystemEntries(dir).Any())
                            {
                                Directory.Delete(dir);
                                logger.LogDebug("本地空目录已删除：{DirectoryPath}", dir);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "删除本地空目录失败：{DirectoryPath}", dir);
                        }
                    }

                    // 最后尝试删除根输出目录，如果为空
                    if (!Directory.EnumerateFileSystemEntries(filePath).Any())
                    {
                        Directory.Delete(filePath);
                        logger.LogDebug("本地切片输出根目录已删除：{OutputPath}", filePath);
                    }
                }
            }
            else
            {
                if (minioService == null)
                {
                    logger.LogWarning("MinIO存储服务未注入，无法删除切片文件: {FilePath}", filePath);
                    return;
                }

                await minioService.DeleteFileAsync("slices", $"{filePath}/tileset.json");
                await minioService.DeleteFileAsync("slices", $"{filePath}/index.json");

                // 删除增量更新索引文件
                await minioService.DeleteFileAsync("slices", $"{filePath}/incremental_index.json");
            }
        }
    }
}