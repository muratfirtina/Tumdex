using System.Text;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Email;
using Application.Abstraction.Services.Utilities;
using Application.Repositories;
using Application.Services;
using Domain;
using Domain.Entities;
using Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Mail;

/// <summary>
/// Bülten aboneliği ve gönderim işlemlerini yöneten servis
/// </summary>
public class NewsletterService : INewsletterService
{
    private readonly INewsletterRepository _newsletterRepository;
    private readonly INewsletterLogRepository _newsletterLogRepository;
    private readonly IProductRepository _productRepository;
    private readonly IProductLikeRepository _productLikeRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderItemRepository _orderItemRepository;
    private readonly INewsletterEmailService _newsletterEmailService; // Interface türünü değiştirdik
    private readonly ILogger<NewsletterService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IBackgroundTaskQueue _backgroundTaskQueue;

    public NewsletterService(
        INewsletterRepository newsletterRepository,
        IProductRepository productRepository,
        IProductLikeRepository productLikeRepository,
        IOrderRepository orderRepository,
        INewsletterEmailService newsletterEmailService, // Interface türünü değiştirdik
        ILogger<NewsletterService> logger,
        INewsletterLogRepository newsletterLogRepository,
        IConfiguration configuration,
        IOrderItemRepository orderItemRepository,
        IBackgroundTaskQueue backgroundTaskQueue)
    {
        _newsletterRepository = newsletterRepository;
        _productRepository = productRepository;
        _productLikeRepository = productLikeRepository;
        _orderRepository = orderRepository;
        _newsletterEmailService = newsletterEmailService;
        _logger = logger;
        _newsletterLogRepository = newsletterLogRepository;
        _configuration = configuration;
        _orderItemRepository = orderItemRepository;
        _backgroundTaskQueue = backgroundTaskQueue;
    }

    /// <summary>
    /// Bülten aboneliği oluşturur
    /// </summary>
    public async Task<Newsletter> SubscribeAsync(string email, string? source = null, string? userId = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required");

        return await _newsletterRepository.SubscribeAsync(email, source, userId);
    }

    /// <summary>
    /// Token ile bülten aboneliğini iptal eder
    /// </summary>
    public async Task<Newsletter> UnsubscribeAsync(string token)
    {
        try
        {
            var (email, _) = _newsletterEmailService.DecodeUnsubscribeToken(token);
            return await _newsletterRepository.UnsubscribeAsync(email);
        }
        catch
        {
            throw new Exception("Invalid or expired unsubscribe link");
        }
    }

    /// <summary>
    /// E-posta ile bülten aboneliğini iptal eder
    /// </summary>
    public async Task<Newsletter> UnsubscribeAsync(string email, bool isDirectCall = false)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required");

        return await _newsletterRepository.UnsubscribeAsync(email);
    }

    /// <summary>
    /// Kullanıcı kaydı sonrası bülten aboneliği işlemlerini yönetir
    /// </summary>
    public async Task HandleUserRegistrationAsync(AppUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            await SubscribeAsync(user.Email, "Registration", user.Id);
        }
    }

    /// <summary>
    /// En çok satan ürünleri getirir
    /// </summary>
    private async Task<List<Product>> GetBestSellingProductsAsync()
    {
        try
        {
            // En çok sipariş edilen ürünleri bul
            var topProducts = await _orderItemRepository.GetMostOrderedProductsAsync(5);

            // İlgili ürün detaylarını getir
            var productIds = topProducts.Select(x => x.ProductId).ToList();
            var products = await _productRepository.GetListAsync(
                predicate: p => productIds.Contains(p.Id),
                include: q => q
                    .Include(p => p.ProductImageFiles)
                    .Include(p => p.Brand)
            );

            // Sonuçları orijinal sırada döndür
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

    /// <summary>
    /// En çok beğenilen ürünleri getirir
    /// </summary>
    private async Task<List<Product>> GetMostLikedProductsAsync()
    {
        try
        {
            // En çok beğenilen ürünlerin ID'lerini al
            var topLikedProducts = await _productLikeRepository.GetMostLikedProductsAsync(5);

            // Ürün detaylarını getir
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

    /// <summary>
    /// Aylık bülten gönderimi işlemini gerçekleştirir
    /// </summary>
    public async Task SendMonthlyNewsletterAsync(bool isTest = false)
    {
        try
        {
            _logger.LogInformation("Starting to send {Mode} newsletter", isTest ? "test" : "monthly");

            // Test modu için sadece birkaç abone al veya test e-postaları kullan
            var subscribers = isTest
                ? (await _newsletterRepository.GetActiveSubscribersAsync()).Take(3).ToList()
                : await _newsletterRepository.GetActiveSubscribersAsync();

            if (!subscribers.Any())
            {
                _logger.LogInformation("No active subscribers found. Newsletter sending skipped.");
                return;
            }

            // Son 30 gündeki yeni ürünleri getir
            var newProducts = await _productRepository.GetListAsync(
                predicate: p => p.CreatedDate >= DateTime.UtcNow.AddDays(-30),
                include: i => i
                    .Include(p => p.ProductImageFiles)
                    .Include(p => p.Brand),
                orderBy: q => q.OrderByDescending(p => p.CreatedDate)
            );

            // Rastgele 5 yeni ürün seç
            var randomNewProducts = newProducts.Items
                .OrderBy(x => Guid.NewGuid())
                .Take(5)
                .ToList();

            var mostLikedProducts = await GetMostLikedProductsAsync();
            var bestSellingProducts = await GetBestSellingProductsAsync();

            var successCount = 0;
            var failCount = 0;
            var firstEmailContent = string.Empty; // İlk e-posta içeriğini sakla

            // Tahmini tamamlanma süresini hesapla
            var delayBetweenEmails = _configuration.GetValue<int>("Newsletter:Throttling:DelayBetweenEmails", 1000);
            var estimatedDuration = TimeSpan.FromMilliseconds(subscribers.Count * delayBetweenEmails);
            var estimatedEndTime = DateTime.Now.Add(estimatedDuration);

            _logger.LogInformation("Sending newsletter to {Count} subscribers. Estimated completion: {Time}",
                subscribers.Count, estimatedEndTime.ToString("HH:mm:ss"));

            // E-postaları işlemek için aboneleri döngüye al
            foreach (var subscriber in subscribers)
            {
                try
                {
                    // Her abone için kişiselleştirilmiş içerik oluştur
                    var emailContent = await _newsletterEmailService.BuildNewsletterContent(
                        randomNewProducts,
                        mostLikedProducts,
                        bestSellingProducts,
                        subscriber.Email);

                    if (string.IsNullOrEmpty(firstEmailContent))
                    {
                        firstEmailContent = emailContent; // İlk e-posta içeriğini sakla (log için)
                    }

                    var finalEmailContent = await _newsletterEmailService.BuildEmailTemplate(
                        emailContent,
                        "TUMDEX Monthly Newsletter");

                    await _newsletterEmailService.SendEmailAsync(
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

            // Log kaydı oluştur
            var log = new NewsletterLog
            {
                NewsletterType = isTest ? "Test" : "Monthly",
                SentDate = DateTime.UtcNow,
                EmailContent = firstEmailContent, // E-posta içeriğini sakla
                TotalRecipients = subscribers.Count,
                SuccessfulDeliveries = successCount,
                FailedDeliveries = failCount
            };

            await _newsletterLogRepository.LogNewsletterSendAsync(log);

            _logger.LogInformation("Newsletter sent successfully. Success: {Success}, Failed: {Failed}",
                successCount, failCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send {Mode} newsletter", isTest ? "test" : "monthly");
            throw;
        }
    }

    /// <summary>
    /// Aylık bülten gönderimini kuyruğa ekler
    /// </summary>
    public async Task QueueSendMonthlyNewsletterAsync(bool isTest = false)
    {
        _backgroundTaskQueue.QueueBackgroundWorkItem(async (token) => { await SendMonthlyNewsletterAsync(isTest); });

        _logger.LogInformation("Queued monthly newsletter sending. Test mode: {IsTest}", isTest);
    }
}