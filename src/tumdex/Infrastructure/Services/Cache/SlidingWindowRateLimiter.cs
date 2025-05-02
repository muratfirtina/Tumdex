using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services.Cache;

public class SlidingWindowRateLimiter
{
    private readonly IDatabase _redisDb;
    private readonly ILogger<SlidingWindowRateLimiter> _logger;
    private readonly TimeSpan _defaultWindowSize;
    private readonly int _defaultLimit;
    private readonly SemaphoreSlim _throttler;

    public SlidingWindowRateLimiter(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<SlidingWindowRateLimiter> logger,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(configuration);

        _redisDb = connectionMultiplexer.GetDatabase();
        _logger = logger;

        _defaultWindowSize = TimeSpan.FromMinutes(
            configuration.GetValue<int>("Security:RateLimiting:WindowSizeInMinutes", 60));
        _defaultLimit = configuration.GetValue<int>("Security:RateLimiting:RequestsPerHour", 3600);
        
        // Paralel işlemler için throttler yapılandır
        int parallelOperations = configuration.GetValue<int>("Security:RateLimiting:ParallelOperations", 200);
        _throttler = new SemaphoreSlim(parallelOperations, parallelOperations);
    }

    public async Task<(bool IsAllowed, int CurrentCount, TimeSpan? RetryAfter)> CheckRateLimitAsync(
        string key, int? customLimit = null, TimeSpan? customWindowSize = null)
    {
        if (!await _throttler.WaitAsync(TimeSpan.FromSeconds(2)))
        {
            _logger.LogWarning("Rate limit check throttled for key: {Key}", key);
            return (false, 0, TimeSpan.FromSeconds(2));
        }

        try
        {
            var now = DateTime.UtcNow.Ticks;
            var windowSize = customWindowSize ?? _defaultWindowSize;
            var windowStart = now - windowSize.Ticks;
            var limit = customLimit ?? _defaultLimit;

            var batch = _redisDb.CreateBatch();

            // Eski kayıtları temizle
            var cleanupTask = batch.SortedSetRemoveRangeByScoreAsync(
                key,
                0,
                windowStart
            );

            // Yeni istek ekle
            var addTask = batch.SortedSetAddAsync(
                key,
                now.ToString(),
                now
            );

            // Mevcut sayıyı al
            var countTask = batch.SortedSetLengthAsync(key);

            batch.Execute();
            await Task.WhenAll(cleanupTask, addTask);
            var currentCount = await countTask;

            if (currentCount > limit)
            {
                var oldestRequest = await _redisDb.SortedSetRangeByScoreAsync(
                    key,
                    take: 1
                );

                var retryAfter = TimeSpan.FromTicks(
                    windowStart + windowSize.Ticks -
                    long.Parse(oldestRequest.First().ToString())
                );

                _logger.LogWarning(
                    "Rate limit exceeded for key: {Key}, Count: {Count}, Limit: {Limit}, WindowSize: {WindowSize}", 
                    key, currentCount, limit, windowSize);
                
                return (false, (int)currentCount, retryAfter);
            }

            // Redis'te gereksiz verileri önlemek için keyin süresini ayarla
            await _redisDb.KeyExpireAsync(key, windowSize);
            return (true, (int)currentCount, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for key: {Key}", key);
            return (true, 0, null); // Hata durumunda açık davran
        }
        finally
        {
            _throttler.Release();
        }
    }
}