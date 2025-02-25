using System.Text.Json;
using Application.Abstraction.Helpers;
using Application.Abstraction.Services;
using Application.Dtos.Token;
using Application.Exceptions;
using Application.Tokens;
using Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Persistence.Services;

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenHandler _tokenHandler;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IUserService _userService;
    private readonly IMailService _mailService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(
        IConfiguration configuration,
        UserManager<AppUser> userManager,
        ITokenHandler tokenHandler,
        SignInManager<AppUser> signInManager,
        IUserService userService,
        IMailService mailService,
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _userManager = userManager;
        _tokenHandler = tokenHandler;
        _signInManager = signInManager;
        _userService = userService;
        _mailService = mailService;
        _httpContextAccessor = httpContextAccessor;
    }

    // Updated to use async token creation
    async Task<Token> CreateUserExternalLoginAsync(AppUser? user, string email, string name, int accessTokenLifetime, UserLoginInfo info)
    {
        bool result = user != null;
        if (user == null)
        {
            user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = email,
                    UserName = email,
                    NameSurname = name,
                };
                IdentityResult identityResult = await _userManager.CreateAsync(user);
                result = identityResult.Succeeded;
            }
        }

        if (result)
        {
            await _userManager.AddLoginAsync(user, info);

            // Now using async token creation
            Token token = await _tokenHandler.CreateAccessTokenAsync(accessTokenLifetime, user);
            await _userService.UpdateRefreshTokenAsync(token.RefreshToken, user, token.Expiration, refreshTokenLifetime: 5);
            return token;
        }
        throw new Exception("Invalid external authentication.");
    }

    public async Task<Token> LoginAsync(string userNameOrEmail, string password, int accessTokenLifetime)
    {
        var user = await _userManager.FindByNameAsync(userNameOrEmail);
        if (user == null)
            user = await _userManager.FindByEmailAsync(userNameOrEmail);
        if (user == null)
            throw new NotFoundUserExceptions();

        var result = await _signInManager.PasswordSignInAsync(user, password, false, false);
        if (result.Succeeded)
        {
            // Using async token creation
            Token token = await _tokenHandler.CreateAccessTokenAsync(accessTokenLifetime, user);
            await _userService.UpdateRefreshTokenAsync(token.RefreshToken, user, token.Expiration, refreshTokenLifetime: 12000);

            // Setting HTTP-only cookie for enhanced security
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = token.Expiration,
                SameSite = SameSiteMode.Strict  // Adding SameSite protection
            };

            _httpContextAccessor.HttpContext?.Response.Cookies.Append("access_token", token.AccessToken, cookieOptions);
            return token;
        }
        throw new AuthenticationErrorException();
    }

    public async Task<AppUser?> LogoutAsync()
    {
        AppUser? user = await _userManager.GetUserAsync(_httpContextAccessor.HttpContext?.User);
        if (user != null)
        {
            user.RefreshToken = null;
            user.RefreshTokenEndDateTime = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            
            // Also remove the access token cookie
            if (_httpContextAccessor.HttpContext != null)
            {
                _httpContextAccessor.HttpContext.Response.Cookies.Delete("access_token");
            }
        }
        return null;
    }

    public async Task<Token> RefreshTokenLoginAsync(string refreshToken)
    {
        AppUser? user = await _userManager.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
        if (user != null && user.RefreshTokenEndDateTime > DateTime.UtcNow)
        {
            // Using async token creation
            Token token = await _tokenHandler.CreateAccessTokenAsync(8000, user);
            await _userService.UpdateRefreshTokenAsync(token.RefreshToken, user, token.Expiration, refreshTokenLifetime: 12000);
            return token;
        }
        throw new AuthenticationErrorException();
    }

    // Password reset methods remain largely unchanged as they don't interact with token handling
    public async Task PasswordResetAsync(string email)
    {
        AppUser? user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
            string resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            resetToken = resetToken.UrlEncode();
            await _mailService.SendPasswordResetEmailAsync(user.Email, user.Id, resetToken);
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
}