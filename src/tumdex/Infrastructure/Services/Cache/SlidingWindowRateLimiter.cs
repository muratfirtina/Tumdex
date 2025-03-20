using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Infrastructure.Services.Cache;

public class SlidingWindowRateLimiter
{
    private readonly IDatabase _redisDb;
    private readonly ILogger<SlidingWindowRateLimiter> _logger;
    private readonly TimeSpan _windowSize;
    private readonly int _limit;
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

        _windowSize = TimeSpan.Parse(
            configuration.GetValue<string>("Security:RateLimiting:WindowSize") ?? "01:00:00");
        _limit = configuration.GetValue<int>("Security:RateLimiting:RequestsPerHour", 1000);
        _throttler = new SemaphoreSlim(200, 200);
    }

    public async Task<(bool IsAllowed, int CurrentCount, TimeSpan? RetryAfter)> CheckRateLimitAsync(string key)
    {
        if (!await _throttler.WaitAsync(TimeSpan.FromSeconds(2)))
        {
            _logger.LogWarning("Rate limit check throttled for key: {Key}", key);
            return (false, 0, TimeSpan.FromSeconds(2));
        }

        try
        {
            var now = DateTime.UtcNow.Ticks;
            var windowStart = now - _windowSize.Ticks;

            var batch = _redisDb.CreateBatch();

            // Cleanup old records
            var cleanupTask = batch.SortedSetRemoveRangeByScoreAsync(
                key,
                0,
                windowStart
            );

            // Add new request
            var addTask = batch.SortedSetAddAsync(
                key,
                now.ToString(),
                now
            );

            // Get current count
            var countTask = batch.SortedSetLengthAsync(key);

            batch.Execute();
            await Task.WhenAll(cleanupTask, addTask);
            var currentCount = await countTask;

            if (currentCount > _limit)
            {
                var oldestRequest = await _redisDb.SortedSetRangeByScoreAsync(
                    key,
                    take: 1
                );

                var retryAfter = TimeSpan.FromTicks(
                    windowStart + _windowSize.Ticks -
                    long.Parse(oldestRequest.First().ToString())
                );

                _logger.LogWarning("Rate limit exceeded for key: {Key}, Count: {Count}", key, currentCount);
                return (false, (int)currentCount, retryAfter);
            }

            await _redisDb.KeyExpireAsync(key, _windowSize);
            return (true, (int)currentCount, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for key: {Key}", key);
            return (true, 0, null); // Fail open in case of errors
        }
        finally
        {
            _throttler.Release();
        }
    }
}