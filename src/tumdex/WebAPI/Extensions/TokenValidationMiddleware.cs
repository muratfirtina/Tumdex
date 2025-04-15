using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Domain.Identity; // AppUser için using
using Microsoft.AspNetCore.Http; // HttpContext için
using Microsoft.AspNetCore.Identity; // UserManager için
using Microsoft.Extensions.Caching.Distributed; // IDistributedCache için
using Microsoft.Extensions.Logging; // ILogger için

namespace WebAPI.Extensions; // Namespace'i projenize göre ayarlayın

public class TokenValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenValidationMiddleware> _logger;
    private readonly IDistributedCache _cache;

    public TokenValidationMiddleware(
        RequestDelegate next,
        ILogger<TokenValidationMiddleware> logger,
        IDistributedCache cache)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task InvokeAsync(HttpContext context, UserManager<AppUser> userManager)
    {
        if (ShouldSkipValidation(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();
            bool respondWith401 = false;
            string revocationReason = "Geçersiz token."; // Varsayılan mesaj

            try
            {
                var (isRevoked, userId, reason) = await CheckIfTokenIsRevokedAsync(token, userManager);

                if (isRevoked)
                {
                    respondWith401 = true;
                    revocationReason = reason; // Gerçek nedeni ata
                    _logger.LogWarning("İptal edilmiş token kullanım girişimi: {Path} - UserId: {UserId}, Reason: {Reason}",
                        context.Request.Path, userId ?? "Bilinmiyor", reason);
                }
            }
            catch (Exception ex) // CheckIfTokenIsRevokedAsync içindeki beklenmedik hatalar
            {
                 _logger.LogError(ex, "Token iptal kontrolü sırasında beklenmedik hata: {Path}. Token geçersiz sayılacak.", context.Request.Path);
                 respondWith401 = true;
                 revocationReason = "Token doğrulama hatası.";
            }

            if (respondWith401)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    error = "Token has been revoked or validation failed",
                    message = revocationReason
                }));
                return;
            }
        }

        await _next(context);
    }

    private bool ShouldSkipValidation(PathString path)
    {
        // Token doğrulamasından muaf tutulacak path segmentleri
        var skipPaths = new[]
        {
            "/api/auth",         // Tüm auth endpoint'leri (login, register, refresh, password-reset)
            "/api/token",        // Tüm token endpoint'leri (activation, verify etc.)
            "/api/users/update-forgot-password", // Şifre güncelleme
            "/health",           // Health check endpoint'leri (genellikle /health ile başlar)
            "/swagger",          // Swagger UI
            "/order-hub",        // SignalR Hub endpoint'leri
            "/visitor-tracking-hub"
        };

        // Tam eşleşme veya segment başlangıcı kontrolü
        return skipPaths.Any(skipPath => path.StartsWithSegments(skipPath, StringComparison.OrdinalIgnoreCase));
    }


    private async Task<(bool isRevoked, string? userId, string reason)> CheckIfTokenIsRevokedAsync(string token, UserManager<AppUser> userManager)
    {
        string? currentUserId = null;
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            if (!tokenHandler.CanReadToken(token))
            {
                 _logger.LogWarning("Okunamaz veya geçersiz JWT formatı.");
                 return (true, null, "Geçersiz token formatı.");
            }
            var jwtToken = tokenHandler.ReadJwtToken(token);

            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            currentUserId = userIdClaim?.Value;

            if (string.IsNullOrEmpty(currentUserId))
            {
                _logger.LogWarning("Token içinde geçerli bir kullanıcı kimliği (NameIdentifier) bulunamadı.");
                return (true, null, "Token kullanıcı kimliği içermiyor.");
            }

            var user = await userManager.FindByIdAsync(currentUserId);
            if (user == null)
            {
                 _logger.LogWarning("Token'daki kullanıcı kimliği ({UserId}) veritabanında bulunamadı.", currentUserId);
                return (true, currentUserId, "Kullanıcı bulunamadı.");
            }
             // Kullanıcı aktif değilse token'ı geçersiz say
             if(!user.IsActive) {
                  _logger.LogWarning("Token sahibi kullanıcı ({UserId}) aktif değil.", currentUserId);
                  return (true, currentUserId, "Kullanıcı hesabı aktif değil.");
             }

            var tokenSessionIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "session_id");
            if (tokenSessionIdClaim != null)
            {
                var tokenSessionId = tokenSessionIdClaim.Value;
                 string? currentSessionId = await _cache.GetStringAsync($"UserSession:{currentUserId}") ?? user.SessionId;

                 if (string.IsNullOrEmpty(currentSessionId)) {
                      _logger.LogWarning("Kullanıcının ({UserId}) güncel session ID'si bulunamadı.", currentUserId);
                      // Session ID yoksa ne yapılacağına karar verilmeli. Güvenlik için iptal edilebilir.
                      return (true, currentUserId, "Kullanıcı oturum bilgisi bulunamadı.");
                 }
                 else if (tokenSessionId != currentSessionId)
                {
                     _logger.LogWarning("Token session ID ({TokenSessionId}) kullanıcının güncel session ID'si ({CurrentSessionId}) ile eşleşmiyor. UserId: {UserId}",
                        tokenSessionId, currentSessionId, currentUserId);
                    return (true, currentUserId, "Oturum başka bir yerden sonlandırıldı veya token eski.");
                }
                 return (false, currentUserId, string.Empty);
            }

            // Session ID yoksa eski mekanizma (artık önerilmez ama varsa diye)
            _logger.LogDebug("Token'da session_id claim'i bulunamadı, eski iptal mekanizması kontrol ediliyor (varsa). UserId: {UserId}", currentUserId);
            string revokeKey = $"UserTokensRevoked:{currentUserId}";
            string? revokedTimeString = await _cache.GetStringAsync(revokeKey);

            if (!string.IsNullOrEmpty(revokedTimeString))
            {
                 // Güvenlik için, session_id yoksa ve eski iptal kaydı varsa token'ı iptal et
                 _logger.LogWarning("Eski token iptal kaydı bulundu (session_id olmayan token için). UserId: {UserId}", currentUserId);
                 return (true, currentUserId, "Bu kullanıcının tüm tokenları (eski mekanizma) iptal edildi.");
            }

            return (false, currentUserId, string.Empty);
        }
        catch (Exception ex) // SecurityTokenException vb. de buraya düşebilir
        {
            _logger.LogError(ex, "Token iptal kontrolü sırasında beklenmeyen hata. UserId: {UserId}", currentUserId ?? "Bilinmiyor");
            return (true, currentUserId, "Token doğrulama sırasında bir hata oluştu.");
        }
    }
}