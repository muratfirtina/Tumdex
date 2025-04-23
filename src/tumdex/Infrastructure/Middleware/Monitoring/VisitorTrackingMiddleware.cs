using System.Security.Claims;
using System.Text.RegularExpressions;
using Application.Models.Monitoring;
using Application.Repositories;
using Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UAParser;

namespace Infrastructure.Middleware.Monitoring
{
    public class VisitorTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<VisitorTrackingMiddleware> _logger;
        private readonly Parser _uaParser;

        public VisitorTrackingMiddleware(
            RequestDelegate next,
            ILogger<VisitorTrackingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
            _uaParser = Parser.GetDefault();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Statik dosya isteklerini veya API çağrılarını takip etmiyoruz
            string path = context.Request.Path.Value?.ToLower() ?? string.Empty;
            
            if (!path.Contains(".") && 
                !path.StartsWith("/api/") && 
                !path.StartsWith("/signalr/") &&
                !path.Contains("favicon.ico") &&
                !IsBot(context.Request.Headers["User-Agent"].ToString()))
            {
                try
                {
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

                    // Kullanıcı kimlik bilgisini al
                    bool isAuthenticated = context.User?.Identity?.IsAuthenticated ?? false;
                    string username = "Anonim";
                    string userId = null;
                    
                    if (isAuthenticated)
                    {
                        username = context.User.Identity.Name ?? "Anonim";
                        
                        // Eğer name claim yoksa, email claim'i kontrol et
                        if (string.IsNullOrEmpty(username))
                        {
                            username = context.User.FindFirstValue(System.Security.Claims.ClaimTypes.Email) ?? "Anonim";
                        }
                        
                        // Kullanıcı ID'sini al
                        userId = context.User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
                    }
                    
                    // Referrer (yönlendiren) URL
                    string referrer = context.Request.Headers["Referer"].ToString();
                    
                    // IP adresi
                    string ipAddress = GetClientIpAddress(context);
                    
                    // User-Agent
                    string userAgent = context.Request.Headers["User-Agent"].ToString();
                    
                    // Cihaz ve tarayıcı bilgilerini analiz et
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
                    
                    // UTM parametrelerini alma
                    context.Request.Query.TryGetValue("utm_source", out var utmSource);
                    context.Request.Query.TryGetValue("utm_medium", out var utmMedium);
                    context.Request.Query.TryGetValue("utm_campaign", out var utmCampaign);
                    
                    // Referrer domain ve tipini çıkar
                    string referrerDomain = string.Empty;
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
                            referrerType = "Diğer";
                        }
                    }
                    
                    // Ziyaret kaydı oluştur
                    var visitEvent = new VisitorTrackingEvent
                    {
                        SessionId = sessionId,
                        IpAddress = ipAddress,
                        UserAgent = userAgent,
                        Page = context.Request.Path.Value ?? "/",
                        IsAuthenticated = isAuthenticated,
                        Username = username ?? "Anonim",
                        VisitTime = DateTime.UtcNow,
                        Referrer = referrer ?? string.Empty, // Null ise boş string kullan
                        ReferrerDomain = referrerDomain, // Artık bu değer asla null olmayacak
                        ReferrerType = referrerType,
                        UTMSource = utmSource.ToString() ?? string.Empty, // Null olabilecek değerler için boş string
                        UTMMedium = utmMedium.ToString() ?? string.Empty,
                        UTMCampaign = utmCampaign.ToString() ?? string.Empty,
                        IsNewVisitor = isNewVisitor,
                        BrowserName = clientInfo.Browser.Family,
                        DeviceType = deviceType
                    };
                    
                    // Servis locator ile repository'e eriş ve ziyareti kaydet
                    using (var scope = context.RequestServices.CreateScope())
                    {
                        var repository = scope.ServiceProvider.GetRequiredService<IVisitorAnalyticsRepository>();
                        await repository.LogVisitAsync(visitEvent);
                    }
                    
                    _logger.LogDebug(
                        "Sayfa ziyareti kaydedildi: {Page}, Kullanıcı: {User}, IP: {IP}, Referrer: {Referrer}, Device: {Device}",
                        context.Request.Path.Value, 
                        isAuthenticated ? username : "Anonim", 
                        ipAddress,
                        string.IsNullOrEmpty(referrer) ? "Doğrudan" : referrer,
                        deviceType);
                }
                catch (Exception ex)
                {
                    // Middleware hatası uygulamayı durdurmamalı
                    _logger.LogError(ex, "Ziyaretçi takibi sırasında hata oluştu: {Message}", ex.Message);
                }
            }
            
            // İşlem hattını devam ettir
            await _next(context);
        }
        
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
        
        private bool IsBot(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
                return false;
                
            // Yaygın bot/crawler user agent'larını kontrol et
            return userAgent.Contains("bot") || 
                   userAgent.Contains("crawler") || 
                   userAgent.Contains("spider") || 
                   userAgent.Contains("googlebot") ||
                   userAgent.Contains("bingbot") ||
                   userAgent.Contains("yandex") ||
                   userAgent.Contains("baidu") ||
                   userAgent.Contains("ahref") ||
                   userAgent.Contains("semrush");
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

    // Extension metodu
    public static class VisitorTrackingMiddlewareExtensions
    {
        public static IApplicationBuilder UseVisitorTracking(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<VisitorTrackingMiddleware>();
        }
    }
}