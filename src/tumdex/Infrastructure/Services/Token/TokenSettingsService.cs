using System.Text;
using System.Text.Json;
using Azure.Security.KeyVault.Secrets;
using Infrastructure.Services.Security.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Token;

public class TokenSettingsService : ITokenSettingsService
{
    private readonly ILogger<TokenSettingsService> _logger;
    private readonly SecretClient _secretClient;
    private readonly IDistributedCache _cache;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private TokenSettings _memoryCache;
    private DateTime _memoryCacheExpiration = DateTime.MinValue;
    private const string CacheKeyPrefix = "TOKEN_SETTINGS_";
    private const int MemoryCacheMinutes = 5;
    private const int RedisCacheMinutes = 30;

    public TokenSettingsService(
        ILogger<TokenSettingsService> logger,
        SecretClient secretClient,
        IDistributedCache cache)
    {
        _logger = logger;
        _secretClient = secretClient;
        _cache = cache;
    }

    public async Task<TokenSettings> GetTokenSettingsAsync()
    {
        // Memory Cache Kontrolü
        if (_memoryCache != null && DateTime.UtcNow < _memoryCacheExpiration)
        {
            _logger.LogDebug("Token Settings memory cache'den alındı");
            return _memoryCache;
        }

        await _semaphore.WaitAsync();
        try
        {
            // Double-check locking pattern
            if (_memoryCache != null && DateTime.UtcNow < _memoryCacheExpiration)
            {
                return _memoryCache;
            }

            // Redis Cache Kontrolü
            var cacheKey = CacheKeyPrefix + "CONFIG";
            var cachedBytes = await _cache.GetAsync(cacheKey);
            if (cachedBytes != null)
            {
                var cachedJson = Encoding.UTF8.GetString(cachedBytes);
                var cached = JsonSerializer.Deserialize<TokenSettings>(cachedJson);
                if (cached != null)
                {
                    UpdateMemoryCache(cached);
                    return cached;
                }
            }

            // Key Vault'tan Ayarları Al
            var settings = await GetFromKeyVault();
            
            // Redis Cache'e Kaydet
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(RedisCacheMinutes)
            };
            
            await _cache.SetAsync(cacheKey, 
                JsonSerializer.SerializeToUtf8Bytes(settings), 
                options);
            
            // Memory Cache'e Kaydet
            UpdateMemoryCache(settings);
            
            return settings;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<TokenSettings> GetFromKeyVault()
    {
        try
        {
            var securityKeyResponse = await _secretClient.GetSecretAsync("TokenSecurityKey");
            var issuerResponse = await _secretClient.GetSecretAsync("TokenIssuer");
            var audienceResponse = await _secretClient.GetSecretAsync("TokenAudience");

            if (securityKeyResponse?.Value == null || 
                issuerResponse?.Value == null || 
                audienceResponse?.Value == null)
            {
                throw new InvalidOperationException("Key Vault'ta gerekli Token secret'ları eksik");
            }

            return new TokenSettings
            {
                SecurityKey = securityKeyResponse.Value.Value,
                Issuer = issuerResponse.Value.Value,
                Audience = audienceResponse.Value.Value
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Key Vault'tan Token ayarları alınırken hata");
            throw;
        }
    }

    private void UpdateMemoryCache(TokenSettings settings)
    {
        _memoryCache = settings;
        _memoryCacheExpiration = DateTime.UtcNow.AddMinutes(MemoryCacheMinutes);
    }
}