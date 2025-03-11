using Application.Abstraction.Services;
using Application.Storage;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Mail;

/// <summary>
/// Hesap işlemleri için e-posta servisi (parola sıfırlama, e-posta doğrulama vb.)
/// </summary>
public class AccountEmailService : BaseEmailService, IAccountEmailService
{
    private readonly IServiceProvider _serviceProvider;
    protected override string ServiceType => "ACCOUNT_EMAIL";
    protected override string ConfigPrefix => "Email:AccountEmail";
    protected override string PasswordSecretName => "AccountEmailPassword";
    

    public AccountEmailService(
        ILogger<AccountEmailService> logger,
        ICacheService cacheService,
        IMetricsService metricsService,
        IStorageService storageService,
        SecretClient secretClient,
        IConfiguration configuration, IServiceProvider serviceProvider)
        : base(logger, cacheService, metricsService, storageService, secretClient, configuration)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task CheckRateLimit(string[] recipients)
    {
        // Ortam kontrolü yapabiliriz
        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        
        // Geliştirme ortamında hız sınırını yükseltebilir veya tamamen kaldırabiliriz
        var hourlyLimit = isDevelopment ? 50 : 10; // Geliştirme ortamında 50, production'da 10
        
        foreach (var recipient in recipients)
        {
            // Günlük ve saatlik limit için farklı anahtarlar kullanalım
            var hourlyRateLimitKey = $"account_email_ratelimit_{recipient}_{DateTime.UtcNow:yyyyMMddHH}";
            var dailyRateLimitKey = $"account_email_daily_limit_{recipient}_{DateTime.UtcNow:yyyyMMdd}";
            
            var hourlyCount = await _cacheService.GetCounterAsync(hourlyRateLimitKey);
            var dailyCount = await _cacheService.GetCounterAsync(dailyRateLimitKey);
            
            // Eğer zaten aynı alıcıya e-posta göndermişsek ve bir önceki gönderimden çok kısa
            // bir süre geçmişse (örn. 10 saniye), hızlı tekrarlanan istekleri engelleyelim
            var recentEmailKey = $"recent_email_{recipient}";
            var recentEmailSent = await _cacheService.TryGetValueAsync<bool>(recentEmailKey);
            
            if (recentEmailSent.success)
            {
                // Son e-postadan sonra en az 10 saniye bekleyelim (throttling)
                _logger.LogWarning($"Too many rapid requests for recipient: {recipient}. Please wait a moment before trying again.");
                throw new Exception($"Please wait a moment before sending another email to: {recipient}");
            }
            
            // Saatlik limit kontrolü
            if (hourlyCount >= hourlyLimit)
            {
                _logger.LogWarning($"Hourly email rate limit ({hourlyLimit}) exceeded for recipient: {recipient}");
                
                // Ne zaman tekrar e-posta gönderebileceklerini belirtelim
                var nextHour = DateTime.UtcNow.AddHours(1).ToString("HH:00");
                throw new Exception($"Email rate limit exceeded for recipient: {recipient}. Please try again after {nextHour} UTC.");
            }
            
            // Günlük limit kontrolü (günde 20 e-posta)
            var dailyLimit = 20;
            if (dailyCount >= dailyLimit)
            {
                _logger.LogWarning($"Daily email rate limit ({dailyLimit}) exceeded for recipient: {recipient}");
                throw new Exception($"Daily email limit exceeded for recipient: {recipient}. Please try again tomorrow.");
            }
            
            // Sayaçları artır
            await _cacheService.IncrementAsync(hourlyRateLimitKey, 1, TimeSpan.FromHours(1));
            await _cacheService.IncrementAsync(dailyRateLimitKey, 1, TimeSpan.FromDays(1));
            
            // Kısa süre için "son gönderilen" işaretini ayarla
            await _cacheService.SetAsync(recentEmailKey, true, TimeSpan.FromSeconds(10));
        }
    }

    protected override string GetFooterMessage()
    {
        return "This is an automated message from the TUMDEX account system.<br>Please do not reply to this email.";
    }

    /// <summary>
    /// Parola sıfırlama e-postası gönderir
    /// </summary>
    public async Task SendPasswordResetEmailAsync(string to, string userId, string resetToken)
    {
        try
        {
            var clientUrl = _configuration["AngularClientUrl"]?.TrimEnd('/') ?? "http://localhost:4200";
            var resetLink = $"{clientUrl}/#/update-password/{userId}/{resetToken}";

            var content = $@"
                <div style='text-align: center;'>
                    <p style='font-size: 16px; color: #333;'>Dear User,</p>
                    <p style='font-size: 16px; color: #333;'>We have received your password reset request.</p>
                    <div style='margin: 30px 0;'>
                        <a href='{resetLink}' style='background-color: #0d6efd; color: white; 
                                  padding: 12px 30px; text-decoration: none; border-radius: 5px;
                                  font-weight: bold; display: inline-block;'>
                            Reset Password
                        </a>
                    </div>
                    <p style='color: #666; font-size: 14px;'>
                        If you didn't initiate this request, please ignore this email.
                    </p>
                    <p style='color: #666; font-size: 14px;'>
                        For your security, this password reset link is valid for 1 hour.
                    </p>
                </div>";

            var emailBody = await BuildEmailTemplate(content, "Password Reset");
            await SendEmailAsync(to, "Password Reset Request", emailBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", to);
            throw;
        }
    }

    /// <summary>
    /// E-posta doğrulama e-postası gönderir
    /// </summary>
    public async Task SendEmailConfirmationAsync(string to, string userId, string confirmationToken)
    {
        try
        {
            var clientUrl = _configuration["AngularClientUrl"]?.TrimEnd('/') ?? "http://localhost:4200";
            var confirmationLink = $"{clientUrl}/confirm-email/{userId}/{confirmationToken}";

            var content = $@"
            <div style='text-align: center;'>
                <p style='font-size: 16px; color: #333;'>Dear User,</p>
                <p style='font-size: 16px; color: #333;'>Thank you for registering. Please confirm your email address to complete your account setup.</p>
                <div style='margin: 30px 0;'>
                    <a href='{confirmationLink}' style='background-color: #0d6efd; color: white; 
                              padding: 12px 30px; text-decoration: none; border-radius: 5px;
                              font-weight: bold; display: inline-block;'>
                        Confirm Email
                    </a>
                </div>
                <p style='color: #666; font-size: 14px;'>
                    If you didn't create this account, please ignore this email.
                </p>
                <p style='color: #666; font-size: 14px;'>
                    For your security, this confirmation link is valid for 24 hours.
                </p>
            </div>";

            var emailBody = await BuildEmailTemplate(content, "Email Confirmation");
            await SendEmailAsync(to, "Please Confirm Your Email", emailBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email confirmation email to {Email}", to);
            throw;
        }
    }
    
    /// <summary>
    /// İki faktörlü kimlik doğrulama kodu gönderir
    /// </summary>
    public async Task SendTwoFactorCodeAsync(string to, string code)
    {
        try
        {
            var content = $@"
            <div style='text-align: center;'>
                <p style='font-size: 16px; color: #333;'>Dear User,</p>
                <p style='font-size: 16px; color: #333;'>Your authentication code is:</p>
                <div style='margin: 30px 0;'>
                    <p style='font-size: 32px; font-weight: bold; letter-spacing: 5px; color: #0d6efd;'>{code}</p>
                </div>
                <p style='color: #666; font-size: 14px;'>
                    This code will expire in 5 minutes.
                </p>
                <p style='color: #666; font-size: 14px;'>
                    If you didn't request this code, please change your password immediately.
                </p>
            </div>";

            var emailBody = await BuildEmailTemplate(content, "Authentication Code");
            await SendEmailAsync(to, "Your Authentication Code", emailBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send 2FA code email to {Email}", to);
            throw;
        }
    }

    /// <summary>
    /// Hesap güvenlik uyarısı gönderir (şüpheli giriş, başarısız oturum açma girişimleri vb.)
    /// </summary>
    public async Task SendSecurityAlertAsync(string to, string alertType, string details, string? ipAddress = null, string? location = null, string? deviceInfo = null)
    {
        try
        {
            var alertTitle = alertType switch
            {
                "login_attempt" => "Unsuccessful Login Attempts",
                "password_changed" => "Password Changed",
                "suspicious_activity" => "Suspicious Account Activity",
                _ => "Security Alert"
            };

            var content = $@"
            <div style='text-align: left;'>
                <p style='font-size: 16px; color: #333;'>Dear User,</p>
                <p style='font-size: 16px; color: #333;'>We've detected a security event on your account.</p>
                
                <div style='margin: 20px 0; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107; border-radius: 3px;'>
                    <h3 style='margin-top: 0; color: #856404;'>{alertTitle}</h3>
                    <p style='color: #333;'>{details}</p>
                    
                    {(!string.IsNullOrEmpty(ipAddress) ? $"<p><strong>IP Address:</strong> {ipAddress}</p>" : "")}
                    {(!string.IsNullOrEmpty(location) ? $"<p><strong>Location:</strong> {location}</p>" : "")}
                    {(!string.IsNullOrEmpty(deviceInfo) ? $"<p><strong>Device:</strong> {deviceInfo}</p>" : "")}
                    <p><strong>Time:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
                </div>
                
                <p style='color: #666;'>If this was you, you can ignore this message.</p>
                <p style='color: #d32f2f; font-weight: bold;'>
                    If you didn't perform this action, please secure your account immediately by changing your password.
                </p>
                
                <div style='margin: 30px 0; text-align: center;'>
                    <a href='{_configuration["AngularClientUrl"]}/reset-password' 
                       style='background-color: #d32f2f; color: white; padding: 12px 20px; 
                              text-decoration: none; border-radius: 5px; font-weight: bold;'>
                        Secure My Account
                    </a>
                </div>
            </div>";

            var emailBody = await BuildEmailTemplate(content, "Security Alert");
            await SendEmailAsync(to, $"Security Alert: {alertTitle}", emailBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send security alert email to {Email}", to);
            throw;
        }
    }
    
    public async Task SendEmailActivationCodeAsync(string to, string userId, string activationCode)
    {
        try
        {
            // Aktivasyon URL'sini oluştur
            using var scope = _serviceProvider.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        
            // Güvenli aktivasyon URL'si oluştur
            string activationUrl = await authService.GenerateActivationUrlAsync(userId, to);
        
            // Hem kod hem de buton içeren e-posta içeriği
            var content = $@"
        <div style='text-align: center;'>
            <p style='font-size: 16px; color: #333;'>Sayın Kullanıcı,</p>
            <p style='font-size: 16px; color: #333;'>Kayıt olduğunuz için teşekkürler. Aktivasyon kodunuz:</p>
            <div style='margin: 30px 0; padding: 20px; background-color: #f8f9fa; border-radius: 10px;'>
                <p style='font-size: 28px; font-weight: bold; letter-spacing: 5px; color: #e53935;'>{activationCode}</p>
            </div>
            <p style='color: #666; font-size: 14px;'>
                Ayrıca aşağıdaki butona tıklayarak da aktivasyon sayfasına gidebilirsiniz:
            </p>
            <a href='{activationUrl}' 
               style='display: inline-block; padding: 12px 24px; background-color: #e53935; color: white; 
                      text-decoration: none; border-radius: 4px; margin: 20px 0;'>
                Aktivasyon Sayfasına Git
            </a>
            <p style='color: #666; font-size: 14px;'>
                Bu aktivasyon kodu 24 saat geçerlidir.
            </p>
        </div>";

            var emailBody = await BuildEmailTemplate(content, "E-posta Doğrulama Kodu");
            await SendEmailAsync(to, "Aktivasyon Kodunuz", emailBody);
        
            _logger.LogInformation("Aktivasyon e-postası gönderildi: {Email}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aktivasyon kodu e-postası gönderilirken hata oluştu: {Email}", to);
            throw;
        }
    }
}