using System.Data.Common;
using System.Diagnostics;
using Application.Abstraction.Services;
using Application.Events.OrderEvetns;
using Application.Repositories;
using Domain;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Consumers;

public class StockUpdatedEventConsumer : IConsumer<StockUpdatedEvent>
{
    private readonly ILogger<StockUpdatedEventConsumer> _logger;
    private readonly IProductRepository _productRepository;
    private readonly ICacheService _cacheService;
    private readonly IMetricsService _metricsService;
    private readonly INotificationService _notificationService;
    private readonly SemaphoreSlim _throttler;

    private const string PRODUCT_CACHE_KEY_PREFIX = "product:";
    private const string STOCK_UPDATE_RATE_PREFIX = "stock-update-rate:";
    private const string PRODUCT_STOCK_HISTORY_PREFIX = "stock-history:";

    public StockUpdatedEventConsumer(
        ILogger<StockUpdatedEventConsumer> logger,
        IProductRepository productRepository,
        ICacheService cacheService,
        IMetricsService metricsService,
        INotificationService notificationService)
    {
        _logger = logger;
        _productRepository = productRepository;
        _cacheService = cacheService;
        _metricsService = metricsService;
        _notificationService = notificationService;
        _throttler = new SemaphoreSlim(100, 100);
    }

    public async Task Consume(ConsumeContext<StockUpdatedEvent> context)
    {
        var stockEvent = context.Message;
        var sw = Stopwatch.StartNew();

        try
        {
            if (!await _throttler.WaitAsync(TimeSpan.FromSeconds(5)))
            {
                _logger.LogWarning("Stock update throttled for ProductId: {ProductId}", stockEvent.ProductId);
                throw new ThrottlingException("Stock update is currently throttled");
            }

            _logger.LogInformation("Processing stock update for ProductId: {ProductId}, NewStock: {NewStock}", 
                stockEvent.ProductId, stockEvent.NewStock);

            // API metriği kaydet
            _metricsService.RecordApiCall("stock-update", "POST", "v1");

            // Rate limiting kontrolü
            var rateLimitKey = $"{STOCK_UPDATE_RATE_PREFIX}{stockEvent.ProductId}";
            var updateCount = await _cacheService.GetCounterAsync(rateLimitKey);
            if (updateCount > 1000) // Saatte maksimum 1000 stok güncelleme
            {
                _metricsService.IncrementRateLimitHit(stockEvent.ProductId, "stock-update");
                throw new RateLimitExceededException("Stock update rate limit exceeded");
            }

            // Cache'den ürünü al veya oluştur
            var productCacheKey = $"{PRODUCT_CACHE_KEY_PREFIX}{stockEvent.ProductId}";
            var product = await _cacheService.GetOrCreateAsync(
                productCacheKey,
                async () => await _productRepository.GetAsync(p => p.Id == stockEvent.ProductId),
                TimeSpan.FromMinutes(30)
            );

            if (product == null)
            {
                throw new Exception($"Product not found: {stockEvent.ProductId}");
            }

            // Stok değişim geçmişini cache'e kaydet
            var historyKey = $"{PRODUCT_STOCK_HISTORY_PREFIX}{stockEvent.ProductId}";
            var stockChange = new
            {
                OldStock = product.Stock,
                NewStock = stockEvent.NewStock,
                Timestamp = DateTime.UtcNow
            };

            await _cacheService.GetOrCreateAsync(
                historyKey,
                async () => new List<object> { stockChange },
                TimeSpan.FromDays(1)
            );

            // Stok güncelleme
            var oldStock = product.Stock;
            product.Stock = stockEvent.NewStock;
            await _productRepository.UpdateAsync(product);

            // Cache güncelleme
            var cacheUpdates = new Dictionary<string, Product>
            {
                { productCacheKey, product }
            };
            await _cacheService.SetManyAsync(cacheUpdates, TimeSpan.FromMinutes(30));

            // Rate limit sayacını artır
            await _cacheService.IncrementAsync(rateLimitKey, 1, TimeSpan.FromHours(1));

            // Stok düşüş kontrolü
            if (stockEvent.NewStock < stockEvent.MinStockLevel)
            {
                await _notificationService.SendSlackMessageAsync(
                    $"Low stock alert: Product {stockEvent.ProductName} (ID: {stockEvent.ProductId}) " +
                    $"has fallen below minimum level. Current stock: {stockEvent.NewStock}, " +
                    $"Minimum level: {stockEvent.MinStockLevel}");

                _metricsService.IncrementAlertCounter("low_stock", new Dictionary<string, string>
                {
                    { "product_id", stockEvent.ProductId },
                    { "severity", "warning" },
                    { "stock_level", stockEvent.NewStock.ToString() }
                });
            }

            // Stok değişim yüzdesini hesapla
            var oldStockDecimal = (decimal)oldStock;
            var newStockDecimal = (decimal)stockEvent.NewStock;
            var stockChangePercentage = oldStockDecimal != 0 
                ? Math.Abs(((newStockDecimal - oldStockDecimal) / oldStockDecimal) * 100)
                : 100;

            if (stockChangePercentage > 50) // %50'den fazla değişim
            {
                await _notificationService.SendSlackMessageAsync(
                    $"Significant stock change detected: Product {stockEvent.ProductName} " +
                    $"stock changed by {stockChangePercentage:F2}% " +
                    $"(Old: {oldStock}, New: {stockEvent.NewStock})");

                _metricsService.IncrementAlertCounter("significant_stock_change", new Dictionary<string, string>
                {
                    { "product_id", stockEvent.ProductId },
                    { "severity", "warning" },
                    { "change_percentage", stockChangePercentage.ToString("F2") }
                });
            }

            // Metrikler
            sw.Stop();
            _metricsService.RecordApiLatency("stock-update", "POST", sw.ElapsedMilliseconds);
            _metricsService.RecordSecurityEvent("stock_updated", "info");

            // Stok artış/azalış metriklerini kaydet
            if (stockEvent.NewStock > oldStock)
            {
                _metricsService.RecordApiCall("stock-increase", "POST", "v1");
                _metricsService.RecordSecurityEvent("stock_increased", "info");
            }
            else
            {
                _metricsService.RecordApiCall("stock-decrease", "POST", "v1");
                _metricsService.RecordSecurityEvent("stock_decreased", "info");
            }

            _logger.LogInformation(
                "Stock update processed successfully. ProductId: {ProductId}, OldStock: {OldStock}, " +
                "NewStock: {NewStock}, Duration: {Duration}ms",
                stockEvent.ProductId, oldStock, stockEvent.NewStock, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing stock update for ProductId: {ProductId}", 
                stockEvent.ProductId);

            _metricsService.IncrementAlertCounter("stock_update_error", new Dictionary<string, string>
            {
                { "product_id", stockEvent.ProductId },
                { "severity", "error" },
                { "error_type", ex.GetType().Name }
            });

            if (ex is DbException)
            {
                _metricsService.RecordSecurityEvent("database_error", "error");
            }

            throw;
        }
        finally
        {
            _throttler.Release();
        }
    }
}

// Exception sınıfları
public class ThrottlingException : Exception
{
    public ThrottlingException(string message) : base(message)
    {
    }
}

public class RateLimitExceededException : Exception
{
    public RateLimitExceededException(string message) : base(message)
    {
    }
}