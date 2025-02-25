using Application.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Events.User.UserRegister;

public class UserRegisteredEventHandler : INotificationHandler<UserRegisteredEvent>
{
    private readonly INewsletterService _newsletterService;
    private readonly ILogger<UserRegisteredEventHandler> _logger;

    public UserRegisteredEventHandler(
        INewsletterService newsletterService,
        ILogger<UserRegisteredEventHandler> logger)
    {
        _newsletterService = newsletterService;
        _logger = logger;
    }

    public async Task Handle(UserRegisteredEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _newsletterService.HandleUserRegistrationAsync(notification.User);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle user registration for newsletter subscription");
        }
    }
}