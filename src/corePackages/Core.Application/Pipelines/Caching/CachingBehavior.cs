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
             _logger.LogInformation("----- CachingBehavior ----- Skipping cache check (BypassCache=true). Request: {RequestName}", typeof(TRequest).Name);
            return await next();
        }

         _logger.LogInformation("----- CachingBehavior ----- Request: {RequestName}, GroupKey: {CacheGroupKey}, CacheKey: {CacheKey}", typeof(TRequest).Name, request.CacheGroupKey, request.CacheKey);

        // Generate cache key (user-specific if necessary)
        string cacheKey = await _cacheKeyGenerator.GenerateKeyAsync(request.CacheKey, request.CacheGroupKey);
        _logger.LogDebug("Using cache key: {CacheKey} for request {RequestName}", cacheKey, typeof(TRequest).Name);

        byte[]? cachedResponseBytes = null;
        try
        {
             cachedResponseBytes = await _cache.GetAsync(cacheKey, cancellationToken);
        }
        catch (Exception ex) // Catch potential cache provider errors (e.g., Redis down)
        {
             _logger.LogError(ex, "Error retrieving data from cache for key: {CacheKey}. Proceeding without cache.", cacheKey);
             // Proceed to get fresh data
             return await GetResponseAndAddToCache(request, next, cacheKey, cancellationToken);
        }


        if (cachedResponseBytes != null && cachedResponseBytes.Length > 0) // Check for empty byte array
        {
            _logger.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
            try
            {
                // Use UTF8 for deserialization
                var cachedResponseString = Encoding.UTF8.GetString(cachedResponseBytes);
                 // Add null/empty check for the string as well
                if (string.IsNullOrWhiteSpace(cachedResponseString))
                {
                    _logger.LogWarning("Cached value for key {CacheKey} is empty or whitespace. Fetching fresh data.", cacheKey);
                    // Treat as cache miss if data is invalid
                    await _cache.RemoveAsync(cacheKey, cancellationToken); // Remove invalid entry
                    return await GetResponseAndAddToCache(request, next, cacheKey, cancellationToken);
                }
                var result = JsonSerializer.Deserialize<TResponse>(cachedResponseString);

                 if (result == null && typeof(TResponse).IsClass) // Handle potential null result if TResponse is a class
                 {
                    _logger.LogWarning("Deserialized cached value is null for key: {CacheKey}. Fetching fresh data.", cacheKey);
                    await _cache.RemoveAsync(cacheKey, cancellationToken); // Remove invalid entry
                    return await GetResponseAndAddToCache(request, next, cacheKey, cancellationToken);
                 }

                return result!; // Nullable handling depends on TResponse constraints
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Error deserializing cached JSON value for key: {CacheKey}", cacheKey);
                 // Remove corrupted cache entry
                await _cache.RemoveAsync(cacheKey, cancellationToken);
                return await GetResponseAndAddToCache(request, next, cacheKey, cancellationToken);
            }
            catch (Exception ex) // Catch other potential deserialization errors
            {
                 _logger.LogError(ex, "Error processing cached value for key: {CacheKey}", cacheKey);
                 // Remove potentially problematic cache entry
                 await _cache.RemoveAsync(cacheKey, cancellationToken);
                 return await GetResponseAndAddToCache(request, next, cacheKey, cancellationToken);
            }
        }

        _logger.LogInformation("Cache miss for key: {CacheKey}", cacheKey);
        return await GetResponseAndAddToCache(request, next, cacheKey, cancellationToken);
    }

    private async Task<TResponse> GetResponseAndAddToCache(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        string cacheKey,
        CancellationToken cancellationToken)
    {
         _logger.LogDebug("Fetching fresh data for key: {CacheKey}", cacheKey);
        TResponse response = await next();

        // Sliding expiration from request or settings
        TimeSpan slidingExpiration = request.SlidingExpiration ?? TimeSpan.FromMinutes(_cacheSettings.SlidingExpiration ?? 5); // Default 5 mins
        DistributedCacheEntryOptions cacheEntryOptions = new() { SlidingExpiration = slidingExpiration };

        try
        {
             // Check if response is null before serialization (especially for reference types)
             if (response == null && typeof(TResponse).IsClass)
             {
                _logger.LogWarning("Response to cache is null for key: {CacheKey}. Skipping caching.", cacheKey);
                return response; // Return the null response without caching
             }

            // Use UTF8 for serialization
            byte[] serializeData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));

             // Prevent caching empty data
             if (serializeData == null || serializeData.Length == 0)
             {
                 _logger.LogWarning("Serialized data is empty for key: {CacheKey}. Skipping caching.", cacheKey);
                 return response;
             }

            await _cache.SetAsync(cacheKey, serializeData, cacheEntryOptions, cancellationToken);
            _logger.LogInformation("Successfully cached response for key: {CacheKey} with expiration {Expiration}", cacheKey, slidingExpiration);

            // Add key to group(s) if CacheGroupKey is specified
            if (!string.IsNullOrWhiteSpace(request.CacheGroupKey))
            {
                var groupKeys = request.CacheGroupKey.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                         .Select(k => k.Trim());

                foreach (var groupKey in groupKeys)
                {
                     if (string.IsNullOrWhiteSpace(groupKey)) continue; // Skip empty group keys

                    // Generate the key for the group itself (potentially user-specific)
                    string groupCacheKey = await _cacheKeyGenerator.GenerateKeyAsync("", groupKey); // Base key is empty for group key
                    await AddCacheKeyToGroup(cacheKey, groupCacheKey, slidingExpiration, cancellationToken);
                }
            }
        }
        catch (JsonException jsonEx)
        {
             _logger.LogError(jsonEx, "Error serializing response for caching. Key: {CacheKey}", cacheKey);
             // Optionally, decide if you still want to return the response even if caching failed
        }
        catch (Exception ex) // Catch potential cache provider errors (e.g., Redis Set failed)
        {
            _logger.LogError(ex, "Error setting cache for key: {CacheKey}", cacheKey);
            // Optionally, decide if you still want to return the response even if caching failed
        }

        return response;
    }

    private async Task AddCacheKeyToGroup(
        string cacheKey,
        string groupCacheKey,
        TimeSpan slidingExpiration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            _logger.LogWarning("Attempted to add null or empty cache key to group {GroupKey}.", groupCacheKey);
            return;
        }
         if (string.IsNullOrWhiteSpace(groupCacheKey))
        {
            _logger.LogWarning("Attempted to add cache key {CacheKey} to a null or empty group key.", cacheKey);
            return;
        }


        try
        {
            _logger.LogDebug("Adding cache key {CacheKey} to group {GroupKey}", cacheKey, groupCacheKey);
            byte[]? cacheGroupBytes = await _cache.GetAsync(groupCacheKey, cancellationToken);
            HashSet<string> cacheKeysInGroup;

            if (cacheGroupBytes != null && cacheGroupBytes.Length > 0)
            {
                 string groupJson = Encoding.UTF8.GetString(cacheGroupBytes); // Use UTF8
                 try
                 {
                    cacheKeysInGroup = JsonSerializer.Deserialize<HashSet<string>>(groupJson) ?? new HashSet<string>();
                 }
                 catch (JsonException jsonEx)
                 {
                    _logger.LogError(jsonEx, "Error deserializing existing group data for group key: {GroupKey}. Initializing new group. Data: {GroupData}", groupCacheKey, groupJson);
                    cacheKeysInGroup = new HashSet<string>(); // Corrupted data, start fresh
                 }
            }
            else
            {
                cacheKeysInGroup = new HashSet<string>();
            }

            // Add the new key if it's not already present
            if (cacheKeysInGroup.Add(cacheKey)) // Add returns true if the item was added
             {
                _logger.LogTrace("Key {CacheKey} was added to the group set for {GroupKey}.", cacheKey, groupCacheKey);
             }
             else
             {
                _logger.LogTrace("Key {CacheKey} already exists in the group set for {GroupKey}.", cacheKey, groupCacheKey);
                // No need to update if the key is already there, unless expiration needs extension (handled below)
             }


            // --- Expiration Handling ---
            // We want the group entry to expire based on the *longest* sliding expiration
            // of any item added to it. We store this max expiration duration separately.
            string groupExpirationKey = $"{groupCacheKey}SlidingExpiration";
            double currentMaxExpirationSeconds = 0;

            byte[]? expirationBytes = await _cache.GetAsync(groupExpirationKey, cancellationToken);
            if (expirationBytes != null && expirationBytes.Length > 0)
            {
                string expirationString = Encoding.UTF8.GetString(expirationBytes); // Use UTF8
                if (double.TryParse(expirationString, out double storedExpiration))
                {
                    currentMaxExpirationSeconds = storedExpiration;
                }
                else
                {
                    _logger.LogWarning("Could not parse stored expiration value '{ExpirationValue}' for group {GroupKey}. Resetting.", expirationString, groupCacheKey);
                }
            }


            // Update max expiration if the current item's expiration is longer
             double newItemExpirationSeconds = slidingExpiration.TotalSeconds;
            if (newItemExpirationSeconds > currentMaxExpirationSeconds)
            {
                 currentMaxExpirationSeconds = newItemExpirationSeconds;
                 _logger.LogDebug("Updating max sliding expiration for group {GroupKey} to {ExpirationSeconds} seconds.", groupCacheKey, currentMaxExpirationSeconds);
                 byte[] serializedExpiration = Encoding.UTF8.GetBytes(currentMaxExpirationSeconds.ToString()); // Use UTF8
                 var expirationEntryOptions = new DistributedCacheEntryOptions
                 {
                     SlidingExpiration = TimeSpan.FromSeconds(currentMaxExpirationSeconds)
                 };
                 await _cache.SetAsync(groupExpirationKey, serializedExpiration, expirationEntryOptions, cancellationToken);
            }
            else
            {
                 _logger.LogTrace("Current item expiration ({NewItemExpiration}s) is not longer than group max expiration ({GroupMaxExpiration}s) for {GroupKey}.", newItemExpirationSeconds, currentMaxExpirationSeconds, groupCacheKey);
            }


            // --- Update Group Data ---
            // Always update the group data itself with the potentially updated expiration
             byte[] newCacheGroupBytes = JsonSerializer.SerializeToUtf8Bytes(cacheKeysInGroup); // Use UTF8
             var groupEntryOptions = new DistributedCacheEntryOptions
             {
                 SlidingExpiration = TimeSpan.FromSeconds(currentMaxExpirationSeconds) // Use the determined max expiration
             };

            await _cache.SetAsync(groupCacheKey, newCacheGroupBytes, groupEntryOptions, cancellationToken);
            _logger.LogDebug("Updated cache group data for {GroupKey} with {KeyCount} keys and expiration {ExpirationSeconds}s.", groupCacheKey, cacheKeysInGroup.Count, currentMaxExpirationSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding cache key {CacheKey} to group {GroupKey}", cacheKey, groupCacheKey);
             // Decide how to handle: maybe remove the key from cache if adding to group failed?
        }
    }
}