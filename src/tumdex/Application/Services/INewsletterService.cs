using Domain;
using Domain.Identity;

namespace Application.Services;

public interface INewsletterService
{
    Task<Newsletter> SubscribeAsync(string email, string? source = null, string? userId = null);
    Task<Newsletter> UnsubscribeAsync(string email);
    Task SendMonthlyNewsletterAsync();
    Task HandleUserRegistrationAsync(AppUser user);
}