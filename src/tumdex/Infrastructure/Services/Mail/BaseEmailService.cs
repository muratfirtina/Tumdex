using System.Text;
using Application.Abstraction.Services;
using Application.Storage;
using Azure.Security.KeyVault.Secrets;
using Ganss.Xss;
using Infrastructure.Services.Mail.Models;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Infrastructure.Services.Mail;

/// <summary>
/// Tüm e-posta servislerinin ortak işlevlerini içeren temel sınıf
/// </summary>
public abstract class BaseEmailService : IEmailService
{
    protected readonly ILogger _logger;
    protected readonly ICacheService _cacheService;
    protected readonly IMetricsService _metricsService;
    protected readonly IStorageService _storageService;
    protected readonly SecretClient _secretClient;
    protected readonly IConfiguration _configuration;
    protected readonly SemaphoreSlim _throttler;
    protected EmailConfig? _emailConfig;
    
    // Email service tipi (log ve metrikler için)
    protected abstract string ServiceType { get; }
    
    // Email configuration prefix (appsettings.json'daki bölüm adı)
    protected abstract string ConfigPrefix { get; }
    
    // Email secret name (Key Vault'taki şifre anahtar adı)
    protected abstract string PasswordSecretName { get; }

    protected BaseEmailService(
        ILogger logger,
        ICacheService cacheService,
        IMetricsService metricsService,
        IStorageService storageService,
        SecretClient secretClient,
        IConfiguration configuration,
        int maxConcurrentEmails = 5)
    {
        _logger = logger;
        _cacheService = cacheService;
        _metricsService = metricsService;
        _storageService = storageService;
        _secretClient = secretClient;
        _configuration = configuration;
        _throttler = new SemaphoreSlim(maxConcurrentEmails, maxConcurrentEmails);
    }

    protected async Task InitializeAsync()
    {
        if (_emailConfig != null) return;

        try
        {
            // Password'u Key Vault'tan al, diğer bilgileri appsettings.json'dan al
            var password = await _secretClient.GetSecretAsync(PasswordSecretName);
            
            _emailConfig = new EmailConfig
            {
                FromName = _configuration[$"{ConfigPrefix}:FromName"],
                FromAddress = _configuration[$"{ConfigPrefix}:FromAddress"],
                Server = _configuration[$"{ConfigPrefix}:SmtpServer"],
                Port = int.Parse(_configuration[$"{ConfigPrefix}:SmtpPort"]),
                Username = _configuration[$"{ConfigPrefix}:Username"],
                Password = password.Value.Value,
                UseSsl = bool.Parse(_configuration[$"{ConfigPrefix}:SmtpUseSsl"]),
                RequireTls = bool.Parse(_configuration[$"{ConfigPrefix}:SmtpRequireTls"]),
                AllowInvalidCert = bool.Parse(_configuration[$"{ConfigPrefix}:SmtpAllowInvalidCert"])
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize {ServiceType} email configuration", ServiceType);
            throw new InvalidOperationException($"{ServiceType} email service configuration failed", ex);
        }
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isBodyHtml = true)
    {
        await SendEmailAsync(new[] { to }, subject, body, isBodyHtml);
    }

    public virtual async Task SendEmailAsync(string[] tos, string subject, string body, bool isBodyHtml = true)
    {
        try
        {
            await InitializeAsync();

            // Rate limit kontrolü - alt sınıflar kendi kurallarını uygulamak için override edebilir
            await CheckRateLimit(tos);

            // Throttling
            if (!await _throttler.WaitAsync(TimeSpan.FromSeconds(30)))
                throw new Exception("Email sending is currently throttled");

            try
            {
                var sanitizer = new HtmlSanitizer();
                var sanitizedBody = sanitizer.Sanitize(body);

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailConfig.FromName, _emailConfig.FromAddress));
                message.To.AddRange(tos.Select(x => new MailboxAddress("", x)));
                message.Subject = subject;
                message.Body = new TextPart(isBodyHtml ? "html" : "plain") { Text = sanitizedBody };

                using var client = new MailKit.Net.Smtp.SmtpClient();
                try
                {
                    // Port bazlı güvenlik ayarları
                    SecureSocketOptions secureSocketOptions;
                    if (_emailConfig.Port == 587)
                    {
                        secureSocketOptions = SecureSocketOptions.StartTls;
                        _logger.LogInformation("Using STARTTLS for port 587");
                    }
                    else if (_emailConfig.Port == 465)
                    {
                        secureSocketOptions = SecureSocketOptions.SslOnConnect;
                        _logger.LogInformation("Using SSL/TLS for port 465");
                    }
                    else
                    {
                        secureSocketOptions = (_emailConfig.UseSsl, _emailConfig.RequireTls) switch
                        {
                            (true, true) => SecureSocketOptions.SslOnConnect,
                            (true, false) => SecureSocketOptions.StartTls,
                            (false, true) => SecureSocketOptions.StartTlsWhenAvailable,
                            _ => SecureSocketOptions.None
                        };
                        _logger.LogInformation($"Using {secureSocketOptions} for port {_emailConfig.Port}");
                    }

                    if (_emailConfig.AllowInvalidCert)
                    {
                        client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                        _logger.LogWarning("SSL certificate validation is disabled");
                    }

                    _logger.LogInformation("Connecting to SMTP server {Server}:{Port} with {Options}", 
                        _emailConfig.Server, _emailConfig.Port, secureSocketOptions);

                    await client.ConnectAsync(
                        _emailConfig.Server,
                        _emailConfig.Port,
                        secureSocketOptions);

                    if (client.IsConnected)
                    {
                        if (client.IsSecure)
                        {
                            _logger.LogInformation("Connected to SMTP server with secure connection (SSL/TLS)");
                        }
                        else
                        {
                            _logger.LogWarning("Connected to SMTP server without secure connection");
                        }
                    }

                    await client.AuthenticateAsync(
                        _emailConfig.Username,
                        _emailConfig.Password);

                    await client.SendAsync(message);
                    
                    _logger.LogInformation("{ServiceType} email sent successfully to {Recipients}", 
                        ServiceType, string.Join(", ", tos));
                    _metricsService.IncrementTotalRequests(ServiceType, "SEND", "200");
                    
                    // Yapılandırıldıysa e-postalar arasında gecikme uygula
                    var delayMs = _configuration.GetValue<int>($"{ConfigPrefix}:Throttling:DelayBetweenEmails", 0);
                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs);
                    }
                }
                finally
                {
                    await client.DisconnectAsync(true);
                }
            }
            finally
            {
                _throttler.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send {ServiceType} email", ServiceType);
            _metricsService.IncrementTotalRequests(ServiceType, "SEND", "500");
            throw;
        }
    }

    // Alt sınıflar kendi hız sınırlama kurallarını uygulayabilir
    protected virtual async Task CheckRateLimit(string[] recipients)
    {
        foreach (var recipient in recipients)
        {
            var rateLimitKey = $"{ServiceType.ToLower()}_email_ratelimit_{recipient}_{DateTime.UtcNow:yyyyMMddHH}";
            var count = await _cacheService.GetCounterAsync(rateLimitKey);

            if (count >= 10) // Varsayılan saat başı 10 e-posta limiti
                throw new Exception($"Email rate limit exceeded for recipient: {recipient}");

            await _cacheService.IncrementAsync(rateLimitKey, 1, TimeSpan.FromHours(1));
        }
    }

    public async Task<string> BuildEmailTemplate(string content, string title = "")
    {
        await InitializeAsync();

        var logoUrl = _storageService.GetCompanyLogoUrl();
        var titleColor = GetEmailTitleColor(); // Alt sınıflar için özelleştirilebilir renk

        return $@"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
        </head>
        <body style='margin: 0; padding: 0; background-color: #f6f9fc; font-family: Arial, sans-serif;'>
            <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px;'>
                <div style='text-align: center; padding: 20px; background-color: #e0e0e0; margin-bottom: 20px;'>
                    <img src='{logoUrl}' alt='Company Logo' style='max-width: 200px;'/>
                </div>
                
                {(string.IsNullOrEmpty(title) ? "" : $"<h1 style='color: {titleColor}; text-align: center; margin-bottom: 30px;'>{title}</h1>")}

                <div style='padding: 20px; line-height: 1.6;'>
                    {content}
                </div>

                <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; text-align: center;'>
                    {await BuildFooterContent()}
                </div>
            </div>
        </body>
        </html>";
    }

    // Her servis için özelleştirilebilen başlık rengi
    protected virtual string GetEmailTitleColor()
    {
        return "#0d6efd"; // Default mavi renk
    }

    // Ortak footer oluşturma metodu
    protected async Task<string> BuildFooterContent()
    {
        var sb = new StringBuilder();

        try
        {
            // Şirket bilgilerini yapılandırmadan al
            var companyName = _configuration["CompanyInfo:Name"];
            var companyAddress = _configuration["CompanyInfo:Address"];
            var companyPhone = _configuration["CompanyInfo:Phone"];
            var companyEmail = _configuration["CompanyInfo:Email"];
            var companyLinkedIn = _configuration["CompanyInfo:SocialMedia:LinkedIn"];
            var companyWhatsapp = _configuration["CompanyInfo:SocialMedia:Whatsapp"];

            // Servis tipine göre özel footer mesajı
            var footerMessage = GetFooterMessage();
            sb.AppendLine($@"<p style='color: #666; font-size: 12px;'>{footerMessage}</p>");

            // Sosyal medya linkleri
            sb.AppendLine(@"<div style='margin: 15px 0;'>");
            if (!string.IsNullOrEmpty(companyLinkedIn))
            {
                sb.AppendLine($@"<a href='{companyLinkedIn}' style='margin: 0 10px; text-decoration: none;'>
                    <img src='http://localhost:4200/assets/icon/linkedin.png' alt='LinkedIn' style='width: 24px; height: 24px;' />
                </a>");
            }
            if (!string.IsNullOrEmpty(companyWhatsapp))
            {
                sb.AppendLine($@"<a href='{companyWhatsapp}' style='margin: 0 10px; text-decoration: none;'>
                    <img src='http://localhost:4200/assets/whatsapp.webp' alt='WhatsApp' style='width: 24px; height: 24px;' />
                </a>");
            }
            sb.AppendLine("</div>");

            // Şirket bilgileri
            sb.AppendLine($@"
            <div style='margin-top: 20px; color: #666; font-size: 12px;'>
                <p>{companyName}</p>
                <p>{companyAddress}</p>
                <p>Tel: {companyPhone} | Email: {companyEmail}</p>
                <p>&copy; {DateTime.Now.Year} {companyName}. All rights reserved.</p>
            </div>");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building footer content");
            // Hata durumunda basit footer
            sb.Clear();
            sb.AppendLine(@"<p style='color: #666; font-size: 12px; text-align: center;'>
                &copy; " + DateTime.Now.Year + @" All rights reserved.
            </p>");
        }

        return sb.ToString();
    }

    // Alt sınıflar için özelleştirilebilir footer mesajı
    protected virtual string GetFooterMessage()
    {
        return "This is an automated message.<br>Please do not reply to this email.";
    }
}