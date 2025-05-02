using System.Text;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Email;
using Application.Abstraction.Services.Utilities;
using Application.Extensions.ImageFileExtensions;
using Application.Storage;
using Azure.Security.KeyVault.Secrets;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Mail;

/// <summary>
/// Bülten e-postaları için özelleştirilmiş servis
/// </summary>
public class NewsletterEmailService : BaseEmailService, INewsletterEmailService
{
    protected override string ServiceType => "NEWSLETTER_EMAIL";
    protected override string ConfigPrefix => "Email:NewsletterEmail";
    protected override string PasswordSecretName => "NewsletterEmailPassword";

    public NewsletterEmailService(
        ILogger<NewsletterEmailService> logger,
        ICacheService cacheService,
        IMetricsService metricsService,
        IStorageService storageService,
        SecretClient secretClient,
        IConfiguration configuration) 
        : base(logger, cacheService, metricsService, storageService, secretClient, configuration)
    {
        // Yapılandırmadan throttling ayarlarını al
        var maxConcurrentEmails = configuration.GetValue<int>("Newsletter:Throttling:MaxConcurrentEmails", 5);
    }

    protected override async Task CheckRateLimit(string[] recipients)
    {
        foreach (var recipient in recipients)
        {
            // Bültenler için 24 saatlik pencere kullan (günlük)
            var rateLimitKey = $"newsletter_email_ratelimit_{recipient}_{DateTime.UtcNow:yyyyMMdd}";
            var count = await _cacheService.GetCounterAsync(rateLimitKey,cancellationToken: CancellationToken.None);

            // Alıcı başına günde 2 bülten ile sınırla
            if (count >= 2)
            {
                _logger.LogWarning("Newsletter rate limit exceeded for recipient: {Email}", recipient);
                throw new Exception($"Newsletter rate limit exceeded for recipient: {recipient}");
            }

            await _cacheService.IncrementAsync(rateLimitKey, 1, TimeSpan.FromDays(1),cancellationToken: CancellationToken.None);
        }
    }

    protected override string GetFooterMessage()
    {
        return "This is an automated newsletter from TUMDEX.<br>Please do not reply to this email.";
    }

    /// <summary>
    /// Bir ürün kartı HTML içeriği oluşturur
    /// </summary>
    public string BuildProductCard(Product product, string? additionalInfo = null)
    {
        var imageUrl = product.ProductImageFiles?
            .FirstOrDefault(pif => pif.Showcase)?
            .SetImageUrl(_storageService);

        if (string.IsNullOrEmpty(imageUrl))
        {
            imageUrl = _configuration["Newsletter:DefaultImages:ProductPlaceholder"];
        }

        var clientUrl = _configuration["AngularClientUrl"]?.TrimEnd('/') ?? "http://localhost:4200";
        var productUrl = $"{clientUrl}/{product.Id}";

        return $@"
        <td style='width: 33.33%; padding: 10px; vertical-align: top;'>
            <div style='border: 1px solid #e0e0e0; border-radius: 8px; padding: 10px; background-color: white;'>
                <div style='text-align: center; margin-bottom: 10px;'>
                    <img src='{imageUrl}' 
                         alt='{product.Name}' 
                         style='width: 150px; height: 150px; border-radius: 4px; object-fit: cover;'/>
                </div>
                <div style='text-align: center;'>
                    <h3 style='color: #333; margin: 5px 0; font-size: 16px;'>{product.Name}</h3>
                    {(!string.IsNullOrEmpty(product.Title) ? $"<h4 style='color: #666; margin: 3px 0; font-size: 14px;'>{product.Title}</h4>" : "")}
                    {(!string.IsNullOrEmpty(additionalInfo) ? $"<p style='color: #059669; margin: 3px 0; font-size: 12px;'>{additionalInfo}</p>" : "")}
                    {(product.Brand != null ? $"<p style='color: #0d6efd; margin: 3px 0; font-size: 12px;'>{product.Brand.Name}</p>" : "")}
                    <!-- <p style='color: #059669; font-size: 16px; font-weight: bold; margin: 5px 0;'>
                        ₺{product.Price:N2}
                    </p> -->
                    <a href='{productUrl}' 
                       style='display: inline-block; background-color: #0d6efd; color: white; text-decoration: none; 
                              padding: 8px 15px; border-radius: 5px; margin-top: 5px; font-size: 12px;'>
                       See Details
                    </a>
                </div>
            </div>
        </td>";
    }

    /// <summary>
    /// Bülten içeriğini ürün koleksiyonlarıyla oluşturur
    /// </summary>
    public async Task<string> BuildNewsletterContent(
        IList<Product> newProducts,
        List<Product> mostLikedProducts,
        List<Product> bestSellingProducts,
        string email) 
    {
        var content = new StringBuilder();

        // Banner
        content.AppendLine(@"
        <div style='background-color: #f8f9fa; padding: 20px; text-align: center; margin-bottom: 20px; border-radius: 8px;'>
            <h1 style='color: #333; margin-bottom: 10px; font-size: 24px;'>Welcome to Our Monthly Newsletter!</h1>
            <p style='color: #666; font-size: 14px;'>Discover newly added products and our most popular items below.</p>
        </div>");

        // Yeni Ürünler Bölümü
        if (newProducts.Any())
        {
            content.AppendLine(@"
            <div style='margin-bottom: 30px;'>
                <h2 style='color: #333; text-align: center; margin-bottom: 15px; font-size: 20px;'>
                    New Arrivals This Month
                </h2>
                <table style='width: 100%; border-collapse: collapse;'><tr>");

            var newProductsCount = 0;
            foreach (var product in newProducts.Take(5)) // 5 ürünle sınırlı
            {
                content.AppendLine(BuildProductCard(product, "New Arrival!"));
                newProductsCount++;
                if (newProductsCount % 3 == 0 && newProductsCount != newProducts.Count())
                {
                    content.AppendLine("</tr><tr>");
                }
            }

            content.AppendLine("</tr></table></div>");
        }

        // En Çok Satan Ürünler
        if (bestSellingProducts.Any())
        {
            content.AppendLine(@"
            <div style='margin-bottom: 30px;'>
                <h2 style='color: #333; text-align: center; margin-bottom: 15px; font-size: 20px;'>
                    Best Sellers
                </h2>
                <table style='width: 100%; border-collapse: collapse;'><tr>");

            var bestSellingCount = 0;
            foreach (var product in bestSellingProducts.Take(5))
            {
                content.AppendLine(BuildProductCard(product, "Top Seller!"));
                bestSellingCount++;
                if (bestSellingCount % 3 == 0 && bestSellingCount != 5)
                {
                    content.AppendLine("</tr><tr>");
                }
            }

            content.AppendLine("</tr></table></div>");
        }

        // En Çok Beğenilen Ürünler
        var likedProducts = mostLikedProducts.Where(p => (p.ProductLikes?.Count ?? 0) > 0).Take(5).ToList();
        if (likedProducts.Any())
        {
            content.AppendLine(@"
            <div style='margin-bottom: 30px;'>
                <h2 style='color: #333; text-align: center; margin-bottom: 15px; font-size: 20px;'>
                    Most Liked Products
                </h2>
                <table style='width: 100%; border-collapse: collapse;'><tr>");

            var likedCount = 0;
            foreach (var product in likedProducts)
            {
                var likeCount = product.ProductLikes?.Count ?? 0;
                content.AppendLine(BuildProductCard(product, $"{likeCount} people liked this"));
                likedCount++;
                if (likedCount % 3 == 0 && likedCount != likedProducts.Count)
                {
                    content.AppendLine("</tr><tr>");
                }
            }

            content.AppendLine("</tr></table></div>");
        }

        // Abonelikten çıkma linki
        var unsubscribeToken = GenerateUnsubscribeToken(email);
        var clientUrl = _configuration["AngularClientUrl"]?.TrimEnd('/') ?? "http://localhost:4200";
    
        content.AppendLine($@"
        <div style='text-align: center; margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0;'>
            <p style='color: #666; font-size: 12px;'>
                If you do not wish to receive this email, 
                <a href='{clientUrl}/newsletter/unsubscribe?token={unsubscribeToken}' style='color: #0d6efd;'>click here</a> 
                to unsubscribe from the newsletter.
            </p>
        </div>");

        return content.ToString();
    }

    /// <summary>
    /// Abonelikten çıkmak için token oluşturma
    /// </summary>
    private string GenerateUnsubscribeToken(string email)
    {
        var guid = Guid.NewGuid().ToString("N");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{guid}"));
    }

    /// <summary>
    /// Abonelikten çıkma tokenini çözümleme
    /// </summary>
    public (string email, string guid) DecodeUnsubscribeToken(string token)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = decoded.Split(':');
            if (parts.Length != 2)
                throw new Exception("Invalid token format");
            
            return (parts[0], parts[1]);
        }
        catch
        {
            throw new Exception("Invalid unsubscribe token");
        }
    }
}