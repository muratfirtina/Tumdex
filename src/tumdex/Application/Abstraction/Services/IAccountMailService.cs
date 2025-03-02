namespace Application.Abstraction.Services;

/// <summary>
/// Kullanıcı hesap işlemleri için e-posta gönderim servisi.
/// </summary>
public interface IAccountEmailService : IEmailService
{
    /// <summary>
    /// Şifre sıfırlama e-postası gönderir.
    /// </summary>
    Task SendPasswordResetEmailAsync(string to, string userId, string resetToken);
    
    /// <summary>
    /// E-posta doğrulama e-postası gönderir.
    /// </summary>
    Task SendEmailConfirmationAsync(string to, string userId, string confirmationToken);
}