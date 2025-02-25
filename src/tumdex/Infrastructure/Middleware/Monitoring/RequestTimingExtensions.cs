using Microsoft.AspNetCore.Builder;

namespace Infrastructure.Middleware.Monitoring;

public static class RequestTimingExtensions
{
    public static IApplicationBuilder UseRequestTiming(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestTimingMiddleware>();
    }
}