using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Collections.Concurrent;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Utilities;
using Microsoft.Extensions.Configuration;
using System.Threading;

namespace Infrastructure.Services.Cache;

public class RedisCacheService : ICacheService, IDisposable
{
    private IDatabase? _redisDb;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly IMetricsService _metrics;
    private readonly IConnectionMultiplexer? _connectionMultiplexer;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keySemaphores = new();
    private readonly JsonSerializerOptions _jsonOptions;

    private const int ConnectionRetryCount = 3;
    private const int OperationTimeoutSeconds = 5; // 2 saniyeden 5 saniyeye çıkarıldı
    private bool _isAvailable = false;
    private readonly string _instanceName;

    public RedisCacheService(
        IConnectionMultiplexer? connectionMultiplexer,
        ILogger<RedisCacheService> logger,
        IMetricsService metrics,
        IConfiguration configuration,
        IOptions<JsonSerializerOptions> jsonOptions = null)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _instanceName = configuration.GetValue<string>("Redis:InstanceName", "Tumdex_");

        _jsonOptions = jsonOptions?.Value ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        InitializeConnection();
    }

    private void InitializeConnection()
    {
        if (_connectionMultiplexer == null)
        {
            _logger.LogWarning("Redis ConnectionMultiplexer sağlanmadı. RedisCacheService devre dışı.");
            _isAvailable = false;
            return;
        }

        try
        {
            if (_connectionMultiplexer.IsConnected)
            {
                _redisDb = _connectionMultiplexer.GetDatabase();
                _isAvailable = _redisDb != null;
                _logger.LogInformation("Redis Cache Service başlatıldı ve Redis'e bağlandı.");
            }
            else
            {
                 _logger.LogWarning("Redis bağlantısı başlangıçta kurulamadı. RedisCacheService devre dışı kalacak. Yeniden bağlanma denenecek.");
                _isAvailable = false;
                 _connectionMultiplexer.ConnectionRestored += HandleConnectionRestored;
                 _connectionMultiplexer.ConnectionFailed += HandleConnectionFailed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis veritabanını alırken hata oluştu. RedisCacheService devre dışı.");
            _isAvailable = false;
        }
    }

    private void HandleConnectionRestored(object? sender, ConnectionFailedEventArgs e)
    {
        _logger.LogInformation("Redis bağlantısı yeniden kuruldu: {EndPoint}", e.EndPoint);
        try
        {
            _redisDb = _connectionMultiplexer?.GetDatabase();
            _isAvailable = _redisDb != null;
             if(_isAvailable) _logger.LogInformation("RedisCacheService tekrar aktif.");
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Redis veritabanını yeniden bağlanma sonrası alırken hata.");
             _isAvailable = false;
        }
    }

    private void HandleConnectionFailed(object? sender, ConnectionFailedEventArgs e)
    {
         _logger.LogError("Redis bağlantısı başarısız oldu. EndPoint: {EndPoint}, Type: {FailureType}, Exception: {Exception}",
            e.EndPoint, e.FailureType, e.Exception?.Message ?? "N/A");
         _isAvailable = false;
         _redisDb = null;
    }

    private bool CheckAvailability(string operation)
    {
        if (!_isAvailable || _redisDb == null)
        {
            return false;
        }
        return true;
    }

    private string GetPrefixedKey(string key)
    {
        return $"{_instanceName}{key}";
    }

    private string RemovePrefix(string prefixedKey)
    {
        if (prefixedKey.StartsWith(_instanceName))
        {
            return prefixedKey.Substring(_instanceName.Length);
        }
        return prefixedKey;
    }

    #region Original Methods (for backward compatibility)

    public async Task<(bool success, T value)> TryGetValueAsync<T>(string key)
    {
        return await TryGetValueAsync<T>(key, CancellationToken.None);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration)
    {
        await SetAsync<T>(key, value, expiration, CancellationToken.None);
    }

    public async Task<bool> RemoveAsync(string key)
    {
        return await RemoveAsync(key, CancellationToken.None);
    }

    public async Task<Dictionary<string, T>> GetManyAsync<T>(IEnumerable<string> keys)
    {
        return await GetManyAsync<T>(keys, CancellationToken.None);
    }

    public async Task SetManyAsync<T>(Dictionary<string, T> keyValues, TimeSpan? expiry = null)
    {
        await SetManyAsync<T>(keyValues, expiry, CancellationToken.None);
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
    {
        return await GetOrCreateAsync<T>(key, factory, expiry, CancellationToken.None);
    }

    public async Task<bool> IncrementAsync(string key, int value = 1, TimeSpan? expiry = null)
    {
        return await IncrementAsync(key, value, expiry, CancellationToken.None);
    }

    public async Task<int> GetCounterAsync(string key)
    {
        return await GetCounterAsync(key, CancellationToken.None);
    }

    public async Task<List<string>> GetKeysAsync(string pattern)
    {
        return await GetKeysAsync(pattern, CancellationToken.None);
    }

    #endregion

    #region CancellationToken Supported Methods

    public async Task<(bool success, T value)> TryGetValueAsync<T>(string key, CancellationToken cancellationToken)
    {
        // Kullanılabilirliği kontrol edin ve Redis durumunu günlüğe kaydedin
        if (!_isAvailable) {
            _logger.LogWarning("Redis kullanılamıyor, şu anahtar için bellek önbelleğine dönülüyor: {Key}", key);
        }
        ArgumentNullException.ThrowIfNull(key);
        if (!CheckAvailability(nameof(TryGetValueAsync))) return (false, default(T)!);

        string prefixedKey = GetPrefixedKey(key);
        try
        {
            var semaphore = GetKeyLock(prefixedKey);
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var redisValue = await ExecuteWithRetryAsync(() => _redisDb!.StringGetAsync(prefixedKey), cancellationToken);
                if (!redisValue.IsNull)
                {
                    _metrics?.IncrementCacheHit(prefixedKey);
                    var value = JsonSerializer.Deserialize<T>(redisValue!, _jsonOptions);
                    return (true, value!);
                }
                _metrics?.IncrementCacheMiss(prefixedKey);
                return (false, default(T)!);
            }
            finally { semaphore.Release(); }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("TryGetValueAsync işlemi iptal edildi. Key: {PrefixedKey}", prefixedKey);
            return (false, default(T)!);
        }
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "TryGetValueAsync hatası. Key: {PrefixedKey}", prefixedKey); 
            return (false, default(T)!); 
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        if (!CheckAvailability(nameof(SetAsync))) return;

        string prefixedKey = GetPrefixedKey(key);
        try
        {
            var semaphore = GetKeyLock(prefixedKey);
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
                await ExecuteWithRetryAsync(() => _redisDb!.StringSetAsync(prefixedKey, serializedValue, expiration), cancellationToken);
                _logger.LogDebug("Başarıyla set edildi. Key: {PrefixedKey}", prefixedKey);
            }
            finally { semaphore.Release(); }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("SetAsync işlemi iptal edildi. Key: {PrefixedKey}", prefixedKey);
        }
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "SetAsync hatası. Key: {PrefixedKey}", prefixedKey); 
            throw; 
        }
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!CheckAvailability(nameof(RemoveAsync))) return false;

        string prefixedKey = GetPrefixedKey(key);
        try
        {
            var semaphore = GetKeyLock(prefixedKey);
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ExecuteWithRetryAsync(() => _redisDb!.KeyDeleteAsync(prefixedKey), cancellationToken);
            }
            finally { semaphore.Release(); }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("RemoveAsync işlemi iptal edildi. Key: {PrefixedKey}", prefixedKey);
            return false;
        }
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "RemoveAsync hatası. Key: {PrefixedKey}", prefixedKey); 
            throw; 
        }
    }

    public async Task<Dictionary<string, T>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var response = new Dictionary<string, T>();
        if (!keys.Any() || !CheckAvailability(nameof(GetManyAsync))) return response;

        try
        {
            var originalKeys = keys.ToList();
            var prefixedKeys = originalKeys.Select(GetPrefixedKey).ToArray();
            var redisKeys = prefixedKeys.Select(k => (RedisKey)k).ToArray();

            var redisValues = await ExecuteWithRetryAsync(() => _redisDb!.StringGetAsync(redisKeys), cancellationToken);

            for (int i = 0; i < prefixedKeys.Length; i++)
            {
                var key = originalKeys[i];
                var prefixedKey = prefixedKeys[i];
                var value = redisValues[i];

                if (!value.IsNull)
                {
                    try
                    {
                        var deserializedValue = JsonSerializer.Deserialize<T>(value!, _jsonOptions);
                        response[key] = deserializedValue!;
                        _metrics?.IncrementCacheHit(prefixedKey);
                    }
                    catch (Exception ex) 
                    { 
                        _logger.LogWarning(ex, "Deserialize hatası. Key: {PrefixedKey}", prefixedKey); 
                        _metrics?.IncrementCacheMiss(prefixedKey); 
                    }
                }
                else { _metrics?.IncrementCacheMiss(prefixedKey); }
            }
            return response;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetManyAsync işlemi iptal edildi. Keys: {Keys}", string.Join(", ", keys));
            return response;
        }
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "GetManyAsync hatası. Keys: {Keys}", string.Join(", ", keys)); 
            return response; 
        }
    }

    public async Task SetManyAsync<T>(Dictionary<string, T> keyValues, TimeSpan? expiry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keyValues);
        if (!keyValues.Any() || !CheckAvailability(nameof(SetManyAsync))) return;

        try
        {
            var prefixedKeyValues = keyValues.ToDictionary(kv => GetPrefixedKey(kv.Key), kv => kv.Value);

            var batch = _redisDb!.CreateBatch();
            var tasks = new List<Task>();
            foreach (var kv in prefixedKeyValues)
            {
                var serializedValue = JsonSerializer.Serialize(kv.Value, _jsonOptions);
                tasks.Add(batch.StringSetAsync(kv.Key, serializedValue, expiry));
            }

            batch.Execute();
            await Task.WhenAll(tasks).WaitAsync(cancellationToken);

            _logger.LogDebug("Başarıyla set edildi (çoklu). Keys: {Keys}", string.Join(", ", keyValues.Keys));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("SetManyAsync işlemi iptal edildi. Keys: {Keys}", string.Join(", ", keyValues.Keys));
        }
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "SetManyAsync hatası. Keys: {Keys}", string.Join(", ", keyValues.Keys)); 
            throw; 
        }
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        var (success, value) = await TryGetValueAsync<T>(key, cancellationToken);
        if (success) return value;

        string prefixedKey = GetPrefixedKey(key);
        var semaphore = GetKeyLock(prefixedKey);
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            (success, value) = await TryGetValueAsync<T>(key, cancellationToken);
            if (success) return value;

            T newValue = await factory().WaitAsync(cancellationToken);

            if (CheckAvailability(nameof(GetOrCreateAsync))) {
                await SetAsync(key, newValue, expiry ?? TimeSpan.FromMinutes(5), cancellationToken);
            } else {
                _logger.LogWarning("GetOrCreateAsync - Redis müsait değil, değer cache'lenmedi. Key: {Key}", key);
            }
            return newValue;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<bool> IncrementAsync(string key, int value, TimeSpan? expiry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!CheckAvailability(nameof(IncrementAsync))) return false;

        string prefixedKey = GetPrefixedKey(key);
        try
        {
            var semaphore = GetKeyLock(prefixedKey);
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                long result = await ExecuteWithRetryAsync(() => _redisDb!.StringIncrementAsync(prefixedKey, value), cancellationToken);
                if (result == value && expiry.HasValue)
                {
                    await ExecuteWithRetryAsync(() => _redisDb!.KeyExpireAsync(prefixedKey, expiry.Value), cancellationToken);
                }
                return true;
            }
            finally { semaphore.Release(); }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("IncrementAsync işlemi iptal edildi. Key: {PrefixedKey}", prefixedKey);
            return false;
        }
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "IncrementAsync hatası. Key: {PrefixedKey}", prefixedKey); 
            return false; 
        }
    }

    public async Task<int> GetCounterAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!CheckAvailability(nameof(GetCounterAsync))) return 0;

        string prefixedKey = GetPrefixedKey(key);
        try
        {
            var redisValue = await ExecuteWithRetryAsync(() => _redisDb!.StringGetAsync(prefixedKey), cancellationToken);
            return redisValue.IsNull ? 0 : (int)redisValue;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetCounterAsync işlemi iptal edildi. Key: {PrefixedKey}", prefixedKey);
            return 0;
        }
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "GetCounterAsync hatası. Key: {PrefixedKey}", prefixedKey); 
            return 0; 
        }
    }

    public async Task<List<string>> GetKeysAsync(string pattern, CancellationToken cancellationToken)
    {
        if (!CheckAvailability(nameof(GetKeysAsync))) return new List<string>();

        try
        {
            var server = _connectionMultiplexer!.GetServer(_connectionMultiplexer.GetEndPoints().First());
            var prefixedPattern = $"{_instanceName}{pattern}";
            var keys = new List<string>();

            await foreach (var key in server.KeysAsync(pattern: prefixedPattern).WithCancellation(cancellationToken))
            {
                keys.Add(RemovePrefix(key.ToString()));
            }

            _logger.LogDebug("Pattern {Pattern} (Prefixed: {PrefixedPattern}) için {Count} anahtar bulundu", pattern, prefixedPattern, keys.Count);
            return keys;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetKeysAsync işlemi iptal edildi. Pattern: {Pattern}", pattern);
            return new List<string>();
        }
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "GetKeysAsync ({Pattern}) hatası", pattern); 
            return new List<string>(); 
        }
    }

    #endregion

    private SemaphoreSlim GetKeyLock(string key)
    {
        return _keySemaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        if (!CheckAvailability(nameof(ExecuteWithRetryAsync)))
            throw new InvalidOperationException("Redis is not available for operation.");

        var lastException = default(Exception);
        for (int i = 0; i < ConnectionRetryCount; i++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
            
                return await operation().WaitAsync(linkedCts.Token);
            }
            catch (Exception ex) when (ex is RedisConnectionException || ex is RedisServerException || 
                                       ex is RedisTimeoutException || ex is TimeoutException || 
                                       ex is OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException("Operation was canceled", ex, cancellationToken);
                
                lastException = ex;
                _logger.LogWarning(ex, "Redis operasyonu deneme {Attempt}/{MaxAttempts} başarısız.", i + 1, ConnectionRetryCount);
                if (i < ConnectionRetryCount - 1)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * (i + 1)), CancellationToken.None); // Bu satır değişti
                    if(!_connectionMultiplexer?.IsConnected ?? true) {
                        _isAvailable = false;
                        throw lastException ?? ex;
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Redis operasyonu sırasında beklenmeyen hata.");
                throw;
            }
        }
        throw lastException ?? new RedisException($"Redis operation failed after {ConnectionRetryCount} attempts.");
    }

    public void Dispose()
    {
        if (_connectionMultiplexer != null)
        {
            _connectionMultiplexer.ConnectionRestored -= HandleConnectionRestored;
            _connectionMultiplexer.ConnectionFailed -= HandleConnectionFailed;
        }
        foreach (var semaphore in _keySemaphores.Values) { semaphore.Dispose(); }
        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}