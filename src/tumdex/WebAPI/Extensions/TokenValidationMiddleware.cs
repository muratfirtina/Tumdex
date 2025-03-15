using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;

namespace WebAPI.Extensions
{
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
            // Bazı endpoint'leri kontrol dışı bırakmak için
            if (ShouldSkipValidation(context.Request.Path))
            {
                await _next(context);
                return;
            }

            // Authorization header'ı kontrol et
            var authHeader = context.Request.Headers["Authorization"].ToString();

            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();

                try
                {
                    // Token'ın iptal edilip edilmediğini kontrol et
                    var (isRevoked, userId, reason) = await CheckIfTokenIsRevokedAsync(token, userManager);

                    if (isRevoked)
                    {
                        _logger.LogWarning("İptal edilmiş token kullanım girişimi: {Path} - UserId: {UserId}, Reason: {Reason}",
                            context.Request.Path, userId, reason);

                        // 401 Unauthorized yanıtı
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";

                        await context.Response.WriteAsync(JsonSerializer.Serialize(new
                        {
                            error = "Token has been revoked",
                            message = reason
                        }));
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Token iptal kontrolü sırasında hata: {Path}", context.Request.Path);
                }
            }

            // Sonraki middleware'a geç
            await _next(context);
        }

        private bool ShouldSkipValidation(PathString path)
        {
            // Token doğrulamasından muaf tutulacak path'ler
            var skipPaths = new[]
            {
                "/api/auth/login",
                "/api/auth/register",
                "/api/auth/refresh",
                "/api/auth/password-reset",
                "/api/health",
                "/api/swagger"
            };

            foreach (var skipPath in skipPaths)
            {
                if (path.StartsWithSegments(skipPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<(bool isRevoked, string userId, string reason)> CheckIfTokenIsRevokedAsync(string token, UserManager<AppUser> userManager)
        {
            try
            {
                // JWT token'dan gerekli bilgileri çıkar
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);

                // User ID çıkar
                var userIdClaim = jwtToken.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.NameIdentifier ||
                    c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

                if (userIdClaim == null)
                {
                    return (false, string.Empty, string.Empty);
                }

                var userId = userIdClaim.Value;

                // Token'dan session ID'yi çıkar
                var sessionIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "session_id");

                if (sessionIdClaim == null)
                {
                    // Eski tokenlar için, eski kontrolü uygula
                    string revokeKey = $"UserTokensRevoked:{userId}";
                    string? revokedTimeString = await _cache.GetStringAsync(revokeKey);

                    if (!string.IsNullOrEmpty(revokedTimeString))
                    {
                        return (true, userId, "Bu kullanıcının tüm tokenları iptal edildi");
                    }

                    return (false, userId, string.Empty);
                }

                var tokenSessionId = sessionIdClaim.Value;

                // Kullanıcının güncel session ID'sini al
                var user = await userManager.FindByIdAsync(userId);

                if (user == null)
                {
                    return (true, userId, "Kullanıcı bulunamadı");
                }

                // Önce cache'ten kontrol et (performans için)
                string currentSessionId = await _cache.GetStringAsync($"UserSession:{userId}") ?? user.SessionId ?? "";

                // Session ID eşleşmiyorsa, token iptal edilmiş demektir
                if (tokenSessionId != currentSessionId)
                {
                    _logger.LogWarning("Token farklı session ID'ye sahip. TokenID: {TokenID}, CurrentID: {CurrentID}",
                        tokenSessionId, currentSessionId);

                    return (true, userId, "Bu kullanıcının tüm tokenları iptal edildi");
                }

                return (false, userId, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token iptal kontrolü sırasında hata");
                return (false, string.Empty, "Token kontrol hatası");
            }
        }
    }
}