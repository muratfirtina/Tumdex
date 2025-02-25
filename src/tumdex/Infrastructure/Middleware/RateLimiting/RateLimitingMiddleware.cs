using System.Text.Json;
using Application.Abstraction.Services;
using Application.Enums;
using Domain;
using Infrastructure.Services.Cache;
using Infrastructure.Services.Security.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Context;

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
        _settings = securitySettings.RateLimitConfig ?? throw new ArgumentNullException("RateLimitConfig is not configured");
        
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        
        _settings.MaxConcurrentRequests = _settings.MaxConcurrentRequests <= 0 ? 100 : _settings.MaxConcurrentRequests;
        _throttler = new SemaphoreSlim(_settings.MaxConcurrentRequests);

        _logger.LogInformation(
            "Rate limiting initialized with: {MaxRequests} requests per minute, {MaxConcurrent} concurrent requests",
            _settings.RequestsPerHour,
            _settings.MaxConcurrentRequests);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_settings.Enabled)
        {
            await _next(context);
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
            var userId = context.User?.Identity?.Name ?? "anonymous";
            var path = context.Request.Path;
            var key = GenerateRateLimitKey(context);

            var (isAllowed, currentCount, retryAfter) = await _rateLimiter.CheckRateLimitAsync(key);

            using (LogContext.PushProperty("RateLimitInfo", new
            {
                UserId = userId,
                ClientIP = clientIp,
                RequestCount = currentCount,
                MaxRequests = _settings.RequestsPerHour,
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
                        context, clientIp, path, currentCount, retryAfter, alertService, logService);
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
            var userId = context.User?.Identity?.Name ?? "anonymous";
            _metrics.TrackActiveConnection(userId, -1);
            _throttler.Release();
        }
    }

    private async Task HandleRateLimitExceeded(
        HttpContext context,
        string clientIp,
        PathString path,
        int currentCount,
        TimeSpan? retryAfter,
        IAlertService alertService,
        ILogService logService)
    {
        var userId = context.User?.Identity?.Name ?? "anonymous";
        
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
            MaxRequests = _settings.RequestsPerHour,
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
                ["maxRequests"] = _settings.RequestsPerHour.ToString(),
                ["retryAfter"] = retryAfter?.TotalSeconds.ToString() ?? "N/A",
                ["windowSize"] = _settings.WindowSizeInMinutes.ToString()
            });

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = "Rate limit exceeded. Please try again later.",
            retryAfter = retryAfter?.TotalSeconds ?? _settings.WindowSizeInMinutes,
            details = new
            {
                userId,
                currentCount,
                limit = _settings.RequestsPerHour,
                windowSize = _settings.WindowSizeInMinutes,
                path = path.ToString()
            }
        };

        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task HandleThrottlingResponse(HttpContext context)
    {
        var userId = context.User?.Identity?.Name ?? "anonymous";
        var clientIp = GetClientIpAddress(context);

        _logger.LogWarning(
            "Request throttled for user {UserId} from IP {ClientIP}",
            userId, clientIp);

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";
        
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
        var userId = context.User?.Identity?.Name ?? "anonymous";
        // Dakika bazlı pencere için daha hassas izleme
        return $"rate_limit_{userId}_{clientIp}_{DateTime.UtcNow:yyyyMMddHHmm}";
    }

    private string GetClientIpAddress(HttpContext context)
    {
        return context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
               context.Connection.RemoteIpAddress?.ToString() ??
               "unknown";
    }
}