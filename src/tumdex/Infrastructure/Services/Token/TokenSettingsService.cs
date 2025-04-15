using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstraction.Services.Configurations;
using Azure.Security.KeyVault.Secrets; // Nullable referans türleri etkinse ? gerekebilir
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration; // IConfiguration eklendi
using Microsoft.Extensions.Logging;
using Infrastructure.Services.Security.Models; // TokenSettings için

namespace Infrastructure.Services.Token;

public class TokenSettingsService : ITokenSettingsService
{
    private readonly ILogger<TokenSettingsService> _logger;
    private readonly SecretClient? _secretClient; // Nullable yapıldı
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _configuration; // IConfiguration eklendi
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private TokenSettings? _memoryCache; // Nullable yapıldı
    private DateTime _memoryCacheExpiration = DateTime.MinValue;
    private const string CacheKeyPrefix = "TOKEN_SETTINGS_";
    private const int MemoryCacheMinutes = 5;
    private const int RedisCacheMinutes = 30;

    public TokenSettingsService(
        ILogger<TokenSettingsService> logger,
        IDistributedCache cache,
        IConfiguration configuration, // IConfiguration inject edildi
        SecretClient? secretClient = null) // SecretClient opsiyonel yapıldı
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _secretClient = secretClient; // Null olabilir

        if (_secretClient == null)
        {
            _logger.LogWarning("SecretClient sağlanmadı. Token ayarları IConfiguration'dan okunacak.");
        }
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
            // Double-check locking
            if (_memoryCache != null && DateTime.UtcNow < _memoryCacheExpiration) return _memoryCache;

            // Redis Cache Kontrolü
            var cacheKey = CacheKeyPrefix + "CONFIG";
            var cachedBytes = await _cache.GetAsync(cacheKey);
            if (cachedBytes != null)
            {
                try
                {
                    var cachedJson = Encoding.UTF8.GetString(cachedBytes);
                    var cached = JsonSerializer.Deserialize<TokenSettings>(cachedJson);
                    if (cached != null && IsTokenSettingsValid(cached)) // Doğrulama eklendi
                    {
                         _logger.LogInformation("Token Settings Redis cache'den alındı.");
                        UpdateMemoryCache(cached);
                        return cached;
                    }
                     _logger.LogWarning("Redis cache'deki Token Settings geçersiz veya deserialize edilemedi.");
                     await _cache.RemoveAsync(cacheKey); // Geçersiz veriyi cache'den kaldır
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Token Settings cache deserialize hatası");
                    await _cache.RemoveAsync(cacheKey); // Hatalı veriyi kaldır
                }
            }

            // Ayarları Al (Önce Key Vault, olmazsa IConfiguration)
            var settings = await GetSettingsFromSource();

            // Alınan ayarları doğrula
            if (!IsTokenSettingsValid(settings))
            {
                _logger.LogError("Geçersiz token ayarları alındı (Kaynak: {Source}). Lütfen yapılandırmayı kontrol edin.",
                    _secretClient != null ? "Key Vault" : "IConfiguration");
                throw new InvalidOperationException("Uygulama başlatılamadı: Geçersiz veya eksik token ayarları.");
            }

            // Redis Cache'e Kaydet
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(RedisCacheMinutes)
            };
            try
            {
                await _cache.SetAsync(cacheKey, JsonSerializer.SerializeToUtf8Bytes(settings), options);
                 _logger.LogInformation("Token Settings Redis cache'e kaydedildi.");
            }
            catch (Exception ex)
            { _logger.LogWarning(ex, "Token Settings Redis cache güncelleme hatası"); }

            // Memory Cache'e Kaydet
            UpdateMemoryCache(settings);

            return settings;
        }
        finally
        {
            _semaphore.Release();
        }
    }

     private async Task<TokenSettings> GetSettingsFromSource()
    {
        if (_secretClient != null)
        {
            _logger.LogInformation("Token ayarları Azure Key Vault'tan alınıyor...");
            try
            {
                // Key Vault'tan gizli anahtarları al
                // GetSecretAsync bir Response<KeyVaultSecret> döndürür
                var securityKeyTask = _secretClient.GetSecretAsync("TokenSecurityKey");
                var issuerTask = _secretClient.GetSecretAsync("TokenIssuer");
                var audienceTask = _secretClient.GetSecretAsync("TokenAudience");

                await Task.WhenAll(securityKeyTask, issuerTask, audienceTask);

                // --- DÜZELTME: Değere .Value.Value ile eriş ---
                var securityKeySecret = securityKeyTask.Result?.Value; // KeyVaultSecret nesnesi (nullable olabilir)
                var issuerSecret = issuerTask.Result?.Value;
                var audienceSecret = audienceTask.Result?.Value;

                 // Alınan secret nesnelerinin ve içindeki Value'nun null olup olmadığını kontrol et
                 if (securityKeySecret?.Value != null && issuerSecret?.Value != null && audienceSecret?.Value != null)
                 {
                      _logger.LogInformation("Token ayarları Azure Key Vault'tan başarıyla alındı.");
                     return new TokenSettings
                     {
                         SecurityKey = securityKeySecret.Value, // .Value ile string değere eriş
                         Issuer = issuerSecret.Value,          // .Value ile string değere eriş
                         Audience = audienceSecret.Value       // .Value ile string değere eriş
                     };
                 }
                 // --- DÜZELTME SONU ---

                  _logger.LogWarning("Key Vault'ta gerekli Token anahtarları (TokenSecurityKey, TokenIssuer, TokenAudience) bulunamadı veya değerleri boş. IConfiguration'a fallback yapılıyor.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Key Vault'tan Token ayarları alınırken hata oluştu. IConfiguration'a fallback yapılıyor.");
            }
        }

        // ... (IConfiguration'dan okuma kısmı aynı kalır) ...
         _logger.LogInformation("Token ayarları IConfiguration'dan okunuyor...");
        return new TokenSettings
        {
            SecurityKey = _configuration["Security:TokenSettings:SecurityKey"],
            Issuer = _configuration["Security:TokenSettings:Issuer"],
            Audience = _configuration["Security:TokenSettings:Audience"]
        };
    }

    private void UpdateMemoryCache(TokenSettings settings)
    {
        _memoryCache = settings;
        _memoryCacheExpiration = DateTime.UtcNow.AddMinutes(MemoryCacheMinutes);
        _logger.LogDebug("Token Settings memory cache güncellendi.");
    }

    private bool IsTokenSettingsValid(TokenSettings? settings)
    {
        if (settings == null) { _logger.LogWarning("TokenSettings nesnesi null."); return false; }

        bool isKeyValid = !string.IsNullOrEmpty(settings.SecurityKey) && settings.SecurityKey.Length >= 32;
        if (!isKeyValid) _logger.LogWarning("Token güvenlik anahtarı (SecurityKey) geçersiz veya çok kısa (en az 32 karakter olmalı). Değer: '{SecurityKeyValue}'", settings.SecurityKey ?? "NULL"); // Değeri logla (dikkat!)

        bool isIssuerValid = !string.IsNullOrEmpty(settings.Issuer);
         if (!isIssuerValid) _logger.LogWarning("Token Issuer değeri boş veya eksik.");

        bool isAudienceValid = !string.IsNullOrEmpty(settings.Audience);
        if (!isAudienceValid) _logger.LogWarning("Token Audience değeri boş veya eksik.");

        return isKeyValid && isIssuerValid && isAudienceValid;
    }
}