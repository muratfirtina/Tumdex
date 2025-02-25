using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace Infrastructure.Logging.Enrichers;

public class SecurityEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SecurityEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "ClientIP", GetClientIpAddress(context)));

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "Path", context.Request.Path));

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "UserAgent", context.Request.Headers["User-Agent"].ToString()));

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "UserName", context.User.Identity.Name));
        }
    }

    private string GetClientIpAddress(HttpContext context)
    {
        return context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
               context.Connection.RemoteIpAddress?.ToString() ??
               "unknown";
    }
}