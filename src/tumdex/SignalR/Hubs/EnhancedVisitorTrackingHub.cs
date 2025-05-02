using Application.Abstraction.Services;
using Application.Abstraction.Services.Utilities;
using Application.Models.Monitoring;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SignalR.Extensions;
using System.Text.RegularExpressions;
using Application.Services;
using UAParser;
using UAParser.Objects;

namespace SignalR.Hubs
{
    public class EnhancedVisitorTrackingHub : Hub
    {
        private readonly ILogger<EnhancedVisitorTrackingHub> _logger;
        private readonly IUserService _userService;
        private readonly ICacheService _cacheService;
        private readonly IVisitorAnalyticsService _analyticsService;
        private readonly Parser _uaParser;

        private const string VISITORS_CACHE_KEY = "active_visitors";
        private readonly TimeSpan VISITOR_EXPIRY = TimeSpan.FromHours(1);

        // Gereksiz sık güncellemeleri önlemek için
        private const int BROADCAST_INTERVAL_SECONDS = 15;
        private static DateTime _lastBroadcastTime = DateTime.MinValue;
        private readonly IBackgroundTaskQueue _backgroundTaskQueue;

        public EnhancedVisitorTrackingHub(
            ILogger<EnhancedVisitorTrackingHub> logger,
            IUserService userService,
            ICacheService cacheService,
            IVisitorAnalyticsService analyticsService,
            IBackgroundTaskQueue backgroundTaskQueue)
        {
            _logger = logger;
            _userService = userService;
            _cacheService = cacheService;
            _analyticsService = analyticsService;
            _backgroundTaskQueue = backgroundTaskQueue;
            _uaParser = Parser.GetDefault();
        }

        // OnConnectedAsync metodunu daha da optimize edelim
        public override async Task OnConnectedAsync()
{
    try 
    {
        _logger.LogInformation("VisitorTrackingHub - Bağlantı başlatılıyor: {ConnectionId}", Context.ConnectionId);
        
        // Hub context bilgilerini önce yerel değişkenlere kopyala
        var connectionId = Context.ConnectionId;
        var httpContext = Context.GetHttpContext();
        var isAuthenticated = Context.User?.Identity?.IsAuthenticated ?? false;
        var username = isAuthenticated ? Context.User?.Identity?.Name : "Anonim";
        var ipAddress = httpContext?.GetRealIpAddress() ?? "Bilinmiyor";
        var userAgent = httpContext?.Request.Headers["User-Agent"].ToString() ?? "Bilinmiyor";
        
        // Hub nesnesini kullanan işlemleri şimdi tamamla
        await base.OnConnectedAsync();
        
        // Eğer admin ise, gruba ekleyin (hub metodu içinde yapılmalı)
        if (isAuthenticated)
        {
            bool isAdmin = await _userService.IsAdminAsync();
            if (isAdmin)
            {
                await Groups.AddToGroupAsync(connectionId, "Admins");
            }
        }
        
        // Ziyaretçi verilerini BackgroundTaskQueue kullanarak kaydet
        _backgroundTaskQueue.QueueBackgroundWorkItem(async (cancellationToken) => {
            // Önemli: BackgroundTaskQueue içinde IServiceProvider'a erişimimiz yok
            // Bu nedenle gereken tüm servisleri enjekte etmeliyiz

            try 
            {
                // Ziyaretçi nesnesini oluştur
                var session = new VisitorTracking
                {
                    Id = connectionId,
                    IpAddress = ipAddress,
                    IsAuthenticated = isAuthenticated,
                    Username = username,
                    ConnectionId = connectionId,
                    ConnectedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow,
                    CurrentPage = "/",
                    UserAgent = userAgent
                };
                
                // Enjekte edilmiş cache servisini kullan
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
                
                await _cacheService.SetAsync($"{VISITORS_CACHE_KEY}:{connectionId}", session, 
                                          VISITOR_EXPIRY, linkedCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ziyaretçi bilgilerini saklarken hata: {ConnectionId}", connectionId);
            }
        });
        
        _logger.LogInformation("Ziyaretçi bağlantısı tamamlandı: {ConnectionId}", connectionId);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "VisitorTrackingHub - OnConnectedAsync'de hata");
        throw;
    }
}

        public async Task PageChanged(string page)
        {
            try
            {
                var visitorId = Context.ConnectionId;
        
                // Task.Run ile sayfa değişikliğini arka planda işle
                _ = Task.Run(async () => {
                    try
                    {
                        // Timeout ekle
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                
                        // Ziyaretçi bilgilerini al ve güncelle
                        var result = await _cacheService.TryGetValueAsync<VisitorTracking>(
                            $"{VISITORS_CACHE_KEY}:{visitorId}", cts.Token);
                
                        bool success = result.success;
                        VisitorTracking? session = result.value;
                
                        if (success && session != null)
                        {
                            // Optimizasyon: Aynı sayfaysa gereksiz işlem yapma
                            if (session.CurrentPage == page)
                            {
                                return;
                            }
                    
                            session.CurrentPage = page;
                            session.LastActivityAt = DateTime.UtcNow;
                    
                            // Güncellenmiş bilgileri Redis'e kaydet
                            await _cacheService.SetAsync($"{VISITORS_CACHE_KEY}:{visitorId}", 
                                session, VISITOR_EXPIRY, cts.Token);
                    
                            _logger.LogDebug($"Ziyaretçi {visitorId} şu sayfaya navigasyon yaptı: {page}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "PageChanged metodunda hata: {Message}", ex.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PageChanged metodunda hata: {Message}", ex.Message);
            }
        }

        private async Task SendAdminInitialData()
        {
            try
            {
                // Daha önceden hesaplanmış önbellekteki verileri kontrol etmek için iyileştirme fırsatı

                // Tüm aktif ziyaretçileri al ve gönder - Bu maliyetli bir işlem, optimize edilebilir
                var activeVisitors = await GetActiveVisitorsAsync();
                await Clients.Caller.SendAsync("ReceiveVisitorsList", activeVisitors);

                // Google Analytics durumunu gönder - Sabit değer, optimize edilebilir
                await Clients.Caller.SendAsync("ReceiveAnalyticsStatus", new
                {
                    GoogleAnalyticsEnabled = true
                });

                // Son 30 günlük özet istatistikleri gönder - Önbelleğe alınabilir
                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

                // Burada önbellek eklenebilir
                var cacheKey = $"analytics_summary_30days_{DateTime.UtcNow:yyyyMMdd}";
                var (exists, cachedSummary) = await _cacheService.TryGetValueAsync<object>(cacheKey,cancellationToken: CancellationToken.None);

                if (exists && cachedSummary != null)
                {
                    await Clients.Caller.SendAsync("ReceiveAnalyticsSummary", cachedSummary);
                }
                else
                {
                    var summary = await _analyticsService.GetDateRangeAnalyticsAsync(thirtyDaysAgo, DateTime.UtcNow);

                    // 1 günlük süreyle önbelleğe al
                    await _cacheService.SetAsync(cacheKey, summary, TimeSpan.FromHours(24),cancellationToken: CancellationToken.None);

                    await Clients.Caller.SendAsync("ReceiveAnalyticsSummary", summary);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin için başlangıç verilerini gönderirken hata oluştu");
            }
        }

        private string DetermineDeviceType(string userAgent, ClientInfo clientInfo)
        {
            // Regex yerine daha hızlı string.Contains kullan
            if (clientInfo.Device.Family.Contains("Mobile") ||
                userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
                userAgent.Contains("iPod", StringComparison.OrdinalIgnoreCase))
            {
                return "Mobil";
            }
            else if (clientInfo.Device.Family.Contains("Tablet") ||
                     userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
            {
                return "Tablet";
            }
            else
            {
                return "Masaüstü";
            }
        }

        private async Task ThrottledBroadcastVisitorStats()
        {
            // İstatistikleri sadece belirli aralıklarla yayınla
            var now = DateTime.UtcNow;
            if ((now - _lastBroadcastTime).TotalSeconds < BROADCAST_INTERVAL_SECONDS)
            {
                _logger.LogDebug("Broadcast istek sınırlaması nedeniyle atlandı. Son yayın: {LastBroadcast}",
                    _lastBroadcastTime);
                return;
            }

            await BroadcastVisitorStats();
            _lastBroadcastTime = now;
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            try
            {
                var visitorId = Context.ConnectionId;

                // Redis'ten ziyaretçiyi kaldır
                await _cacheService.RemoveAsync($"{VISITORS_CACHE_KEY}:{visitorId}",cancellationToken: CancellationToken.None);

                // Kısıtlama uygula
                await ThrottledBroadcastVisitorStats();

                _logger.LogInformation($"Ziyaretçi ayrıldı: {visitorId}");

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnDisconnectedAsync işleminde hata oluştu");
                throw;
            }
        }

        // Yeni Metotlar

        // Tarih aralığına göre ziyaretçi istatistiklerini getir
        public async Task GetVisitorStatsByDate(DateTime startDate, DateTime endDate)
        {
            try
            {
                var isAdmin = await _userService.IsAdminAsync();
                if (!isAdmin)
                {
                    await Clients.Caller.SendAsync("ReceiveError",
                        "Bu işlemi gerçekleştirmek için yetkiniz bulunmuyor.");
                    return;
                }

                // Tarih aralığını UTC'ye çevir
                var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0,
                    DateTimeKind.Utc);
                var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);

                // İstatistikleri getir
                var stats = await _analyticsService.GetDateRangeAnalyticsAsync(utcStartDate, utcEndDate);

                // İsteyen kullanıcıya gönder
                await Clients.Caller.SendAsync("ReceiveVisitorStatsByDate", stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetVisitorStatsByDate metodunda hata oluştu");
                await Clients.Caller.SendAsync("ReceiveError",
                    "İstatistikler alınırken bir hata oluştu: " + ex.Message);
            }
        }

        // En çok görüntülenen sayfaları getir
        public async Task GetTopPages(DateTime startDate, DateTime endDate, int limit = 10)
        {
            try
            {
                var isAdmin = await _userService.IsAdminAsync();
                if (!isAdmin)
                {
                    await Clients.Caller.SendAsync("ReceiveError",
                        "Bu işlemi gerçekleştirmek için yetkiniz bulunmuyor.");
                    return;
                }

                // Tarih aralığını UTC'ye çevir
                var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0,
                    DateTimeKind.Utc);
                var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);

                // En çok görüntülenen sayfaları getir
                var topPages = await _analyticsService.GetTopPagesAsync(utcStartDate, utcEndDate, limit);

                // İsteyen kullanıcıya gönder
                await Clients.Caller.SendAsync("ReceiveTopPages", topPages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTopPages metodunda hata oluştu");
                await Clients.Caller.SendAsync("ReceiveError",
                    "Popüler sayfalar alınırken bir hata oluştu: " + ex.Message);
            }
        }

        // Referrer kaynaklarını getir
        public async Task GetTopReferrers(DateTime startDate, DateTime endDate, int limit = 10)
        {
            try
            {
                var isAdmin = await _userService.IsAdminAsync();
                if (!isAdmin)
                {
                    await Clients.Caller.SendAsync("ReceiveError",
                        "Bu işlemi gerçekleştirmek için yetkiniz bulunmuyor.");
                    return;
                }

                // Tarih aralığını UTC'ye çevir
                var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0,
                    DateTimeKind.Utc);
                var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);

                // En çok görüntülenen sayfaları getir
                var topReferrers = await _analyticsService.GetTopReferrersAsync(utcStartDate, utcEndDate, limit);

                // İsteyen kullanıcıya gönder
                await Clients.Caller.SendAsync("ReceiveTopReferrers", topReferrers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTopReferrers metodunda hata oluştu");
                await Clients.Caller.SendAsync("ReceiveError",
                    "Referrer kaynakları alınırken bir hata oluştu: " + ex.Message);
            }
        }

        private async Task<List<VisitorTracking>> GetActiveVisitorsAsync()
        {
            try
            {
                // Maksimum 3 saniye süreyle çalış, sonra iptal et
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        
                // 1. Önce ziyaretçi keyleri için cache sorgusu yap
                var allVisitorKeys = await _cacheService.GetKeysAsync($"{VISITORS_CACHE_KEY}:*", cts.Token);
        
                if (!allVisitorKeys.Any())
                    return new List<VisitorTracking>();
            
                // 2. Veri sayısını sınırlandır - en fazla 100 ziyaretçi getir (performans için)
                var limitedKeys = allVisitorKeys.Take(100).ToList();
        
                // 3. Sınırlı anahtar kümesiyle verileri getir
                using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var visitorData = await _cacheService.GetManyAsync<VisitorTracking>(limitedKeys, cts2.Token);
        
                return visitorData.Values.ToList();
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("GetActiveVisitorsAsync - Zaman aşımı oluştu");
                return new List<VisitorTracking>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetActiveVisitorsAsync - Aktif ziyaretçileri alırken hata oluştu");
                return new List<VisitorTracking>();
            }
        }

        private async Task BroadcastVisitorStats()
        {
            try 
            {
                _logger.LogInformation("BroadcastVisitorStats - İstatistikler hesaplanıyor...");
        
                List<VisitorTracking> activeVisitors;
        
                try {
                    // Timeout ile aktif ziyaretçileri getir
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    activeVisitors = await GetActiveVisitorsAsync();
                } catch (Exception ex) {
                    _logger.LogError(ex, "Ziyaretçi listesi alınırken hata oluştu");
                    // Redis çalışmasa bile bir yanıt dönebilmek için boş liste kullan
                    activeVisitors = new List<VisitorTracking>();
                }
        
                // İstatistikleri hesapla
                var totalVisitors = activeVisitors.Count;
                var authenticatedVisitors = activeVisitors.Count(v => v.IsAuthenticated);
                var anonymousVisitors = totalVisitors - authenticatedVisitors;
        
                // Sadece en temel verileri topla
                var stats = new
                {
                    TotalVisitors = totalVisitors,
                    AuthenticatedVisitors = authenticatedVisitors,
                    AnonymousVisitors = anonymousVisitors,
                    // Daha fazla veri ekleyebilirsiniz, ancak performans sorunları nedeniyle sınırlı tutun
                };

                await Clients.Group("Admins").SendAsync("ReceiveVisitorStats", stats);
                _logger.LogInformation("BroadcastVisitorStats - Temel istatistikler gönderildi.");
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "BroadcastVisitorStats - Hata: {ErrorMessage}", ex.Message);
            }
        }

        private string DetermineReferrerType(string referrerDomain)
        {
            if (string.IsNullOrEmpty(referrerDomain))
                return "Doğrudan";

            // Arama motorları
            if (referrerDomain.Contains("google.") ||
                referrerDomain.Contains("bing.") ||
                referrerDomain.Contains("yahoo.") ||
                referrerDomain.Contains("yandex."))
                return "Arama Motoru";

            // Sosyal medya
            if (referrerDomain.Contains("facebook.") ||
                referrerDomain.Contains("instagram.") ||
                referrerDomain.Contains("twitter.") ||
                referrerDomain.Contains("linkedin.") ||
                referrerDomain.Contains("t.co") ||
                referrerDomain.Contains("youtube.") ||
                referrerDomain.Contains("pinterest."))
                return "Sosyal Medya";

            // E-posta servisleri
            if (referrerDomain.Contains("mail.") ||
                referrerDomain.Contains("gmail.") ||
                referrerDomain.Contains("outlook.") ||
                referrerDomain.Contains("yahoo."))
                return "E-posta";

            return "Diğer";
        }
    }
}