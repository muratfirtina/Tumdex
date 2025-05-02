using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstraction.Services.Utilities;
using Application.Models.Monitoring;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalR.Hubs;


namespace Infrastructure.BackgroundJobs
{
    public class VisitorStatsBackgroundService : BackgroundService
    {
        private readonly ILogger<VisitorStatsBackgroundService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly TimeSpan _updateInterval;
        private readonly string VISITORS_CACHE_KEY = "active_visitors";
        
        // Son yayın zamanını takip et - gereksiz güncelleme yapmamak için
        private DateTime _lastBroadcastTime = DateTime.MinValue;
        // Minimum yayın aralığı (saniye)
        private readonly int _minBroadcastIntervalSeconds;

        public VisitorStatsBackgroundService(
            ILogger<VisitorStatsBackgroundService> logger,
            IServiceScopeFactory serviceScopeFactory,
            IOptions<VisitorStatsOptions> options)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _updateInterval = TimeSpan.FromSeconds(options?.Value?.UpdateIntervalSeconds ?? 15);
            _minBroadcastIntervalSeconds = options?.Value?.MinBroadcastIntervalSeconds ?? 5;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Ziyaretçi İstatistikleri Servisi başlatılıyor. " +
                                   "Güncelleme Aralığı: {Interval} saniye", _updateInterval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await BroadcastVisitorStatsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    // Hatayı logla ama servisi durdurmadan devam et
                    _logger.LogError(ex, "Ziyaretçi istatistiklerini yayınlarken hata oluştu");
                }

                // Sonraki yayına kadar bekle
                await Task.Delay(_updateInterval, stoppingToken);
            }
        }

        private async Task BroadcastVisitorStatsAsync(CancellationToken cancellationToken)
        {
            // Zaman aşımı kontrolü
            var now = DateTime.UtcNow;
            if ((now - _lastBroadcastTime).TotalSeconds < _minBroadcastIntervalSeconds)
            {
                // Minimum yayın aralığı geçmemişse atla
                return;
            }

            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<EnhancedVisitorTrackingHub>>();
                var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

                // Aktif ziyaretçileri getir
                var activeVisitors = await GetActiveVisitorsAsync(cacheService, cancellationToken);
                
                // İstatistikleri hesapla
                var stats = CalculateStats(activeVisitors);

                // Admins grubuna istatistikleri gönder
                await hubContext.Clients.Group("Admins").SendAsync("ReceiveVisitorStats", stats, cancellationToken);
                
                // Son yayın zamanını güncelle
                _lastBroadcastTime = now;
                
                _logger.LogDebug("Ziyaretçi istatistikleri yayınlandı. Aktif Ziyaretçi Sayısı: {Count}", 
                    activeVisitors.Count);
            }
            catch (OperationCanceledException)
            {
                // İptal durumu - sessizce çık
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ziyaretçi istatistiklerini yayınlarken beklenmeyen hata");
            }
        }

        private async Task<List<VisitorTracking>> GetActiveVisitorsAsync(
            ICacheService cacheService, CancellationToken cancellationToken)
        {
            try
            {
                // 1. Redis'teki tüm aktif ziyaretçi anahtarlarını getir (zaman aşımı ile)
                var allVisitorKeys = await cacheService.GetKeysAsync($"{VISITORS_CACHE_KEY}:*", cancellationToken);
                
                if (!allVisitorKeys.Any())
                    return new List<VisitorTracking>();
                
                // 2. Performans için veri sayısını sınırla
                var limitedKeys = allVisitorKeys.Take(100).ToList();
                
                // 3. Seçilen anahtarlar için verileri getir
                var visitorDataDict = await cacheService.GetManyAsync<VisitorTracking>(limitedKeys, cancellationToken);
                
                return visitorDataDict.Values.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Aktif ziyaretçileri getirirken hata oluştu");
                return new List<VisitorTracking>();
            }
        }

        private object CalculateStats(List<VisitorTracking> activeVisitors)
        {
            var totalVisitors = activeVisitors.Count;
            var authenticatedVisitors = activeVisitors.Count(v => v.IsAuthenticated);
            var anonymousVisitors = totalVisitors - authenticatedVisitors;
            
            // Sayfa istatistikleri
            var pageStats = activeVisitors
                .GroupBy(v => v.CurrentPage)
                .Select(g => new { Page = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();
                
            // Tarayıcı istatistikleri
            var browserStats = activeVisitors
                .GroupBy(v => v.BrowserName)
                .Select(g => new { Browser = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();
                
            // Cihaz istatistikleri
            var deviceStats = activeVisitors
                .GroupBy(v => v.DeviceType)
                .Select(g => new { Device = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();
            
            // Referrer istatistikleri
            var referrerStats = activeVisitors
                .Where(v => !string.IsNullOrEmpty(v.Referrer))
                .GroupBy(v => v.Referrer)
                .Select(g => new { Referrer = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            return new
            {
                TotalVisitors = totalVisitors,
                AuthenticatedVisitors = authenticatedVisitors,
                AnonymousVisitors = anonymousVisitors,
                PageStats = pageStats,
                BrowserStats = browserStats,
                DeviceStats = deviceStats,
                ReferrerStats = referrerStats,
                // ActiveVisitors listesini dahil etme seçeneği
                // Çok fazla veri gönderilmemesi için aktif ziyaretçileri isteğe bağlı olarak gönderin
                ActiveVisitors = activeVisitors
            };
        }
    }

    // Yapılandırma sınıfı
    public class VisitorStatsOptions
    {
        public int UpdateIntervalSeconds { get; set; } = 15;
        public int MinBroadcastIntervalSeconds { get; set; } = 5;
    }
}