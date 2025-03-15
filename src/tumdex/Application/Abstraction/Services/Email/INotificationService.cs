namespace Application.Abstraction.Services.Email;

public interface INotificationService
{
    Task SendEmailAsync(string subject, string message);
    Task SendSlackMessageAsync(string message);
    Task SendTeamsMessageAsync(string message);
}