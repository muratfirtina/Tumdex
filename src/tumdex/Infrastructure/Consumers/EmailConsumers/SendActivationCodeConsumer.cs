using Application.Abstraction.Services.Email;
using Application.Messages.EmailMessages;
using MassTransit;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Infrastructure.Consumers.EmailConsumers;

public class SendActivationCodeConsumer : IConsumer<SendActivationCodeCommand>
{
    private readonly ILogger<SendActivationCodeConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;

    public SendActivationCodeConsumer(
        ILogger<SendActivationCodeConsumer> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task Consume(ConsumeContext<SendActivationCodeCommand> context)
    {
        var message = context.Message;
        _logger.LogInformation("Consuming SendActivationCodeCommand for user {UserId} and email {Email}", 
            message.UserId, message.Email);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var accountEmailService = scope.ServiceProvider.GetRequiredService<IAccountEmailService>();
            var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
            
            // Önce Redis'ten mevcut kodu kontrol et
            string cacheKey = $"email_activation_code_{message.UserId}";
            string codeCacheJson = await cache.GetStringAsync(cacheKey);
            
            string activationCode = message.ActivationCode; // Varsayılan olarak gelen kodu kullan
            
            if (!string.IsNullOrEmpty(codeCacheJson))
            {
                try
                {
                    // Önbellekteki kodu kullan
                    var codeData = JsonSerializer.Deserialize<JsonElement>(codeCacheJson);
                    activationCode = codeData.GetProperty("Code").GetString();
                    
                    _logger.LogInformation("Using Redis activation code {Code} for user {UserId}", 
                        activationCode, message.UserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing cached activation code");
                    // Hata durumunda mesajdaki kodu kullan
                }
            }
            else
            {
                // Redis'te kod yok, gelen kodu kaydet
                var codeData = new
                {
                    Code = activationCode,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    AttemptCount = 0,
                    EmailStatus = "pending"
                };
                
                await cache.SetStringAsync(cacheKey, 
                    JsonSerializer.Serialize(codeData),
                    new DistributedCacheEntryOptions 
                    { 
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) 
                    });
                
                _logger.LogInformation("Saved new activation code {Code} to Redis for user {UserId}", 
                    activationCode, message.UserId);
            }

            // Aktivasyon kodunu gönder
            await accountEmailService.SendEmailActivationCodeAsync(
                message.Email, 
                message.UserId, 
                activationCode);

            _logger.LogInformation("Activation code email sent successfully to {Email} with code {Code}", 
                message.Email, activationCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending activation code email to {Email}", message.Email);
            throw;
        }
    }
}