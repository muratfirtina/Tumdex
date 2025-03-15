using Application.Abstraction.Services.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Security;

public class AntiForgeryService : IAntiForgeryService
{
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<AntiForgeryService> _logger;

    public AntiForgeryService(IAntiforgery antiforgery, ILogger<AntiForgeryService> logger)
    {
        _antiforgery = antiforgery;
        _logger = logger;
    }

    public void GenerateTokens(HttpContext httpContext)
    {
        try
        {
            var tokens = _antiforgery.GetAndStoreTokens(httpContext);
            
            // CSRF token'ı cookie olarak ayarla - frontend tarafından okunabilecek şekilde
            // NOT: HttpOnly=false, çünkü frontend'in bu token'a erişimi gerekiyor
            httpContext.Response.Cookies.Append("X-CSRF-TOKEN", tokens.RequestToken, new CookieOptions
            {
                HttpOnly = false, // Frontend'in erişmesi gerekiyor
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTime.UtcNow.AddHours(1)
            });
            
            _logger.LogDebug("CSRF tokenleri oluşturuldu");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSRF tokenleri oluşturulurken hata oluştu");
        }
    }

    public bool ValidateToken(HttpContext httpContext)
    {
        try
        {
            _antiforgery.ValidateRequestAsync(httpContext).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CSRF token doğrulama hatası");
            return false;
        }
    }

    public void ClearTokens(HttpContext httpContext)
    {
        try
        {
            httpContext.Response.Cookies.Delete("X-CSRF-TOKEN");
            _logger.LogDebug("CSRF tokenleri temizlendi");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSRF tokenleri temizlenirken hata oluştu");
        }
    }
}