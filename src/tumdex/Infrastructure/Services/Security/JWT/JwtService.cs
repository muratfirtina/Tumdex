using System.Text;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json;

namespace Infrastructure.Services.Security.JWT;

public class JwtService : IJwtService
{
    private readonly ILogger<JwtService> _logger;
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private JwtConfiguration _memoryCache;
    private DateTime _memoryCacheExpiration = DateTime.MinValue;
    private const string CacheKeyPrefix = "JWT_CONFIG_";
    private const int MemoryCacheMinutes = 5;
    private const int RedisCacheMinutes = 30;

    public JwtService(
        ILogger<JwtService> logger,
        IDistributedCache cache,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

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

                // Key Vault'tan Alma
                config = await GetFromKeyVault();
                
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

    private async Task<JwtConfiguration> GetFromKeyVault()
    {
        var keyVaultUri = GetKeyVaultUri();
        var credential = CreateAzureCredential();
        var client = new SecretClient(new Uri(keyVaultUri), credential);

        var config = await GetConfigurationFromKeyVault(client);
        ValidateConfiguration(config);

        return config;
    }

    private string GetKeyVaultUri()
    {
        var keyVaultUri = Environment.GetEnvironmentVariable("AZURE_KEYVAULT_URI") ??
                         _configuration["AZURE_KEYVAULT_URI"] ??
                         _configuration["AzureKeyVault:VaultUri"];

        if (string.IsNullOrEmpty(keyVaultUri))
        {
            throw new InvalidOperationException("Key Vault URI bulunamadı");
        }

        return keyVaultUri;
    }

    private static DefaultAzureCredential CreateAzureCredential()
    {
        return new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            Retry = 
            {
                MaxRetries = 3,
                NetworkTimeout = TimeSpan.FromSeconds(5)
            }
        });
    }

    private async Task<JwtConfiguration> GetConfigurationFromKeyVault(SecretClient client)
    {
        var tasks = new[]
        {
            client.GetSecretAsync("JwtSecurityKey"),
            client.GetSecretAsync("JwtIssuer"),
            client.GetSecretAsync("JwtAudience")
        };

        await Task.WhenAll(tasks);

        var securityKeyResponse = tasks[0].Result;
        var issuerResponse = tasks[1].Result;
        var audienceResponse = tasks[2].Result;

        if (securityKeyResponse?.Value == null || 
            issuerResponse?.Value == null || 
            audienceResponse?.Value == null)
        {
            throw new InvalidOperationException("Key Vault'ta gerekli JWT secret'ları eksik");
        }

        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(securityKeyResponse.Value.Value));

        var tokenValidationParameters = CreateTokenValidationParameters(
            securityKey,
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(issuerResponse.Value.Value)),
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(audienceResponse.Value.Value)));

        return new JwtConfiguration
        {
            SecurityKey = securityKey,
            Issuer = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(issuerResponse.Value.Value)),
            Audience = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(audienceResponse.Value.Value)),
            TokenValidationParameters = tokenValidationParameters
        };
    }

    private static TokenValidationParameters CreateTokenValidationParameters(
        SecurityKey securityKey,
        SymmetricSecurityKey issuer,
        SymmetricSecurityKey audience)
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ValidateIssuer = true,
            ValidIssuer = issuer.ToString(),
            ValidateAudience = true,
            ValidAudience = audience.ToString(),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    }

    private void ValidateConfiguration(JwtConfiguration config)
    {
        if (config.SecurityKey.KeySize < 256)
        {
            throw new InvalidOperationException(
                $"Security key en az 256 bit olmalıdır. Mevcut: {config.SecurityKey.KeySize} bits");
        }
    }

    private void UpdateMemoryCache(JwtConfiguration config)
    {
        _memoryCache = config;
        _memoryCacheExpiration = DateTime.UtcNow.AddMinutes(MemoryCacheMinutes);
    }

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

    private static JwtConfiguration CreateConfigurationFromCache(JwtConfigurationCache cached)
    {
        var securityKey = new SymmetricSecurityKey(cached.SecurityKeyBytes);
        var issuerKey = new SymmetricSecurityKey(cached.IssuerBytes);
        var audienceKey = new SymmetricSecurityKey(cached.AudienceBytes);

        var tokenValidationParameters = CreateTokenValidationParameters(
            securityKey,
            issuerKey,
            audienceKey);

        return new JwtConfiguration
        {
            SecurityKey = securityKey,
            Issuer = issuerKey,
            Audience = audienceKey,
            TokenValidationParameters = tokenValidationParameters
        };
    }
}

// Cache için yardımcı sınıf