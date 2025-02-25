using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Middleware.Security;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly string _defaultCsp;

    public SecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _defaultCsp = "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline';";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        try
        {
            // CORS Headers
            var allowedOrigins = _configuration.GetSection("WebAPIConfiguration:AllowedOrigins").Get<string[]>();
            if (allowedOrigins?.Any() == true)
            {
                context.Response.Headers.Add("Access-Control-Allow-Origin", 
                    context.Request.Headers.Origin.ToString());
                context.Response.Headers.Add("Access-Control-Allow-Methods", 
                    "GET, POST, PUT, DELETE, OPTIONS");
                context.Response.Headers.Add("Access-Control-Allow-Headers", 
                    "Content-Type, Authorization, X-Requested-With");
                context.Response.Headers.Add("Access-Control-Allow-Credentials", "true");
            }

            // CSP Header
            var cspBuilder = new StringBuilder();
            var csp = _configuration.GetSection("Security:ContentSecurityPolicy")
                .Get<Dictionary<string, string[]>>();

            if (csp?.Any() == true)
            {
                foreach (var (directive, sources) in csp)
                {
                    if (sources != null)
                    {
                        cspBuilder.Append($"{directive} {string.Join(" ", sources)}; ");
                    }
                }
            }
            
            // Security Headers
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("X-Frame-Options", 
                _configuration["Security:XFrameOptions"] ?? "DENY");
            context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
            context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.Add("Content-Security-Policy", 
                cspBuilder.Length > 0 ? cspBuilder.ToString().Trim() : _defaultCsp);
            context.Response.Headers.Add("X-Permitted-Cross-Domain-Policies", "none");
            context.Response.Headers.Add("Expect-CT", "max-age=86400, enforce");

            // HTTPS Security
            if (_configuration.GetValue<bool>("Security:RequireHttps", false))
            {
                var maxAge = _configuration.GetValue<int>("Security:HstsMaxAge", 365);
                context.Response.Headers.Add(
                    "Strict-Transport-Security",
                    $"max-age={maxAge * 24 * 60 * 60}; includeSubDomains; preload");
            }

            // OPTIONS request için erken dönüş
            if (context.Request.Method == "OPTIONS")
            {
                context.Response.StatusCode = 200;
                return;
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            // Log the error but don't expose it
            // TODO: Add proper logging here
            throw;
        }
    }
}