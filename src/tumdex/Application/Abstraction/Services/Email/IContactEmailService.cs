namespace Application.Abstraction.Services.Email;

/// <summary>
/// İletişim formu e-posta servisi interface'i
/// </summary>
public interface IContactEmailService : IEmailService
{
    /// <summary>
    /// İletişim formu mesajını admin e-posta adresine iletir
    /// </summary>
    Task SendContactFormEmailAsync(string name, string email, string subject, string message);
}