using Application.Abstraction.Services.Email;
using Application.Messages.EmailMessages;
using Domain.Identity;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Infrastructure.Consumers.EmailConsumers;

public class SendActivationEmailConsumer : IConsumer<SendActivationEmailMessage>
{
    private readonly ILogger<SendActivationEmailConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;

    public SendActivationEmailConsumer(
        ILogger<SendActivationEmailConsumer> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task Consume(ConsumeContext<SendActivationEmailMessage> context)
    {
        var message = context.Message;
        _logger.LogInformation("Consuming SendActivationEmailMessage for email {Email}", message.Email);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var accountEmailService = scope.ServiceProvider.GetRequiredService<IAccountEmailService>();
            var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            
            // Kullanıcı ID kontrolü
            string userId = message.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                var user = await userManager.FindByEmailAsync(message.Email);
                if (user == null)
                {
                    _logger.LogWarning("User with email {Email} not found when sending activation code", message.Email);
                    throw new Exception($"User not found with email: {message.Email}");
                }
                userId = user.Id;
            }
            
            // Redis'ten mevcut aktivasyon kodunu kontrol et
            string cacheKey = $"email_activation_code_{userId}";
            string codeCacheJson = await cache.GetStringAsync(cacheKey);
            
            string activationCode = message.ActivationCode; // Varsayılan olarak gelen kodu kullan
            
            if (!string.IsNullOrEmpty(codeCacheJson))
            {
                try
                {
                    // Önbellekteki kodu kullan
                    var codeData = JsonSerializer.Deserialize<JsonElement>(codeCacheJson);
                    activationCode = codeData.GetProperty("Code").GetString();
                    
                    _logger.LogInformation("Using existing activation code from Redis: {Code} for user {UserId}", 
                        activationCode, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving cached activation code for {UserId}", userId);
                    // Hata durumunda mesajdaki kodu kaydet
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
                    activationCode, userId);
            }
            
            // Aktivasyon kodunu gönder
            await accountEmailService.SendEmailActivationCodeAsync(
                message.Email, 
                userId, 
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