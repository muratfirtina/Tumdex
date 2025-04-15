using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Collections.Concurrent;
using Application.Abstraction.Services; // IMetricsService için
using Application.Abstraction.Services.Utilities;
using Microsoft.Extensions.Configuration; // IConfiguration için eklendi

namespace Infrastructure.Services.Cache;

public class RedisCacheService : ICacheService, IDisposable
{
    private IDatabase? _redisDb; // Nullable yapıldı
    private readonly ILogger<RedisCacheService> _logger;
    private readonly IMetricsService _metrics;
    private readonly IConnectionMultiplexer? _connectionMultiplexer; // Nullable yapıldı
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keySemaphores = new();
    private readonly JsonSerializerOptions _jsonOptions;

    private const int ConnectionRetryCount = 3; // ExecuteWithRetryAsync'de kullanılıyor
    private const int OperationTimeoutSeconds = 2;
    private bool _isAvailable = false; // Bağlantı durumunu tutacak flag
    private readonly string _instanceName; // Instance adı eklendi

    public RedisCacheService(
        IConnectionMultiplexer? connectionMultiplexer, // Nullable yapıldı
        ILogger<RedisCacheService> logger,
        IMetricsService metrics,
        IConfiguration configuration, // Instance adı için eklendi
        IOptions<JsonSerializerOptions> jsonOptions = null)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        // Instance adını yapılandırmadan al, varsayılan değer sağla
        _instanceName = configuration.GetValue<string>("Redis:InstanceName", "Tumdex_");

        _jsonOptions = jsonOptions?.Value ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        InitializeConnection(); // Bağlantıyı kurmayı dene ve _isAvailable'ı ayarla
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
                 // Bağlantı olaylarını dinle
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

    // Operasyon öncesi bağlantı kontrolü
    private bool CheckAvailability(string operation)
    {
        if (!_isAvailable || _redisDb == null)
        {
            // _logger.LogWarning("RedisCacheService.{Operation} atlandı: Redis bağlantısı mevcut değil.", operation);
            return false;
        }
        return true;
    }

    // Anahtara instance adını prefix olarak ekle
    private string GetPrefixedKey(string key)
    {
        return $"{_instanceName}{key}";
    }

     // Prefix'i anahtardan kaldır (GetKeysAsync'de lazım)
     private string RemovePrefix(string prefixedKey)
     {
          if (prefixedKey.StartsWith(_instanceName))
          {
              return prefixedKey.Substring(_instanceName.Length);
          }
          return prefixedKey;
     }

    public async Task<(bool success, T value)> TryGetValueAsync<T>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!CheckAvailability(nameof(TryGetValueAsync))) return (false, default(T)!); // default(T)! ile nullable uyarısını gider

        string prefixedKey = GetPrefixedKey(key);
        try
        {
            var semaphore = GetKeyLock(prefixedKey);
            await semaphore.WaitAsync();
            try
            {
                var redisValue = await ExecuteWithRetryAsync(() => _redisDb!.StringGetAsync(prefixedKey));
                if (!redisValue.IsNull)
                {
                    _metrics?.IncrementCacheHit(prefixedKey);
                    var value = JsonSerializer.Deserialize<T>(redisValue!, _jsonOptions);
                    return (true, value!); // Null olamayacağını varsayıyoruz
                }
                _metrics?.IncrementCacheMiss(prefixedKey);
                return (false, default(T)!);
            }
            finally { semaphore.Release(); }
        }
        catch (Exception ex) { _logger.LogError(ex, "TryGetValueAsync hatası. Key: {PrefixedKey}", prefixedKey); return (false, default(T)!); }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        if (!CheckAvailability(nameof(SetAsync))) return;

        string prefixedKey = GetPrefixedKey(key);
        try
        {
            var semaphore = GetKeyLock(prefixedKey);
            await semaphore.WaitAsync();
            try
            {
                var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
                await ExecuteWithRetryAsync(() => _redisDb!.StringSetAsync(prefixedKey, serializedValue, expiration));
                _logger.LogDebug("Başarıyla set edildi. Key: {PrefixedKey}", prefixedKey);
            }
            finally { semaphore.Release(); }
        }
        catch (Exception ex) { _logger.LogError(ex, "SetAsync hatası. Key: {PrefixedKey}", prefixedKey); throw; }
    }

    public async Task<bool> RemoveAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!CheckAvailability(nameof(RemoveAsync))) return false;

        string prefixedKey = GetPrefixedKey(key);
        try
        {
            var semaphore = GetKeyLock(prefixedKey);
            await semaphore.WaitAsync();
            try
            {
                return await ExecuteWithRetryAsync(() => _redisDb!.KeyDeleteAsync(prefixedKey));
            }
            finally { semaphore.Release(); }
        }
        catch (Exception ex) { _logger.LogError(ex, "RemoveAsync hatası. Key: {PrefixedKey}", prefixedKey); throw; }
    }

    public async Task<Dictionary<string, T>> GetManyAsync<T>(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var response = new Dictionary<string, T>();
        if (!keys.Any() || !CheckAvailability(nameof(GetManyAsync))) return response;

        try
        {
            var originalKeys = keys.ToList(); // Orijinal sırayı koru
            var prefixedKeys = originalKeys.Select(GetPrefixedKey).ToArray();
            var redisKeys = prefixedKeys.Select(k => (RedisKey)k).ToArray();

            var redisValues = await ExecuteWithRetryAsync(() => _redisDb!.StringGetAsync(redisKeys));

            for (int i = 0; i < prefixedKeys.Length; i++)
            {
                var key = originalKeys[i]; // Orijinal anahtarı kullan
                var prefixedKey = prefixedKeys[i];
                var value = redisValues[i];

                if (!value.IsNull)
                {
                    try
                    {
                        var deserializedValue = JsonSerializer.Deserialize<T>(value!, _jsonOptions);
                        response[key] = deserializedValue!; // Null olamayacağını varsay
                        _metrics?.IncrementCacheHit(prefixedKey);
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Deserialize hatası. Key: {PrefixedKey}", prefixedKey); _metrics?.IncrementCacheMiss(prefixedKey); }
                }
                else { _metrics?.IncrementCacheMiss(prefixedKey); }
            }
            return response;
        }
        catch (Exception ex) { _logger.LogError(ex, "GetManyAsync hatası. Keys: {Keys}", string.Join(", ", keys)); throw; }
    }

    public async Task SetManyAsync<T>(Dictionary<string, T> keyValues, TimeSpan? expiry = null)
    {
        ArgumentNullException.ThrowIfNull(keyValues);
         if (!keyValues.Any() || !CheckAvailability(nameof(SetManyAsync))) return;

        try
        {
            // Anahtarları prefix'le
            var prefixedKeyValues = keyValues.ToDictionary(kv => GetPrefixedKey(kv.Key), kv => kv.Value);

            var batch = _redisDb!.CreateBatch(); // Null kontrolü
            var tasks = new List<Task>();
            foreach (var kv in prefixedKeyValues)
            {
                 var serializedValue = JsonSerializer.Serialize(kv.Value, _jsonOptions);
                 tasks.Add(batch.StringSetAsync(kv.Key, serializedValue, expiry));
            }

            batch.Execute(); // Batch'i çalıştır
            await Task.WhenAll(tasks); // Tüm görevlerin tamamlanmasını bekle

            _logger.LogDebug("Başarıyla set edildi (çoklu). Keys: {Keys}", string.Join(", ", keyValues.Keys));
        }
        catch (Exception ex) { _logger.LogError(ex, "SetManyAsync hatası. Keys: {Keys}", string.Join(", ", keyValues.Keys)); throw; }
    }


     public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
     {
         ArgumentNullException.ThrowIfNull(key);
         ArgumentNullException.ThrowIfNull(factory);

         // Önce cache'den almayı dene
         var (success, value) = await TryGetValueAsync<T>(key);
         if (success) return value;

         // Cache'de yoksa veya Redis kullanılamıyorsa, kilitle ve factory'yi çalıştır
         string prefixedKey = GetPrefixedKey(key); // Prefix'li anahtarı al
         var semaphore = GetKeyLock(prefixedKey);
         await semaphore.WaitAsync();
         try
         {
              // Kilit alındıktan sonra tekrar kontrol et (double-check locking)
             (success, value) = await TryGetValueAsync<T>(key);
             if (success) return value;

             // Factory ile yeni değeri oluştur
             T newValue = await factory();

             // Eğer Redis müsaitse yeni değeri cache'e ekle
             if (CheckAvailability(nameof(GetOrCreateAsync))) {
                 await SetAsync(key, newValue, expiry ?? TimeSpan.FromMinutes(5)); // Varsayılan 5 dk
             } else {
                 // Redis yoksa sadece oluşturulan değeri dön, cache'leme
                 _logger.LogWarning("GetOrCreateAsync - Redis müsait değil, değer cache'lenmedi. Key: {Key}", key);
             }
             return newValue;
         }
         finally
         {
             semaphore.Release();
         }
     }

     public async Task<bool> IncrementAsync(string key, int value = 1, TimeSpan? expiry = null)
     {
         ArgumentNullException.ThrowIfNull(key);
         if (!CheckAvailability(nameof(IncrementAsync))) return false;

         string prefixedKey = GetPrefixedKey(key);
         try
         {
             var semaphore = GetKeyLock(prefixedKey);
             await semaphore.WaitAsync();
             try
             {
                 long result = await ExecuteWithRetryAsync(() => _redisDb!.StringIncrementAsync(prefixedKey, value));
                 if (result == value && expiry.HasValue) // Eğer anahtar yeni oluşturulduysa expire süresini ayarla
                 {
                     await ExecuteWithRetryAsync(() => _redisDb!.KeyExpireAsync(prefixedKey, expiry.Value));
                 } else if (expiry.HasValue) {
                      // İsteğe bağlı: Her artırmada expire süresini yenilemek isterseniz buraya ekleyin
                      // await ExecuteWithRetryAsync(() => _redisDb!.KeyExpireAsync(prefixedKey, expiry.Value));
                 }
                 return true;
             }
             finally { semaphore.Release(); }
         }
         catch (Exception ex) { _logger.LogError(ex, "IncrementAsync hatası. Key: {PrefixedKey}", prefixedKey); return false; }
     }

     public async Task<int> GetCounterAsync(string key)
     {
         ArgumentNullException.ThrowIfNull(key);
         if (!CheckAvailability(nameof(GetCounterAsync))) return 0; // Redis yoksa 0 dön

         string prefixedKey = GetPrefixedKey(key);
         try
         {
             var redisValue = await ExecuteWithRetryAsync(() => _redisDb!.StringGetAsync(prefixedKey));
             return redisValue.IsNull ? 0 : (int)redisValue;
         }
         catch (Exception ex) { _logger.LogError(ex, "GetCounterAsync hatası. Key: {PrefixedKey}", prefixedKey); return 0; }
     }

    public async Task<List<string>> GetKeysAsync(string pattern)
    {
        if (!CheckAvailability(nameof(GetKeysAsync))) return new List<string>();

        try
        {
            var server = _connectionMultiplexer!.GetServer(_connectionMultiplexer.GetEndPoints().First());
            var prefixedPattern = $"{_instanceName}{pattern}"; // Pattern'e prefix ekle
            var keys = new List<string>();

            // Asenkron olarak KeysAsync kullan
            await foreach (var key in server.KeysAsync(pattern: prefixedPattern))
            {
                keys.Add(RemovePrefix(key.ToString())); // Dönerken prefix'i kaldır
            }

            _logger.LogDebug("Pattern {Pattern} (Prefixed: {PrefixedPattern}) için {Count} anahtar bulundu", pattern, prefixedPattern, keys.Count);
            return keys;
        }
        catch (Exception ex) { _logger.LogError(ex, "GetKeysAsync ({Pattern}) hatası", pattern); return new List<string>(); }
    }


    private SemaphoreSlim GetKeyLock(string key)
    { return _keySemaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1)); }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
    {
        // CheckAvailability burada tekrar kontrol edilmeli
        if (!CheckAvailability(nameof(ExecuteWithRetryAsync)))
             throw new InvalidOperationException("Redis is not available for operation.");

        var lastException = default(Exception);
        for (int i = 0; i < ConnectionRetryCount; i++)
        {
            try
            {
                // CancellationTokenSource ile zaman aşımı ekle
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
                return await operation().WaitAsync(cts.Token);
            }
            catch (Exception ex) when (ex is RedisConnectionException || ex is RedisServerException || ex is RedisTimeoutException || ex is TimeoutException || ex is OperationCanceledException)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Redis operasyonu deneme {Attempt}/{MaxAttempts} başarısız.", i + 1, ConnectionRetryCount);
                if (i < ConnectionRetryCount - 1)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * (i + 1))); // Üssel backoff
                    // Bağlantı durumunu tekrar kontrol et (opsiyonel)
                    if(!_connectionMultiplexer?.IsConnected ?? true) {
                        _isAvailable = false; // Bağlantı koptuysa devre dışı bırak
                        throw lastException ?? ex; // Son hatayı fırlat
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Redis operasyonu sırasında beklenmeyen hata.");
                throw; // Diğer hataları tekrar fırlat
            }
        }
        // Tüm denemeler başarısız olursa son hatayı fırlat
        throw lastException ?? new RedisException($"Redis operation failed after {ConnectionRetryCount} attempts.");
    }

    public void Dispose()
    {
         if (_connectionMultiplexer != null)
         {
             _connectionMultiplexer.ConnectionRestored -= HandleConnectionRestored;
             _connectionMultiplexer.ConnectionFailed -= HandleConnectionFailed;
             // Multiplexer singleton olduğu için burada Dispose edilmemeli.
         }
        foreach (var semaphore in _keySemaphores.Values) { semaphore.Dispose(); }
        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}