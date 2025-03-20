using Application.Services;
using Core.Application.Pipelines.Caching;
using Microsoft.Extensions.Logging;

namespace Application.Abstraction.Helpers;

public class UserBasedCacheKeyGenerator : ICacheKeyGenerator
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UserBasedCacheKeyGenerator> _logger;

    public UserBasedCacheKeyGenerator(
        ICurrentUserService currentUserService,
        ILogger<UserBasedCacheKeyGenerator> logger)
    {
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<string> GenerateKeyAsync(string baseKey, string? groupKey)
    {
        string userId;
        
        try
        {
            userId = await _currentUserService.GetCurrentUserIdAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get user ID for cache key, using default");
            userId = "anonymous";
        }
        
        // Use groupKey in the key generation if available
        string finalKey = string.IsNullOrEmpty(baseKey) 
            ? $"{groupKey ?? "NoGroup"}-{userId}" 
            : $"{baseKey}-{userId}";
        
        _logger.LogDebug("Generated cache key: {CacheKey} for user: {UserId}", finalKey, userId);
        return finalKey;
    }
}