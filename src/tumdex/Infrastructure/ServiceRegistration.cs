using Application.Abstraction.Services;
using Application.Abstraction.Services.Configurations;
using Application.Abstraction.Services.Messaging;
using Application.Abstraction.Services.Tokens;
using Application.Abstraction.Services.Utilities;
using Application.Services;
using Application.Storage;
using Application.Storage.Google;
using Application.Storage.Local;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Persistence.Models;
using Persistence.Services;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();
        
        RegisterKeyVaultServices(services, configuration);
        ConfigureCacheServices(services, configuration);
        ConfigureHealthChecks(services, configuration);
        services.Configure<OutboxSettings>(configuration.GetSection("OutboxSettings"));

        services.Configure<StorageSettings>(configuration.GetSection("Storage"));

        // Storage providers
        RegisterStorageServices(services);

        // Auth, Token , Register servisler
        services.AddScoped<ITokenHandler, TokenHandler>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRegistrationAndPasswordService, RegistrationAndPasswordService>();

        // Uygulama servisleri
        RegisterApplicationServices(services);

        // Email servisleri (tümü)
        services.AddEmailServices(configuration);

        // SEO servisleri
        services.AddScoped<IImageSeoService, ImageSeoService>();
        services.AddScoped<ISitemapService, SitemapService>();
    
        // IMessageBroker servisini kaydedelim
        services.AddScoped<IMessageBroker, RabbitMqMessageBroker>();

        return services;
    }
    
    private static void RegisterStorageServices(IServiceCollection services)
    {
        // Storage providers
        services.AddScoped<ILocalStorage, LocalStorage>();
        //services.AddScoped<ICloudinaryStorage, CloudinaryStorage>();
        services.AddScoped<IGoogleStorage, GoogleStorage>();
        //services.AddScoped<IYandexStorage, YandexStorage>();

        // Storage factory ve service
        services.AddScoped<IStorageProviderFactory, StorageProviderFactory>();
        services.AddScoped<IStorageService, StorageService>();
        
        // Dosya adı servisi
        services.AddScoped<IFileNameService, FileNameService>();
    }
    
    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<ILogService, LogService>();
        services.AddScoped<ICompanyAssetService, CompanyAssetService>();
    }
    
    private static void RegisterKeyVaultServices(IServiceCollection services, IConfiguration configuration)
    {
        var keyVaultUri = configuration["AZURE_KEYVAULT_URI"];
        
        if (!string.IsNullOrEmpty(keyVaultUri))
        {
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeVisualStudioCredential = true,
                ExcludeVisualStudioCodeCredential = true
            });

            services.AddSingleton(_ => new SecretClient(new Uri(keyVaultUri), credential));
        }
        else
        {
            // Fallback: SecretClient yerine null geçebilen bir factory kaydet
            services.AddSingleton<SecretClient>(sp => null);
        }
    }

    private static void ConfigureCacheServices(IServiceCollection services, IConfiguration configuration)
    {
        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

        var redisHost = isDevelopment ? configuration["Redis:Host"] : configuration.GetSecretFromKeyVault("RedisHost");
        var redisPort = isDevelopment ? configuration["Redis:Port"] : configuration.GetSecretFromKeyVault("RedisPort");
        var redisPassword = isDevelopment
            ? configuration["Redis:Password"]
            : configuration.GetSecretFromKeyVault("RedisPassword");

        var redisConfiguration = new ConfigurationOptions
        {
            EndPoints = { $"{redisHost}:{redisPort}" },
            Password = redisPassword,
            AbortOnConnectFail = false,
            ConnectTimeout = 5000,
            SyncTimeout = 5000,
            AllowAdmin = true,
            ClientName = "Tumdex_Api"
        };

        // Bağlantıyı test et
        try
        {
            var multiplexer = ConnectionMultiplexer.Connect(redisConfiguration);
            services.AddSingleton<IConnectionMultiplexer>(multiplexer);

            services.AddStackExchangeRedisCache(redisOptions =>
            {
                redisOptions.ConfigurationOptions = redisConfiguration;
                redisOptions.InstanceName = "Tumdex_";
            });

            services.AddSingleton<ICacheService, RedisCacheService>();

            // SlidingWindowRateLimiter'ı kaydet
            services.AddSingleton<SlidingWindowRateLimiter>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<SlidingWindowRateLimiter>>();
                return new SlidingWindowRateLimiter(multiplexer, logger, configuration);
            });
        }
        catch (RedisConnectionException ex)
        {
            // Loglama yap ve uygun şekilde handle et
            throw new ApplicationException("Redis connection failed", ex);
        }
    }

    private static void ConfigureHealthChecks(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Ortam kontrolü yapıyoruz
        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

        // Redis yapılandırması
        var redisHost = isDevelopment ? configuration["Redis:Host"] : configuration.GetSecretFromKeyVault("RedisHost");
        var redisPort = isDevelopment ? configuration["Redis:Port"] : configuration.GetSecretFromKeyVault("RedisPort");
        var redisPassword = isDevelopment
            ? configuration["Redis:Password"]
            : configuration.GetSecretFromKeyVault("RedisPassword");

        // Redis bağlantı dizesini oluşturuyoruz
        var redisConnectionString = $"{redisHost}:{redisPort},password={redisPassword}";

        // RabbitMQ yapılandırması
        var rabbitMqHost = isDevelopment
            ? configuration["RabbitMQ:Host"]
            : configuration.GetSecretFromKeyVault("RabbitMQHost");
        var rabbitMqPort = isDevelopment
            ? configuration["RabbitMQ:Port"]
            : configuration.GetSecretFromKeyVault("RabbitMQPort");
        var rabbitMqUsername = isDevelopment
            ? configuration["RabbitMQ:Username"]
            : configuration.GetSecretFromKeyVault("RabbitMQUsername");
        var rabbitMqPassword = isDevelopment
            ? configuration["RabbitMQ:Password"]
            : configuration.GetSecretFromKeyVault("RabbitMQPassword");
        var rabbitMqVHost = isDevelopment
            ? configuration["RabbitMQ:VirtualHost"]
            : configuration.GetSecretFromKeyVault("RabbitMQVHost");

        // RabbitMQ bağlantı ayarlarını oluşturuyoruz
        var rabbitMqFactory = new ConnectionFactory
        {
            HostName = rabbitMqHost,
            Port = int.Parse(rabbitMqPort),
            UserName = rabbitMqUsername,
            Password = rabbitMqPassword,
            VirtualHost = rabbitMqVHost ?? "/",
            RequestedHeartbeat = TimeSpan.FromSeconds(60)
        };

        services.AddHealthChecks()
            // Redis sağlık kontrolü
            .AddRedis(
                redisConnectionString,
                name: "redis",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "redis", "cache" })

            // PostgreSQL sağlık kontrolü
            .AddNpgSql(
                configuration.GetConnectionString("TumdexDb"),
                name: "postgresql",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "postgresql", "database" })

            // RabbitMQ sağlık kontrolü - Doğru parametre isimleriyle
            .AddCheck<RabbitMQHealthCheck>("RabbitMQ",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "rabbitmq", "messagebroker" });
    }
}