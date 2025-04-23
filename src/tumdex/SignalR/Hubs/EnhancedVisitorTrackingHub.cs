using Application.Abstraction.Services;
using Application.Abstraction.Services.Utilities;
using Application.Models.Monitoring;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SignalR.Extensions;
using System.Text.RegularExpressions;
using Application.Services;
using UAParser;

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

        public EnhancedVisitorTrackingHub(
            ILogger<EnhancedVisitorTrackingHub> logger,
            IUserService userService,
            ICacheService cacheService,
            IVisitorAnalyticsService analyticsService)
        {
            _logger = logger;
            _userService = userService;
            _cacheService = cacheService;
            _analyticsService = analyticsService;
            _uaParser = Parser.GetDefault();
        }

        public override async Task OnConnectedAsync()
        {
            try 
            {
                _logger.LogInformation("VisitorTrackingHub - Bağlantı başlatılıyor. ConnectionId: {ConnectionId}", Context.ConnectionId);
                await base.OnConnectedAsync();

                var httpContext = Context.GetHttpContext();
                var isAuthenticated = Context.User?.Identity?.IsAuthenticated ?? false;
                var username = isAuthenticated ? Context.User?.Identity?.Name : "Anonim";
                
                // Extension metodunu kullanarak IP adresini al
                string ipAddress = "Bilinmiyor";
                if (httpContext != null)
                {
                    // Extension metodunu kullan
                    ipAddress = httpContext.GetRealIpAddress();
                }
                
                // User-Agent ve Referrer bilgilerini al
                var userAgent = httpContext?.Request.Headers["User-Agent"].ToString() ?? "Bilinmiyor";
                var referrer = httpContext?.Request.Headers["Referer"].ToString() ?? "";
                
                // User-Agent parsing işlemi
                var clientInfo = _uaParser.Parse(userAgent);
                
                var deviceType = "Bilinmiyor";
                if (clientInfo.Device.Family.Contains("Mobile") || 
                    Regex.IsMatch(userAgent, "(Android|iPhone|iPod)", RegexOptions.IgnoreCase))
                {
                    deviceType = "Mobil";
                }
                else if (clientInfo.Device.Family.Contains("Tablet") || 
                    Regex.IsMatch(userAgent, "(iPad)", RegexOptions.IgnoreCase))
                {
                    deviceType = "Tablet";
                }
                else
                {
                    deviceType = "Masaüstü";
                }
                
                // Referrer tipini belirle
                string referrerDomain = "";
                string referrerType = "Doğrudan";
                
                if (!string.IsNullOrEmpty(referrer))
                {
                    try
                    {
                        var uri = new Uri(referrer);
                        referrerDomain = uri.Host;
                        referrerType = DetermineReferrerType(referrerDomain);
                    }
                    catch
                    {
                        referrerDomain = referrer;
                    }
                }
                
                var visitorId = Context.ConnectionId;
                var session = new VisitorTracking
                {
                    Id = visitorId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    IsAuthenticated = isAuthenticated,
                    Username = username,
                    ConnectionId = Context.ConnectionId,
                    ConnectedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow,
                    CurrentPage = "/",
                    Referrer = referrer,
                    DeviceType = deviceType,
                    BrowserName = clientInfo.Browser.Family
                };

                // Redis'e ziyaretçi bilgilerini ekle
                await _cacheService.SetAsync($"{VISITORS_CACHE_KEY}:{visitorId}", session, VISITOR_EXPIRY);

                // Eğer admin ise, admin grubuna ekle
                if (isAuthenticated)
                {
                    bool isAdmin = await _userService.IsAdminAsync();
                    if (isAdmin)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
                        
                        // Tüm aktif ziyaretçileri al ve gönder
                        var activeVisitors = await GetActiveVisitorsAsync();
                        await Clients.Caller.SendAsync("ReceiveVisitorsList", activeVisitors);
                        
                        // Google Analytics durumunu da gönder
                        await Clients.Caller.SendAsync("ReceiveAnalyticsStatus", new { 
                            GoogleAnalyticsEnabled = true 
                        });
                        
                        // Son 30 günlük özet istatistikleri gönder
                        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
                        var summary = await _analyticsService.GetDateRangeAnalyticsAsync(thirtyDaysAgo, DateTime.UtcNow);
                        await Clients.Caller.SendAsync("ReceiveAnalyticsSummary", summary);
                    }
                }

                await BroadcastVisitorStats();
                _logger.LogInformation($"Ziyaretçi bağlandı: {visitorId}, IP: {ipAddress}, Referrer: {referrerDomain}, Device: {deviceType}, Browser: {clientInfo.Browser.Family}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VisitorTrackingHub - OnConnectedAsync'de hata: {ErrorMessage}", ex.Message);
                throw; // Hatayı client'a yönlendir
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            try
            {
                var visitorId = Context.ConnectionId;
                
                // Redis'ten ziyaretçiyi kaldır
                await _cacheService.RemoveAsync($"{VISITORS_CACHE_KEY}:{visitorId}");
                
                await BroadcastVisitorStats();
                _logger.LogInformation($"Ziyaretçi ayrıldı: {visitorId}");
                
                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnDisconnectedAsync işleminde hata oluştu");
                throw;
            }
        }

        public async Task PageChanged(string page)
        {
            try
            {
                var visitorId = Context.ConnectionId;
                
                // Ziyaretçi bilgilerini al ve güncelle
                var (success, session) = await _cacheService.TryGetValueAsync<VisitorTracking>($"{VISITORS_CACHE_KEY}:{visitorId}");
                
                if (success && session != null)
                {
                    session.CurrentPage = page;
                    session.LastActivityAt = DateTime.UtcNow;
                    
                    // Güncellenmiş bilgileri Redis'e kaydet
                    await _cacheService.SetAsync($"{VISITORS_CACHE_KEY}:{visitorId}", session, VISITOR_EXPIRY);
                    
                    await BroadcastVisitorStats();
                    _logger.LogDebug($"Ziyaretçi {visitorId} şu sayfaya navigasyon yaptı: {page}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PageChanged metodunda hata oluştu: {Message}", ex.Message);
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
                    await Clients.Caller.SendAsync("ReceiveError", "Bu işlemi gerçekleştirmek için yetkiniz bulunmuyor.");
                    return;
                }
                
                // Tarih aralığını UTC'ye çevir
                var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc);
                var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);
                
                // İstatistikleri getir
                var stats = await _analyticsService.GetDateRangeAnalyticsAsync(utcStartDate, utcEndDate);
                
                // İsteyen kullanıcıya gönder
                await Clients.Caller.SendAsync("ReceiveVisitorStatsByDate", stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetVisitorStatsByDate metodunda hata oluştu");
                await Clients.Caller.SendAsync("ReceiveError", "İstatistikler alınırken bir hata oluştu: " + ex.Message);
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
                    await Clients.Caller.SendAsync("ReceiveError", "Bu işlemi gerçekleştirmek için yetkiniz bulunmuyor.");
                    return;
                }
                
                // Tarih aralığını UTC'ye çevir
                var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc);
                var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);
                
                // En çok görüntülenen sayfaları getir
                var topPages = await _analyticsService.GetTopPagesAsync(utcStartDate, utcEndDate, limit);
                
                // İsteyen kullanıcıya gönder
                await Clients.Caller.SendAsync("ReceiveTopPages", topPages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTopPages metodunda hata oluştu");
                await Clients.Caller.SendAsync("ReceiveError", "Popüler sayfalar alınırken bir hata oluştu: " + ex.Message);
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
                    await Clients.Caller.SendAsync("ReceiveError", "Bu işlemi gerçekleştirmek için yetkiniz bulunmuyor.");
                    return;
                }
                
                // Tarih aralığını UTC'ye çevir
                var utcStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc);
                var utcEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);
                
                // En çok görüntülenen sayfaları getir
                var topReferrers = await _analyticsService.GetTopReferrersAsync(utcStartDate, utcEndDate, limit);
                
                // İsteyen kullanıcıya gönder
                await Clients.Caller.SendAsync("ReceiveTopReferrers", topReferrers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTopReferrers metodunda hata oluştu");
                await Clients.Caller.SendAsync("ReceiveError", "Referrer kaynakları alınırken bir hata oluştu: " + ex.Message);
            }
        }

        private async Task<List<VisitorTracking>> GetActiveVisitorsAsync()
        {
            try
            {
                var activeVisitors = new List<VisitorTracking>();
        
                // Debug log ekleyelim
                _logger.LogInformation("GetActiveVisitorsAsync - Aktif ziyaretçiler alınıyor...");
        
                // Alternativ yaklaşım: Tüm keyleri almak için Redis SCAN kullanarak önce keyleri bulun
                var allVisitorKeys = await _cacheService.GetKeysAsync($"{VISITORS_CACHE_KEY}:*");
                _logger.LogInformation("GetActiveVisitorsAsync - Redis'ten {KeyCount} anahtar bulundu", allVisitorKeys.Count);

                if (allVisitorKeys.Any())
                {
                    var visitorData = await _cacheService.GetManyAsync<VisitorTracking>(allVisitorKeys);
                    activeVisitors = new List<VisitorTracking>(visitorData.Values);
                    _logger.LogInformation("GetActiveVisitorsAsync - {Count} ziyaretçi verisi çekildi", activeVisitors.Count);
                }
                else
                {
                    _logger.LogWarning("GetActiveVisitorsAsync - Ziyaretçi anahtarı bulunamadı");
                }
        
                return activeVisitors;
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
                // Redis'ten aktif ziyaretçileri al
                var activeVisitors = await GetActiveVisitorsAsync();
                
                // İstatistikleri hesapla
                var totalVisitors = activeVisitors.Count;
                var authenticatedVisitors = activeVisitors.Count(v => v.IsAuthenticated);
                var anonymousVisitors = totalVisitors - authenticatedVisitors;
                
                // Sayfa istatistikleri
                var pageStats = activeVisitors
                    .GroupBy(v => v.CurrentPage)
                    .Select(g => new { Page = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList();
                    
                // Trafik kaynakları (Referrer)
                var referrerStats = activeVisitors
                    .Where(v => !string.IsNullOrEmpty(v.Referrer))
                    .GroupBy(v => v.Referrer)
                    .Select(g => new { Referrer = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList();
                    
                // Cihaz istatistikleri
                var deviceStats = activeVisitors
                    .GroupBy(v => v.DeviceType)
                    .Select(g => new { Device = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList();
                    
                // Tarayıcı istatistikleri
                var browserStats = activeVisitors
                    .GroupBy(v => v.BrowserName)
                    .Select(g => new { Browser = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                var stats = new
                {
                    TotalVisitors = totalVisitors,
                    AuthenticatedVisitors = authenticatedVisitors,
                    AnonymousVisitors = anonymousVisitors,
                    PageStats = pageStats,
                    ReferrerStats = referrerStats,
                    DeviceStats = deviceStats,
                    BrowserStats = browserStats,
                    ActiveVisitors = activeVisitors
                };

                _logger.LogInformation("BroadcastVisitorStats - İstatistikler gönderiliyor. TotalVisitors: {TotalVisitors}, AuthVisitors: {AuthVisitors}",
                    totalVisitors, authenticatedVisitors);
        
                await Clients.Group("Admins").SendAsync("ReceiveVisitorStats", stats);
                _logger.LogInformation("BroadcastVisitorStats - İstatistikler gönderildi.");
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