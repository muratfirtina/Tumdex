using Application.Dtos.Token;
using Application.Features.Users.Commands.LoginUser;
using Domain.Identity;

namespace Application.Abstraction.Services.Authentication;

public interface IInternalAuthentication
{
    // Login methods with enhanced return type
    Task<(Token? token, LoginUserErrorResponse? error)> LoginAsync(string userNameOrEmail, string password, int accessTokenLifetime = 15);
    Task<(Token? token, LoginUserErrorResponse? error)> LoginAsync(string userNameOrEmail, string password, int accessTokenLifetime, string? ipAddress, string? userAgent);
    
    // Logout methods
    Task<AppUser?> LogoutAsync();
    
    // Refresh token methods remain the same
    /*Task<Token> RefreshTokenLoginAsync(string refreshToken);
    Task<Token> RefreshTokenLoginAsync(string refreshToken, string? ipAddress, string? userAgent);*/
}