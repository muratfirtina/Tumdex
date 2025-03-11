using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Application.Dtos.Token;
using Application.Exceptions;
using Application.Tokens;
using Azure.Security.KeyVault.Secrets;
using Domain.Identity;
using Infrastructure.Services.Security.JWT;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Persistence.Context;

namespace Infrastructure.Services.Token;

/// <summary>
/// JWT token oluşturma, yenileme ve iptal etme işlemlerini yönetir.
/// Azure Key Vault'tan güvenlik anahtarlarını alır.
/// </summary>
public class TokenHandler : ITokenHandler
{
    private readonly UserManager<AppUser> _userManager;
    private readonly TumdexDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<TokenHandler> _logger;
    private readonly SecretClient _secretClient;
    
    // Token ayarları için lazy initialization
    private readonly SemaphoreSlim _tokenSettingsLock = new SemaphoreSlim(1, 1);
    private SigningCredentials? _signingCredentials;
    private TokenValidationParameters? _tokenValidationParameters;
    private string? _issuer;
    private string? _audience;
    private string? _securityKeyValue;
    
    private bool _disposed = false;

    /// <summary>
    /// TokenHandler sınıfını başlatır
    /// </summary>
    public TokenHandler(
        UserManager<AppUser> userManager,
        TumdexDbContext context,
        IDistributedCache cache,
        ILogger<TokenHandler> logger,
        SecretClient secretClient)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _secretClient = secretClient ?? throw new ArgumentNullException(nameof(secretClient));
    }

    /// <summary>
    /// Varsayılan parametrelerle erişim tokeni oluşturur
    /// </summary>
    public async Task<TokenDto> CreateAccessTokenAsync(AppUser user, int accessTokenLifetime = 15)
    {
        return await CreateAccessTokenAsync(user, null, accessTokenLifetime);
    }

    /// <summary>
    /// HTTP bağlamı ile birlikte erişim tokeni oluşturur
    /// </summary>
    public async Task<TokenDto> CreateAccessTokenAsync(AppUser user, HttpContext? httpContext = null, int accessTokenLifetime = 15)
    {
        // Güvenlik ayarlarının hazır olduğundan emin ol
        await EnsureTokenSettingsInitializedAsync();

        // Engellenen kullanıcılar için token oluşturmayı engelle
        if (await IsUserBlockedAsync(user.Id))
        {
            throw new AuthenticationErrorException("Bu kullanıcı kimlik doğrulama için engellendi.");
        }

        // Token sürelerini hesapla
        DateTime accessTokenExpiration = DateTime.UtcNow.AddMinutes(accessTokenLifetime);
        
        // Refresh token süresini hesapla (varsayılan 14 gün)
        int refreshTokenLifetimeDays = 14;
        DateTime refreshTokenExpiration = DateTime.UtcNow.AddDays(refreshTokenLifetimeDays);

        // Token için talepleri (claims) oluştur
        var claims = await GenerateClaimsForUser(user);
        string jwtId = Guid.NewGuid().ToString();
        claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jwtId));

        // HTTP bağlamı varsa IP adresi ve kullanıcı aracısı taleplerini ekle
        string ipAddress = "0.0.0.0";
        string userAgent = "Unknown";
        
        if (httpContext != null)
        {
            ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            userAgent = httpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";

            claims.Add(new Claim("ip_address", ipAddress));
            claims.Add(new Claim("user_agent", userAgent));
        }

        // JWT güvenlik tokenini oluştur
        JwtSecurityToken securityToken = new(
            issuer: _issuer,
            audience: _audience,
            expires: accessTokenExpiration,
            notBefore: DateTime.UtcNow,
            claims: claims,
            signingCredentials: _signingCredentials
        );

        // Token dizesini oluştur
        JwtSecurityTokenHandler tokenHandler = new();
        string token = tokenHandler.WriteToken(securityToken);

        // Yenileme tokeni oluştur
        string refreshToken = GenerateRefreshToken();
        string refreshTokenHash = HashToken(refreshToken);

        // Kullanıcı için maksimum aktif token sayısını kontrol et ve gerekirse temizle
        await EnforceMaxActiveTokensAsync(user.Id);

        // Bu kullanıcı için yenileme tokenini sakla
        await StoreRefreshToken(
            refreshToken,
            refreshTokenHash,
            user.Id,
            jwtId,
            ipAddress,
            userAgent,
            refreshTokenExpiration);

        // Tam token DTO'sunu döndür
        return new TokenDto
        {
            AccessToken = token,
            AccessTokenExpiration = accessTokenExpiration,
            RefreshToken = refreshToken,
            RefreshTokenExpiration = refreshTokenExpiration,
            UserId = user.Id
        };
    }

    /// <summary>
    /// Yenileme tokeni kullanarak yeni erişim tokeni oluşturur
    /// </summary>
    public async Task<TokenDto> RefreshAccessTokenAsync(string refreshToken, string? ipAddress = null, string? userAgent = null)
    {
        // Güvenlik ayarlarının hazır olduğundan emin ol
        await EnsureTokenSettingsInitializedAsync();
        
        ipAddress ??= "0.0.0.0";
        userAgent ??= "Unknown";
        
        // Veritabanı araması için token hash'ini oluştur
        string tokenHash = HashToken(refreshToken);
        
        // Veritabanında token'ı bul
        var token = await _context.Set<RefreshToken>()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
        
        if (token == null)
        {
            throw new InvalidOperationException("Geçersiz yenileme tokeni");
        }
        
        if (token.IsRevoked)
        {
            // Güvenlik için ilgili tokenları iptal et
            await RevokeRelatedTokens(token, ipAddress, "İptal edilmiş tokenin kullanım girişimi");
            throw new InvalidOperationException("Yenileme tokeni iptal edilmiş");
        }
        
        if (token.IsUsed)
        {
            // Güvenlik için ilgili tokenları iptal et
            await RevokeRelatedTokens(token, ipAddress, "Kullanılmış tokenin yeniden kullanım girişimi");
            throw new InvalidOperationException("Yenileme tokeni zaten kullanılmış");
        }
        
        if (token.ExpiryDate < DateTime.UtcNow)
        {
            // Tokenı kullanılmış olarak işaretle
            token.IsUsed = true;
            await _context.SaveChangesAsync();
            throw new InvalidOperationException("Yenileme tokeni süresi dolmuş");
        }
        
        // Token'da IP adresi varsa IP adresini doğrula (isteğe bağlı)
        bool validateIp = true; // Yapılandırmadan çekilebilir
        if (validateIp && !string.IsNullOrEmpty(token.CreatedByIp) && token.CreatedByIp != ipAddress)
        {
            _logger.LogWarning("Yenileme tokeni farklı IP'den kullanıldı. Orijinal: {OriginalIp}, Şimdiki: {CurrentIp}",
                token.CreatedByIp, ipAddress);
            
            // Şüpheli aktiviteyi kaydet ama tokenin kullanılmasına hala izin ver
            // Güvenlik politikasına göre burada token iptal edilebilir
        }
        
        // Mevcut tokeni kullanılmış olarak işaretle
        token.IsUsed = true;
        await _context.SaveChangesAsync();
        
        // Yeni bir erişim tokeni oluştur
        var accessTokenLifetime = 30; // Dakika
        var newTokenDto = await CreateAccessTokenAsync(token.User, null, accessTokenLifetime);
        
        // Token aileleri kullanılıyorsa token aile ilişkisini güncelle
        bool useTokenFamilies = true; // Yapılandırmadan çekilebilir
        if (useTokenFamilies && !string.IsNullOrEmpty(token.FamilyId))
        {
            // Yeni token için aynı aile kimliğini ayarla
            var newRefreshToken = await _context.Set<RefreshToken>()
                .FirstOrDefaultAsync(rt => rt.TokenHash == HashToken(newTokenDto.RefreshToken));
            
            if (newRefreshToken != null)
            {
                newRefreshToken.FamilyId = token.FamilyId;
                await _context.SaveChangesAsync();
            }
        }
        
        return newTokenDto;
    }

    /// <summary>
    /// Belirli bir yenileme tokenini iptal eder
    /// </summary>
    public async Task RevokeRefreshTokenAsync(string refreshToken, string? ipAddress = null, string? reasonRevoked = null)
    {
        try
        {
            string tokenHash = HashToken(refreshToken);
            
            // Veritabanında yenileme tokenini bul
            var token = await _context.Set<RefreshToken>()
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash && !rt.IsRevoked);

            if (token != null)
            {
                // Tokeni iptal et
                token.IsRevoked = true;
                token.RevokedDate = DateTime.UtcNow;
                token.RevokedByIp = ipAddress;
                token.ReasonRevoked = reasonRevoked ?? "Token iptal edildi";

                await _context.SaveChangesAsync();
                _logger.LogInformation("Yenileme tokeni iptal edildi: {TokenId}", token.Id);
                
                // Token aileleri kullanılıyorsa, çocuk tokenları da iptal et
                bool useTokenFamilies = true; // Yapılandırmadan çekilebilir
                if (useTokenFamilies && !string.IsNullOrEmpty(token.FamilyId))
                {
                    await RevokeTokenFamily(token.FamilyId, token.Id, ipAddress, 
                        $"Üst token {token.Id} iptal edildi: {reasonRevoked}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yenileme tokenini iptal ederken hata oluştu");
            throw;
        }
    }

    /// <summary>
    /// Belirli bir kullanıcı için tüm yenileme tokenlarını iptal eder
    /// </summary>
    public async Task RevokeAllUserRefreshTokensAsync(string userId, string? ipAddress = null, string? reasonRevoked = null)
    {
        try
        {
            // Bu kullanıcı için tüm aktif yenileme tokenlarını bul
            var tokens = await _context.Set<RefreshToken>()
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in tokens)
            {
                // Her tokeni iptal et
                token.IsRevoked = true;
                token.RevokedDate = DateTime.UtcNow;
                token.RevokedByIp = ipAddress;
                token.ReasonRevoked = reasonRevoked ?? "Tüm kullanıcı tokenları iptal edildi";
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Kullanıcı için tüm yenileme tokenları iptal edildi: {UserId}", userId);

            // Bu kullanıcı için talep önbelleğini geçersiz kıl
            await InvalidateUserClaimsCache(userId);
            
            // Kullanıcı kaydını da güncelle
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
                await _userManager.UpdateAsync(user);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tüm kullanıcı yenileme tokenlarını iptal ederken hata oluştu");
            throw;
        }
    }

    /// <summary>
    /// Erişim tokenini doğrular
    /// </summary>
    public async Task<(bool isValid, string userId, string error)> ValidateAccessTokenAsync(string token)
    {
        try
        {
            // Güvenlik ayarlarının hazır olduğundan emin ol
            await EnsureTokenSettingsInitializedAsync();
            
            JwtSecurityTokenHandler tokenHandler = new();
            var principal = tokenHandler.ValidateToken(token, _tokenValidationParameters, out SecurityToken validatedToken);

            // Taleplerden kullanıcı kimliğini çıkar
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return (false, string.Empty, "Token bir kullanıcı tanımlayıcısı içermiyor");
            }

            string userId = userIdClaim.Value;

            // Kullanıcının engellenip engellenmediğini kontrol et
            if (await IsUserBlockedAsync(userId))
            {
                return (false, userId, "Kullanıcı engellendi");
            }

            return (true, userId, string.Empty);
        }
        catch (SecurityTokenExpiredException)
        {
            return (false, string.Empty, "Token süresi doldu");
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            return (false, string.Empty, "Token geçersiz imzaya sahip");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token doğrulama hatası");
            return (false, string.Empty, $"Token doğrulama başarısız: {ex.Message}");
        }
    }

    /// <summary>
    /// Yenileme tokenini doğrular
    /// </summary>
    public async Task<(bool isValid, AppUser user, RefreshToken refreshToken, string error)> ValidateRefreshTokenAsync(
        string refreshToken, string ipAddress, string userAgent)
    {
        try
        {
            // Veritabanı araması için token hash'ini oluştur
            string tokenHash = HashToken(refreshToken);
            
            // Yenileme tokenini bul
            var token = await _context.Set<RefreshToken>()
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

            if (token == null)
            {
                return (false, null!, null!, "Yenileme tokeni mevcut değil");
            }

            if (token.IsRevoked)
            {
                return (false, null!, null!, "Yenileme tokeni iptal edilmiş");
            }
            
            if (token.IsUsed)
            {
                return (false, null!, null!, "Yenileme tokeni zaten kullanılmış");
            }

            if (token.ExpiryDate < DateTime.UtcNow)
            {
                return (false, null!, null!, "Yenileme tokeni süresi dolmuş");
            }

            // Kullanıcının engellenip engellenmediğini kontrol et
            if (await IsUserBlockedAsync(token.UserId))
            {
                return (false, null!, null!, "Kullanıcı engellendi");
            }

            // IP ve UserAgent doğrulaması (isteğe bağlı)
            bool checkIp = true; // Yapılandırmadan alınabilir
            bool checkUserAgent = true; // Yapılandırmadan alınabilir
            
            if (checkIp && !string.IsNullOrEmpty(token.CreatedByIp) && token.CreatedByIp != ipAddress)
            {
                _logger.LogWarning("Yenileme tokeni farklı IP'den doğrulanmaya çalışıldı: {OriginalIp}, {CurrentIp}", 
                    token.CreatedByIp, ipAddress);
                // Güvenlik politikanıza göre, burada hata dönmek isteyebilirsiniz
            }
            
            if (checkUserAgent && !string.IsNullOrEmpty(token.UserAgent) && token.UserAgent != userAgent)
            {
                _logger.LogWarning("Yenileme tokeni farklı tarayıcıdan doğrulanmaya çalışıldı: {OriginalAgent}, {CurrentAgent}",
                    token.UserAgent, userAgent);
                // Güvenlik politikanıza göre, burada hata dönmek isteyebilirsiniz
            }

            // Yanıt için geçici token ayarla
            token.SetToken(refreshToken);
            
            return (true, token.User, token, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yenileme tokeni doğrulama hatası");
            return (false, null!, null!, $"Yenileme tokeni doğrulama başarısız: {ex.Message}");
        }
    }

    /// <summary>
    /// Kullanıcı taleplerini önbellekten temizler
    /// </summary>
    public async Task InvalidateUserClaimsCache(string userId)
    {
        try
        {
            // Talepleri dağıtılmış önbellekten kaldır
            string cacheKey = $"UserClaims:{userId}";
            await _cache.RemoveAsync(cacheKey);
            _logger.LogInformation("Kullanıcı için talep önbelleği geçersiz kılındı: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kullanıcı talepleri önbelleğini geçersiz kılarken hata oluştu");
        }
    }

    /// <summary>
    /// Bir kullanıcının kimlik doğrulama için engellenip engellenmediğini kontrol eder
    /// </summary>
    public async Task<bool> IsUserBlockedAsync(string userId)
    {
        try
        {
            // Önce engelleme durumu için önbelleği kontrol et
            string cacheKey = $"UserBlocked:{userId}";
            string? cachedValue = await _cache.GetStringAsync(cacheKey);
            
            if (cachedValue != null)
            {
                return bool.Parse(cachedValue);
            }

            // Önbellekte yoksa veritabanını kontrol et
            var user = await _userManager.FindByIdAsync(userId);
            bool isBlocked = user == null || !user.IsActive;

            // Hızlı gelecekteki kontroller için sonucu önbelleğe al
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            };
            
            await _cache.SetStringAsync(cacheKey, isBlocked.ToString(), cacheOptions);
            
            return isBlocked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kullanıcının engellenip engellenmediğini kontrol ederken hata oluştu");
            return false; // Hata durumunda varsayılan olarak engellenmemiş kabul et
        }
    }

    #region Yardımcı Metotlar

    /// <summary>
    /// Token ayarlarını Azure Key Vault'tan alır ve başlatır
    /// </summary>
    private async Task EnsureTokenSettingsInitializedAsync()
    {
        if (_signingCredentials != null && _tokenValidationParameters != null)
        {
            return; // Zaten yüklenmiş
        }

        await _tokenSettingsLock.WaitAsync();
        try
        {
            // Double check locking pattern
            if (_signingCredentials != null && _tokenValidationParameters != null)
            {
                return;
            }

            // Key Vault'tan güvenlik anahtarlarını al
            try
            {
                var securityKeyResponse = await _secretClient.GetSecretAsync("JwtSecurityKey");
                var issuerResponse = await _secretClient.GetSecretAsync("JwtIssuer");
                var audienceResponse = await _secretClient.GetSecretAsync("JwtAudience");

                if (securityKeyResponse?.Value == null || issuerResponse?.Value == null || audienceResponse?.Value == null)
                {
                    throw new InvalidOperationException("JWT anahtarları Key Vault'ta bulunamadı");
                }

                _securityKeyValue = securityKeyResponse.Value.Value;
                _issuer = issuerResponse.Value.Value;
                _audience = audienceResponse.Value.Value;

                // Security key oluştur
                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_securityKeyValue));
                _signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);

                // Token doğrulama parametrelerini oluştur
                _tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = securityKey,
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };

                _logger.LogInformation("JWT token ayarları Azure Key Vault'tan başarıyla yüklendi");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JWT token ayarları yüklenirken hata oluştu");
                throw;
            }
        }
        finally
        {
            _tokenSettingsLock.Release();
        }
    }

    /// <summary>
    /// Kullanıcının maksimum aktif token sayısını kontrol eder ve gerekirse eski tokenları temizler
    /// </summary>
    private async Task EnforceMaxActiveTokensAsync(string userId)
    {
        int maxActiveTokens = 5; // Yapılandırmadan alınabilir
        
        if (maxActiveTokens <= 0)
            return; // Sınırlama yok
            
        // Kullanıcının aktif tokenlarını say
        var activeTokens = await _context.Set<RefreshToken>()
            .Where(t => t.UserId == userId && !t.IsRevoked && !t.IsUsed && t.ExpiryDate > DateTime.UtcNow)
            .OrderByDescending(t => t.CreatedDate)
            .ToListAsync();
            
        // Eğer limit aşıldıysa, en eski tokenları iptal et
        if (activeTokens.Count >= maxActiveTokens)
        {
            // En son maxActiveTokens sayıda token dışındakileri iptal et
            var tokensToRevoke = activeTokens.Skip(maxActiveTokens - 1).ToList();
            
            foreach (var token in tokensToRevoke)
            {
                token.IsRevoked = true;
                token.RevokedDate = DateTime.UtcNow;
                token.ReasonRevoked = "Token sayısı sınırı aşıldı";
            }
            
            await _context.SaveChangesAsync();
            _logger.LogInformation("{Count} eski token iptal edildi - kullanıcı: {UserId}", 
                tokensToRevoke.Count, userId);
        }
    }

    /// <summary>
    /// Bir kullanıcı için talepleri oluşturur
    /// </summary>
    private async Task<List<Claim>> GenerateClaimsForUser(AppUser user)
    {
        // Önbellekten kontrol et
        string cacheKey = $"UserClaims:{user.Id}";
        byte[]? cachedClaimsBytes = await _cache.GetAsync(cacheKey);
        
        if (cachedClaimsBytes != null)
        {
            try
            {
                // Önbellekteki talepleri deserialize et
                string cachedClaimsJson = Encoding.UTF8.GetString(cachedClaimsBytes);
                var cachedClaims = System.Text.Json.JsonSerializer.Deserialize<List<UserClaimCache>>(cachedClaimsJson);
                
                if (cachedClaims != null)
                {
                    return cachedClaims.Select(c => c.ToClaim()).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Önbellekten claim deserialize hatası");
                // Hata durumunda devam et ve yeni claims oluştur
            }
        }
        
        // Standart talepleri ekle
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("name_surname", user.NameSurname ?? string.Empty),
            new("created_date", DateTime.UtcNow.ToString("o"))
        };

        // Kullanıcının rollerini taleplere ekle
        var userRoles = await _userManager.GetRolesAsync(user);
        foreach (var role in userRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }
        
        // Talepleri önbelleğe al
        try
        {
            var claimCacheList = claims.Select(c => new UserClaimCache 
            { 
                Type = c.Type, 
                Value = c.Value,
                ValueType = c.ValueType,
                Issuer = c.Issuer,
                OriginalIssuer = c.OriginalIssuer 
            }).ToList();
            
            string claimsJson = System.Text.Json.JsonSerializer.Serialize(claimCacheList);
            
            await _cache.SetAsync(
                cacheKey,
                Encoding.UTF8.GetBytes(claimsJson),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Talepleri önbelleğe alırken hata oluştu");
        }

        return claims;
    }

    /// <summary>
    /// Rastgele bir yenileme tokeni oluşturur
    /// </summary>
    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
    
    /// <summary>
    /// Bir tokeni hash'ler
    /// </summary>
    private string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Veritabanında bir yenileme tokenini saklar
    /// </summary>
    private async Task<RefreshToken> StoreRefreshToken(
        string token, 
        string tokenHash,
        string userId, 
        string jwtId,
        string ipAddress, 
        string userAgent,
        DateTime expiryDate)
    {
        // Refresh tokenleri ailelendirmek isteniyorsa
        string? familyId = null;
        bool useTokenFamilies = true; // Yapılandırmadan alınabilir
        
        if (useTokenFamilies)
        {
            // Kullanıcının mevcut bir token ailesini kontrol et veya yeni oluştur
            var latestToken = await _context.Set<RefreshToken>()
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .OrderByDescending(rt => rt.CreatedDate)
                .FirstOrDefaultAsync();
                
            familyId = latestToken?.FamilyId ?? Guid.NewGuid().ToString();
        }
        
        // Fabrika metodunu kullanarak yenileme tokeni oluştur
        var refreshToken = RefreshToken.CreateToken(
            token,
            tokenHash,
            userId,
            jwtId,
            ipAddress,
            userAgent,
            expiryDate,
            familyId
        );

        // Veritabanında sakla
        _context.Set<RefreshToken>().Add(refreshToken);
        await _context.SaveChangesAsync();
        
        // Bu yenileme tokeni ile kullanıcı kaydını güncelle
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            user.SetRefreshToken(refreshToken);
            await _userManager.UpdateAsync(user);
        }

        return refreshToken;
    }
    
    /// <summary>
    /// İlgili tokenları iptal eder (token yeniden kullanım tespiti için)
    /// </summary>
    private async Task RevokeRelatedTokens(RefreshToken token, string? ipAddress, string reason)
    {
        // Token aileleri kullanılıyorsa, ailedeki tüm tokenları iptal et
        if (!string.IsNullOrEmpty(token.FamilyId))
        {
            await RevokeTokenFamily(token.FamilyId, token.Id, ipAddress, reason);
        }
    }
    
    /// <summary>
    /// Bir ailedeki tüm tokenları iptal eder
    /// </summary>
    private async Task RevokeTokenFamily(string familyId, string currentTokenId, string? ipAddress, string reason)
    {
        try
        {
            var tokensInFamily = await _context.Set<RefreshToken>()
                .Where(t => t.FamilyId == familyId && t.Id != currentTokenId && !t.IsRevoked)
                .ToListAsync();
                
            foreach (var token in tokensInFamily)
            {
                token.IsRevoked = true;
                token.RevokedDate = DateTime.UtcNow;
                token.RevokedByIp = ipAddress;
                token.ReasonRevoked = reason;
            }
                
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token ailesini iptal ederken hata oluştu: {FamilyId}", familyId);
        }
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// IDisposable uygulaması
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Yönetilen kaynakları serbest bırak
                _tokenSettingsLock.Dispose();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Nesneyi imha eder
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
