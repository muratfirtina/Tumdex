using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Middleware.Security;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SecurityHeadersMiddleware> _logger; // Logger eklendi
    private readonly string _defaultCsp;

    public SecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<SecurityHeadersMiddleware> logger) // Logger inject edildi
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // Logger ataması

        // Varsayılan CSP (Content Security Policy)
        _defaultCsp = configuration.GetValue<string>("Security:DefaultContentSecurityPolicy") ?? // Yapılandırmadan okumayı dene
            "default-src 'self'; object-src 'none'; frame-ancestors 'none'; upgrade-insecure-requests;"; // Daha güvenli bir varsayılan

        // Not: Script ve style kaynakları için daha spesifik kurallar gerekebilir.
        // Örnek: "script-src 'self' https://cdn.example.com; style-src 'self' 'unsafe-inline';"
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        try
        {
            // --- CORS Başlıkları Kaldırıldı ---
            // Bu başlıklar artık Program.cs'deki AddCors/UseCors tarafından yönetiliyor.
            // context.Response.Headers.Add("Access-Control-Allow-Origin", ...);
            // context.Response.Headers.Add("Access-Control-Allow-Methods", ...);
            // context.Response.Headers.Add("Access-Control-Allow-Headers", ...);
            // context.Response.Headers.Add("Access-Control-Allow-Credentials", ...);
            // --- CORS Başlıkları Kaldırıldı SONU ---

            // CSP Header
            var cspValue = BuildContentSecurityPolicy();
            if (!string.IsNullOrWhiteSpace(cspValue))
            {
                 // 'unsafe-inline' veya 'unsafe-eval' kullanılıyorsa logla (güvenlik riski)
                 if (cspValue.Contains("'unsafe-inline'") || cspValue.Contains("'unsafe-eval'"))
                 {
                     _logger.LogWarning("Content-Security-Policy includes 'unsafe-inline' or 'unsafe-eval'. Ensure this is intentional.");
                 }
                context.Response.Headers.Add("Content-Security-Policy", cspValue);
            }


            // Diğer Güvenlik Başlıkları
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("X-Frame-Options", _configuration.GetValue<string>("Security:XFrameOptions") ?? "DENY");
            context.Response.Headers.Add("X-XSS-Protection", "1; mode=block"); // Eski tarayıcılar için hala faydalı olabilir
            context.Response.Headers.Add("Referrer-Policy", _configuration.GetValue<string>("Security:ReferrerPolicy") ?? "strict-origin-when-cross-origin");
            context.Response.Headers.Add("Permissions-Policy", _configuration.GetValue<string>("Security:PermissionsPolicy") ?? "camera=(), microphone=(), geolocation=(), payment=()"); // Örnek, ihtiyaca göre düzenle
            context.Response.Headers.Add("X-Permitted-Cross-Domain-Policies", "none");
            // context.Response.Headers.Add("Expect-CT", "max-age=86400, enforce"); // Expect-CT kullanımdan kalkıyor (deprecated)

            // HTTPS Güvenliği (HSTS) - Program.cs içinde UseHsts() ile yönetilmesi daha yaygın
            // if (_configuration.GetValue<bool>("Security:EnableHsts", true))
            // {
            //     var maxAge = _configuration.GetValue<int>("Security:HstsMaxAgeDays", 365);
            //     context.Response.Headers.Add("Strict-Transport-Security", $"max-age={maxAge * 24 * 60 * 60}; includeSubDomains; preload");
            // }

            // OPTIONS isteği için erken dönüş - Bu genellikle CORS middleware tarafından halledilir.
            // Eğer UseCors doğru ayarlandıysa buna gerek kalmaz.
            // if (context.Request.Method == "OPTIONS")
            // {
            //     context.Response.StatusCode = StatusCodes.Status204NoContent; // Genellikle 204 daha uygundur
            //     return;
            // }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SecurityHeadersMiddleware içinde işlenmeyen hata oluştu.");
            // Hatayı tekrar fırlatmak, diğer hata işleme mekanizmalarının çalışmasını sağlar.
            throw;
        }
    }

    private string BuildContentSecurityPolicy()
    {
        var cspBuilder = new StringBuilder();
        var cspSections = _configuration.GetSection("Security:ContentSecurityPolicy").GetChildren();

        if (!cspSections.Any()) return _defaultCsp; // Yapılandırma yoksa varsayılanı kullan

        foreach (var section in cspSections)
        {
            // Değer bir dizi ise (örn: script-src: ['self', 'https://cdn...'])
            var sources = section.Get<string[]>();
            if (sources != null && sources.Length > 0)
            {
                cspBuilder.Append($"{section.Key} {string.Join(" ", sources)}; ");
            }
            // Değer tek bir string ise (örn: default-src: 'self')
            else if (!string.IsNullOrWhiteSpace(section.Value))
            {
                 cspBuilder.Append($"{section.Key} {section.Value}; ");
            }
        }

        return cspBuilder.Length > 0 ? cspBuilder.ToString().Trim() : _defaultCsp;
    }
}