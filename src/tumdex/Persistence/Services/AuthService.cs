using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Application.Abstraction.Helpers;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Authentication;
using Application.Dtos.Token;
using Application.Exceptions;
using Application.Features.Users.Commands.ActivationCode.ActivationUrlToken;
using Application.Features.Users.Commands.CreateUser;
using Application.Tokens;
using Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Persistence.Services;

public class AuthService : IAuthService, IInternalAuthentication
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenHandler _tokenHandler;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IUserService _userService;
    private readonly IEmailQueueService _emailQueueService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IAccountEmailService _accountEmailService;
    private readonly ICacheService _cacheService;
    private readonly IRateLimitService _rateLimitService;
    private readonly IConfiguration _configuration;
    private readonly IMetricsService _metricsService;
    private readonly IAlertService _alertService;
    private readonly IEncryptionService _encryptionService;
    private readonly IBackgroundTaskQueue _backgroundTaskQueue;
    private readonly IDistributedCache _cache;

    // Configuration parameters for login security
    private readonly int _maxFailedAttempts;
    private readonly int _lockoutMinutes;
    private const int DEFAULT_ACCESS_TOKEN_LIFETIME = 15; // minutes

    public AuthService(
        UserManager<AppUser> userManager,
        ITokenHandler tokenHandler,
        SignInManager<AppUser> signInManager,
        IUserService userService,
        IEmailQueueService emailQueueService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuthService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IAccountEmailService accountEmailService,
        ICacheService cacheService,
        IRateLimitService rateLimitService,
        IConfiguration configuration,
        IMetricsService metricsService,
        IAlertService alertService,
        IEncryptionService encryptionService,
        IBackgroundTaskQueue backgroundTaskQueue, IDistributedCache cache)
    {
        _userManager = userManager;
        _tokenHandler = tokenHandler;
        _signInManager = signInManager;
        _userService = userService;
        _emailQueueService = emailQueueService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _accountEmailService = accountEmailService;
        _cacheService = cacheService;
        _rateLimitService = rateLimitService;
        _configuration = configuration;
        _metricsService = metricsService;
        _alertService = alertService;
        _encryptionService = encryptionService;
        _backgroundTaskQueue = backgroundTaskQueue;
        _cache = cache;

        // Load configuration for login security
        _maxFailedAttempts = configuration.GetValue<int>("Security:Login:MaxFailedAttempts", 5);
        _lockoutMinutes = configuration.GetValue<int>("Security:Login:LockoutMinutes", 15);
    }

    #region Authentication Methods

    // Implements the overload with IP address and user agent
    public async Task<Token> LoginAsync(string userNameOrEmail, string password, int accessTokenLifetime,
        string? ipAddress, string? userAgent)
    {
        // Check IP-based rate limiting first
        if (!string.IsNullOrEmpty(ipAddress))
        {
            string rateLimitKey = $"login_rate_limit_{ipAddress}";
            var (isAllowed, currentCount, retryAfter) = await _rateLimitService.CheckRateLimitAsync(rateLimitKey);

            if (!isAllowed)
            {
                _logger.LogWarning("Login attempt rate limit exceeded for IP: {IpAddress}", ipAddress);

                throw new AuthenticationErrorException(
                    $"Çok fazla giriş denemesi. Lütfen {retryAfter?.TotalMinutes:0} dakika sonra tekrar deneyin.");
            }
        }

        // Try to find the user
        var user = await _userManager.FindByNameAsync(userNameOrEmail);
        if (user == null)
            user = await _userManager.FindByEmailAsync(userNameOrEmail);

        if (user == null)
        {
            // Log failed attempt for non-existent user
            _logger.LogInformation("Login attempt for non-existent user: {Username} from IP: {IpAddress}",
                userNameOrEmail, ipAddress);

            // Add slight delay to prevent username enumeration timing attacks
            await Task.Delay(TimeSpan.FromMilliseconds(new Random().Next(200, 500)));

            throw new AuthenticationErrorException("Geçersiz kullanıcı adı/e-posta veya şifre");
        }

        // Check for user account lockout (using ASP.NET Identity)
        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Attempted login to locked account: {Username} from IP: {IpAddress}",
                user.UserName, ipAddress);

            var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
            var timeRemaining = lockoutEnd.HasValue
                ? (lockoutEnd.Value - DateTimeOffset.UtcNow).TotalMinutes
                : _lockoutMinutes;

            throw new AuthenticationErrorException($"Hesabınız {timeRemaining:0} dakika süreyle kilitlendi. " +
                                                   "Daha sonra tekrar deneyin veya şifrenizi sıfırlayın.");
        }

        // Check for suspicious login patterns
        await CheckForSuspiciousActivityAsync(user, ipAddress, userAgent);

        // Attempt login
        var result = await _signInManager.PasswordSignInAsync(user, password, false, true);

        if (!result.Succeeded)
        {
            // Handle failed login attempt
            await HandleFailedLoginAttemptAsync(user, ipAddress, userAgent);

            // Check if this failure resulted in an account lockout
            if (result.IsLockedOut)
            {
                _logger.LogWarning("Account locked after failed login: {Username} from IP: {IpAddress}",
                    user.UserName, ipAddress);

                throw new AuthenticationErrorException($"Hesabınız {_lockoutMinutes} dakika süreyle kilitlendi. " +
                                                       "Daha sonra tekrar deneyin veya şifrenizi sıfırlayın.");
            }

            throw new AuthenticationErrorException("Geçersiz kullanıcı adı/e-posta veya şifre");
        }

        // Handle successful login
        await HandleSuccessfulLoginAsync(user, ipAddress, userAgent);

        HttpContext? context = _httpContextAccessor.HttpContext;

        // Use IP address and UserAgent if provided
        if (!string.IsNullOrEmpty(ipAddress) || !string.IsNullOrEmpty(userAgent))
        {
            var httpContext = new DefaultHttpContext();

            if (!string.IsNullOrEmpty(ipAddress))
            {
                try
                {
                    httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ipAddress);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Invalid IP address format: {IpAddress}", ipAddress);
                }
            }

            if (!string.IsNullOrEmpty(userAgent))
            {
                httpContext.Request.Headers["User-Agent"] = userAgent;
            }

            context = httpContext;
        }

        // Create token using TokenHandler
        TokenDto tokenDto = await _tokenHandler.CreateAccessTokenAsync(user, context, accessTokenLifetime);

        // Set HTTP-only cookie if we have access to the original HTTP context
        if (_httpContextAccessor.HttpContext != null)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = tokenDto.AccessTokenExpiration,
                SameSite = SameSiteMode.Strict
            };

            _httpContextAccessor.HttpContext.Response.Cookies.Append("access_token", tokenDto.AccessToken,
                cookieOptions);
        }

        // Convert TokenDto to Token and return
        return tokenDto.ToToken();
    }

    private async Task CheckForSuspiciousActivityAsync(AppUser user, string? ipAddress, string? userAgent)
    {
        if (string.IsNullOrEmpty(ipAddress)) return;

        try
        {
            // Get the known IPs for this user from cache
            var key = $"known_ips_{user.Id}";
            var result = await _cacheService.TryGetValueAsync<HashSet<string>>(key);

            if (result.success)
            {
                var knownIps = result.value;

                // If this is a new IP for this user, log it
                if (!knownIps.Contains(ipAddress))
                {
                    _logger.LogInformation("New login location for user: {Username} from IP: {IpAddress}",
                        user.UserName, ipAddress);

                    // Add the new IP to the known IPs set
                    knownIps.Add(ipAddress);
                    await _cacheService.SetAsync(key, knownIps, TimeSpan.FromDays(30));
                }
            }
            else
            {
                // First login we're tracking, initialize the known IPs set
                var knownIps = new HashSet<string> { ipAddress };
                await _cacheService.SetAsync(key, knownIps, TimeSpan.FromDays(30));
            }
        }
        catch (Exception ex)
        {
            // Non-critical error, so just log it
            _logger.LogError(ex, "Error checking for suspicious activity for user: {Username}", user.UserName);
        }
    }

    private async Task HandleFailedLoginAttemptAsync(AppUser user, string? ipAddress, string? userAgent)
    {
        _logger.LogWarning("Failed login attempt for user: {Username} from IP: {IpAddress}",
            user.UserName, ipAddress);

        _metricsService.IncrementCounter("login_attempts", "failed");

        // Increment the failed login counter in ASP.NET Identity
        await _userManager.AccessFailedAsync(user);

        // Track per-IP failed attempts in Redis for rate limiting
        if (!string.IsNullOrEmpty(ipAddress))
        {
            string failedAttemptsKey = $"failed_login_{ipAddress}";

            // Increment the failed attempts counter
            await _cacheService.IncrementAsync(failedAttemptsKey, 1, TimeSpan.FromMinutes(60));

            // Get the current count
            int failedAttempts = await _cacheService.GetCounterAsync(failedAttemptsKey);

            // If we exceed a threshold, send an alert
            if (failedAttempts >= 10)
            {
                await _alertService.SendAlertAsync(
                    Application.Enums.AlertType.Security,
                    $"Multiple failed login attempts for IP {ipAddress}",
                    new Dictionary<string, string>
                    {
                        ["ipAddress"] = ipAddress,
                        ["userAgent"] = userAgent ?? "Unknown",
                        ["attemptCount"] = failedAttempts.ToString(),
                        ["username"] = user.UserName
                    });
            }
        }
    }

    private async Task HandleSuccessfulLoginAsync(AppUser user, string? ipAddress, string? userAgent)
    {
        _logger.LogInformation("Successful login for user: {Username} from IP: {IpAddress}",
            user.UserName, ipAddress);

        // Reset the failed access count for the user
        await _userManager.ResetAccessFailedCountAsync(user);

        // Clear any IP-based failed attempts for this user (optional)
        if (!string.IsNullOrEmpty(ipAddress))
        {
            string failedAttemptsKey = $"failed_login_{ipAddress}";
            await _cacheService.RemoveAsync(failedAttemptsKey);
        }

        // Update the last login information
        user.LastLoginDate = DateTime.UtcNow;
        user.LastLoginIp = ipAddress;
        user.LastLoginUserAgent = userAgent;
        user.FailedLoginAttempts = 0;
        await _userManager.UpdateAsync(user);
    }

    // Simplified overload for when IP and user agent aren't specified
    public async Task<Token> LoginAsync(string userNameOrEmail, string password,
        int accessTokenLifetime = DEFAULT_ACCESS_TOKEN_LIFETIME)
    {
        // Get current context information
        string? ipAddress = null;
        string? userAgent = null;

        if (_httpContextAccessor.HttpContext != null)
        {
            ipAddress = GetIpAddress(_httpContextAccessor.HttpContext);
            userAgent = _httpContextAccessor.HttpContext.Request.Headers["User-Agent"].ToString();
        }

        // Use the primary implementation
        return await LoginAsync(userNameOrEmail, password, accessTokenLifetime, ipAddress, userAgent);
    }

    public async Task<AppUser?> LogoutAsync()
    {
        AppUser? user = await _userManager.GetUserAsync(_httpContextAccessor.HttpContext?.User);
        if (user != null && _httpContextAccessor.HttpContext != null)
        {
            // Revoke refresh token
            string? refreshToken = user.RefreshToken;
            if (!string.IsNullOrEmpty(refreshToken))
            {
                string ipAddress = GetIpAddress(_httpContextAccessor.HttpContext);
                await _tokenHandler.RevokeRefreshTokenAsync(refreshToken, ipAddress, "User logout");
            }

            // Remove access token cookie
            _httpContextAccessor.HttpContext.Response.Cookies.Delete("access_token");
        }

        return user;
    }

    // Simple refresh token method
    public async Task<Token> RefreshTokenLoginAsync(string refreshToken)
    {
        return await RefreshTokenLoginAsync(refreshToken, null, null);
    }

    // Extended refresh token method with IP address and user agent
    public async Task<Token> RefreshTokenLoginAsync(string refreshToken, string? ipAddress, string? userAgent)
    {
        // Get IP address and UserAgent if not provided but HttpContext is available
        if (string.IsNullOrEmpty(ipAddress) && _httpContextAccessor.HttpContext != null)
        {
            ipAddress = GetIpAddress(_httpContextAccessor.HttpContext);
        }

        if (string.IsNullOrEmpty(userAgent) && _httpContextAccessor.HttpContext != null)
        {
            userAgent = _httpContextAccessor.HttpContext.Request.Headers["User-Agent"].ToString();
        }

        // Default values for missing information
        ipAddress ??= "0.0.0.0";
        userAgent ??= "Unknown";

        // Delegate token refresh to TokenHandler
        TokenDto tokenDto = await _tokenHandler.RefreshAccessTokenAsync(refreshToken, ipAddress, userAgent);

        // Set HTTP-only cookie if we have access to the original HTTP context
        if (_httpContextAccessor.HttpContext != null)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = tokenDto.AccessTokenExpiration,
                SameSite = SameSiteMode.Strict
            };

            _httpContextAccessor.HttpContext.Response.Cookies.Append("access_token", tokenDto.AccessToken,
                cookieOptions);
        }

        // Convert TokenDto to Token and return
        return tokenDto.ToToken();
    }

    #endregion

    #region User Registration and Management

    // Password reset methods
    public async Task PasswordResetAsync(string email)
    {
        AppUser? user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
            // Generate token for password reset
            string resetToken = await GenerateSecureTokenAsync(user.Id, user.Email, "PasswordReset");

            // Queue password reset email in background
            _backgroundTaskQueue.QueueBackgroundWorkItem(async cancellationToken =>
            {
                try
                {
                    await _accountEmailService.SendPasswordResetEmailAsync(user.Email, user.Id, resetToken);
                    _logger.LogInformation("Password reset email sent to {Email}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
                }
            });
        }
        else
        {
            // Add delay to prevent user enumeration
            await Task.Delay(TimeSpan.FromMilliseconds(new Random().Next(200, 500)));
            _logger.LogInformation("Password reset requested for non-existent email: {Email}", email);
        }
    }

    public async Task<bool> VerifyResetPasswordTokenAsync(string userId, string resetToken)
    {
        return await VerifySecureTokenAsync(userId, resetToken, "PasswordReset");
    }

    // User registration
    public async Task<(IdentityResult result, AppUser user)> RegisterUserAsync(CreateUserCommand model)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = model.UserName,
            Email = model.Email,
            NameSurname = model.NameSurname,
            EmailConfirmed = false, // Email confirmation required
            IsActive = true, // User is active by default
            CreatedDate = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            // Add user to "User" role
            await _userManager.AddToRoleAsync(user, "User");

            // Generate activation code
            var activationCode = await GenerateActivationCodeAsync(user.Id);

            // Queue activation email asynchronously
            _backgroundTaskQueue.QueueBackgroundWorkItem(async token =>
            {
                // Yeni: Her background task için yeni bir scope oluştur
                using var scope = _serviceScopeFactory.CreateScope();

                try
                {
                    var emailService = scope.ServiceProvider.GetRequiredService<IAccountEmailService>();
                    await emailService.SendEmailActivationCodeAsync(user.Email, user.Id, activationCode);
                    _logger.LogInformation("Activation email sent to {Email}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send activation email to {Email}", user.Email);
                }
            });
        }

        return (result, user);
    }

    // Email confirmation endpoint
    public async Task<IdentityResult> ConfirmEmailAsync(string userId, string token)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new NotFoundUserExceptions();

        // Decode token and verify
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decode email confirmation token for user {UserId}", userId);
            return IdentityResult.Failed(new IdentityError
            {
                Code = "InvalidToken",
                Description = "The confirmation token is invalid."
            });
        }

        return await _userManager.ConfirmEmailAsync(user, token);
    }

    // Resend for confirmation-link email
    public async Task ResendConfirmationEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user != null && !user.EmailConfirmed)
        {
            _backgroundTaskQueue.QueueBackgroundWorkItem(async token =>
            {
                try
                {
                    string confirmationToken = await GenerateSecureTokenAsync(user.Id, user.Email, "EmailConfirmation");
                    await _accountEmailService.SendEmailConfirmationAsync(user.Email, user.Id, confirmationToken);
                    _logger.LogInformation("Confirmation email resent to {Email}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to resend confirmation email to {Email}", user.Email);
                }
            });
        }
    }

    public async Task<AppUser> GetUserByEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
            throw new NotFoundUserExceptions();

        return user;
    }

    #endregion

    #region Helper Methods

    // Helper method to get IP address
    private string GetIpAddress(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.ContainsKey("X-Forwarded-For"))
        {
            return httpContext.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
    }

    #endregion

    #region Token Generation and Verification Methods

    /// <summary>
    /// Generates a secure token for user authentication or verification purposes
    /// </summary>
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

    /// <summary>
    /// Verifies a secure token for the specified purpose
    /// </summary>
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

    /// <summary>
    /// Generates a secure activation token for a user
    /// </summary>
    public async Task<string> GenerateSecureActivationTokenAsync(string userId, string email)
    {
        return await GenerateSecureTokenAsync(userId, email, "activation", 24);
    }

    /// <summary>
    /// Generates a secure activation URL for email verification
    /// </summary>
    /*public async Task<string> GenerateActivationUrlAsync(string userId, string email)
    {
        // Client URL'ini yapılandırmadan al
        var clientUrl = "http://localhost:4200";

        // Token artık URL'de taşınmayacak, sunucuda doğrulanacak
        // Bunun yerine userId ve email parametrelerini güvenli bir şekilde ileteceğiz

        // Kullanıcı ve e-posta bilgisini URL'de taşı
        return $"{clientUrl}/activation-code?userId={Uri.EscapeDataString(userId)}&email={Uri.EscapeDataString(email)}";
    }*/
    public async Task<string> GenerateActivationUrlAsync(string userId, string email)
    {
        // Client URL'ini yapılandırmadan al
        var clientUrl ="http://localhost:4200";

        // Hem token hem de userId/email parametrelerini ekle
        var token = await GenerateSecureActivationTokenAsync(userId, email);
    
        // Aktivasyon URL'sini oluştur (hem token hem de yedek parametrelerle)
        return $"{clientUrl}/activation-code?token={token}&userId={Uri.EscapeDataString(userId)}&email={Uri.EscapeDataString(email)}";
    }

    #endregion

    #region Email Activation Methods

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

        // Kod oluşturma zamanını da sakla (hız sınırlama ve yeniden oluşturma için)
        var codeData = new
        {
            Code = activationCode,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            AttemptCount = 0
        };

        // JSON olarak serialize et ve önbelleğe al
        string codeJson = JsonSerializer.Serialize(codeData);
        await _cache.SetStringAsync(cacheKey, codeJson, options);

        return activationCode;
    }

    /// <summary>
    /// Verifies an email activation code (direct alias for VerifyActivationCodeAsync)
    /// </summary>
    public async Task<bool> VerifyActivationCodeAsync(string userId, string code)
        {
            var cacheKey = $"email_activation_code_{userId}";
            var storedCodeData = await _cache.GetStringAsync(cacheKey);

            // Logger'ı _userManager aracılığıyla kullanmak için
            var loggerFactory = _httpContextAccessor.HttpContext?
                .RequestServices.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            var logger = loggerFactory?.CreateLogger<UserService>();

            if (string.IsNullOrEmpty(storedCodeData))
            {
                logger?.LogWarning("Aktivasyon kodu bulunamadı: {UserId}", userId);
                return false;
            }

            try
            {
                // JSON verisini parse et
                var codeInfo = JsonSerializer.Deserialize<JsonElement>(storedCodeData);
                var storedCode = codeInfo.GetProperty("Code").GetString();
            
                if (storedCode != code)
                {
                    logger?.LogWarning("Geçersiz aktivasyon kodu denemesi: {UserId}", userId);
                    return false;
                }

                // Kod doğru, kullanıcıyı güncelle
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    logger?.LogWarning("Kullanıcı bulunamadı: {UserId}", userId);
                    return false;
                }

                // Kullanıcıyı güncelle
                user.EmailConfirmed = true;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    // Kodu önbellekten kaldır
                    await _cache.RemoveAsync(cacheKey);
                    logger?.LogInformation("E-posta başarıyla doğrulandı: {UserId}, {Email}", userId, user.Email);
                    return true;
                }
                else
                {
                    logger?.LogError("Kullanıcı güncellenemedi: {UserId}, {Errors}", userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "E-posta doğrulama hatası: {UserId}", userId);
                return false;
            }
        }

    /// <summary>
    /// Resends activation email to a user
    /// </summary>
    public async Task ResendActivationEmailAsync(string email, string activationCode)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
            _logger.LogInformation("Resending activation email to {Email}", email);

            // Queue the email in the background
            _backgroundTaskQueue.QueueBackgroundWorkItem(async token =>
            {
                try
                {
                    await _accountEmailService.SendEmailActivationCodeAsync(email, user.Id, activationCode);
                    _logger.LogInformation("Activation email successfully queued for {Email}", email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send activation email to {Email}", email);
                }
            });
        }
        else
        {
            _logger.LogWarning("Attempted to resend activation email to non-existent user: {Email}", email);
        }
    }

    #endregion
}