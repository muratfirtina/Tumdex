using Application.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Features.Events.User.UserRegister;

public class UserRegisteredEventHandler : INotificationHandler<UserRegisteredEvent>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<UserRegisteredEventHandler> _logger;

    public UserRegisteredEventHandler(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<UserRegisteredEventHandler> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task Handle(UserRegisteredEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Yeni bir scope oluşturuyoruz
            using var scope = _serviceScopeFactory.CreateScope();
            var newsletterService = scope.ServiceProvider.GetRequiredService<INewsletterService>();
            
            // Newsletter kaydını yeni scope'ta gerçekleştiriyoruz
            await newsletterService.HandleUserRegistrationAsync(notification.User);
            
            _logger.LogInformation("Newsletter subscription processed for user {Email}", notification.User.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle user registration for newsletter subscription");
        }
    }
}