// Infrastructure/Services/Security/RateLimitService.cs
using Application.Abstraction.Services;
using Infrastructure.Services.Cache;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Security;

public class RateLimitService : IRateLimitService
{
    private readonly SlidingWindowRateLimiter _rateLimiter;
    private readonly ILogger<RateLimitService> _logger;

    public RateLimitService(
        SlidingWindowRateLimiter rateLimiter,
        ILogger<RateLimitService> logger)
    {
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public async Task<(bool IsAllowed, int CurrentCount, TimeSpan? RetryAfter)> CheckRateLimitAsync(string key)
    {
        try
        {
            return await _rateLimiter.CheckRateLimitAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for key: {Key}", key);
            return (true, 0, null); // Fail open in case of errors
        }
    }
}