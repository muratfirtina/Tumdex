using System.Diagnostics;
using Application.Abstraction.Services;
using Application.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Middleware.Monitoring;

public class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMetricsService _metrics;
    private readonly ILogger<RequestTimingMiddleware> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public RequestTimingMiddleware(
        RequestDelegate next,
        IMetricsService metrics,
        ILogger<RequestTimingMiddleware> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _next = next;
        _metrics = metrics;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var duration = sw.ElapsedMilliseconds;

            var endpoint = context.Request.Path.Value;
            var method = context.Request.Method;

            _metrics.RecordRequestDuration(method, endpoint, duration);

            // Log slow requests and send alerts
            if (duration > 1000) // 1 second
            {
                _logger.LogWarning(
                    "Slow request detected: {Method} {Path} took {Duration}ms",
                    method, endpoint, duration);

                if (duration > 5000) // 5 seconds
                {
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();
                        await alertService.SendAlertAsync(
                            AlertType.HighLatency,
                            "High latency request detected",
                            new Dictionary<string, string>
                            {
                                ["method"] = method,
                                ["path"] = endpoint,
                                ["duration"] = duration.ToString(),
                                ["severity"] = "warning"
                            });
                    }
                }
            }
        }
    }
}