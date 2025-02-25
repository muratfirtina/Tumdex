using Microsoft.AspNetCore.Builder;

namespace Infrastructure.Services.Monitoring;

// Extension method
public static class AdvancedMetricsMiddlewareExtensions
{
    public static IApplicationBuilder UseAdvancedMetrics(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AdvancedMetricsMiddleware>();
    }
}