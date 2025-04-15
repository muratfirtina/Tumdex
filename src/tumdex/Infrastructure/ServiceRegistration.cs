using Application.Abstraction.Services;
using Application.Abstraction.Services.Configurations;
using Application.Abstraction.Services.Messaging;
using Application.Abstraction.Services.Tokens;
using Application.Abstraction.Services.Utilities;
using Application.Services;
using Application.Storage;
using Application.Storage.Google;
using Application.Storage.Local;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Infrastructure.Configuration;
using Infrastructure.Messaging;
using Infrastructure.Services;
using Infrastructure.Services.Cache;
using Infrastructure.Services.Configurations;
using Infrastructure.Services.Mail;
using Infrastructure.Services.Seo;
using Infrastructure.Services.Storage;
using Infrastructure.Services.Storage.Google;
using Infrastructure.Services.Storage.Local;
using Infrastructure.Services.Token;
using Infrastructure.Services.Security.KeyVault;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Persistence.Models;
using Persistence.Services;
using RabbitMQ.Client;
using StackExchange.Redis;
// using HealthChecks.RabbitMQ; // NuGet paketini ekledikten sonra bu using'i ekle

namespace Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();

        ConfigureKeyVaultServices(services, configuration);
        ConfigureCacheServices(services, configuration); // Redis veya Fallback Cache'i kaydeder
        ConfigureHealthChecks(services, configuration);
        services.Configure<OutboxSettings>(configuration.GetSection("OutboxSettings"));
        services.Configure<StorageSettings>(configuration.GetSection("Storage"));

        RegisterStorageServices(services);

        services.AddScoped<ITokenHandler, TokenHandler>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRegistrationAndPasswordService, RegistrationAndPasswordService>();

        RegisterApplicationServices(services);
        services.AddEmailServices(configuration);
        services.AddScoped<IImageSeoService, ImageSeoService>();
        services.AddScoped<ISitemapService, SitemapService>();
        services.AddScoped<IMessageBroker, RabbitMqMessageBroker>();

        return services;
    }

    private static void RegisterStorageServices(IServiceCollection services)
    {
        services.AddScoped<ILocalStorage, LocalStorage>();
        services.AddScoped<IGoogleStorage, GoogleStorage>();
        services.AddScoped<IStorageProviderFactory, StorageProviderFactory>();
        services.AddScoped<IStorageService, StorageService>();
        services.AddScoped<IFileNameService, FileNameService>();
    }

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<ILogService, LogService>();
        services.AddScoped<ICompanyAssetService, CompanyAssetService>();
    }

    private static void ConfigureKeyVaultServices(IServiceCollection services, IConfiguration configuration)
    {
        var logger = services.BuildServiceProvider().GetService<ILogger<object>>();

        var keyVaultUri = configuration["AZURE_KEYVAULT_URI"];

        if (!string.IsNullOrEmpty(keyVaultUri) && Uri.TryCreate(keyVaultUri, UriKind.Absolute, out _))
        {
            try
            {
                TokenCredential credential = new DefaultAzureCredential();
                services.AddSingleton(sp =>
                {
                    var clientLogger = sp.GetRequiredService<ILogger<SecretClient>>();
                    clientLogger.LogInformation("Azure Key Vault için SecretClient oluşturuluyor: {KeyVaultUri}", keyVaultUri);
                    try
                    {
                        return new SecretClient(new Uri(keyVaultUri), credential);
                    }
                    catch (Exception clientEx)
                    {
                        clientLogger.LogError(clientEx, "SecretClient oluşturulurken veya Key Vault'a erişirken hata. URI: {KeyVaultUri}", keyVaultUri);
                        return null; // Hata durumunda null döndür
                    }
                });

                logger?.LogInformation("Azure Key Vault yapılandırıldı: {KeyVaultUri}", keyVaultUri);
                services.AddSingleton<IKeyVaultInitializationService, KeyVaultInitializationService>();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Azure Key Vault yapılandırması başarısız oldu. Key Vault URI: {KeyVaultUri}. Uygulama IConfiguration'dan okumaya devam edecek.", keyVaultUri);
                services.AddSingleton<SecretClient>(_ => null);
            }
        }
        else
        {
            logger?.LogWarning("Azure Key Vault URI'si ('AZURE_KEYVAULT_URI') yapılandırılmamış veya geçersiz. Key Vault servisleri atlanıyor.");
            services.AddSingleton<SecretClient>(_ => null);
        }
    }

    // Eski yapıdan alınmış ve geliştirilmiş Redis yapılandırması
    private static void ConfigureCacheServices(IServiceCollection services, IConfiguration configuration)
    {
        var logger = services.BuildServiceProvider().GetService<ILogger<object>>();
        logger?.LogInformation("Redis yapılandırması başlatılıyor...");

        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        bool useRedis = configuration.GetValue<bool>("CacheSettings:UseRedis", true);

        // Redis kullanımı kapatılmışsa
        if (!useRedis)
        {
            logger?.LogInformation("Yapılandırmada Redis kullanımı (CacheSettings:UseRedis) devre dışı bırakılmış. In-Memory Cache kullanılacak.");
            services.AddDistributedMemoryCache();
            return;
        }

        string redisHost;
        string redisPort;
        string redisPassword;

        try
        {
            // İlk olarak yapılandırmadan okumayı dene
            redisHost = configuration["Redis:Host"];
            redisPort = configuration["Redis:Port"];
            redisPassword = configuration["Redis:Password"];

            // Yapılandırmadan okunan değerler çevre değişkenleri içeriyorsa
            if (string.IsNullOrEmpty(redisHost) || redisHost.Contains("${") || 
                string.IsNullOrEmpty(redisPort) || redisPort.Contains("${"))
            {
                logger?.LogInformation("Redis yapılandırması çevre değişkenleri içeriyor veya boş, Key Vault kullanılacak...");
                
                // Key Vault'tan okuma
                redisHost = isDevelopment ? configuration["Redis:Host"] : configuration.GetSecretFromKeyVault("RedisHost");
                redisPort = isDevelopment ? configuration["Redis:Port"] : configuration.GetSecretFromKeyVault("RedisPort");
                redisPassword = isDevelopment ? configuration["Redis:Password"] : configuration.GetSecretFromKeyVault("RedisPassword");
            }

            // Değerler hala geçersizse fallback değerleri kullan
            if (string.IsNullOrEmpty(redisHost))
            {
                logger?.LogWarning("Redis Host değeri bulunamadı, varsayılan değer 'localhost' kullanılacak.");
                redisHost = "localhost";
            }

            if (string.IsNullOrEmpty(redisPort))
            {
                logger?.LogWarning("Redis Port değeri bulunamadı, varsayılan değer '6379' kullanılacak.");
                redisPort = "6379";
            }

            logger?.LogInformation("Redis yapılandırması: Host={RedisHost}, Port={RedisPort}", redisHost, redisPort);

            var redisConfiguration = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                ConnectTimeout = 5000,
                SyncTimeout = 5000,
                AllowAdmin = true,
                ClientName = "Tumdex_Api"
            };

            // Endpoint'i ekle
            redisConfiguration.EndPoints.Add($"{redisHost}:{redisPort}");

            // Password varsa ekle
            if (!string.IsNullOrEmpty(redisPassword))
            {
                redisConfiguration.Password = redisPassword;
            }

            // Bağlantıyı test et
            var multiplexer = ConnectionMultiplexer.Connect(redisConfiguration);
            services.AddSingleton<IConnectionMultiplexer>(multiplexer);

            services.AddStackExchangeRedisCache(redisOptions =>
            {
                redisOptions.ConfigurationOptions = redisConfiguration;
                redisOptions.InstanceName = "Tumdex_";
            });

            services.AddSingleton<ICacheService, RedisCacheService>();
            logger?.LogInformation("Redis Cache Service başarıyla yapılandırıldı.");

            // SlidingWindowRateLimiter'ı kaydet
            services.AddSingleton<SlidingWindowRateLimiter>(sp =>
            {
                var limiterLogger = sp.GetRequiredService<ILogger<SlidingWindowRateLimiter>>();
                return new SlidingWindowRateLimiter(multiplexer, limiterLogger, configuration);
            });
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Redis bağlantısı veya yapılandırması başarısız oldu. Fallback olarak In-Memory Cache kullanılacak.");
            
            // Servisleri temizle
            services.RemoveAll<IConnectionMultiplexer>();
            services.RemoveAll<ICacheService>();
            services.RemoveAll<SlidingWindowRateLimiter>();
            services.RemoveAll<IDistributedCache>();
            
            // In-Memory Cache'e geç
            services.AddDistributedMemoryCache();
            logger?.LogInformation("Fallback olarak In-Memory Cache (IDistributedCache) yapılandırıldı.");
        }
    }

    private static void ConfigureHealthChecks(IServiceCollection services, IConfiguration configuration)
    {
        var logger = services.BuildServiceProvider().GetService<ILogger<object>>();
        var healthChecksBuilder = services.AddHealthChecks();

        try
        {
            // PostgreSQL sağlık kontrolü
            var dbConnectionString = configuration.GetConnectionString("TumdexDb");
            if (!string.IsNullOrEmpty(dbConnectionString))
            {
                healthChecksBuilder.AddNpgSql(
                    dbConnectionString,
                    name: "postgresql",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "database", "postgresql", "ready" });
                
                logger?.LogInformation("PostgreSQL sağlık kontrolü yapılandırıldı.");
            }
            else
            {
                logger?.LogError("Database connection string 'TumdexDb' sağlık kontrolleri için bulunamadı.");
            }

            // Redis sağlık kontrolü
            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            var redisHost = isDevelopment ? configuration["Redis:Host"] : configuration.GetSecretFromKeyVault("RedisHost");
            var redisPort = isDevelopment ? configuration["Redis:Port"] : configuration.GetSecretFromKeyVault("RedisPort");
            var redisPassword = isDevelopment ? configuration["Redis:Password"] : configuration.GetSecretFromKeyVault("RedisPassword");

            if (!string.IsNullOrEmpty(redisHost) && !string.IsNullOrEmpty(redisPort))
            {
                string redisConnectionString = $"{redisHost}:{redisPort}";
                if (!string.IsNullOrEmpty(redisPassword))
                {
                    redisConnectionString += $",password={redisPassword}";
                }

                healthChecksBuilder.AddRedis(
                    redisConnectionString,
                    name: "redis",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "redis", "cache", "ready" });
                
                logger?.LogInformation("Redis sağlık kontrolü yapılandırıldı.");
            }
            else
            {
                logger?.LogWarning("Redis sağlık kontrolü atlandı: Redis yapılandırması eksik.");
            }

            // RabbitMQ sağlık kontrolü
            try
            {
                // RabbitMQ sağlık kontrolü için özel sınıf kullan
                healthChecksBuilder.AddCheck<RabbitMQHealthCheck>(
                    "RabbitMQ",
                    failureStatus: HealthStatus.Unhealthy,
                    tags: new[] { "rabbitmq", "messagebroker", "ready" });
                
                logger?.LogInformation("RabbitMQ sağlık kontrolü yapılandırıldı.");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "RabbitMQ sağlık kontrolü yapılandırması başarısız oldu.");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Sağlık kontrolleri yapılandırılırken genel bir hata oluştu.");
        }
    }
}