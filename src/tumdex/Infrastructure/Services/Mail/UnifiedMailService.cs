using System.Text;
using Application.Abstraction.Services;
using Application.Features.Orders.Dtos;
using Application.Features.UserAddresses.Dtos;
using Application.Storage;
using Domain.Enum;
using Ganss.Xss;
using Infrastructure.Configuration;
using Infrastructure.Enums;
using Infrastructure.Services.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Security.KeyVault.Secrets;
using Infrastructure.Consumers;
using MailKit.Security;
using MimeKit;

namespace Infrastructure.Services.Mail;

public class UnifiedMailService : IMailService
{
    private readonly ILogger<UnifiedMailService> _logger;
    private readonly ICacheService _cacheService;
    private readonly IMetricsService _metricsService;
    private readonly IStorageService _storageService;
    private readonly IOptionsSnapshot<StorageSettings> _storageSettings;
    private readonly SecretClient _secretClient;
    private readonly SemaphoreSlim _throttler;
    private EmailProvider _emailProvider;
    private EmailConfig? _emailConfig;

    public UnifiedMailService(
        ILogger<UnifiedMailService> logger,
        ICacheService cacheService,
        IMetricsService metricsService,
        IStorageService storageService,
        IOptionsSnapshot<StorageSettings> storageSettings,
        SecretClient secretClient)
    {
        _logger = logger;
        _cacheService = cacheService;
        _metricsService = metricsService;
        _storageService = storageService;
        _storageSettings = storageSettings;
        _secretClient = secretClient;
        _throttler = new SemaphoreSlim(5, 5);
    }

    private async Task InitializeAsync()
    {
        if (_emailConfig != null) return;

        try
        {
            // Email provider'ı al
            var providerResponse = await _secretClient.GetSecretAsync("EmailProvider");
            _emailProvider = Enum.Parse<EmailProvider>(providerResponse.Value.Value);

            // Provider'a özgü ayarları al
            _emailConfig = new EmailConfig
            {
                Provider = _emailProvider,
                FromName = (await _secretClient.GetSecretAsync("CustomEmailFromName")).Value.Value,
                FromAddress = (await _secretClient.GetSecretAsync("CustomEmailFromAddress")).Value.Value,
                SmtpConfig = new SmtpConfig
                {
                    Server = (await _secretClient.GetSecretAsync("CustomSmtpServer")).Value.Value,
                    Port = int.Parse((await _secretClient.GetSecretAsync("CustomSmtpPort")).Value.Value),
                    Username = (await _secretClient.GetSecretAsync("CustomEmailUsername")).Value.Value,
                    Password = (await _secretClient.GetSecretAsync("CustomEmailPassword")).Value.Value,
                    UseSsl = bool.Parse((await _secretClient.GetSecretAsync("CustomSmtpUseSsl")).Value.Value),
                    RequireTls = bool.Parse((await _secretClient.GetSecretAsync("CustomSmtpRequireTls")).Value.Value),
                    AllowInvalidCert = bool.Parse((await _secretClient.GetSecretAsync("CustomSmtpAllowInvalidCert")).Value.Value)
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize email configuration from Key Vault");
            throw new InvalidOperationException("Email service configuration failed", ex);
        }
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isBodyHtml = true)
    {
        await SendEmailAsync(new[] { to }, subject, body, isBodyHtml);
    }

    public async Task SendEmailAsync(string[] tos, string subject, string body, bool isBodyHtml = true)
    {
        try
        {
            await InitializeAsync();

            // Rate limit kontrolü
            await CheckRateLimit(tos);

            // Throttling
            if (!await _throttler.WaitAsync(TimeSpan.FromSeconds(30)))
                throw new ThrottlingException("Email sending is currently throttled");

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
                    var secureSocketOptions = (_emailConfig.SmtpConfig.UseSsl, _emailConfig.SmtpConfig.RequireTls) switch
                    {
                        (true, true) => SecureSocketOptions.SslOnConnect,
                        (true, false) => SecureSocketOptions.StartTls,
                        (false, true) => SecureSocketOptions.StartTlsWhenAvailable,
                        _ => SecureSocketOptions.None
                    };

                    if (_emailConfig.SmtpConfig.AllowInvalidCert)
                    {
                        client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    }

                    await client.ConnectAsync(
                        _emailConfig.SmtpConfig.Server,
                        _emailConfig.SmtpConfig.Port,
                        secureSocketOptions);

                    await client.AuthenticateAsync(
                        _emailConfig.SmtpConfig.Username,
                        _emailConfig.SmtpConfig.Password);

                    await client.SendAsync(message);
                    
                    _logger.LogInformation("Email sent successfully to {Recipients}", string.Join(", ", tos));
                    _metricsService.IncrementTotalRequests("EMAIL", "SEND", "200");
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
            _logger.LogError(ex, "Failed to send email");
            _metricsService.IncrementTotalRequests("EMAIL", "SEND", "500");
            throw;
        }
    }

    private async Task CheckRateLimit(string[] recipients)
    {
        foreach (var recipient in recipients)
        {
            var rateLimitKey = $"email_ratelimit_{recipient}_{DateTime.UtcNow:yyyyMMddHH}";
            var count = await _cacheService.GetCounterAsync(rateLimitKey);

            if (count >= 10)
                throw new RateLimitExceededException($"Email rate limit exceeded for recipient: {recipient}");

            await _cacheService.IncrementAsync(rateLimitKey, 1, TimeSpan.FromHours(1));
        }
    }

    public async Task<string> BuildEmailTemplate(string content, string title = "")
    {
        await InitializeAsync();

        var logoUrl = _storageService.GetCompanyLogoUrl();

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
                
                {(string.IsNullOrEmpty(title) ? "" : $"<h1 style='color: #059669; text-align: center; margin-bottom: 30px;'>{title}</h1>")}

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

    private async Task<string> BuildFooterContent()
    {
        var sb = new StringBuilder();

        try
        {
            // Company info değerlerini Key Vault'tan al
            var companyName = (await _secretClient.GetSecretAsync("CompanyName")).Value.Value;
            var companyAddress = (await _secretClient.GetSecretAsync("CompanyAddress")).Value.Value;
            var companyPhone = (await _secretClient.GetSecretAsync("CompanyPhone")).Value.Value;
            var companyEmail = (await _secretClient.GetSecretAsync("CompanyEmail")).Value.Value;
            
            var linkedInUrl = (await _secretClient.GetSecretAsync("LinkedInUrl")).Value.Value;
            var whatsappUrl = (await _secretClient.GetSecretAsync("WhatsappUrl")).Value.Value;

            // Social media icons (base64 encoded)
            const string linkedinIconBase64 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0...";
            const string whatsappIconBase64 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0...";

            // Footer template
            sb.AppendLine(@"<p style='color: #666; font-size: 12px;'>
                This is an automated notification message.<br>
                If you have any questions, please contact us.
            </p>");

            // Social media links
            sb.AppendLine("<div style='margin-top: 20px;'>");

            if (!string.IsNullOrEmpty(linkedInUrl))
            {
                sb.AppendLine($@"
                <a href='{linkedInUrl}' style='display: inline-block; margin: 0 10px; text-decoration: none;' target='_blank'>
                    <img src='{linkedinIconBase64}' alt='LinkedIn' style='width: 32px; height: 32px;' />
                </a>");
            }

            if (!string.IsNullOrEmpty(whatsappUrl))
            {
                sb.AppendLine($@"
                <a href='{whatsappUrl}' style='display: inline-block; margin: 0 10px; text-decoration: none;' target='_blank'>
                    <img src='{whatsappIconBase64}' alt='WhatsApp' style='width: 32px; height: 32px;' />
                </a>");
            }

            sb.AppendLine("</div>");

            // Company info
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
            // Fallback to basic footer if there's an error
            sb.Clear();
            sb.AppendLine(@"<p style='color: #666; font-size: 12px; text-align: center;'>
                &copy; " + DateTime.Now.Year + @" All rights reserved.
            </p>");
        }

        return sb.ToString();
    }

    public async Task SendPasswordResetEmailAsync(string to, string userId, string resetToken)
    {
        try
        {
            var clientUrl = (await _secretClient.GetSecretAsync("AngularClientUrl")).Value.Value ?? "http://localhost:4200";
            var resetLink = $"{clientUrl.TrimEnd('/')}/update-password/{userId}/{resetToken}";

            var content = $@"
                <div style='text-align: center;'>
                    <p style='font-size: 16px; color: #333;'>Dear User,</p>
                    <p style='font-size: 16px; color: #333;'>We have received your password reset request.</p>
                    <div style='margin: 30px 0;'>
                        <a href='{resetLink}' style='background-color: #e53935; color: white; 
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

    private class EmailConfig
    {
        public EmailProvider Provider { get; set; }
        public string FromName { get; set; }
        public string FromAddress { get; set; }
        public SmtpConfig SmtpConfig { get; set; }
    }

    private class SmtpConfig
    {
        public string Server { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool UseSsl { get; set; }
        public bool RequireTls { get; set; }
        public bool AllowInvalidCert { get; set; }
    }

    // Order notification methods
    public async Task SendCreatedOrderEmailAsync(
        string to,
        string orderCode,
        string orderDescription,
        UserAddressDto? orderAddress,
        DateTime orderCreatedDate,
        string userName,
        List<OrderItemDto> orderCartItems,
        decimal? orderTotalPrice)
    {
        try
        {
            var content = new StringBuilder();
            content.Append(await BuildOrderConfirmationContent(
                userName,
                orderCode,
                orderDescription,
                orderAddress,
                orderCreatedDate,
                orderCartItems,
                orderTotalPrice));

            var emailBody = await BuildEmailTemplate(content.ToString(), "Order Confirmation");
            await SendEmailAsync(to, "Order Created ✓", emailBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order confirmation email");
            throw;
        }
    }

    public async Task SendOrderUpdateNotificationAsync(
        string to,
        string? orderCode,
        string? adminNote,
        OrderStatus? originalStatus,
        OrderStatus? updatedStatus,
        decimal? originalTotalPrice,
        decimal? updatedTotalPrice,
        List<OrderItemUpdateDto>? updatedItems)
    {
        try
        {
            var content = new StringBuilder();
            content.Append(await BuildOrderUpdateContent(
                orderCode, adminNote, originalStatus, updatedStatus,
                originalTotalPrice, updatedTotalPrice, updatedItems));

            var emailBody = await BuildEmailTemplate(content.ToString(), "Order Update Notification");
            await SendEmailAsync(to, "Order Update Notification", emailBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order update notification email");
            throw;
        }
    }

    private async Task<string> BuildOrderUpdateContent(
        string? orderCode,
        string? adminNote,
        OrderStatus? originalStatus,
        OrderStatus? updatedStatus,
        decimal? originalTotalPrice,
        decimal? updatedTotalPrice,
        List<OrderItemUpdateDto>? updatedItems)
    {
        var sb = new StringBuilder();

        // Header with order info
        sb.Append($@"
            <div style='background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;'>
                <p style='font-size: 16px; color: #333;'>Dear valued customer,</p>
                <p style='color: #666;'>We would like to inform you about updates to your order.</p>
            </div>

            <div style='background-color: #fff3cd; padding: 20px; border-radius: 5px; margin-bottom: 20px;'>
                <p style='color: #e53935;'><strong>Order Code: {orderCode}</strong></p>");

        // Show status change if both statuses exist and are different
        if (originalStatus.HasValue && updatedStatus.HasValue && originalStatus != updatedStatus)
        {
            sb.Append($@"
                <p style='color: #856404;'>
                    <strong>Order Status:</strong> Changed from <span style='color: #6c757d;'>{originalStatus}</span> 
                    to <span style='color: #28a745;'>{updatedStatus}</span>
                </p>");
        }

        // Admin note if exists
        if (!string.IsNullOrEmpty(adminNote))
        {
            sb.Append($@"<p style='color: #856404;'><strong>Admin Note:</strong> {adminNote}</p>");
        }

        sb.Append("</div>");

        // Updated items table if exists
        if (updatedItems?.Any() == true)
        {
            sb.Append(BuildUpdatedItemsTable(updatedItems, originalTotalPrice, updatedTotalPrice));
        }

        return sb.ToString();
    }

    private string BuildUpdatedItemsTable(
        List<OrderItemUpdateDto> items,
        decimal? originalTotalPrice,
        decimal? updatedTotalPrice)
    {
        var sb = new StringBuilder();

        sb.Append(@"
        <table style='width: 100%; border-collapse: collapse; margin-top: 20px;'>
            <tr style='background-color: #333333; color: white;'>
                <th style='padding: 12px; text-align: left;'>Product</th>
                <th style='padding: 12px; text-align: right;'>Old Price</th>
                <th style='padding: 12px; text-align: right;'>New Price</th>
                <th style='padding: 12px; text-align: center;'>Quantity</th>
                <th style='padding: 12px; text-align: center;'>Lead Time</th>
                <th style='padding: 12px; text-align: right;'>Total</th>
                <th style='padding: 12px; text-align: center;'>Image</th>
            </tr>");

        foreach (var item in items)
        {
            if (item.UpdatedPrice.HasValue && item.Price.HasValue && item.Quantity.HasValue)
            {
                var itemTotal = item.UpdatedPrice.Value * item.Quantity.Value;
                var priceChange = item.UpdatedPrice > item.Price ? "color: #dc3545;" : "color: #28a745;";

                string imageUrl = item.ShowcaseImage?.Url ?? string.Empty;

                sb.Append($@"
                <tr style='border-bottom: 1px solid #e0e0e0;'>
                    <td style='padding: 12px;'>
                        <strong style='color: #333;'>{item.BrandName}</strong><br>
                        <span style='color: #666;'>{item.ProductName}</span>
                    </td>
                    <td style='padding: 12px; text-align: right;'>${item.Price:N2}</td>
                    <td style='padding: 12px; text-align: right; {priceChange}'>${item.UpdatedPrice:N2}</td>
                    <td style='padding: 12px; text-align: center;'>{item.Quantity}</td>
                    <td style='padding: 12px; text-align: center;'>{item.LeadTime} {(item.LeadTime == 1 ? "day" : "days")}</td>
                    <td style='padding: 12px; text-align: right;'>${itemTotal:N2}</td>
                    <td style='padding: 12px; text-align: center;'>
                        <img src='{imageUrl}'
                             style='max-width: 80px; max-height: 80px; border-radius: 4px;'
                             alt='{item.ProductName}'/>
                    </td>
                </tr>");
            }
        }

        // Show total price changes if both values exist
        if (originalTotalPrice.HasValue && updatedTotalPrice.HasValue)
        {
            var totalPriceChange = updatedTotalPrice > originalTotalPrice ? "color: #dc3545;" : "color: #28a745;";
            sb.Append($@"
            <tr style='background-color: #f8f9fa;'>
                <td colspan='5' style='padding: 12px; text-align: right;'>Original Total:</td>
                <td colspan='2' style='padding: 12px; text-align: right; color: #666;'>${originalTotalPrice:N2}</td>
            </tr>
            <tr style='background-color: #f8f9fa; font-weight: bold;'>
                <td colspan='5' style='padding: 12px; text-align: right;'>New Total Amount:</td>
                <td colspan='2' style='padding: 12px; text-align: right; {totalPriceChange}'>${updatedTotalPrice:N2}</td>
            </tr>");
        }

        sb.Append("</table>");
        return sb.ToString();
    }

    private string BuildOrderItemsTable(List<OrderItemDto> items)
    {
        var sb = new StringBuilder();
        decimal totalAmount = 0;

        sb.Append(@"
        <table style='width: 100%; border-collapse: collapse; margin-top: 10px;'>
            <tr style='background-color: #333333; color: white;'>
                <th style='padding: 12px; text-align: left;'>Product</th>
                <th style='padding: 12px; text-align: right;'>Price</th>
                <th style='padding: 12px; text-align: center;'>Quantity</th>
                <th style='padding: 12px; text-align: right;'>Total</th>
                <th style='padding: 12px; text-align: center;'>Image</th>
            </tr>");

        foreach (var item in items)
        {
            var itemTotal = (item.Price ?? 0) * (item.Quantity ?? 0);
            totalAmount += itemTotal;

            string imageUrl = item.ShowcaseImage?.Url ?? "";

            sb.Append($@"
            <tr style='border-bottom: 1px solid #e0e0e0;'>
                <td style='padding: 12px;'>
                    <strong style='color: #333;'>{item.BrandName}</strong><br>
                    <span style='color: #666;'>{item.ProductName}</span>
                </td>
                <td style='padding: 12px; text-align: right;'>${item.Price:N2}</td>
                <td style='padding: 12px; text-align: center;'>{item.Quantity}</td>
                <td style='padding: 12px; text-align: right;'>${itemTotal:N2}</td>
                <td style='padding: 12px; text-align: center;'>
                    <img src='{imageUrl}' style='max-width: 80px; max-height: 80px; border-radius: 4px;'
                         alt='{item.ProductName}'/>
                </td>
            </tr>");
        }

        sb.Append($@"
        <tr style='background-color: #f8f9fa; color: #059669; font-weight: bold;'>
            <td colspan='3' style='padding: 12px; text-align: right;'>Total Amount:</td>
            <td colspan='2' style='padding: 12px; text-align: right;'>${totalAmount:N2}</td>
        </tr>");

        sb.Append("</table>");
        return sb.ToString();
    }

    private async Task<string> BuildOrderConfirmationContent(
        string userName,
        string orderCode,
        string orderDescription,
        UserAddressDto? orderAddress,
        DateTime orderCreatedDate,
        List<OrderItemDto> orderCartItems,
        decimal? orderTotalPrice)
    {
        var sb = new StringBuilder();

        // Header
        sb.Append($@"
        <div style='background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;'>
            <p style='font-size: 16px; color: #333;'>Hello {userName},</p>
            <p style='color: #666;'>Your order has been successfully created.</p>
        </div>");

        // Order items table
        sb.Append(BuildOrderItemsTable(orderCartItems));

        // Order information
        sb.Append($@"
        <div style='margin-top: 30px; padding: 20px; background-color: #f8f9fa; border-radius: 5px;'>
            <h3 style='color: #333333; margin-bottom: 15px;'>Order Details</h3>
            <p style='color: #e53935;'><strong>Order Code: {orderCode}</strong></p>
            <p><strong>Order Date:</strong> {orderCreatedDate:dd.MM.yyyy HH:mm}</p>
            <p><strong>Delivery Address:</strong><br>{FormatAddress(orderAddress)}</p>
            <p><strong>Order Note:</strong><br>{orderDescription}</p>
            <p style='color: #059669;'><strong>Total Amount:</strong><br>${orderTotalPrice:N2}</p>
        </div>");

        return sb.ToString();
    }

    private string FormatAddress(UserAddressDto? address)
    {
        if (address == null) return "No address provided";

        var formattedAddress = new StringBuilder();
        formattedAddress.AppendLine(address.Name);
        formattedAddress.AppendLine(address.AddressLine1);

        if (!string.IsNullOrEmpty(address.AddressLine2))
            formattedAddress.AppendLine(address.AddressLine2);

        formattedAddress.AppendLine(
            $"{address.City}{(!string.IsNullOrEmpty(address.State) ? $", {address.State}" : "")} {address.PostalCode}");
        formattedAddress.Append(address.Country);

        return formattedAddress.ToString();
    }
}