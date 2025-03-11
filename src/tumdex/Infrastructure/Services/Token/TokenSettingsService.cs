using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstraction.Services.Configurations;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Infrastructure.Services.Security.Models;

namespace Infrastructure.Services.Token;

/// <summary>
/// Token ayarlarını Azure Key Vault'tan alır, önbelleğe alma stratejileri kullanarak performansı artırır.
/// </summary>
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

    /// <summary>
    /// TokenSettingsService sınıfını başlatır
    /// </summary>
    public TokenSettingsService(
        ILogger<TokenSettingsService> logger,
        SecretClient secretClient,
        IDistributedCache cache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _secretClient = secretClient ?? throw new ArgumentNullException(nameof(secretClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Token ayarlarını alır, önbelleği kontrol eder, gerekirse Azure Key Vault'tan yükler
    /// </summary>
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
                try
                {
                    var cachedJson = Encoding.UTF8.GetString(cachedBytes);
                    var cached = JsonSerializer.Deserialize<TokenSettings>(cachedJson);
                    if (cached != null)
                    {
                        UpdateMemoryCache(cached);
                        return cached;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Token Settings cache deserialize hatası");
                }
            }

            // Key Vault'tan Ayarları Al
            var settings = await GetFromKeyVault();
            
            // Redis Cache'e Kaydet
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(RedisCacheMinutes)
            };
            
            try
            {
                await _cache.SetAsync(cacheKey, 
                    JsonSerializer.SerializeToUtf8Bytes(settings), 
                    options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token Settings Redis cache güncelleme hatası");
            }
            
            // Memory Cache'e Kaydet
            UpdateMemoryCache(settings);
            
            return settings;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Azure Key Vault'tan token ayarlarını alır
    /// </summary>
    private async Task<TokenSettings> GetFromKeyVault()
    {
        try
        {
            // Doğrudan SecretClient ile Token anahtarlarını al
            var securityKeyResponse = await _secretClient.GetSecretAsync("TokenSecurityKey");
            var issuerResponse = await _secretClient.GetSecretAsync("TokenIssuer");
            var audienceResponse = await _secretClient.GetSecretAsync("TokenAudience");

            if (securityKeyResponse?.Value == null || 
                issuerResponse?.Value == null || 
                audienceResponse?.Value == null)
            {
                throw new InvalidOperationException(
                    "Key Vault'ta gerekli Token anahtarları eksik. TokenSecurityKey, TokenIssuer ve TokenAudience değerlerinin varlığını kontrol edin.");
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
            _logger.LogError(ex, "Key Vault'tan Token ayarları alınırken hata oluştu");
            throw;
        }
    }

    /// <summary>
    /// Bellek önbelleğini günceller
    /// </summary>
    private void UpdateMemoryCache(TokenSettings settings)
    {
        _memoryCache = settings;
        _memoryCacheExpiration = DateTime.UtcNow.AddMinutes(MemoryCacheMinutes);
    }

    /// <summary>
    /// Token ayarlarının güvenlik kontrollerini yapar
    /// </summary>
    private void ValidateTokenSettings(TokenSettings settings)
    {
        if (string.IsNullOrEmpty(settings.SecurityKey) || settings.SecurityKey.Length < 32)
        {
            throw new InvalidOperationException("Token güvenlik anahtarı çok kısa. En az 32 karakter olmalıdır.");
        }

        if (string.IsNullOrEmpty(settings.Issuer))
        {
            throw new InvalidOperationException("Token Issuer değeri boş olamaz.");
        }

        if (string.IsNullOrEmpty(settings.Audience))
        {
            throw new InvalidOperationException("Token Audience değeri boş olamaz.");
        }
    }
}