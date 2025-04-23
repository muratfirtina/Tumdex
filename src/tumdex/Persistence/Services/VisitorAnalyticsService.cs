using System.Text.RegularExpressions;
using Application.Models.Monitoring.Analytics;
using Application.Repositories;
using Application.Services;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using UAParser;

namespace Persistence.Services;
public class VisitorAnalyticsService : IVisitorAnalyticsService
{
    private readonly IVisitorAnalyticsRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<VisitorAnalyticsService> _logger;
    private readonly Parser _uaParser;

    public VisitorAnalyticsService(
        IVisitorAnalyticsRepository repository,
        IMemoryCache cache,
        ILogger<VisitorAnalyticsService> logger,
        Parser uaParser)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
        _uaParser = uaParser;
    }

    public async Task LogVisitAsync(HttpContext context, bool isAuthenticated, string username = null)
    {
        try
        {
            var referrer = context.Request.Headers["Referer"].ToString();
            var userAgent = context.Request.Headers["User-Agent"].ToString();
            var url = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}";
            
            // Oturum ID'sini çerezden al veya yeni oluştur
            var sessionId = context.Request.Cookies["visitor_session_id"];
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString();
                context.Response.Cookies.Append("visitor_session_id", sessionId, new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddDays(1),
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax
                });
            }

            // UTM parametrelerini alma
            context.Request.Query.TryGetValue("utm_source", out var utmSource);
            context.Request.Query.TryGetValue("utm_medium", out var utmMedium);
            context.Request.Query.TryGetValue("utm_campaign", out var utmCampaign);

            // IP adresi
            string ipAddress = GetClientIpAddress(context);
            
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

            // Referrer domain'ini ayıklama
            string referrerDomain = string.Empty; // Varsayılan değer boş string
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
                    // URI ayrıştırılamazsa, referrer string'ini domain olarak kullan
                    referrerDomain = referrer;
                    referrerType = "Diğer";
                }
            }

            // İlk ziyaret mi kontrol et
            bool isNewVisitor = !context.Request.Cookies.ContainsKey("returning_visitor");
            if (isNewVisitor)
            {
                context.Response.Cookies.Append("returning_visitor", "true", new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddYears(1),
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax
                });
            }

            // Yeni ziyaret kaydı oluştur
            var visitEvent = new VisitorTrackingEvent
            {
                SessionId = sessionId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Page = context.Request.Path.Value ?? "/",
                IsAuthenticated = isAuthenticated,
                Username = username ?? "Anonim",
                VisitTime = DateTime.UtcNow,
                Referrer = referrer ?? string.Empty,
                ReferrerDomain = referrerDomain, // Artık boş string veya değer içeriyor
                ReferrerType = referrerType,
                UTMSource = utmSource.ToString() ?? string.Empty,
                UTMMedium = utmMedium.ToString() ?? string.Empty,
                UTMCampaign = utmCampaign.ToString() ?? string.Empty,
                IsNewVisitor = isNewVisitor,
                BrowserName = clientInfo.Browser.Family ?? "Bilinmiyor",
                DeviceType = deviceType
            };

            // Ziyaret kaydını veritabanına kaydet
            await _repository.LogVisitAsync(visitEvent);

            // Günlük özet istatistiklerini önbellekten temizle (yeniden hesaplanması için)
            string cacheKey = $"analytics_summary_{DateTime.UtcNow:yyyy-MM-dd}";
            _cache.Remove(cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ziyaret kaydedilirken hata oluştu");
        }
    }

    public async Task<VisitorAnalyticsSummary> GetDailyAnalyticsAsync(DateTime date)
    {
        // Önbellek anahtarı oluştur
        string cacheKey = $"analytics_summary_{date:yyyy-MM-dd}";
        
        // Önbellekte varsa getir
        if (_cache.TryGetValue(cacheKey, out VisitorAnalyticsSummary cachedSummary))
        {
            return cachedSummary;
        }
        
        // Yoksa repository'den getir
        var summary = await _repository.GetDailyAnalyticsAsync(date);
        
        // Önbelleğe kaydet (24 saat süreyle)
        _cache.Set(cacheKey, summary, TimeSpan.FromHours(24));
        
        return summary;
    }

    public async Task<VisitorAnalyticsSummary> GetDateRangeAnalyticsAsync(DateTime startDate, DateTime endDate)
    {
        // Tarih aralığı çok büyükse sınırlandırma
        if ((endDate - startDate).TotalDays > 90)
        {
            _logger.LogWarning("90 günden fazla veri sorgusu istendi, 90 gün ile sınırlandırılıyor");
            startDate = endDate.AddDays(-90);
        }
        
        // Önbellek anahtarı
        string cacheKey = $"analytics_range_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}";
        
        // Önbellekte varsa getir
        if (_cache.TryGetValue(cacheKey, out VisitorAnalyticsSummary cachedSummary))
        {
            return cachedSummary;
        }
        
        // Yoksa repository'den getir
        var summary = await _repository.GetDateRangeAnalyticsAsync(startDate, endDate);
        
        // Önbelleğe kaydet (6 saat süreyle)
        _cache.Set(cacheKey, summary, TimeSpan.FromHours(6));
        
        return summary;
    }

    public async Task<List<ReferrerSummary>> GetTopReferrersAsync(DateTime startDate, DateTime endDate, int limit = 10)
    {
        // Önbellek anahtarı
        string cacheKey = $"top_referrers_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}_{limit}";
        
        // Önbellekte varsa getir
        if (_cache.TryGetValue(cacheKey, out List<ReferrerSummary> cachedReferrers))
        {
            return cachedReferrers;
        }
        
        // Yoksa repository'den getir
        var referrers = await _repository.GetTopReferrersAsync(startDate, endDate, limit);
        
        // Önbelleğe kaydet (6 saat süreyle)
        _cache.Set(cacheKey, referrers, TimeSpan.FromHours(6));
        
        return referrers;
    }

    public async Task<List<PageViewSummary>> GetTopPagesAsync(DateTime startDate, DateTime endDate, int limit = 10)
    {
        // Önbellek anahtarı
        string cacheKey = $"top_pages_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}_{limit}";
        
        // Önbellekte varsa getir
        if (_cache.TryGetValue(cacheKey, out List<PageViewSummary> cachedPages))
        {
            return cachedPages;
        }
        
        // Yoksa repository'den getir
        var pages = await _repository.GetTopPagesAsync(startDate, endDate, limit);
        
        // Önbelleğe kaydet (6 saat süreyle)
        _cache.Set(cacheKey, pages, TimeSpan.FromHours(6));
        
        return pages;
    }

    public async Task<List<CampaignSummary>> GetTopCampaignsAsync(DateTime startDate, DateTime endDate, int limit = 10)
    {
        // Önbellek anahtarı
        string cacheKey = $"top_campaigns_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}_{limit}";
        
        // Önbellekte varsa getir
        if (_cache.TryGetValue(cacheKey, out List<CampaignSummary> cachedCampaigns))
        {
            return cachedCampaigns;
        }
        
        // Yoksa repository'den getir
        var campaigns = await _repository.GetTopCampaignsAsync(startDate, endDate, limit);
        
        // Önbelleğe kaydet (6 saat süreyle)
        _cache.Set(cacheKey, campaigns, TimeSpan.FromHours(6));
        
        return campaigns;
    }

    public async Task<List<GeographySummary>> GetTopLocationsAsync(DateTime startDate, DateTime endDate, int limit = 10)
    {
        // Önbellek anahtarı
        string cacheKey = $"top_locations_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}_{limit}";
        
        // Önbellekte varsa getir
        if (_cache.TryGetValue(cacheKey, out List<GeographySummary> cachedLocations))
        {
            return cachedLocations;
        }
        
        // Yoksa repository'den getir
        var locations = await _repository.GetTopLocationsAsync(startDate, endDate, limit);
        
        // Önbelleğe kaydet (6 saat süreyle)
        _cache.Set(cacheKey, locations, TimeSpan.FromHours(6));
        
        return locations;
    }

    public async Task<Dictionary<DateTime, int>> GetVisitorTimelineAsync(DateTime startDate, DateTime endDate)
    {
        // Önbellek anahtarı
        string cacheKey = $"visitor_timeline_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}";
        
        // Önbellekte varsa getir
        if (_cache.TryGetValue(cacheKey, out Dictionary<DateTime, int> cachedTimeline))
        {
            return cachedTimeline;
        }
        
        // Yoksa repository'den getir
        var timeline = await _repository.GetVisitorTimelineAsync(startDate, endDate);
        
        // Önbelleğe kaydet (6 saat süreyle)
        _cache.Set(cacheKey, timeline, TimeSpan.FromHours(6));
        
        return timeline;
    }

    #region Helper Methods
    
    private string GetClientIpAddress(HttpContext context)
    {
        string ip = context.Request.Headers["X-Forwarded-For"].ToString();
        if (string.IsNullOrEmpty(ip))
        {
            ip = context.Request.Headers["X-Real-IP"].ToString();
        }
        
        if (string.IsNullOrEmpty(ip))
        {
            ip = context.Connection.RemoteIpAddress?.ToString() ?? "Bilinmiyor";
        }
        
        // Virgülle ayrılmış birden fazla IP varsa, ilkini al
        if (ip.Contains(","))
        {
            ip = ip.Split(',')[0].Trim();
        }
        
        // IPv6 "::1" yerel adresini yakalamak için
        if (ip == "::1")
        {
            ip = "127.0.0.1";
        }
        
        return ip;
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
    
    #endregion
}