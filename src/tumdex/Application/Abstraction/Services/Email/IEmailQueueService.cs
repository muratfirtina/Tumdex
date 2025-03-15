using Application.Enums;
using Application.Models.Queue.Email;

namespace Application.Abstraction.Services.Email;

public interface IEmailQueueService
{
    /// <summary>
    /// E-posta mesajını kuyruğa ekler
    /// </summary>
    Task QueueEmailAsync(string to, string subject, string body, EmailType type, Dictionary<string, string> metadata = null);
    
    /// <summary>
    /// E-posta doğrulama e-postasını kuyruğa ekler
    /// </summary>
    Task QueueEmailConfirmationAsync(string email, string userId, string token);
    
    /// <summary>
    /// Parola sıfırlama e-postasını kuyruğa ekler
    /// </summary>
    Task QueuePasswordResetAsync(string email, string userId, string token);
    
    /// <summary>
    /// İletişim formu bildirim e-postasını kuyruğa ekler
    /// </summary>
    Task QueueContactFormEmailAsync(string name, string email, string subject, string message);
    
    /// <summary>
    /// Kuyruktaki e-postaları işler
    /// </summary>
    Task ProcessQueuedEmailsAsync(CancellationToken cancellationToken = default);
}