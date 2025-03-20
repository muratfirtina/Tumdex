using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using InvalidOperationException = System.InvalidOperationException;

namespace Core.Application.Pipelines.Caching;

public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ICachableRequest
{
    private readonly CacheSettings _cacheSettings;
    private readonly IDistributedCache _cache;
    private readonly ICacheKeyGenerator _cacheKeyGenerator;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(
        IDistributedCache distributedCache, 
        IConfiguration configuration,
        ICacheKeyGenerator cacheKeyGenerator,
        ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cacheSettings = configuration.GetSection("CacheSettings").Get<CacheSettings>() ??
                        throw new InvalidOperationException("Cache settings not found");
        _cache = distributedCache;
        _cacheKeyGenerator = cacheKeyGenerator;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request.BypassCache)
        {
            return await next();
        }

        // Generate user-specific cache key
        string cacheKey = await _cacheKeyGenerator.GenerateKeyAsync(request.CacheKey, request.CacheGroupKey);
        _logger.LogDebug("Using cache key: {CacheKey}", cacheKey);

        byte[]? cachedResponse = await _cache.GetAsync(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            _logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
            try
            {
                var result = JsonSerializer.Deserialize<TResponse>(Encoding.Default.GetString(cachedResponse));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing cached value for key: {CacheKey}", cacheKey);
                return await GetResponseAndAddToCache(request, next, cacheKey, cancellationToken);
            }
        }
        
        _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);
        return await GetResponseAndAddToCache(request, next, cacheKey, cancellationToken);
    }

    private async Task<TResponse> GetResponseAndAddToCache(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        string cacheKey,
        CancellationToken cancellationToken)
    {
        TResponse response = await next();
        
        TimeSpan slidingExpiration = request.SlidingExpiration ?? TimeSpan.FromMinutes(_cacheSettings.SlidingExpiration ?? 2);
        DistributedCacheEntryOptions cacheEntryOptions = new() {SlidingExpiration = slidingExpiration};
        
        try
        {
            byte[] serializeData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));
            await _cache.SetAsync(cacheKey, serializeData, cacheEntryOptions, cancellationToken);
            
            if (request.CacheGroupKey != null)
            {
                string groupCacheKey = await _cacheKeyGenerator.GenerateKeyAsync("", request.CacheGroupKey);
                await AddCacheKeyToGroup(cacheKey, groupCacheKey, slidingExpiration, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching response for key: {CacheKey}", cacheKey);
        }
        
        return response;
    }
    
    private async Task AddCacheKeyToGroup(
        string cacheKey, 
        string groupCacheKey, 
        TimeSpan slidingExpiration, 
        CancellationToken cancellationToken)
    {
        try
        {
            byte[]? cacheGroupCache = await _cache.GetAsync(key: groupCacheKey, cancellationToken);
            HashSet<string> cacheKeysInGroup;
            
            if (cacheGroupCache != null)
            {
                cacheKeysInGroup = JsonSerializer.Deserialize<HashSet<string>>(Encoding.Default.GetString(cacheGroupCache))!;
                if (!cacheKeysInGroup.Contains(cacheKey))
                    cacheKeysInGroup.Add(cacheKey);
            }
            else
            {
                cacheKeysInGroup = new HashSet<string>(new[] { cacheKey });
            }
            
            byte[] newCacheGroupCache = JsonSerializer.SerializeToUtf8Bytes(cacheKeysInGroup);

            byte[]? cacheGroupCacheSlidingExpirationCache = await _cache.GetAsync(
                key: $"{groupCacheKey}SlidingExpiration",
                cancellationToken
            );
            
            int? cacheGroupCacheSlidingExpirationValue = null;
            if (cacheGroupCacheSlidingExpirationCache != null)
                cacheGroupCacheSlidingExpirationValue = Convert.ToInt32(Encoding.Default.GetString(cacheGroupCacheSlidingExpirationCache));
            
            if (cacheGroupCacheSlidingExpirationValue == null || slidingExpiration.TotalSeconds > cacheGroupCacheSlidingExpirationValue)
                cacheGroupCacheSlidingExpirationValue = Convert.ToInt32(slidingExpiration.TotalSeconds);
            
            byte[] serializeCachedGroupSlidingExpirationData = JsonSerializer.SerializeToUtf8Bytes(cacheGroupCacheSlidingExpirationValue);

            DistributedCacheEntryOptions cacheOptions =
                new() { SlidingExpiration = TimeSpan.FromSeconds(Convert.ToDouble(cacheGroupCacheSlidingExpirationValue)) };

            await _cache.SetAsync(key: groupCacheKey, newCacheGroupCache, cacheOptions, cancellationToken);
            
            await _cache.SetAsync(
                key: $"{groupCacheKey}SlidingExpiration",
                serializeCachedGroupSlidingExpirationData,
                cacheOptions,
                cancellationToken
            );
            
            _logger.LogDebug("Added cache key to group. Key: {CacheKey}, Group: {GroupKey}", cacheKey, groupCacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding cache key to group. Key: {CacheKey}, Group: {GroupKey}", cacheKey, groupCacheKey);
        }
    }
}