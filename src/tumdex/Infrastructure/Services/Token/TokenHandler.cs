using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Application.Abstraction.Services;
using Application.Tokens;
using Domain.Identity;
using Infrastructure.Services.Security.JWT;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services.Token;

public class TokenHandler : ITokenHandler
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtService _jwtService;
    private readonly ILogger<TokenHandler> _logger;
    private readonly ICacheService _cache;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    private const string UserClaimsCachePrefix = "UserClaims_";
    private static readonly TimeSpan ClaimsCacheDuration = TimeSpan.FromMinutes(30);

    public TokenHandler(
        UserManager<AppUser> userManager,
        IJwtService jwtService,
        ILogger<TokenHandler> logger,
        ICacheService cache)
    {
        _userManager = userManager;
        _jwtService = jwtService;
        _logger = logger;
        _cache = cache;
    }

    public async Task<Application.Dtos.Token.Token> CreateAccessTokenAsync(int second, AppUser appUser)
    {
        try
        {
            // JWT yapılandırmasını ve kullanıcı rollerini parallel olarak al
            var jwtConfigTask = _jwtService.GetJwtConfigurationAsync();
            var claimsTask = GetUserClaimsAsync(appUser);
            
            await Task.WhenAll(jwtConfigTask, claimsTask);
            
            var jwtConfig = await jwtConfigTask;
            var claims = await claimsTask;

            _logger.LogInformation("JWT yapılandırması alındı. SecurityKey: {KeyId}", 
                jwtConfig.SecurityKey?.KeyId ?? "No KeyId");

            var token = new Application.Dtos.Token.Token();

            // SigningCredentials oluştur
            var signingCredentials = new SigningCredentials(
                jwtConfig.SecurityKey, 
                SecurityAlgorithms.HmacSha256);

            // Token'ın son kullanma tarihini ayarla
            token.Expiration = DateTime.UtcNow.AddSeconds(second);

            // JWT Token oluştur
            var securityToken = new JwtSecurityToken(
                issuer: jwtConfig.Issuer.ToString(),
                audience: jwtConfig.Audience.ToString(),
                expires: token.Expiration,
                notBefore: DateTime.UtcNow,
                signingCredentials: signingCredentials,
                claims: claims
            );

            // Token'ı string'e çevir
            var tokenHandler = new JwtSecurityTokenHandler();
            token.AccessToken = tokenHandler.WriteToken(securityToken);

            // Refresh token oluştur
            token.RefreshToken = CreateRefreshToken();

            _logger.LogInformation("Token başarıyla oluşturuldu");
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token oluşturulurken hata oluştu. Kullanıcı: {UserId}", appUser.Id);
            throw;
        }
    }

    public Task<Application.Dtos.Token.Token> CreateAccessTokenAsync(AppUser user)
    {
        return CreateAccessTokenAsync(120 * 60, user); // 120 dakika
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

    public string CreateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        
        return Convert.ToBase64String(randomNumber);
    }

    public async Task InvalidateUserClaimsCache(string userId)
    {
        var cacheKey = $"{UserClaimsCachePrefix}{userId}";
        await _cache.RemoveAsync(cacheKey);
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}