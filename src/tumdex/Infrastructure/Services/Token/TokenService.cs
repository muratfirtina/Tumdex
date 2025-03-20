using System.Text;
using System.Text.Json;
using Application.Abstraction.Services.Tokens;
using Application.Abstraction.Services.Utilities;
using Application.Features.Tokens.Command.ActivationCode.ActivationUrlToken;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Persistence.Services;

namespace Infrastructure.Services.Token;

public class TokenService : ITokenService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<TokenService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _cache;

    public TokenService(UserManager<AppUser> userManager, IEncryptionService encryptionService,
        ILogger<TokenService> logger, IConfiguration configuration, IDistributedCache cache)
    {
        _userManager = userManager;
        _encryptionService = encryptionService;
        _logger = logger;
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<string> GenerateSecureTokenAsync(string userId, string email, string purpose,
        int expireHours = 24)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
        {
            throw new ArgumentException("User ID and email are required for token generation");
        }

        AppUser user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new ArgumentException($"User with ID {userId} not found");
        }

        // Generate token based on purpose
        string token;
        switch (purpose.ToLower())
        {
            case "emailconfirmation":
                token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                break;
            case "passwordreset":
                token = await _userManager.GeneratePasswordResetTokenAsync(user);
                break;
            case "activation":
                token = await _encryptionService.GenerateActivationTokenAsync(userId, email);
                break;
            default:
                // For custom tokens, create a secure payload with expiration
                var tokenData = new
                {
                    UserId = userId,
                    Email = email,
                    Purpose = purpose,
                    ExpiryTime = DateTime.UtcNow.AddHours(expireHours),
                    Nonce = Guid.NewGuid().ToString()
                };

                // Serialize and encrypt the token data
                token = await _encryptionService.EncryptAsync(JsonSerializer.Serialize(tokenData));
                break;
        }

        // Encode for URL safety
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }

    public async Task<bool> VerifySecureTokenAsync(string userId, string token, string purpose)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            return false;
        }

        AppUser user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return false;
        }

        // Decode the URL-safe token
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decode token for user {UserId}", userId);
            return false;
        }

        // Verify token based on purpose
        switch (purpose.ToLower())
        {
            case "emailconfirmation":
                return await _userManager.VerifyUserTokenAsync(
                    user,
                    _userManager.Options.Tokens.EmailConfirmationTokenProvider,
                    UserManager<AppUser>.ConfirmEmailTokenPurpose,
                    token);

            case "passwordreset":
                return await _userManager.VerifyUserTokenAsync(
                    user,
                    _userManager.Options.Tokens.PasswordResetTokenProvider,
                    UserManager<AppUser>.ResetPasswordTokenPurpose,
                    token);

            case "activation":
                return await _encryptionService.VerifyActivationTokenAsync(userId, user.Email, token);

            default:
                // For custom tokens, decrypt and verify
                try
                {
                    string decrypted = await _encryptionService.DecryptAsync(token);
                    JsonDocument tokenData = JsonDocument.Parse(decrypted);

                    // Extract and verify token data
                    string tokenUserId = tokenData.RootElement.GetProperty("UserId").GetString();
                    string tokenEmail = tokenData.RootElement.GetProperty("Email").GetString();
                    string tokenPurpose = tokenData.RootElement.GetProperty("Purpose").GetString();
                    DateTime expiryTime = tokenData.RootElement.GetProperty("ExpiryTime").GetDateTime();

                    return tokenUserId == userId &&
                           tokenEmail == user.Email &&
                           tokenPurpose == purpose &&
                           expiryTime > DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to verify token for user {UserId}", userId);
                    return false;
                }
        }
    }

    public async Task<TokenValidationResult> VerifyActivationTokenAsync(string token, string purpose,
        string expectedUserId = null)
    {
        var result = new TokenValidationResult();

        if (string.IsNullOrEmpty(token))
        {
            result.IsValid = false;
            result.Message = "Token is required";
            return result;
        }

        try
        {
            // Decode the URL-safe token
            string decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));

            // For activation tokens, use the encryption service
            if (purpose.ToLower() == "activation")
            {
                try
                {
                    string decrypted = await _encryptionService.DecryptAsync(decodedToken);
                    JsonDocument tokenData = JsonDocument.Parse(decrypted);

                    // Extract token data
                    string userId = tokenData.RootElement.GetProperty("UserId").GetString();
                    string email = tokenData.RootElement.GetProperty("Email").GetString();
                    DateTime expiryTime = tokenData.RootElement.GetProperty("ExpiryTime").GetDateTime();

                    // Validate expiration
                    if (expiryTime <= DateTime.UtcNow)
                    {
                        result.IsValid = false;
                        result.Message = "Token has expired";
                        return result;
                    }

                    // Validate user ID if expected value is provided
                    if (!string.IsNullOrEmpty(expectedUserId) && userId != expectedUserId)
                    {
                        result.IsValid = false;
                        result.Message = "Invalid token";
                        return result;
                    }

                    // Verify the user exists
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user == null)
                    {
                        result.IsValid = false;
                        result.Message = "User not found";
                        return result;
                    }

                    // All validation passed
                    result.IsValid = true;
                    result.UserId = userId;
                    result.Email = email;
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to decrypt activation token");
                    result.IsValid = false;
                    result.Message = "Invalid token format";
                    return result;
                }
            }
            else
            {
                // For other token types, implement appropriate logic
                result.IsValid = false;
                result.Message = $"Unsupported token purpose: {purpose}";
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token validation failed");
            result.IsValid = false;
            result.Message = "Token validation failed";
            return result;
        }
    }

    public async Task<string> GenerateSecureActivationTokenAsync(string userId, string email)
    {
        return await GenerateSecureTokenAsync(userId, email, "activation", 24);
    }

    public async Task<bool> VerifyResetPasswordTokenAsync(string userId, string resetToken)
    {
        return await VerifySecureTokenAsync(userId, resetToken, "PasswordReset");
    }

    /// <summary>
    /// Generates an email activation code for a user
    /// </summary>
    public async Task<string> GenerateActivationCodeAsync(string userId)
    {
        // 6 haneli rastgele bir kod oluştur
        var random = new Random();
        var activationCode = random.Next(100000, 999999).ToString();

        // Kodu önbellekte sakla (24 saat süreyle)
        var cacheKey = $"email_activation_code_{userId}";
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        };

        // Kod oluşturma zamanını da sakla
        var codeData = new
        {
            Code = activationCode,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            AttemptCount = 0,
            EmailStatus = "pending" // Yeni: E-posta durumunu izle
        };

        // JSON olarak serialize et ve önbelleğe al
        string codeJson = JsonSerializer.Serialize(codeData);
        await _cache.SetStringAsync(cacheKey, codeJson, options);
    
        _logger.LogInformation("Aktivasyon kodu oluşturuldu: UserId={UserId}, Kod={Code}", userId, activationCode);
    
        return activationCode;
    }
    
    public async Task<bool> VerifyActivationCodeAsync(string userId, string code)
{
    var cacheKey = $"email_activation_code_{userId}";
    var storedCodeData = await _cache.GetStringAsync(cacheKey);

    _logger.LogInformation("Verifying activation code: UserId={UserId}, InputCode={Code}, HasCachedCode={HasCode}", 
                          userId, code, !string.IsNullOrEmpty(storedCodeData));

    if (string.IsNullOrEmpty(storedCodeData))
    {
        _logger.LogWarning("Aktivasyon kodu bulunamadı: {UserId}", userId);
        return false;
    }

    try
    {
        // JSON verisini parse et
        var codeInfo = JsonSerializer.Deserialize<JsonElement>(storedCodeData);
        var storedCode = codeInfo.GetProperty("Code").GetString();

        _logger.LogInformation("Comparing codes: Input={InputCode}, Stored={StoredCode}", code, storedCode);

        if (storedCode != code)
        {
            _logger.LogWarning("Geçersiz aktivasyon kodu denemesi: {UserId}", userId);
            return false;
        }

        // Kod doğru, kullanıcıyı güncelle
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Kullanıcı bulunamadı: {UserId}", userId);
            return false;
        }

        // Kullanıcıyı güncelle
        user.EmailConfirmed = true;
        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            // Kodu önbellekten kaldır
            await _cache.RemoveAsync(cacheKey);
            _logger.LogInformation("E-posta başarıyla doğrulandı: {UserId}, {Email}", userId, user.Email);
            return true;
        }
        else
        {
            _logger.LogError("Kullanıcı güncellenemedi: {UserId}, {Errors}", userId,
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return false;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "E-posta doğrulama hatası: {UserId}", userId);
        return false;
    }
}
    public async Task<string> GenerateActivationUrlAsync(string userId, string email)
    {
        // Client URL'ini yapılandırmadan al
        var clientUrl = _configuration["AngularClientUrl"]?.TrimEnd('/') ?? "http://localhost:4200";

        // Hem token hem de userId/email parametrelerini ekle
        var token = await GenerateSecureActivationTokenAsync(userId, email);

        // Aktivasyon URL'sini oluştur (hem token hem de yedek parametrelerle)
        return
            $"{clientUrl}/activation-code?token={token}&userId={Uri.EscapeDataString(userId)}&email={Uri.EscapeDataString(email)}";
    }
}