using System;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Azure.Security.KeyVault.Secrets;
using Infrastructure.Services.Security.Models;

namespace Infrastructure.Services.Security.JWT;

/// <summary>
/// JWT yapılandırma ayarlarını yönetir ve Azure Key Vault'tan güvenlik anahtarlarını alır.
/// </summary>
public class JwtService : IJwtService
{
    private readonly ILogger<JwtService> _logger;
    private readonly IDistributedCache _cache;
    private readonly SecretClient _secretClient;
    private readonly JwtSettings _jwtSettings;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private JwtConfiguration _memoryCache;
    private DateTime _memoryCacheExpiration = DateTime.MinValue;
    private const string CacheKeyPrefix = "JWT_CONFIG_";
    private const int MemoryCacheMinutes = 5;
    private const int RedisCacheMinutes = 30;

    /// <summary>
    /// JwtService sınıfını başlatır
    /// </summary>
    public JwtService(
        ILogger<JwtService> logger,
        IDistributedCache cache,
        SecretClient secretClient,
        IOptions<JwtSettings> jwtOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _secretClient = secretClient ?? throw new ArgumentNullException(nameof(secretClient));
        _jwtSettings = jwtOptions.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
    }

    /// <summary>
    /// JWT yapılandırma ayarlarını alır, önbellekten kontrol eder veya Azure Key Vault'tan yükler
    /// </summary>
    public async Task<JwtConfiguration> GetJwtConfigurationAsync()
    {
        try
        {
            // Memory Cache Kontrolü
            if (_memoryCache != null && DateTime.UtcNow < _memoryCacheExpiration)
            {
                _logger.LogDebug("JWT yapılandırması memory cache'den alındı");
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
                var config = await GetFromRedisCache();
                if (config != null)
                {
                    UpdateMemoryCache(config);
                    return config;
                }

                // Azure Key Vault'tan ayarları al ve JwtConfiguration'a dönüştür
                config = await CreateConfigurationFromKeyVault();

                // Yapılandırmayı doğrula
                ValidateConfiguration(config);

                // Her iki cache'e de kaydetme
                await UpdateRedisCacheAsync(config);
                UpdateMemoryCache(config);

                return config;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JWT yapılandırması alınırken hata oluştu");
            throw;
        }
    }

    /// <summary>
    /// Redis önbelleğinden JWT yapılandırmasını alır
    /// </summary>
    private async Task<JwtConfiguration> GetFromRedisCache()
    {
        try
        {
            var cachedBytes = await _cache.GetAsync(CacheKeyPrefix + "CONFIG");
            if (cachedBytes == null) return null;

            var cachedJson = Encoding.UTF8.GetString(cachedBytes);
            var cached = JsonSerializer.Deserialize<JwtConfigurationCache>(cachedJson);

            if (cached == null) return null;

            return CreateConfigurationFromCache(cached);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache'den JWT yapılandırması alınırken hata oluştu");
            return null;
        }
    }

    /// <summary>
    /// Azure Key Vault'tan JWT yapılandırmasını oluşturur
    /// </summary>
    private async Task<JwtConfiguration> CreateConfigurationFromKeyVault()
    {
        // Doğrudan Key Vault'tan güvenlik ayarlarını al
        var securityKeyResponse = await _secretClient.GetSecretAsync("JwtSecurityKey");
        var issuerResponse = await _secretClient.GetSecretAsync("JwtIssuer");
        var audienceResponse = await _secretClient.GetSecretAsync("JwtAudience");

        if (securityKeyResponse?.Value == null ||
            issuerResponse?.Value == null ||
            audienceResponse?.Value == null)
        {
            throw new InvalidOperationException(
                "Key Vault'ta JWT yapılandırma anahtarları eksik. JwtSecurityKey, JwtIssuer ve JwtAudience değerlerinin varlığını kontrol edin.");
        }

        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(securityKeyResponse.Value.Value));

        var issuerKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(issuerResponse.Value.Value));

        var audienceKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(audienceResponse.Value.Value));

        var tokenValidationParameters = CreateTokenValidationParameters(
            securityKey,
            issuerResponse.Value.Value,
            audienceResponse.Value.Value);

        return new JwtConfiguration
        {
            SecurityKey = securityKey,
            Issuer = issuerKey,
            Audience = audienceKey,
            TokenValidationParameters = tokenValidationParameters
        };
    }

    /// <summary>
    /// Token doğrulama parametrelerini oluşturur
    /// </summary>
    private TokenValidationParameters CreateTokenValidationParameters(
        SecurityKey securityKey,
        string issuer,
        string audience)
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = _jwtSettings.ValidateIssuerSigningKey,
            IssuerSigningKey = securityKey,
            ValidateIssuer = _jwtSettings.ValidateIssuer,
            ValidIssuer = issuer,
            ValidateAudience = _jwtSettings.ValidateAudience,
            ValidAudience = audience,
            ValidateLifetime = _jwtSettings.ValidateLifetime,
            ClockSkew = TimeSpan.FromMinutes(_jwtSettings.ClockSkewMinutes),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    }

    /// <summary>
    /// JWT yapılandırmasının geçerliliğini doğrular
    /// </summary>
    private void ValidateConfiguration(JwtConfiguration config)
    {
        if (config.SecurityKey.KeySize < 256)
        {
            throw new InvalidOperationException(
                $"Security key en az 256 bit olmalıdır. Mevcut: {config.SecurityKey.KeySize} bits");
        }
    }

    /// <summary>
    /// JWT yapılandırmasını bellek önbelleğine kaydeder
    /// </summary>
    private void UpdateMemoryCache(JwtConfiguration config)
    {
        _memoryCache = config;
        _memoryCacheExpiration = DateTime.UtcNow.AddMinutes(MemoryCacheMinutes);
    }

    /// <summary>
    /// JWT yapılandırmasını Redis önbelleğine kaydeder
    /// </summary>
    private async Task UpdateRedisCacheAsync(JwtConfiguration config)
    {
        try
        {
            var cacheModel = new JwtConfigurationCache
            {
                SecurityKeyBytes = config.SecurityKey.Key,
                IssuerBytes = config.Issuer.Key,
                AudienceBytes = config.Audience.Key
            };

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(RedisCacheMinutes)
            };

            await _cache.SetAsync(CacheKeyPrefix + "CONFIG",
                JsonSerializer.SerializeToUtf8Bytes(cacheModel),
                options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache güncelleme hatası");
        }
    }

    /// <summary>
    /// Önbellekten yüklenen verilerden JWT yapılandırması oluşturur
    /// </summary>
    private static JwtConfiguration CreateConfigurationFromCache(JwtConfigurationCache cached)
    {
        var securityKey = new SymmetricSecurityKey(cached.SecurityKeyBytes);
        var issuerKey = new SymmetricSecurityKey(cached.IssuerBytes);
        var audienceKey = new SymmetricSecurityKey(cached.AudienceBytes);

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ValidateIssuer = true,
            ValidIssuer = Encoding.UTF8.GetString(cached.IssuerBytes),
            ValidateAudience = true,
            ValidAudience = Encoding.UTF8.GetString(cached.AudienceBytes),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };

        return new JwtConfiguration
        {
            SecurityKey = securityKey,
            Issuer = issuerKey,
            Audience = audienceKey,
            TokenValidationParameters = tokenValidationParameters
        };
    }
}