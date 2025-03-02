using System.Text.Json;
using Application.Abstraction.Helpers;
using Application.Abstraction.Services;
using Application.Dtos.Token;
using Application.Exceptions;
using Application.Features.Users.Commands.CreateUser;
using Application.Tokens;
using Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Persistence.Services;

public class AuthService : IAuthService
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

    public AuthService(
        UserManager<AppUser> userManager,
        ITokenHandler tokenHandler,
        SignInManager<AppUser> signInManager,
        IUserService userService,
        IEmailQueueService emailQueueService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuthService> logger,
        IServiceScopeFactory serviceScopeFactory, 
        IAccountEmailService accountEmailService)
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
    }

    public async Task<Token> LoginAsync(string userNameOrEmail, string password, int accessTokenLifetime, string? ipAddress, string? userAgent)
{
    var user = await _userManager.FindByNameAsync(userNameOrEmail);
    if (user == null)
        user = await _userManager.FindByEmailAsync(userNameOrEmail);
    if (user == null)
        throw new NotFoundUserExceptions();

    var result = await _signInManager.PasswordSignInAsync(user, password, false, false);
    if (result.Succeeded)
    {
        HttpContext context = _httpContextAccessor.HttpContext;
        
        // IP adresi ve UserAgent bilgisini kullan (varsa)
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
        
        // Token oluşturmayı TokenHandler'a delege et
        TokenDto tokenDto = await _tokenHandler.CreateAccessTokenAsync(accessTokenLifetime, user, context);
        
        // TokenDto -> Token dönüşümü (uyumluluk için)
        Token token = new()
        {
            AccessToken = tokenDto.AccessToken,
            RefreshToken = tokenDto.RefreshToken,
            Expiration = tokenDto.AccessTokenExpiration,
            UserId = tokenDto.UserId // TokenDto'dan alınan UserId
        };
        
        // HTTP-only cookie ile token'ı ayarla (güvenlik için) - eğer orijinal HTTP context varsa
        if (_httpContextAccessor.HttpContext != null)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = token.Expiration,
                SameSite = SameSiteMode.Strict
            };

            _httpContextAccessor.HttpContext.Response.Cookies.Append("access_token", token.AccessToken, cookieOptions);
        }
        
        return token;
    }
    throw new AuthenticationErrorException();
}

    public async Task<Token> LoginAsync(string email, string password, int accessTokenLifetime)
    {
        return await LoginAsync(email, password, accessTokenLifetime, null, null);
    }

    public async Task<AppUser?> LogoutAsync()
    {
        AppUser? user = await _userManager.GetUserAsync(_httpContextAccessor.HttpContext?.User);
        if (user != null)
        {
            // Token iptal işlemini TokenHandler'a delege et
            string? refreshToken = user.RefreshToken;
            if (!string.IsNullOrEmpty(refreshToken) && _httpContextAccessor.HttpContext != null)
            {
                string ipAddress = GetIpAddress(_httpContextAccessor.HttpContext);
                await _tokenHandler.RevokeRefreshTokenAsync(refreshToken, ipAddress, "User logout");
            }
            
            // Access token cookie'yi kaldır
            if (_httpContextAccessor.HttpContext != null)
            {
                _httpContextAccessor.HttpContext.Response.Cookies.Delete("access_token");
            }
        }
        return null;
    }

    public async Task<Token> RefreshTokenLoginAsync(string refreshToken)
    {
        return await RefreshTokenLoginAsync(refreshToken, null, null);
    }

    public async Task<Token> RefreshTokenLoginAsync(string refreshToken, string? ipAddress, string? userAgent)
    {
        // IP adresi ve UserAgent bilgisini al
        if (string.IsNullOrEmpty(ipAddress) && _httpContextAccessor.HttpContext != null)
        {
            ipAddress = GetIpAddress(_httpContextAccessor.HttpContext);
        }
        
        if (string.IsNullOrEmpty(userAgent) && _httpContextAccessor.HttpContext != null)
        {
            userAgent = _httpContextAccessor.HttpContext.Request.Headers["User-Agent"].ToString();
        }
        
        if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = "0.0.0.0";
        }
        
        if (string.IsNullOrEmpty(userAgent))
        {
            userAgent = "Unknown";
        }
        
        // Token yenilemeyi TokenHandler'a delege et
        TokenDto tokenDto = await _tokenHandler.RefreshAccessTokenAsync(refreshToken, ipAddress, userAgent);
        
        // TokenDto -> Token dönüşümü (uyumluluk için)
        Token token = new()
        {
            AccessToken = tokenDto.AccessToken,
            RefreshToken = tokenDto.RefreshToken,
            Expiration = tokenDto.AccessTokenExpiration
        };
        
        return token;
    }
    
    // Password reset methods
    public async Task PasswordResetAsync(string email)
    {
        AppUser? user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
            string resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            resetToken = resetToken.UrlEncode();
            await _accountEmailService.SendPasswordResetEmailAsync(user.Email, user.Id, resetToken);
        }
    }

    public async Task<bool> VerifyResetPasswordTokenAsync(string userId, string resetToken)
    {
        AppUser? user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            resetToken = resetToken.UrlDecode();
            return await _userManager.VerifyUserTokenAsync(
                user, 
                _userManager.Options.Tokens.PasswordResetTokenProvider, 
                "ResetPassword", 
                resetToken
            );
        }
        return false;
    }
    
    // Helper method to get IP address
    private string GetIpAddress(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.ContainsKey("X-Forwarded-For"))
        {
            return httpContext.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
        }
        
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
    }
    
    // User registration
    public async Task<IdentityResult> RegisterUserAsync(CreateUserCommand model)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = model.Email,
            Email = model.Email,
            NameSurname = model.NameSurname,
            EmailConfirmed = false,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
    
        if (result.Succeeded)
        {
            // Add user to default role
            await _userManager.AddToRoleAsync(user, "User");
        
            // Queue email confirmation asynchronously
            QueueEmailConfirmation(user);
        }
    
        return result;
    }

    // Email confirmation endpoint
    public async Task<IdentityResult> ConfirmEmailAsync(string userId, string token)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new NotFoundUserExceptions();

        token = token.UrlDecode();
    
        return await _userManager.ConfirmEmailAsync(user, token);
    }

    // Resend confirmation email
    public async Task ResendConfirmationEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user != null && !user.EmailConfirmed)
        {
            QueueEmailConfirmation(user);
        }
    }

    public async Task<AppUser> GetUserByEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
            throw new NotFoundUserExceptions();
    
        return user;
    }
    
    private void QueueEmailConfirmation(AppUser user)
    {
        try
        {
            // Fire-and-forget email confirmation
            Task.Run(async () =>
            {
                try
                {
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                        var emailQueueSvc = scope.ServiceProvider.GetRequiredService<IEmailQueueService>();
                        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AuthService>>();
                        
                        // E-posta doğrulama tokeni oluştur
                        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
                        token = token.UrlEncode();
                        
                        // E-postayı kuyruğa ekle
                        await emailQueueSvc.QueueEmailConfirmationAsync(user.Email, user.Id, token);
                        
                        logger.LogInformation("Confirmation email for {Email} queued successfully", user.Email);
                    }
                }
                catch (Exception ex)
                {
                    using (var logScope = _serviceScopeFactory.CreateScope())
                    {
                        var logger = logScope.ServiceProvider.GetRequiredService<ILogger<AuthService>>();
                        logger.LogError(ex, "Failed to queue confirmation email for {Email}", user.Email);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize email confirmation task for {Email}", user.Email);
        }
    }
}