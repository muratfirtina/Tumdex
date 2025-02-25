using System.Text;
using Application.Abstraction.Services;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Services;
using Application.Storage;
using Domain;
using Domain.Enum;
using Domain.Identity;
using Infrastructure.Services.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class NewsletterService : INewsletterService
{
    private readonly INewsletterRepository _newsletterRepository;
    private readonly INewsletterLogRepository _newsletterLogRepository;
    private readonly IProductRepository _productRepository;
    private readonly IProductLikeRepository _productLikeRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderItemRepository _orderItemRepository;
    private readonly IMailService _mailService;
    private readonly ILogger<NewsletterService> _logger;
    private readonly IStorageService _storageService;
    private readonly IConfiguration _configuration;

    public NewsletterService(
        INewsletterRepository newsletterRepository,
        IProductRepository productRepository,
        IProductLikeRepository productLikeRepository,
        IOrderRepository orderRepository,
        IMailService mailService,
        ILogger<NewsletterService> logger,
        INewsletterLogRepository newsletterLogRepository,
        IStorageService storageService,
        IConfiguration configuration, IOrderItemRepository orderItemRepository)
    {
        _newsletterRepository = newsletterRepository;
        _productRepository = productRepository;
        _productLikeRepository = productLikeRepository;
        _orderRepository = orderRepository;
        _mailService = mailService;
        _logger = logger;
        _newsletterLogRepository = newsletterLogRepository;
        _storageService = storageService;
        _configuration = configuration;
        _orderItemRepository = orderItemRepository;
    }

    public async Task<Newsletter> SubscribeAsync(string email, string? source = null, string? userId = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required");

        return await _newsletterRepository.SubscribeAsync(email, source, userId);
    }

    public async Task<Newsletter> UnsubscribeAsync(string token)
    {
        try
        {
            var (email, _) = DecodeUnsubscribeToken(token);
            return await _newsletterRepository.UnsubscribeAsync(email);
        }
        catch
        {
            throw new Exception("Invalid or expired unsubscribe link");
        }
    }

    public async Task HandleUserRegistrationAsync(AppUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            await SubscribeAsync(user.Email, "Registration", user.Id);
        }
    }

    private string BuildProductCard(Product product, string? additionalInfo = null)
    {
        var imageUrl = product.ProductImageFiles?
            .FirstOrDefault(pif => pif.Showcase)?
            .SetImageUrl(_storageService);

        if (string.IsNullOrEmpty(imageUrl))
        {
            imageUrl = _configuration["Newsletter:DefaultImages:ProductPlaceholder"];
        }

        var clientUrl = _configuration["AngularClientUrl"]?.TrimEnd('/') ?? "http://localhost:4200";
        var productUrl = $"{clientUrl}/product/{product.Id}";

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
                    <p style='color: #059669; font-size: 16px; font-weight: bold; margin: 5px 0;'>
                        ₺{product.Price:N2}
                    </p>
                    <a href='{productUrl}' 
                       style='display: inline-block; background-color: #0d6efd; color: white; text-decoration: none; 
                              padding: 8px 15px; border-radius: 5px; margin-top: 5px; font-size: 12px;'>
                       See Details
                    </a>
                </div>
            </div>
        </td>";
    }

    private async Task<List<Product>> GetBestSellingProductsAsync()
    {
        try
        {
            // En çok sipariş edilen ürünleri bul
            var topProducts = await _orderItemRepository.GetMostOrderedProductsAsync(5);

            // İlgili ürünleri detaylarıyla birlikte getir
            var productIds = topProducts.Select(x => x.ProductId).ToList();
            var products = await _productRepository.GetListAsync(
                predicate: p => productIds.Contains(p.Id),
                include: q => q
                    .Include(p => p.ProductImageFiles)
                    .Include(p => p.Brand)
            );

            // Sıralamayı koruyarak sonuçları döndür
            return productIds
                .Select(id => products.Items.First(p => p.Id == id))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting best selling products");
            return new List<Product>();
        }
    }

    private async Task<List<Product>> GetMostLikedProductsAsync()
    {
        try
        {
            // En çok beğenilen ürünlerin ID'lerini al
            var topLikedProducts = await _productLikeRepository.GetMostLikedProductsAsync(5);

            // İlgili ürünleri detaylarıyla birlikte getir
            var products = await _productRepository.GetListAsync(
                predicate: p => topLikedProducts.Contains(p.Id),
                include: q => q
                    .Include(p => p.ProductImageFiles)
                    .Include(p => p.Brand)
                    .Include(p => p.ProductLikes)
            );

            // Her ürün için beğeni sayısını hesapla ve sırala
            var productsWithLikes = await Task.WhenAll(products.Items.Select(async p => new
            {
                Product = p,
                LikeCount = await _productLikeRepository.GetProductLikeCountAsync(p.Id)
            }));

            return productsWithLikes
                .OrderByDescending(x => x.LikeCount)
                .Select(x => x.Product)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting most liked products");
            return new List<Product>();
        }
    }

    private async Task<string> BuildNewsletterContent(
        IList<Product> newProducts,
        List<Product> mostLikedProducts,
        List<Product> bestSellingProducts,
        string email) // Yeni eklenen parametre
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
            foreach (var product in newProducts.Take(5)) // 5 ürünle sınırlandırıldı
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

        // Footer ve Unsubscribe Link
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

    public async Task SendMonthlyNewsletterAsync()
    {
        try
        {
            var subscribers = await _newsletterRepository.GetActiveSubscribersAsync();
            if (!subscribers.Any())
                return;

            // Son bir ayda eklenen ürünlerden random 5 tanesini al
            var newProducts = await _productRepository.GetListAsync(
                predicate: p => p.CreatedDate >= DateTime.UtcNow.AddDays(-30),
                include: i => i
                    .Include(p => p.ProductImageFiles)
                    .Include(p => p.Brand),
                orderBy: q => q.OrderByDescending(p => p.CreatedDate)
            );

            var randomNewProducts = newProducts.Items
                .OrderBy(x => Guid.NewGuid())
                .Take(5)
                .ToList();

            var mostLikedProducts = await GetMostLikedProductsAsync();
            var bestSellingProducts = await GetBestSellingProductsAsync();

            var successCount = 0;
            var failCount = 0;
            var firstEmailContent = string.Empty; // İlk oluşturulan email içeriğini saklamak için

            foreach (var subscriber in subscribers)
            {
                try
                {
                    // Her abone için özel içerik oluştur
                    var emailContent = await BuildNewsletterContent(
                        randomNewProducts,
                        mostLikedProducts,
                        bestSellingProducts,
                        subscriber.Email);

                    if (string.IsNullOrEmpty(firstEmailContent))
                    {
                        firstEmailContent = emailContent; // İlk email içeriğini sakla
                    }

                    var finalEmailContent = await ((UnifiedMailService)_mailService).BuildEmailTemplate(
                        emailContent,
                        "TUMDEX Monthly Newsletter");

                    await _mailService.SendEmailAsync(
                        subscriber.Email,
                        "TUMDEX Monthly Newsletter - New Products and Opportunities",
                        finalEmailContent);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send newsletter to {Email}", subscriber.Email);
                    failCount++;
                }
            }

            // Log kaydını email içeriğiyle birlikte oluştur
            var log = new NewsletterLog
            {
                NewsletterType = "Monthly",
                SentDate = DateTime.UtcNow,
                EmailContent = firstEmailContent, // Email içeriğini kaydet
                TotalRecipients = subscribers.Count,
                SuccessfulDeliveries = successCount,
                FailedDeliveries = failCount
            };

            await _newsletterLogRepository.LogNewsletterSendAsync(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send monthly newsletter");
            throw;
        }
        
    }
    
    private string GenerateUnsubscribeToken(string email)
    {
        var guid = Guid.NewGuid().ToString("N");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{guid}"));
    }

    private (string email, string guid) DecodeUnsubscribeToken(string token)
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