using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Core.Application.Pipelines.Caching;

public class CacheRemovingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ICacheRemoverRequest
{
    private readonly IDistributedCache _cache;
    private readonly ICacheKeyGenerator _cacheKeyGenerator;
    private readonly ILogger<CacheRemovingBehavior<TRequest, TResponse>> _logger;

    public CacheRemovingBehavior(
        IDistributedCache cache,
        ICacheKeyGenerator cacheKeyGenerator,
        ILogger<CacheRemovingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _cacheKeyGenerator = cacheKeyGenerator;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting cache removal for {RequestType}", typeof(TRequest).Name);
        _logger.LogDebug("Cache groups to remove: {CacheGroups}", request.CacheGroupKey);
        if (request.BypassCache)
        {
            return await next();
        }

        TResponse response = await next();

        // Generate user-specific cache group key
        if (request.CacheGroupKey != null)
        {
            var cacheGroups = request.CacheGroupKey.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var groupKey in cacheGroups)
            {
                string groupCacheKey = await _cacheKeyGenerator.GenerateKeyAsync("", groupKey.Trim());
                await RemoveCacheGroup(groupCacheKey, cancellationToken);
            }
        }

        // Generate user-specific cache key
        if (!string.IsNullOrEmpty(request.CacheKey))
        {
            string cacheKey = await _cacheKeyGenerator.GenerateKeyAsync(request.CacheKey, null);
            await _cache.RemoveAsync(cacheKey, cancellationToken);
            _logger.LogDebug("Removed cache key: {CacheKey}", cacheKey);
        }

        _logger.LogDebug("Cache removal completed for {RequestType}", typeof(TRequest).Name);
        return response;
        
    }
    
    private async Task RemoveCacheGroup(string groupCacheKey, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Removing cache group: {GroupKey}", groupCacheKey);
            byte[]? cachedGroup = await _cache.GetAsync(groupCacheKey, cancellationToken);
            
            if (cachedGroup != null)
            {
                HashSet<string> keysInGroup = JsonSerializer.Deserialize<HashSet<string>>(Encoding.Default.GetString(cachedGroup))!;
                
                foreach (string key in keysInGroup)
                {
                    _logger.LogDebug("Removing cache key from group: {CacheKey}", key);
                    await _cache.RemoveAsync(key, cancellationToken);
                }

                await _cache.RemoveAsync(groupCacheKey, cancellationToken);
                await _cache.RemoveAsync(key: $"{groupCacheKey}SlidingExpiration", cancellationToken);
                
                _logger.LogDebug("Successfully removed cache group: {GroupKey} with {KeyCount} keys", 
                    groupCacheKey, keysInGroup.Count);
            }
            else
            {
                _logger.LogDebug("Cache group not found: {GroupKey}", groupCacheKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache group: {GroupKey}", groupCacheKey);
        }
    }
}