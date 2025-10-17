using StackExchange.Redis;
using System.Text.Json;

namespace RealScene3D.Infrastructure.Redis;

/// <summary>
/// Redis 缓存服务接口
/// </summary>
public interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task<bool> DeleteAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task<long> IncrementAsync(string key);
    Task<long> DecrementAsync(string key);
    Task<bool> SetHashAsync(string key, string field, string value);
    Task<string?> GetHashAsync(string key, string field);
    Task<Dictionary<string, string>> GetAllHashAsync(string key);
}

/// <summary>
/// Redis 缓存服务实现
/// </summary>
public class RedisCacheService : IRedisCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;

    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _database = redis.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _database.StringGetAsync(key);
        if (!value.HasValue)
            return default;

        return JsonSerializer.Deserialize<T>(value.ToString());
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var json = JsonSerializer.Serialize(value);
        await _database.StringSetAsync(key, json, expiry);
    }

    public async Task<bool> DeleteAsync(string key)
    {
        return await _database.KeyDeleteAsync(key);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return await _database.KeyExistsAsync(key);
    }

    public async Task<long> IncrementAsync(string key)
    {
        return await _database.StringIncrementAsync(key);
    }

    public async Task<long> DecrementAsync(string key)
    {
        return await _database.StringDecrementAsync(key);
    }

    public async Task<bool> SetHashAsync(string key, string field, string value)
    {
        return await _database.HashSetAsync(key, field, value);
    }

    public async Task<string?> GetHashAsync(string key, string field)
    {
        var value = await _database.HashGetAsync(key, field);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task<Dictionary<string, string>> GetAllHashAsync(string key)
    {
        var entries = await _database.HashGetAllAsync(key);
        return entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString()
        );
    }
}
