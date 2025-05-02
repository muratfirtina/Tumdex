using System.Text.Json;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Utilities;
using Application.Enums;
using Domain;
using Domain.Entities;
using Infrastructure.Services.Cache;
using Infrastructure.Services.Security.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Middleware.RateLimiting;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly ICacheService _cache;
    private readonly IMetricsService _metrics;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly RateLimitConfig _settings;
    private readonly SlidingWindowRateLimiter _rateLimiter;
    private readonly SemaphoreSlim _throttler;

    // Ziyaretçi izleme için çerez adı
    private const string VISITOR_ID_COOKIE = "TumdexVid";

    // Çerez geçerlilik süresi (30 gün)
    private static readonly TimeSpan COOKIE_EXPIRY = TimeSpan.FromDays(30);

    // Beyaz listedeki endpoint'ler (rate limit kontrolünden muaf veya farklı limitler)
    private readonly List<string> _whitelistedEndpoints;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        ICacheService cache,
        IMetricsService metrics,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<SecuritySettings> settings,
        SlidingWindowRateLimiter rateLimiter)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));

        var securitySettings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _settings = securitySettings.RateLimitConfig ??
                    throw new ArgumentNullException("RateLimitConfig is not configured");

        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));

        _settings.MaxConcurrentRequests = _settings.MaxConcurrentRequests <= 0 ? 100 : _settings.MaxConcurrentRequests;
        _throttler = new SemaphoreSlim(_settings.MaxConcurrentRequests);

        // Kimlik doğrulanmış ve anonim için varsayılanları yapılandır
        if (_settings.AuthenticatedRequestsPerHour <= 0)
            _settings.AuthenticatedRequestsPerHour = _settings.RequestsPerHour;

        if (_settings.AnonymousRequestsPerHour <= 0)
            _settings.AnonymousRequestsPerHour = _settings.RequestsPerHour / 2;

        // Beyaz listeyi başlat
        _whitelistedEndpoints = new List<string>();
        InitializeWhitelist();

        _logger.LogInformation(
            "Rate limiting initialized with: Auth: {AuthRequests} requests per hour, Anon: {AnonRequests} requests per hour, {MaxConcurrent} concurrent requests, {WhitelistedCount} whitelisted endpoints",
            _settings.AuthenticatedRequestsPerHour,
            _settings.AnonymousRequestsPerHour,
            _settings.MaxConcurrentRequests,
            _whitelistedEndpoints.Count);
    }

    private void InitializeWhitelist()
    {
        // Yüksek frekanslı endpoint'leri beyaz listeye ekle
        _whitelistedEndpoints.Add("/visitor-tracking-hub/negotiate");
        // Endpoint'in tam adını ekleyin
        _whitelistedEndpoints.Add("/visitor-tracking-hub");
        _whitelistedEndpoints.Add("/api/metrics");
        _whitelistedEndpoints.Add("/health");
        _whitelistedEndpoints.Add("/favicon.ico");

        // Yapılandırmadan özel beyaz listeyi ekle
        if (_settings.WhitelistedEndpoints != null && _settings.WhitelistedEndpoints.Any())
        {
            _whitelistedEndpoints.AddRange(_settings.WhitelistedEndpoints);
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // SignalR isteklerini tamamen rate limit dışında tut
        // Path.Value üzerinde tam string kontrolü yerine daha güvenli Contains kullanımı
        if (context.Request.Path.Value?.Contains("/visitor-tracking-hub") == true ||
            context.Request.Path.Value?.Contains("/api/metrics") == true)
        {
            await _next(context);
            return;
        }

        if (!_settings.Enabled)
        {
            await _next(context);
            return;
        }

        // Ziyaretçinin benzersiz bir kimliği olduğundan emin ol
        EnsureVisitorId(context);

        // Beyaz listedeki endpoint'leri kontrol et - bu endpoint'ler farklı sınırlamalara tabi
        string path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (IsWhitelistedEndpoint(path))
        {
            await HandleWhitelistedRequest(context);
            return;
        }

        if (!await _throttler.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            await HandleThrottlingResponse(context);
            return;
        }

        try
        {
            var clientIp = GetClientIpAddress(context);
            var userId = GetUserId(context);
            var key = GenerateRateLimitKey(context);

            // Kimlik doğrulama durumuna göre uygun limiti belirle
            bool isAuthenticated = context.User?.Identity?.IsAuthenticated ?? false;
            int requestLimit = isAuthenticated
                ? _settings.AuthenticatedRequestsPerHour
                : _settings.AnonymousRequestsPerHour;

            var (isAllowed, currentCount, retryAfter) = await _rateLimiter.CheckRateLimitAsync(key, requestLimit);

            using (LogContext.PushProperty("RateLimitInfo", new
                   {
                       UserId = userId,
                       ClientIP = clientIp,
                       RequestCount = currentCount,
                       MaxRequests = requestLimit,
                       Path = path,
                       IsAllowed = isAllowed,
                       WindowSize = _settings.WindowSizeInMinutes
                   }))
            {
                if (!isAllowed)
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();
                    var logService = scope.ServiceProvider.GetRequiredService<ILogService>();

                    await HandleRateLimitExceeded(
                        context, clientIp, path, currentCount, retryAfter, requestLimit, alertService, logService);
                    return;
                }

                _metrics.TrackActiveConnection(userId, 1);
                await _next(context);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in rate limiting middleware");
            throw;
        }
        finally
        {
            var userId = GetUserId(context);
            _metrics.TrackActiveConnection(userId, -1);
            _throttler.Release();
        }
    }

    private bool IsWhitelistedEndpoint(string path)
    {
        // Tam eşleşme kontrolü
        if (_whitelistedEndpoints.Contains(path))
            return true;

        // Kısmi eşleşme kontrolü
        return _whitelistedEndpoints.Any(endpoint => path.StartsWith(endpoint, StringComparison.OrdinalIgnoreCase));
    }

    private async Task HandleWhitelistedRequest(HttpContext context)
    {
        string path = context.Request.Path.Value ?? "";

        // Özel hızlandırılmış rate limit kontrolü - çok daha yüksek limitler
        if (path.Contains("/visitor-tracking-hub/negotiate") || path.Contains("/api/metrics"))
        {
            string key = GenerateWhitelistRateLimitKey(context);

            // Beyaz liste için çok daha yüksek limit
            int whitelistedLimit = _settings.WhitelistedRequestsPerMinute > 0
                ? _settings.WhitelistedRequestsPerMinute
                : 300; // Varsayılan dakikada 300 istek

            var (isAllowed, _, _) = await _rateLimiter.CheckRateLimitAsync(
                key,
                whitelistedLimit,
                TimeSpan.FromMinutes(1)); // Dakikalık zaman penceresi

            if (!isAllowed)
            {
                _logger.LogWarning("Whitelisted endpoint exceeded high rate limit: {Path}", path);
                await HandleThrottlingResponse(context);
                return;
            }
        }

        // Bu noktada talep izin veriliyor
        await _next(context);
    }

    private string GenerateWhitelistRateLimitKey(HttpContext context)
    {
        var clientIp = GetClientIpAddress(context);
        var userId = GetUserId(context);
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Daha kısa zaman penceresi için saniye hassasiyeti kullan
        var timeWindow = DateTime.UtcNow.ToString("yyyyMMddHHmmss")[..12]; // Dakika hassasiyeti

        return $"whitelist_rate_limit_{userId}_{clientIp}_{path}_{timeWindow}";
    }

    private async Task HandleRateLimitExceeded(
        HttpContext context,
        string clientIp,
        string path,
        int currentCount,
        TimeSpan? retryAfter,
        int requestLimit,
        IAlertService alertService,
        ILogService logService)
    {
        var userId = GetUserId(context);

        _metrics.IncrementRateLimitHit(clientIp, path, userId);

        var requestHeaders = context.Request.Headers
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        await logService.CreateLogAsync(new SecurityLog
        {
            Timestamp = DateTime.UtcNow,
            Level = "Warning",
            EventType = "RateLimit",
            ClientIP = clientIp,
            Path = path.ToString(),
            Message = $"Rate limit exceeded for user {userId}",
            RequestCount = currentCount,
            MaxRequests = requestLimit,
            UserAgent = requestHeaders.GetValueOrDefault("User-Agent", "Unknown"),
            UserName = userId,
            Exception = "None",
            AdditionalInfo = JsonSerializer.Serialize(new
            {
                RequestHeaders = requestHeaders,
                RetryAfter = retryAfter?.TotalSeconds,
                WindowSize = _settings.WindowSizeInMinutes,
                Method = context.Request.Method,
                Scheme = context.Request.Scheme,
                Host = context.Request.Host.ToString(),
                QueryString = context.Request.QueryString.ToString()
            })
        });

        await alertService.SendAlertAsync(
            AlertType.RateLimit,
            $"Rate limit exceeded by user {userId}",
            new Dictionary<string, string>
            {
                ["userId"] = userId,
                ["clientIp"] = clientIp,
                ["path"] = path.ToString(),
                ["requestCount"] = currentCount.ToString(),
                ["maxRequests"] = requestLimit.ToString(),
                ["retryAfter"] = retryAfter?.TotalSeconds.ToString() ?? "N/A",
                ["windowSize"] = _settings.WindowSizeInMinutes.ToString()
            });

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";

        // Mümkünse retry-after başlığı ekle
        if (retryAfter.HasValue)
        {
            context.Response.Headers["Retry-After"] = ((int)retryAfter.Value.TotalSeconds).ToString();
        }

        var response = new
        {
            error = "Rate limit exceeded. Please try again later.",
            retryAfter = retryAfter?.TotalSeconds ?? _settings.WindowSizeInMinutes * 60,
            details = new
            {
                userId,
                currentCount,
                limit = requestLimit,
                windowSize = _settings.WindowSizeInMinutes,
                path = path.ToString()
            }
        };

        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task HandleThrottlingResponse(HttpContext context)
    {
        var userId = GetUserId(context);
        var clientIp = GetClientIpAddress(context);

        _logger.LogWarning(
            "Request throttled for user {UserId} from IP {ClientIP}",
            userId, clientIp);

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";

        // Retry-after başlığı ekle
        context.Response.Headers["Retry-After"] = "5";

        var response = new
        {
            error = "Server is busy. Please try again later.",
            retryAfter = 5,
            details = new
            {
                userId,
                clientIp,
                maxConcurrentRequests = _settings.MaxConcurrentRequests
            }
        };

        await context.Response.WriteAsJsonAsync(response);
    }

    private string GenerateRateLimitKey(HttpContext context)
    {
        var clientIp = GetClientIpAddress(context);
        var userId = GetUserId(context);

        // Zaman penceresi için dakika hassasiyeti kullan
        var timeWindow = DateTime.UtcNow.ToString("yyyyMMddHHmm");

        return $"rate_limit_{userId}_{clientIp}_{timeWindow}";
    }

    private string GetClientIpAddress(HttpContext context)
    {
        return context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
               context.Request.Headers["X-Real-IP"].FirstOrDefault() ??
               context.Connection.RemoteIpAddress?.ToString() ??
               "unknown";
    }

    private string GetUserId(HttpContext context)
    {
        // Öncelikle kullanıcının kimliği doğrulanmış mı kontrol et
        if (context.User?.Identity?.IsAuthenticated ?? false)
        {
            return context.User.Identity.Name ?? "user";
        }

        // Anonim kullanıcılar için çerezden ziyaretçi kimliğini kullan
        var visitorId = context.Request.Cookies[VISITOR_ID_COOKIE];
        if (!string.IsNullOrEmpty(visitorId))
        {
            return $"anon_{visitorId}";
        }

        // Bu durumun oluşmaması gerekir çünkü çerezin varlığını sağlıyoruz, ama yine de
        return "anonymous";
    }

    private void EnsureVisitorId(HttpContext context)
    {
        // Kimliği doğrulanmış kullanıcılar için atla
        if (context.User?.Identity?.IsAuthenticated ?? false)
        {
            return;
        }

        // Ziyaretçi kimliği çerezi var mı kontrol et
        var visitorId = context.Request.Cookies[VISITOR_ID_COOKIE];
        if (string.IsNullOrEmpty(visitorId))
        {
            // Yeni bir ziyaretçi kimliği oluştur
            visitorId = Guid.NewGuid().ToString("N");

            // Çerezi ayarla
            context.Response.Cookies.Append(VISITOR_ID_COOKIE, visitorId, new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.Add(COOKIE_EXPIRY),
                IsEssential = true // GDPR için gerekli olarak işaretle
            });

            _logger.LogDebug("Created new visitor ID cookie: {VisitorId}", visitorId);
        }
    }
}