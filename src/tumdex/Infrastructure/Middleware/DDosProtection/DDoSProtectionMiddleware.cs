using Application.Abstraction.Services;
using Application.Enums;
using Domain;
using Infrastructure.Services.Cache;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Middleware.DDosProtection;

public class DDoSProtectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DDoSProtectionMiddleware> _logger;
    private readonly ICacheService _cache;
    private readonly IMetricsService _metrics;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly SlidingWindowRateLimiter _rateLimiter;
    private readonly SemaphoreSlim _throttler;

    public DDoSProtectionMiddleware(
        RequestDelegate next,
        ILogger<DDoSProtectionMiddleware> logger,
        ICacheService cache,
        IMetricsService metrics,
        IServiceScopeFactory serviceScopeFactory,
        SlidingWindowRateLimiter rateLimiter,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _cache = cache;
        _metrics = metrics;
        _serviceScopeFactory = serviceScopeFactory;
        _rateLimiter = rateLimiter;
        _throttler = new SemaphoreSlim(
            configuration.GetValue<int>("Security:DDoSProtection:MaxConcurrentRequests", 100));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var clientIp = GetClientIpAddress(context);
        var path = context.Request.Path;
        var key = $"ddos_protection_{clientIp}_{DateTime.UtcNow:yyyyMMddHHmm}";

        try
        {
            if (!await _throttler.WaitAsync(TimeSpan.FromSeconds(5)))
            {
                await HandleThrottlingResponse(context, clientIp, path);
                return;
            }

            try
            {
                var (isAllowed, currentCount, retryAfter) =
                    await _rateLimiter.CheckRateLimitAsync(key);

                if (!isAllowed)
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();
                    var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
                    
                    await HandleDDoSAttempt(context, clientIp, path, currentCount, alertService, logService);
                    return;
                }

                // Active connection tracking
                _metrics?.TrackActiveConnection("http", 1);
                await _next(context);
            }
            finally
            {
                // Decrement active connections in finally block
                try
                {
                    _metrics?.TrackActiveConnection("http", -1);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error decrementing active connections");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DDoS protection middleware");
            throw;
        }
        finally
        {
            _throttler.Release();
        }
    }

    private async Task HandleDDoSAttempt(
        HttpContext context,
        string clientIp,
        PathString path,
        int currentCount,
        IAlertService alertService,
        ILogService logService)
    {
        // Güvenlik log kaydı
        await logService.CreateLogAsync(new SecurityLog
        {
            Timestamp = DateTime.UtcNow,
            Level = "Warning",
            EventType = "DDoS_Attempt",
            ClientIP = clientIp,
            Path = path.ToString(),
            Message = "Possible DDoS attack detected",
            RequestCount = currentCount,
            UserAgent = context.Request.Headers["User-Agent"].ToString(),
            UserName = context.User?.Identity?.Name
        });

        // Alert gönderimi
        await alertService.SendAlertAsync(
            AlertType.DDoS,
            "Possible DDoS attack detected",
            new Dictionary<string, string>
            {
                ["clientIp"] = clientIp,
                ["path"] = path.ToString(),
                ["requestCount"] = currentCount.ToString(),
                ["timeWindow"] = "1 minute"
            });

        // Metrik kaydı
        _metrics.IncrementDdosAttempt(clientIp, path.ToString());

        // Response
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";
        
        var response = new 
        { 
            error = "Too many requests detected.",
            retryAfter = 60
        };
        
        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task HandleThrottlingResponse(HttpContext context, string clientIp, string path)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";
        
        var response = new 
        { 
            error = "Server is busy. Please try again later.",
            retryAfter = 5
        };
        
        await context.Response.WriteAsJsonAsync(response);
        
        _metrics.IncrementDdosAttempt(clientIp, path.ToString());
    }

    private string GetClientIpAddress(HttpContext context)
    {
        return context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
               context.Connection.RemoteIpAddress?.ToString() ??
               "unknown";
    }
}