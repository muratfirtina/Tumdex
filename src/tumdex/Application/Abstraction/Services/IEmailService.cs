namespace Application.Abstraction.Services;

/// <summary>
/// Tüm e-posta servislerinin uygulayacağı temel interface.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Tek bir alıcıya e-posta gönderir.
    /// </summary>
    Task SendEmailAsync(string to, string subject, string body, bool isBodyHtml = true);
    
    /// <summary>
    /// Birden fazla alıcıya e-posta gönderir.
    /// </summary>
    Task SendEmailAsync(string[] tos, string subject, string body, bool isBodyHtml = true);
    
    /// <summary>
    /// E-posta şablonu oluşturur.
    /// </summary>
    Task<string> BuildEmailTemplate(string content, string title = "");
}