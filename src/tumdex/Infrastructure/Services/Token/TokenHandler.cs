using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Application.Abstraction.Services;
using Application.Dtos.Token;
using Application.Tokens;
using Domain.Identity;
using Infrastructure.Services.Security.JWT;
using Infrastructure.Services.Security.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Persistence.Context;

namespace Infrastructure.Services.Token
{
    public class TokenHandler : ITokenHandler
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IJwtService _jwtService;
        private readonly ILogger<TokenHandler> _logger;
        private readonly ICacheService _cache;
        private readonly TumdexDbContext _dbContext;
        private readonly JwtSettings _jwtSettings;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private const string UserClaimsCachePrefix = "UserClaims_";
        private const string UserBlockedCachePrefix = "UserBlocked_";
        private static readonly TimeSpan ClaimsCacheDuration = TimeSpan.FromMinutes(30);

        public TokenHandler(
            UserManager<AppUser> userManager,
            IJwtService jwtService,
            ILogger<TokenHandler> logger,
            ICacheService cache,
            TumdexDbContext dbContext,
            IOptions<JwtSettings> jwtSettings)
        {
            _userManager = userManager;
            _jwtService = jwtService;
            _logger = logger;
            _cache = cache;
            _dbContext = dbContext;
            _jwtSettings = jwtSettings.Value;
        }

        // ACCESS TOKEN OLUŞTURMA
        public async Task<TokenDto> CreateAccessTokenAsync(int seconds, AppUser appUser, HttpContext httpContext)
{
    try
    {
        // Kullanıcı engellenmiş mi kontrol et
        if (await IsUserBlockedAsync(appUser.Id))
        {
            throw new InvalidOperationException("Bu kullanıcı engellenmiştir");
        }

        // JWT yapılandırmasını ve kullanıcı rollerini parallel olarak al
        var jwtConfigTask = _jwtService.GetJwtConfigurationAsync();
        var claimsTask = GetUserClaimsAsync(appUser);

        await Task.WhenAll(jwtConfigTask, claimsTask);

        var jwtConfig = await jwtConfigTask;
        var claims = await claimsTask;

        _logger.LogInformation("JWT yapılandırması alındı. SecurityKey: {KeyId}",
            jwtConfig.SecurityKey?.KeyId ?? "No KeyId");

        var token = new TokenDto();

        // SigningCredentials oluştur
        var signingCredentials = new SigningCredentials(
            jwtConfig.SecurityKey,
            SecurityAlgorithms.HmacSha256);

        // Token'ın son kullanma tarihini ayarla
        token.AccessTokenExpiration = DateTime.UtcNow.AddSeconds(seconds);

        // JWT Token oluştur
        var securityToken = new JwtSecurityToken(
            issuer: jwtConfig.Issuer.ToString(),
            audience: jwtConfig.Audience.ToString(),
            expires: token.AccessTokenExpiration,
            notBefore: DateTime.UtcNow,
            signingCredentials: signingCredentials,
            claims: claims
        );

        // Token'ı string'e çevir
        var tokenHandler = new JwtSecurityTokenHandler();
        token.AccessToken = tokenHandler.WriteToken(securityToken);

        // Refresh token oluşturma ve yapılandırma
        var refreshToken = CreateRefreshToken();
        token.RefreshToken = refreshToken;
        token.RefreshTokenExpiration = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenLifetimeDays);
        
        // UserId bilgisini ekle
        token.UserId = appUser.Id;

        // Jti claim'i al
        var jti = securityToken.Id;
        if (string.IsNullOrEmpty(jti))
        {
            jti = claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value;
        }

        // IP adresi ve tarayıcı bilgilerini al
        var ipAddress = GetIpAddress(httpContext);
        var userAgent = GetUserAgent(httpContext);

        // Kullanıcının aktif token sayısını kontrol et ve sınırla
        await LimitActiveRefreshTokensAsync(appUser.Id);

        // Aile ID'si oluştur (isteğe bağlı)
        string familyId = null;
        if (_jwtSettings.UseTokenFamilies)
        {
            familyId = Guid.NewGuid().ToString();
        }

        // Refresh token'ı hash'le ve veritabanına kaydet
        var tokenHash = HashToken(refreshToken);
        var refreshTokenEntity = RefreshToken.CreateToken(
            refreshToken,
            tokenHash,
            appUser.Id,
            jti,
            ipAddress,
            userAgent,
            token.RefreshTokenExpiration,
            familyId
        );

        await _dbContext.RefreshTokens.AddAsync(refreshTokenEntity);
        await _dbContext.SaveChangesAsync();

        // Token'ı döndür
        _logger.LogInformation("Token başarıyla oluşturuldu: {UserId}", appUser.Id);
        return token;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Token oluşturulurken hata oluştu. Kullanıcı: {UserId}", appUser.Id);
        throw;
    }
}

        public Task<TokenDto> CreateAccessTokenAsync(AppUser user, HttpContext httpContext)
        {
            // Access token'ı dakika cinsinden oluştur (varsayılan 30 dakika)
            return CreateAccessTokenAsync(_jwtSettings.AccessTokenLifetimeMinutes * 60, user, httpContext);
        }

        // REFRESH TOKEN İŞLEMLERİ
        public async Task<TokenDto> RefreshAccessTokenAsync(string refreshToken, string ipAddress, string userAgent)
        {
            try
            {
                var validationResult = await ValidateRefreshTokenAsync(refreshToken, ipAddress, userAgent);

                if (!validationResult.isValid)
                {
                    _logger.LogWarning("Geçersiz refresh token: {Error}", validationResult.error);
                    throw new InvalidOperationException(validationResult.error);
                }

                var oldRefreshToken = validationResult.refreshToken;
                var user = validationResult.user;

                // Mevcut token'ı kullanılmış olarak işaretle
                oldRefreshToken.IsUsed = true;
                _dbContext.RefreshTokens.Update(oldRefreshToken);
                await _dbContext.SaveChangesAsync();

                // Yeni access token oluştur
                var newToken = await CreateAccessTokenAsync(user, null);
        
                // UserId bilgisini ekle
                newToken.UserId = user.Id;

                // Aile ID'sini aktar (isteğe bağlı)
                if (_jwtSettings.UseTokenFamilies && _jwtSettings.RotateRefreshTokens &&
                    !string.IsNullOrEmpty(oldRefreshToken.FamilyId))
                {
                    var newRefreshTokenEntity = await _dbContext.RefreshTokens
                        .FirstOrDefaultAsync(rt => rt.TokenHash == HashToken(newToken.RefreshToken));

                    if (newRefreshTokenEntity != null)
                    {
                        newRefreshTokenEntity.FamilyId = oldRefreshToken.FamilyId;
                        _dbContext.RefreshTokens.Update(newRefreshTokenEntity);
                        await _dbContext.SaveChangesAsync();
                    }
                }

                return newToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh token işlemi sırasında hata oluştu");
                throw;
            }
        }

        public async Task RevokeRefreshTokenAsync(string refreshToken, string ipAddress, string reasonRevoked)
        {
            var tokenHash = HashToken(refreshToken);

            var storedToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

            if (storedToken != null && !storedToken.IsRevoked)
            {
                // Token'ı iptal et
                storedToken.IsRevoked = true;
                storedToken.RevokedByIp = ipAddress;
                storedToken.ReasonRevoked = reasonRevoked;
                storedToken.RevokedDate = DateTime.UtcNow;

                // İptal nedeni ve IP adresini log'la
                _logger.LogInformation(
                    "Token iptal edildi: {TokenId}. Sebep: {Reason}, IP: {IP}",
                    storedToken.Id, reasonRevoked, ipAddress
                );

                // Aynı ailedeki diğer token'ları da iptal et (güvenlik için)
                if (_jwtSettings.UseTokenFamilies && !string.IsNullOrEmpty(storedToken.FamilyId))
                {
                    var familyTokens = await _dbContext.RefreshTokens
                        .Where(rt => rt.FamilyId == storedToken.FamilyId && !rt.IsRevoked)
                        .ToListAsync();

                    foreach (var familyToken in familyTokens)
                    {
                        familyToken.IsRevoked = true;
                        familyToken.RevokedByIp = ipAddress;
                        familyToken.ReasonRevoked = $"Family token revoked: {reasonRevoked}";
                        familyToken.RevokedDate = DateTime.UtcNow;
                    }
                }

                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task RevokeAllUserRefreshTokensAsync(string userId, string ipAddress, string reasonRevoked)
        {
            var userTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in userTokens)
            {
                token.IsRevoked = true;
                token.RevokedByIp = ipAddress;
                token.ReasonRevoked = reasonRevoked;
                token.RevokedDate = DateTime.UtcNow;
            }

            // Log olayı
            _logger.LogInformation("Tüm tokenlar iptal edildi. Kullanıcı: {UserId}, Sebep: {Reason}, IP: {IP}",
                userId, reasonRevoked, ipAddress);

            await _dbContext.SaveChangesAsync();
        }

        // TOKEN DOĞRULAMA
        public async Task<(bool isValid, string userId, string error)> ValidateAccessTokenAsync(string token)
        {
            try
            {
                var jwtConfig = await _jwtService.GetJwtConfigurationAsync();
                var tokenHandler = new JwtSecurityTokenHandler();

                var principal = tokenHandler.ValidateToken(
                    token,
                    jwtConfig.TokenValidationParameters,
                    out SecurityToken validatedToken);

                if (validatedToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256,
                        StringComparison.InvariantCultureIgnoreCase))
                {
                    return (false, null, "Invalid token");
                }

                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return (false, null, "User ID not found in token");
                }

                // Kullanıcı engellenmiş mi kontrol et
                if (await IsUserBlockedAsync(userId))
                {
                    return (false, null, "User is blocked");
                }

                return (true, userId, null);
            }
            catch (SecurityTokenExpiredException)
            {
                return (false, null, "Token expired");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token doğrulama hatası");
                return (false, null, ex.Message);
            }
        }

        public async Task<(bool isValid, AppUser user, RefreshToken refreshToken, string error)>
            ValidateRefreshTokenAsync(
                string refreshToken, string ipAddress, string userAgent)
        {
            try
            {
                // Refresh token'ı hash'le
                var tokenHash = HashToken(refreshToken);

                // Veritabanında token'ı bul
                var storedToken = await _dbContext.RefreshTokens
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

                if (storedToken == null)
                {
                    return (false, null, null, "Refresh token not found");
                }

                // Token aktif mi kontrol et
                if (!storedToken.IsActive)
                {
                    return (false, null, storedToken, "Refresh token is not active");
                }

                // IP adresi kontrolü
                if (_jwtSettings.CheckIpAddress && storedToken.CreatedByIp != ipAddress)
                {
                    // Token'ı iptal et ve hata döndür
                    storedToken.IsRevoked = true;
                    storedToken.RevokedByIp = ipAddress;
                    storedToken.ReasonRevoked = "IP address mismatch";
                    storedToken.RevokedDate = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    return (false, null, storedToken, "IP address mismatch");
                }

                // Tarayıcı kontrolü
                if (_jwtSettings.CheckUserAgent && storedToken.UserAgent != userAgent)
                {
                    // Token'ı iptal et ve hata döndür
                    storedToken.IsRevoked = true;
                    storedToken.RevokedByIp = ipAddress;
                    storedToken.ReasonRevoked = "User agent mismatch";
                    storedToken.RevokedDate = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    return (false, null, storedToken, "User agent mismatch");
                }

                // Kullanıcı engellenmiş mi kontrol et
                if (await IsUserBlockedAsync(storedToken.UserId))
                {
                    storedToken.IsRevoked = true;
                    storedToken.RevokedByIp = ipAddress;
                    storedToken.ReasonRevoked = "User is blocked";
                    storedToken.RevokedDate = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    return (false, null, storedToken, "User is blocked");
                }

                return (true, storedToken.User, storedToken, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh token doğrulama hatası");
                return (false, null, null, ex.Message);
            }
        }

        // YARDIMCI METOTLAR
        private string CreateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);

            return Convert.ToBase64String(randomNumber);
        }

        private string HashToken(string token)
        {
            using var sha256 = SHA256.Create();
            var tokenBytes = Encoding.UTF8.GetBytes(token);
            var hashBytes = sha256.ComputeHash(tokenBytes);
            return Convert.ToBase64String(hashBytes);
        }

        private async Task<List<Claim>> GetUserClaimsAsync(AppUser user)
        {
            var cacheKey = $"{UserClaimsCachePrefix}{user.Id}";

            // Try to get from Redis cache
            var (success, cachedClaims) = await _cache.TryGetValueAsync<List<UserClaimCache>>(cacheKey);
            if (success && cachedClaims != null)
            {
                return cachedClaims.Select(cc => cc.ToClaim()).ToList();
            }

            await _semaphore.WaitAsync();
            try
            {
                // Double check
                (success, cachedClaims) = await _cache.TryGetValueAsync<List<UserClaimCache>>(cacheKey);
                if (success && cachedClaims != null)
                {
                    return cachedClaims.Select(cc => cc.ToClaim()).ToList();
                }

                var claims = await CreateUserClaimsAsync(user);

                // Convert to cache model before saving
                var claimCacheModels = claims.Select(UserClaimCache.FromClaim).ToList();
                await _cache.SetAsync(cacheKey, claimCacheModels, ClaimsCacheDuration);

                return claims;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<List<Claim>> CreateUserClaimsAsync(AppUser user)
        {
            try
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id),
                    new(ClaimTypes.Name, user.UserName),
                    new(JwtRegisteredClaimNames.Email, user.Email),
                    new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new("NameSurname", user.NameSurname)
                };

                var userRoles = await _userManager.GetRolesAsync(user);
                claims.AddRange(userRoles.Select(role => new Claim(ClaimTypes.Role, role)));

                // Özel claim'leri ekle
                if (!string.IsNullOrEmpty(user.PhoneNumber))
                {
                    claims.Add(new Claim(ClaimTypes.MobilePhone, user.PhoneNumber));
                }

                if (user.EmailConfirmed)
                {
                    claims.Add(new Claim("email_verified", "true"));
                }

                return claims;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kullanıcı claim'leri oluşturulurken hata oluştu. Kullanıcı: {UserId}", user.Id);
                throw;
            }
        }

        public async Task InvalidateUserClaimsCache(string userId)
        {
            var cacheKey = $"{UserClaimsCachePrefix}{userId}";
            await _cache.RemoveAsync(cacheKey);
        }

        public async Task<bool> IsUserBlockedAsync(string userId)
        {
            var cacheKey = $"{UserBlockedCachePrefix}{userId}";

            // Cache'ten kontrol et
            var (success, isBlocked) = await _cache.TryGetValueAsync<bool>(cacheKey);
            if (success)
            {
                return isBlocked;
            }

            // E-posta doğrulama zorunluluğunu kontrol et
            bool requireEmailConfirmation = _jwtSettings.RequireEmailConfirmation;

            // Veritabanından kontrol et
            var user = await _userManager.FindByIdAsync(userId);
            var blocked = user == null || 
                          !user.IsActive || 
                          user.LockoutEnd > DateTime.UtcNow || // Hesap kilitli
                          (requireEmailConfirmation && !user.EmailConfirmed); // E-posta doğrulanmamış ve zorunluysa

            // Cache'e kaydet
            await _cache.SetAsync(cacheKey, blocked, TimeSpan.FromMinutes(10));

            return blocked;
        }

        private async Task LimitActiveRefreshTokensAsync(string userId)
        {
            if (_jwtSettings.MaxActiveRefreshTokens <= 0)
            {
                return;
            }

            // Kullanıcının aktif token sayısını bul
            var activeTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked && !rt.IsUsed && rt.ExpiryDate > DateTime.UtcNow)
                .OrderByDescending(rt => rt.CreatedDate)
                .ToListAsync();

            // Limit aşıldıysa en eski token'ları iptal et
            if (activeTokens.Count >= _jwtSettings.MaxActiveRefreshTokens)
            {
                var tokensToRevoke = activeTokens.Skip(_jwtSettings.MaxActiveRefreshTokens - 1).ToList();

                foreach (var token in tokensToRevoke)
                {
                    token.IsRevoked = true;
                    token.ReasonRevoked = "Max active tokens exceeded";
                    token.RevokedDate = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();
            }
        }

        private string GetIpAddress(HttpContext httpContext)
        {
            if (httpContext == null)
            {
                return "0.0.0.0";
            }

            // X-Forwarded-For veya gerçek IP adresini al
            if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                var ips = forwardedFor.ToString().Split(',');
                return ips[0].Trim();
            }

            return httpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        }

        private string GetUserAgent(HttpContext httpContext)
        {
            if (httpContext == null)
            {
                return "Unknown";
            }

            return httpContext.Request.Headers.UserAgent.ToString() ?? "Unknown";
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }

    // Cache model sınıfı
    public class UserClaimCache
    {
        public string Type { get; set; }
        public string Value { get; set; }
        
        public Claim ToClaim()
        {
            return new Claim(Type, Value);
        }
        
        public static UserClaimCache FromClaim(Claim claim)
        {
            return new UserClaimCache
            {
                Type = claim.Type,
                Value = claim.Value
            };
        }
    }
}