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
using Infrastructure.Services.Cache; // InMemoryCacheService tanımlıysa using ekle
using Infrastructure.Services.Configurations;
using Infrastructure.Services.Mail;
using Infrastructure.Services.Seo;
using Infrastructure.Services.Storage;
using Infrastructure.Services.Storage.Google;
using Infrastructure.Services.Storage.Local;
using Infrastructure.Services.Token;
using Infrastructure.Services.Security.KeyVault;
using Microsoft.Extensions.Caching.Distributed; // KeyVaultInitializationService için using eklendi
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions; // RemoveAll için
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

    // ... (RegisterStorageServices, RegisterApplicationServices aynı kalır) ...
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
    // --- Key Vault Servis Kaydı Güncellendi ---
    private static void ConfigureKeyVaultServices(IServiceCollection services, IConfiguration configuration)
    {
        // DÜZELTME: Logger'ı factory ile alırken statik olmayan bir tip kullan
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
                // KeyVaultInitializationService gerçekten varsa kaydet
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
    // --- Key Vault Servis Kaydı Güncellemesi Sonu ---

    // --- Redis Cache Servis Kaydı Güncellendi ---
    private static void ConfigureCacheServices(IServiceCollection services, IConfiguration configuration)
    {
        var logger = services.BuildServiceProvider().GetService<ILogger<object>>(); // Statik olmayan tip kullan
        bool useRedis = configuration.GetValue<bool>("CacheSettings:UseRedis", true);
        string? redisHost = configuration.GetValue<string>("Redis:Host");
        string? redisPort = configuration.GetValue<string>("Redis:Port");
        string? redisPassword = configuration.GetValue<string>("Redis:Password");

        if (useRedis && !string.IsNullOrEmpty(redisHost) && !string.IsNullOrEmpty(redisPort))
        {
            var redisConfiguration = new ConfigurationOptions { /* ... yapılandırma ... */ };
            redisConfiguration.EndPoints.Add($"{redisHost}:{redisPort}");
            redisConfiguration.Password = redisPassword;
            redisConfiguration.AbortOnConnectFail = false;
            redisConfiguration.ConnectTimeout = 5000;
            redisConfiguration.SyncTimeout = 5000;
            redisConfiguration.AllowAdmin = true;
            redisConfiguration.ClientName = "Tumdex_Api";
            

            try
            {
                var multiplexer = ConnectionMultiplexer.Connect(redisConfiguration);
                services.AddSingleton<IConnectionMultiplexer>(multiplexer);
                services.AddStackExchangeRedisCache(redisOptions => { redisOptions.ConfigurationOptions = redisConfiguration; });
                services.AddSingleton<ICacheService, RedisCacheService>();
                logger?.LogInformation("Redis Cache Service (RedisCacheService) başarıyla yapılandırıldı.");
                services.AddSingleton<SlidingWindowRateLimiter>(sp => new SlidingWindowRateLimiter(multiplexer, sp.GetRequiredService<ILogger<SlidingWindowRateLimiter>>(), configuration));
                return;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Redis bağlantısı veya yapılandırması başarısız oldu. Fallback olarak In-Memory Cache kullanılacak.");
                // DÜZELTME: Servisleri doğru şekilde kaldır
                services.RemoveAll<IConnectionMultiplexer>();
                services.RemoveAll<ICacheService>(); // Hem Redis hem InMemory ICacheService'i kaldırabilir, dikkat! Önce ICacheService eklenmediyse sorun olmaz.
                services.RemoveAll<SlidingWindowRateLimiter>();
                // IDistributedCache'in Redis implementasyonunu kaldır
                services.RemoveAll<IDistributedCache>();
            }
        }
         else if (!useRedis) {
             logger?.LogInformation("Yapılandırmada Redis kullanımı (CacheSettings:UseRedis) devre dışı bırakılmış. In-Memory Cache kullanılacak.");
        }
        else {
             logger?.LogWarning("Redis Host veya Port yapılandırılmamış. Fallback olarak In-Memory Cache kullanılacak.");
        }
        
        logger?.LogInformation("Fallback olarak In-Memory Cache (IDistributedCache) yapılandırıldı.");
        services.AddDistributedMemoryCache();
    }
    // --- Redis Cache Servis Kaydı Güncellemesi Sonu ---

    private static void ConfigureHealthChecks(IServiceCollection services, IConfiguration configuration)
    {
        var logger = services.BuildServiceProvider().GetService<ILogger<object>>(); // Statik olmayan tip kullan
        var healthChecksBuilder = services.AddHealthChecks();

        // PostgreSQL (Aynı kalabilir)
        var dbConnectionString = configuration.GetConnectionString("TumdexDb");
        if (!string.IsNullOrEmpty(dbConnectionString)) {
             healthChecksBuilder.AddNpgSql(dbConnectionString, name: "postgresql", failureStatus: HealthStatus.Degraded, tags: new[] { "database", "postgresql", "ready" });
        } else {
             logger?.LogError("Database connection string 'TumdexDb' not found for health checks.");
        }

        // Redis (Aynı kalabilir)
        bool useRedis = configuration.GetValue<bool>("CacheSettings:UseRedis", true);
        string? redisHost = configuration.GetValue<string>("Redis:Host");
        string? redisPort = configuration.GetValue<string>("Redis:Port");
        if(useRedis && !string.IsNullOrEmpty(redisHost) && !string.IsNullOrEmpty(redisPort))
        {
            string? redisPassword = configuration.GetValue<string>("Redis:Password");
            string redisConnectionString = $"{redisHost}:{redisPort}";
            if (!string.IsNullOrEmpty(redisPassword)) { redisConnectionString += $",password={redisPassword}"; }
            healthChecksBuilder.AddRedis(redisConnectionString, name: "redis", failureStatus: HealthStatus.Degraded, tags: new[] { "cache", "redis", "ready" });
        } else {
            logger?.LogWarning("Redis health check skipped: Redis is disabled or configuration is missing.");
        }

        // RabbitMQ (Aynı kalabilir, NuGet paketi eklenmeli)
        string? rabbitMqHost = configuration.GetValue<string>("RabbitMQ:Host");
        if(!string.IsNullOrEmpty(rabbitMqHost))
        {
            try {
                 var rabbitMqPort = configuration.GetValue<string>("RabbitMQ:Port", "5672");
                 var rabbitMqUser = configuration.GetValue<string>("RabbitMQ:Username");
                 var rabbitMqPass = configuration.GetValue<string>("RabbitMQ:Password");
                 var rabbitMqVHost = configuration.GetValue<string>("RabbitMQ:VirtualHost", "/");
                 var uriBuilder = new UriBuilder("amqp", rabbitMqHost, int.Parse(rabbitMqPort), rabbitMqVHost.TrimStart('/'));
                 if(!string.IsNullOrEmpty(rabbitMqUser)) uriBuilder.UserName = Uri.EscapeDataString(rabbitMqUser);
                 if(!string.IsNullOrEmpty(rabbitMqPass)) uriBuilder.Password = Uri.EscapeDataString(rabbitMqPass);

                 
                    healthChecksBuilder.AddCheck<RabbitMQHealthCheck>("RabbitMQ",
                     failureStatus: HealthStatus.Unhealthy,
                     tags: new[] { "rabbitmq", "messagebroker" });


            } catch (Exception ex) {
                 logger?.LogError(ex, "RabbitMQ health check configuration failed.");
            }
        } else {
             logger?.LogWarning("RabbitMQ health check skipped: RabbitMQ configuration is missing.");
        }
    }
}