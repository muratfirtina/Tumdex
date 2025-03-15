using Application.Abstraction.Services;
using Application.Abstraction.Services.Email;
using Application.Abstraction.Services.Utilities;
using Application.Storage;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Mail;

/// <summary>
/// İletişim formu mesajlarını yöneticilere ileten e-posta servisi
/// </summary>
public class ContactEmailService : BaseEmailService, IContactEmailService
{
    protected override string ServiceType => "CONTACT_EMAIL";
    protected override string ConfigPrefix => "Email:ContactEmail";
    protected override string PasswordSecretName => "ContactEmailPassword";

    public ContactEmailService(
        ILogger<ContactEmailService> logger,
        ICacheService cacheService,
        IMetricsService metricsService,
        IStorageService storageService,
        SecretClient secretClient,
        IConfiguration configuration)
        : base(logger, cacheService, metricsService, storageService, secretClient, configuration)
    {
    }

    protected override async Task CheckRateLimit(string[] recipients)
    {
        // İletişim formu için hız sınırlaması
        // IP adresine göre hız sınırlaması yapılabilir, ancak burada alıcı e-posta adresi (admin) sabittir
        // Bu nedenle, form gönderilerini IP bazlı veya toplam olarak sınırlandırmak daha mantıklı olabilir
        
        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        var hourlyLimit = isDevelopment ? 30 : 15; // Geliştirme ortamında 30, üretimde 15
        
        // Saatlik toplam form gönderimi için bir anahtar
        var hourlyRateLimitKey = $"contact_form_submissions_{DateTime.UtcNow:yyyyMMddHH}";
        var dailyRateLimitKey = $"contact_form_daily_{DateTime.UtcNow:yyyyMMdd}";
        
        var hourlyCount = await _cacheService.GetCounterAsync(hourlyRateLimitKey);
        var dailyCount = await _cacheService.GetCounterAsync(dailyRateLimitKey);
        
        // Saatlik limit kontrolü
        if (hourlyCount >= hourlyLimit)
        {
            _logger.LogWarning($"Hourly contact form submission limit ({hourlyLimit}) exceeded");
            throw new Exception($"Contact form submission limit exceeded. Please try again later.");
        }
        
        // Günlük limit kontrolü (günde 100 form)
        var dailyLimit = 100;
        if (dailyCount >= dailyLimit)
        {
            _logger.LogWarning($"Daily contact form submission limit ({dailyLimit}) exceeded");
            throw new Exception($"Daily contact form limit exceeded. Please try again tomorrow.");
        }
        
        // Sayaçları artır
        await _cacheService.IncrementAsync(hourlyRateLimitKey, 1, TimeSpan.FromHours(1));
        await _cacheService.IncrementAsync(dailyRateLimitKey, 1, TimeSpan.FromDays(1));
    }

    protected override string GetEmailTitleColor()
    {
        return "#28a745"; // İletişim formları için yeşil renk
    }

    protected override string GetFooterMessage()
    {
        return "This is an automated message from the contact form system.<br>To reply, please use your email client to respond directly to the sender.";
    }

    /// <summary>
    /// İletişim formu mesajını admin e-posta adresine iletir
    /// </summary>
    public async Task SendContactFormEmailAsync(string name, string email, string subject, string message)
    {
        try
        {
            // Önce email konfigürasyonunu başlat
            await InitializeAsync();
            
            // Admin e-posta adresi, appsettings.json'dan alınır
            var adminEmail = _configuration[$"{ConfigPrefix}:ToAddress"];
            
            if (string.IsNullOrEmpty(adminEmail))
            {
                _logger.LogWarning("Admin email address is not configured in appsettings.json. Using FromAddress instead.");
                // Alıcı adresi bulunamadıysa, gönderen adresini kullan (kendisine göndersin)
                adminEmail = _emailConfig?.FromAddress;
                
                // Eğer FromAddress de null ise, varsayılan değer kullan
                if (string.IsNullOrEmpty(adminEmail))
                {
                    adminEmail = "contact@tumdex.com";
                    _logger.LogWarning("Using default email address: {DefaultEmail}", adminEmail);
                }
            }

            // İletişim formu mesajı için e-posta içeriği
            var content = $@"
                <div>
                    <p style='font-size: 16px; color: #333;'>A new contact message has been submitted through the website:</p>
                    
                    <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                        <tr>
                            <th style='text-align: left; padding: 10px; border-bottom: 1px solid #ddd; width: 100px;'>Name:</th>
                            <td style='padding: 10px; border-bottom: 1px solid #ddd;'>{name}</td>
                        </tr>
                        <tr>
                            <th style='text-align: left; padding: 10px; border-bottom: 1px solid #ddd;'>Email:</th>
                            <td style='padding: 10px; border-bottom: 1px solid #ddd;'>
                                <a href='mailto:{email}'>{email}</a>
                            </td>
                        </tr>
                        <tr>
                            <th style='text-align: left; padding: 10px; border-bottom: 1px solid #ddd;'>Subject:</th>
                            <td style='padding: 10px; border-bottom: 1px solid #ddd;'>{subject}</td>
                        </tr>
                    </table>
                    
                    <div style='background-color: #f9f9f9; padding: 15px; border-left: 4px solid #28a745; margin: 20px 0;'>
                        <p style='margin: 0;'>{message.Replace("\n", "<br>")}</p>
                    </div>
                    
                    <p style='color: #666; font-size: 14px;'>
                        This message was sent on {DateTime.UtcNow:yyyy-MM-dd} at {DateTime.UtcNow:HH:mm:ss} UTC.
                    </p>
                </div>";

            // E-posta şablonunu oluştur
            var emailBody = await BuildEmailTemplate(content, "New Contact Form Submission");
            
            // E-postaya yanıt vermek için, Reply-To header'ını kullanıcının e-posta adresi olarak ayarla
            // Bu, BaseEmailService'de desteklenmiyorsa, kullanıcının e-posta adresini subject içine ekleyebiliriz
            var emailSubject = $"Contact Form: {subject} [From: {name}]";
            
            // Admin e-posta adresine gönder
            await SendEmailAsync(adminEmail, emailSubject, emailBody);
            
            _logger.LogInformation("Contact form email sent successfully from {Name} <{Email}>", name, email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send contact form email from {Name} <{Email}>", name, email);
            throw;
        }
    }
}