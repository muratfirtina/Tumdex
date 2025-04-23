using System.Collections;
using System.Reflection;
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
    private static readonly Dictionary<string, string[]> CompositeGroupMap = InitializeCompositeGroupMap(); // Kompozit grup haritası

    public CacheRemovingBehavior(
        IDistributedCache cache,
        ICacheKeyGenerator cacheKeyGenerator,
        ILogger<CacheRemovingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _cacheKeyGenerator = cacheKeyGenerator;
        _logger = logger;
    }

     // CacheGroups sınıfındaki kompozit anahtarları ve karşılıklarını yükler
    private static Dictionary<string, string[]> InitializeCompositeGroupMap()
    {
        var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var fields = typeof(CacheGroups).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        foreach (var fieldInfo in fields)
        {
            if (fieldInfo.IsLiteral && !fieldInfo.IsInitOnly && fieldInfo.FieldType == typeof(string))
            {
                string? compositeKey = fieldInfo.Name; // Örn: "ProductRelated"
                string? value = fieldInfo.GetValue(null) as string; // Örn: "Products,Categories,Brands..."

                if (value != null && value.Contains(','))
                {
                    var simpleKeys = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(k => k.Trim())
                                          .ToArray();
                    // Kompozit anahtarın kendisini ve değerini (basit anahtarları) ekle
                    if (!string.IsNullOrEmpty(compositeKey))
                        map[compositeKey] = simpleKeys; // "ProductRelated" -> ["Products", "Categories", ...]
                }
            }
        }
        return map;
    }


    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.CacheGroupKey) || !string.IsNullOrWhiteSpace(request.CacheKey))
            _logger.LogInformation("----- CacheRemovingBehavior ----- Request: {RequestName}, GroupKey: {CacheGroupKey}, CacheKey: {CacheKey}", typeof(TRequest).Name, request.CacheGroupKey, request.CacheKey);

        if (request.BypassCache)
        {
            _logger.LogInformation("----- CacheRemovingBehavior ----- Skipping cache removal (BypassCache=true). Request: {RequestName}", typeof(TRequest).Name);
            return await next();
        }

        TResponse response = await next();

        // 1. Resolve Group Keys (Handles comma-separated and composite keys)
        if (!string.IsNullOrWhiteSpace(request.CacheGroupKey))
        {
            HashSet<string> groupsToRemove = ResolveGroupKeys(request.CacheGroupKey);
            _logger.LogDebug("Resolved cache groups to remove: {ResolvedGroups} for initial key: {InitialKey}", string.Join(", ", groupsToRemove), request.CacheGroupKey);

            foreach (var groupKey in groupsToRemove)
            {
                // Generate user-specific cache group key if necessary (handled by generator)
                string groupCacheKey = await _cacheKeyGenerator.GenerateKeyAsync("", groupKey.Trim());
                await RemoveCacheGroup(groupCacheKey, cancellationToken);
            }
        }

        // 2. Remove Specific Cache Key
        if (!string.IsNullOrWhiteSpace(request.CacheKey))
        {
             // Generate user-specific cache key if necessary (handled by generator)
             // Pass null for groupKey as we are generating a specific key, not a group key
            string cacheKey = await _cacheKeyGenerator.GenerateKeyAsync(request.CacheKey, null);
            await _cache.RemoveAsync(cacheKey, cancellationToken);
            _logger.LogDebug("Removed specific cache key: {CacheKey}", cacheKey);
        }

        _logger.LogInformation("----- CacheRemovingBehavior ----- Cache removal completed for {RequestType}", typeof(TRequest).Name);
        return response;
    }

    // Resolves composite and comma-separated group keys into a set of simple group keys
    private HashSet<string> ResolveGroupKeys(string groupKey)
    {
        var resolvedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var initialKeys = groupKey.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(k => k.Trim());

        foreach (var key in initialKeys)
        {
            // Check if it's a known composite key
            if (CompositeGroupMap.TryGetValue(key, out var simpleKeys))
            {
                foreach (var simpleKey in simpleKeys)
                {
                    resolvedKeys.Add(simpleKey);
                }
                _logger.LogDebug("Resolved composite key '{CompositeKey}' to: {SimpleKeys}", key, string.Join(", ", simpleKeys));
            }
            else
            {
                // Assume it's a simple key
                resolvedKeys.Add(key);
            }
        }
        return resolvedKeys;
    }

    private async Task RemoveCacheGroup(string groupCacheKey, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Attempting to remove cache group: {GroupKey}", groupCacheKey);
            byte[]? cachedGroupData = await _cache.GetAsync(groupCacheKey, cancellationToken);

            if (cachedGroupData != null)
            {
                string groupJson = Encoding.UTF8.GetString(cachedGroupData); // Use UTF8
                 // Check if JSON is empty or just whitespace
                if (string.IsNullOrWhiteSpace(groupJson))
                {
                    _logger.LogWarning("Cache group data is empty or whitespace for group key: {GroupKey}", groupCacheKey);
                    await _cache.RemoveAsync(groupCacheKey, cancellationToken); // Remove the invalid group entry
                    await _cache.RemoveAsync($"{groupCacheKey}SlidingExpiration", cancellationToken);
                    return;
                }

                HashSet<string> keysInGroup;
                try
                {
                   keysInGroup = JsonSerializer.Deserialize<HashSet<string>>(groupJson)!;
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Error deserializing cache group data for key: {GroupKey}. Data: {GroupData}", groupCacheKey, groupJson);
                     // Attempt to remove the corrupted group data
                    await _cache.RemoveAsync(groupCacheKey, cancellationToken);
                    await _cache.RemoveAsync($"{groupCacheKey}SlidingExpiration", cancellationToken);
                    return; // Stop processing this group
                }


                if (keysInGroup != null && keysInGroup.Any())
                {
                    int removeCount = 0;
                    foreach (string key in keysInGroup)
                    {
                         if (!string.IsNullOrWhiteSpace(key)) // Ensure key is not empty
                         {
                            _logger.LogTrace("Removing cache key from group {GroupKey}: {CacheKey}", groupCacheKey, key);
                            await _cache.RemoveAsync(key, cancellationToken);
                            removeCount++;
                         }
                         else
                         {
                            _logger.LogWarning("Found null or empty key in cache group: {GroupKey}", groupCacheKey);
                         }
                    }
                    _logger.LogDebug("Removed {RemoveCount} keys from group: {GroupKey}", removeCount, groupCacheKey);
                }
                 else
                {
                    _logger.LogDebug("Cache group {GroupKey} was empty or contained no valid keys.", groupCacheKey);
                }


                // Remove the group list itself and its expiration marker
                await _cache.RemoveAsync(groupCacheKey, cancellationToken);
                await _cache.RemoveAsync($"{groupCacheKey}SlidingExpiration", cancellationToken);

                _logger.LogDebug("Successfully removed cache group list and expiration: {GroupKey}", groupCacheKey);
            }
            else
            {
                _logger.LogDebug("Cache group not found or already removed: {GroupKey}", groupCacheKey);
            }
        }
        catch (Exception ex)
        {
            // Log detailed error including the group key
            _logger.LogError(ex, "Error removing cache group: {GroupKey}", groupCacheKey);
            // Consider re-throwing if this error should halt the process, or handle gracefully
        }
    }
}