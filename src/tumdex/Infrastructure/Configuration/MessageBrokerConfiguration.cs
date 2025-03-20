using Infrastructure.Consumers;
using Infrastructure.Consumers.EmailConsumers;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Infrastructure.Configuration;

public static class MessageBrokerConfiguration
{
    public static IServiceCollection AddMessageBrokerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // MassTransit servisini ekliyoruz ve yapılandırıyoruz
        services.AddMassTransit(x =>
        {
            // Consumer'ları kaydediyoruz
            RegisterConsumers(x);

            // RabbitMQ'yu yapılandırıyoruz
            x.UsingRabbitMq((context, cfg) =>
            {
                var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

                // Ortama göre yapılandırma seçimi
                if (isDevelopment)
                {
                    ConfigureDevelopmentEnvironment(cfg, configuration);
                }
                else
                {
                    ConfigureProductionEnvironment(cfg, configuration);
                }

                // Kuyrukları yapılandırıyoruz
                ConfigureQueues(cfg, context);

                // Global yeniden deneme politikası
                ConfigureGlobalRetryPolicy(cfg);
            });
        });


        return services;
    }
    

    private static void RegisterConsumers(IBusRegistrationConfigurator configurator)
    {
        // Event consumer'larını kaydediyoruz
        configurator.AddConsumer<OrderCreatedEventConsumer>();
        configurator.AddConsumer<CartUpdatedEventConsumer>();
        configurator.AddConsumer<StockUpdatedEventConsumer>();
        configurator.AddConsumer<OrderUpdatedEventConsumer>();
        
        configurator.AddConsumer<SendActivationCodeConsumer>();
        configurator.AddConsumer<SendActivationEmailConsumer>();
    }

    private static void ConfigureDevelopmentEnvironment(
        IRabbitMqBusFactoryConfigurator cfg,
        IConfiguration configuration)
    {
        // Hem Development ortamında hem de Production ortamında Key Vault'tan bağlantı bilgilerini öncelikle deneyin
        string host, vhost, username, password;
        int port;

        try
        {
            // Önce Key Vault'tan almayı dene
            host = configuration.GetSecretFromKeyVault("RabbitMQHost");
            port = int.Parse(configuration.GetSecretFromKeyVault("RabbitMQPort") ?? "5672");
            username = configuration.GetSecretFromKeyVault("RabbitMQUsername");
            password = configuration.GetSecretFromKeyVault("RabbitMQPassword");
            vhost = configuration.GetSecretFromKeyVault("RabbitMQVHost") ?? "/";

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("Key Vault credentials are incomplete");
            }
        }
        catch (Exception)
        {
            // Key Vault'tan alınamazsa yapılandırma dosyasından veya ortam değişkenlerinden al
            host = configuration["RABBITMQ_HOST"] ?? "localhost";
            port = int.Parse(configuration["RABBITMQ_PORT"] ?? "5672");
            username = configuration["RABBITMQ_USERNAME"] ?? "admin";
            password = configuration["RABBITMQ_PASSWORD"] ?? "123456";
            vhost = configuration["RABBITMQ_VHOST"] ?? "/";
        }

        // Host yapılandırması
        cfg.Host(new Uri($"rabbitmq://{host}:{port}/{vhost}"), h =>
        {
            h.Username(username);
            h.Password(password);
        });
    }

    private static void ConfigureProductionEnvironment(
        IRabbitMqBusFactoryConfigurator cfg,
        IConfiguration configuration)
    {
        // Production ortamı için KeyVault'tan bağlantı bilgilerini okuma
        var host = configuration.GetSecretFromKeyVault("RabbitMQHost")
                   ?? throw new InvalidOperationException("RabbitMQ host not found");
        var port = int.Parse(configuration.GetSecretFromKeyVault("RabbitMQPort")
                             ?? throw new InvalidOperationException("RabbitMQ port not found"));
        var username = configuration.GetSecretFromKeyVault("RabbitMQUsername")
                       ?? throw new InvalidOperationException("RabbitMQ username not found");
        var password = configuration.GetSecretFromKeyVault("RabbitMQPassword")
                       ?? throw new InvalidOperationException("RabbitMQ password not found");
        var vhost = configuration.GetSecretFromKeyVault("RabbitMQVHost")
                    ?? throw new InvalidOperationException("RabbitMQ vhost not found");

        // Host yapılandırması
        cfg.Host(new Uri($"rabbitmq://{host}:{port}/{vhost}"), h =>
        {
            h.Username(username);
            h.Password(password);
        });
    }

    private static void ConfigureQueues(
        IRabbitMqBusFactoryConfigurator cfg,
        IBusRegistrationContext context)
    {
        // Sipariş oluşturma kuyruğu
        cfg.ReceiveEndpoint("order-created-queue", e =>
        {
            e.ConfigureConsumer<OrderCreatedEventConsumer>(context);
            ConfigureEndpoint(e);
        });

        // Sepet güncelleme kuyruğu
        cfg.ReceiveEndpoint("cart-updated-queue", e =>
        {
            e.ConfigureConsumer<CartUpdatedEventConsumer>(context);
            ConfigureEndpoint(e);
        });

        // Stok güncelleme kuyruğu
        cfg.ReceiveEndpoint("stock-updated-queue", e =>
        {
            e.ConfigureConsumer<StockUpdatedEventConsumer>(context);
            ConfigureEndpoint(e);
        });

        // Sipariş güncelleme kuyruğu
        cfg.ReceiveEndpoint("order-updated-queue", e =>
        {
            e.ConfigureConsumer<OrderUpdatedEventConsumer>(context);
            ConfigureEndpoint(e);
        });
        
        cfg.ReceiveEndpoint("email-activation-code-queue", e =>
        {
            e.ConfigureConsumer<SendActivationCodeConsumer>(context);
            ConfigureEndpoint(e);
            
            // x-expires değerini doğrudan ayarla (milisaniye cinsinden)
            e.SetQueueArgument("x-expires", 600000); // 10 dakika
        });
    
        cfg.ReceiveEndpoint("email-resend-activation-code-queue", e =>
        {
            e.ConfigureConsumer<SendActivationEmailConsumer>(context);
            ConfigureEndpoint(e);
            
            // x-expires değerini doğrudan ayarla (milisaniye cinsinden)
            e.SetQueueArgument("x-expires", 600000); // 10 dakika
        });
    }

    private static void ConfigureEndpoint(IRabbitMqReceiveEndpointConfigurator endpoint)
    {
        // Yeniden deneme politikası
        endpoint.UseMessageRetry(r => r
            .Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));

        // Prefetch count ayarı
        endpoint.PrefetchCount = 16;

        // Devre kesici (Circuit breaker)
        endpoint.UseCircuitBreaker(cb =>
        {
            cb.TrackingPeriod = TimeSpan.FromMinutes(1);
            cb.TripThreshold = 15;
            cb.ActiveThreshold = 10;
            cb.ResetInterval = TimeSpan.FromMinutes(5);
        });

        // Kuyruk ömrü (TTL) - QueueExpiration özelliği yerine x-expires kullan
        // endpoint.QueueExpiration = TimeSpan.FromMinutes(10);
        endpoint.SetQueueArgument("x-expires", 600000); // 10 dakika

        // Mesaj ömrü (Message TTL)
        endpoint.SetQueueArgument("x-message-ttl", 300000); // 5 dakika

        // Kuyruk boyutu sınırı
        endpoint.SetQueueArgument("x-max-length", 1000);

        // Dead Letter Exchange (DLX)
        endpoint.SetQueueArgument("x-dead-letter-exchange", "dead-letter-exchange");

        // Mesaj öncelikleri
        endpoint.SetQueueArgument("x-max-priority", 10);

        // Lazy Queue
        endpoint.SetQueueArgument("x-queue-mode", "lazy");

        // Mesaj yeniden gönderme
        endpoint.UseDelayedRedelivery(r => r.Interval(3, TimeSpan.FromSeconds(10)));
    }

    private static void ConfigureGlobalRetryPolicy(IRabbitMqBusFactoryConfigurator cfg)
    {
        // Global yeniden deneme politikası
        cfg.UseMessageRetry(r => r.Immediate(5));
    }
}