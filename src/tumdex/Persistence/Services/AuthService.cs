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
using Application.Abstraction.Services.Email;
using Application.Abstraction.Services.Tokens;
using Application.Abstraction.Services.Utilities;
using Application.Dtos.Token;
using Application.Exceptions;
using Application.Features.Users.Commands.CreateUser;
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
}