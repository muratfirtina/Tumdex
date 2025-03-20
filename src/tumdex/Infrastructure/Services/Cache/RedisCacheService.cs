using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Collections.Concurrent;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Utilities;

namespace Infrastructure.Services.Cache;

public class RedisCacheService : ICacheService, IDisposable
{
    private readonly IDatabase _redisDb;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly IMetricsService _metrics;
    private readonly IConnectionMultiplexer _connectionMultiplexer; // IConnectionMultiplexer olarak değiştirildi
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keySemaphores = new();
    private readonly JsonSerializerOptions _jsonOptions;

    private const int ConnectionRetryCount = 3;
    private const int OperationTimeoutSeconds = 2;

    public RedisCacheService(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisCacheService> logger,
        IMetricsService metrics,
        IOptions<JsonSerializerOptions> jsonOptions = null)
    {
        _connectionMultiplexer = connectionMultiplexer ??
                                 throw new ArgumentNullException(nameof(connectionMultiplexer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _redisDb = _connectionMultiplexer.GetDatabase();

        _jsonOptions = jsonOptions?.Value ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        _logger.LogInformation("Redis Cache Service initialized");
    }

    public async Task<(bool success, T value)> TryGetValueAsync<T>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        try
        {
            var semaphore = GetKeyLock(key);
            await semaphore.WaitAsync();

            try
            {
                var redisValue = await ExecuteWithRetryAsync(() =>
                    _redisDb.StringGetAsync(key));

                if (!redisValue.IsNull)
                {
                    _metrics?.IncrementCacheHit(key);
                    var value = JsonSerializer.Deserialize<T>(redisValue!, _jsonOptions);
                    return (true, value);
                }

                _metrics?.IncrementCacheMiss(key);
                return (false, default);
            }
            finally
            {
                semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TryGetValueAsync for key: {Key}", key);
            return (false, default);
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            var semaphore = GetKeyLock(key);
            await semaphore.WaitAsync();

            try
            {
                var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
                await ExecuteWithRetryAsync(() =>
                    _redisDb.StringSetAsync(key, serializedValue, expiration));

                _logger.LogDebug("Successfully set value for key: {Key}", key);
            }
            finally
            {
                semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SetAsync for key: {Key}", key);
            throw;
        }
    }

    public async Task<bool> RemoveAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        try
        {
            var semaphore = GetKeyLock(key);
            await semaphore.WaitAsync();

            try
            {
                return await ExecuteWithRetryAsync(() =>
                    _redisDb.KeyDeleteAsync(key));
            }
            finally
            {
                semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RemoveAsync for key: {Key}", key);
            throw;
        }
    }

    public async Task<Dictionary<string, T>> GetManyAsync<T>(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        try
        {
            var keyArray = keys.ToArray();
            var batch = _redisDb.CreateBatch();
            var tasks = keyArray.Select(key => batch.StringGetAsync(key)).ToList();

            batch.Execute();
            var results = await Task.WhenAll(tasks);
            var response = new Dictionary<string, T>();

            for (var i = 0; i < keyArray.Length; i++)
            {
                var key = keyArray[i];
                var value = results[i];

                if (!value.IsNull)
                {
                    try
                    {
                        var deserializedValue = JsonSerializer.Deserialize<T>(value!, _jsonOptions);
                        response[key] = deserializedValue;
                        _metrics.IncrementCacheHit(key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize value for key: {Key}", key);
                        _metrics.IncrementCacheMiss(key);
                    }
                }
                else
                {
                    _metrics.IncrementCacheMiss(key);
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetManyAsync for keys: {Keys}",
                string.Join(", ", keys));
            throw;
        }
    }

    public async Task SetManyAsync<T>(Dictionary<string, T> keyValues, TimeSpan? expiry = null)
    {
        ArgumentNullException.ThrowIfNull(keyValues);

        try
        {
            var batch = _redisDb.CreateBatch();
            var tasks = keyValues.Select(kv =>
            {
                var serializedValue = JsonSerializer.Serialize(kv.Value, _jsonOptions);
                return batch.StringSetAsync(kv.Key, serializedValue, expiry);
            }).ToList();

            batch.Execute();
            await Task.WhenAll(tasks);

            _logger.LogDebug("Successfully set multiple values. Keys: {Keys}",
                string.Join(", ", keyValues.Keys));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SetManyAsync for keys: {Keys}",
                string.Join(", ", keyValues.Keys));
            throw;
        }
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        try
        {
            var result = await TryGetValueAsync<T>(key);
            if (result.success)
            {
                return result.value;
            }

            var semaphore = GetKeyLock(key);
            await semaphore.WaitAsync();

            try
            {
                // Double check after acquiring lock
                result = await TryGetValueAsync<T>(key);
                if (result.success)
                {
                    return result.value;
                }

                var newValue = await factory();
                await SetAsync(key, newValue, expiry ?? TimeSpan.FromHours(1));
                return newValue;
            }
            finally
            {
                semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrCreateAsync for key: {Key}", key);
            return await factory();
        }
    }

    public async Task<bool> IncrementAsync(
        string key,
        int value = 1,
        TimeSpan? expiry = null)
    {
        ArgumentNullException.ThrowIfNull(key);

        try
        {
            var semaphore = GetKeyLock(key);
            await semaphore.WaitAsync();

            try
            {
                var result = await ExecuteWithRetryAsync(() =>
                    _redisDb.StringIncrementAsync(key, value));

                if (expiry.HasValue)
                {
                    await ExecuteWithRetryAsync(() =>
                        _redisDb.KeyExpireAsync(key, expiry.Value));
                }

                return true;
            }
            finally
            {
                semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing key: {Key}", key);
            return false;
        }
    }

    public async Task<int> GetCounterAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        try
        {
            var value = await ExecuteWithRetryAsync(() =>
                _redisDb.StringGetAsync(key));

            return value.IsNull ? 0 : (int)value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting counter for key: {Key}", key);
            return 0;
        }
    }

    private SemaphoreSlim GetKeyLock(string key)
    {
        return _keySemaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
    {
        for (int i = 0; i < ConnectionRetryCount; i++)
        {
            try
            {
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(OperationTimeoutSeconds));
                var operationTask = operation();

                var completedTask = await Task.WhenAny(operationTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException($"Operation timed out after {OperationTimeoutSeconds} seconds");
                }

                return await operationTask;
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogWarning(ex, "Redis connection attempt {Attempt} of {MaxAttempts} failed",
                    i + 1, ConnectionRetryCount);

                if (i == ConnectionRetryCount - 1) throw;

                await Task.Delay((i + 1) * 200); // Exponential backoff
            }
        }

        throw new InvalidOperationException("Should not reach here");
    }

    public void Dispose()
    {
        foreach (var semaphore in _keySemaphores.Values)
        {
            semaphore.Dispose();
        }

        _connectionLock.Dispose();
    }
}