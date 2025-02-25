using System.Diagnostics;
using Application.Abstraction.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Middleware.Monitoring;

public class EnhancedMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMetricsService _metrics;
    private readonly ILogger<EnhancedMetricsMiddleware> _logger;

    public EnhancedMetricsMiddleware(
        RequestDelegate next,
        IMetricsService metrics,
        ILogger<EnhancedMetricsMiddleware> logger)
    {
        _next = next;
        _metrics = metrics;
        _logger = logger;
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
            
            var path = context.Request.Path.Value;
            var method = context.Request.Method;
            
            _metrics.RecordRequestDuration(method, path, duration);
            _metrics.IncrementTotalRequests(
                method, 
                path, 
                context.Response.StatusCode.ToString()
            );

            if (duration > 1000) // 1 saniye
            {
                _logger.LogWarning(
                    "Long running request: {Method} {Path} took {Duration}ms",
                    method, path, duration);
            }
        }
    }
}