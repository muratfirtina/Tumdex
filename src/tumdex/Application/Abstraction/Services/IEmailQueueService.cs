using Application.Queue.Email;

namespace Application.Abstraction.Services;

public interface IEmailQueueService
{
    Task QueueEmailAsync(string to, string subject, string body, EmailType type, Dictionary<string, string> metadata = null);
    Task QueueEmailConfirmationAsync(string email, string userId, string token);
    Task QueuePasswordResetAsync(string email, string userId, string token);
    Task ProcessQueuedEmailsAsync(CancellationToken cancellationToken = default);
}