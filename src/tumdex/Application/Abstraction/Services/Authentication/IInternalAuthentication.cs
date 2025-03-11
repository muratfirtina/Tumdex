using Application.Dtos.Token;
using Domain.Identity;

namespace Application.Abstraction.Services.Authentication;

public interface IInternalAuthentication
{
    // Login methods with consistent signatures
    Task<Token> LoginAsync(string userNameOrEmail, string password, int accessTokenLifetime = 15);
    Task<Token> LoginAsync(string userNameOrEmail, string password, int accessTokenLifetime, string? ipAddress, string? userAgent);
    
    // Logout methods
    Task<AppUser?> LogoutAsync();
    
    // Refresh token methods with consistent signatures
    Task<Token> RefreshTokenLoginAsync(string refreshToken);
    Task<Token> RefreshTokenLoginAsync(string refreshToken, string? ipAddress, string? userAgent);
}
