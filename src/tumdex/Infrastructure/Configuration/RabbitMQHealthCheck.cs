using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Infrastructure.Configuration;

public class RabbitMQHealthCheck : IHealthCheck
    {
        private readonly IConfiguration _configuration;

        public RabbitMQHealthCheck(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _configuration.GetSecretFromKeyVault("RabbitMQHost"),
                    Port = int.Parse(_configuration.GetSecretFromKeyVault("RabbitMQPort")),
                    UserName = _configuration.GetSecretFromKeyVault("RabbitMQUsername"),
                    Password = _configuration.GetSecretFromKeyVault("RabbitMQPassword"),
                    VirtualHost = _configuration.GetSecretFromKeyVault("RabbitMQVHost"),
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(3),
                    RequestedHeartbeat = TimeSpan.FromSeconds(30),
                    AutomaticRecoveryEnabled = true
                };

                using var connection = await factory.CreateConnectionAsync(cancellationToken);
                using var channel = await connection.CreateChannelAsync();

                // Exchange ve queue kontrolleri
                await channel.ExchangeDeclarePassiveAsync("order-events");
                await channel.QueueDeclarePassiveAsync("order-created-queue");

                return HealthCheckResult.Healthy(
                    "RabbitMQ bağlantısı ve temel yapılandırması başarılı.",
                    new Dictionary<string, object>
                    {
                        { "connection", $"{factory.HostName}:{factory.Port}" },
                        { "virtualHost", factory.VirtualHost },
                        { "exchangeExists", true },
                        { "queueExists", true }
                    });
            }
            catch (Exception ex)
            {
                var errorMessage = ex switch
                {
                    BrokerUnreachableException => "RabbitMQ sunucusuna erişilemiyor.",
                    AuthenticationFailureException => "RabbitMQ kimlik doğrulama hatası.",
                    OperationInterruptedException => "RabbitMQ operasyonu kesintiye uğradı.",
                    _ => "RabbitMQ bağlantısında beklenmeyen bir hata oluştu."
                };

                return HealthCheckResult.Unhealthy(
                    errorMessage,
                    ex,
                    new Dictionary<string, object>
                    {
                        { "errorType", ex.GetType().Name },
                        { "errorDetails", ex.Message }
                    });
            }
        }
    }