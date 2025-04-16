using Application.Abstraction.Services;
using Application.Abstraction.Services.Utilities;
using Application.Models.Monitoring;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SignalR.Extensions;

namespace SignalR.Hubs
{
    public class VisitorTrackingHub : Hub
    {
        private readonly ILogger<VisitorTrackingHub> _logger;
        private readonly IUserService _userService;
        private readonly ICacheService _cacheService;

        private const string VISITORS_CACHE_KEY = "active_visitors";
        private readonly TimeSpan VISITOR_EXPIRY = TimeSpan.FromHours(1);

        public VisitorTrackingHub(
            ILogger<VisitorTrackingHub> logger,
            IUserService userService,
            ICacheService cacheService)
        {
            _logger = logger;
            _userService = userService;
            _cacheService = cacheService;
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
                    
                    // Debug: Tüm header bilgilerini logla (sorun gidermek için)
                    _logger.LogDebug("HTTP Headers: {@Headers}", httpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
                }
                
                var userAgent = httpContext?.Request.Headers["User-Agent"].ToString() ?? "Bilinmiyor";

                var visitorId = Context.ConnectionId;
                var session = new VisitorSession
                {
                    Id = visitorId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    IsAuthenticated = isAuthenticated,
                    Username = username,
                    ConnectionId = Context.ConnectionId,
                    ConnectedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow
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
                    }
                }

                await BroadcastVisitorStats();
                _logger.LogInformation($"Ziyaretçi bağlandı: {visitorId}, IP: {ipAddress}");
                _logger.LogInformation("VisitorTrackingHub - Bağlantı başarılı. ConnectionId: {ConnectionId}, IsAuthenticated: {IsAuthenticated}", 
                    Context.ConnectionId, isAuthenticated);
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
                var (success, session) = await _cacheService.TryGetValueAsync<VisitorSession>($"{VISITORS_CACHE_KEY}:{visitorId}");
                
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

        private async Task<List<VisitorSession>> GetActiveVisitorsAsync()
        {
            try
            {
                var activeVisitors = new List<VisitorSession>();
        
                // Debug log ekleyelim
                _logger.LogInformation("GetActiveVisitorsAsync - Aktif ziyaretçiler alınıyor...");
        
                // Alternativ yaklaşım: Tüm keyleri almak için Redis SCAN kullanarak önce keyleri bulun
                var allVisitorKeys = await _cacheService.GetKeysAsync($"{VISITORS_CACHE_KEY}:*");
                _logger.LogInformation("GetActiveVisitorsAsync - Redis'ten {KeyCount} anahtar bulundu", allVisitorKeys.Count);

                if (allVisitorKeys.Any())
                {
                    var visitorData = await _cacheService.GetManyAsync<VisitorSession>(allVisitorKeys);
                    activeVisitors = new List<VisitorSession>(visitorData.Values);
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
                return new List<VisitorSession>();
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
                var pageStats = activeVisitors
                    .GroupBy(v => v.CurrentPage)
                    .Select(g => new { Page = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                var stats = new
                {
                    TotalVisitors = totalVisitors,
                    AuthenticatedVisitors = authenticatedVisitors,
                    AnonymousVisitors = anonymousVisitors,
                    PageStats = pageStats,
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
    }
}