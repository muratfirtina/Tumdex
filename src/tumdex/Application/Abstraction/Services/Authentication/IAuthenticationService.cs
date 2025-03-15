using Application.Dtos.Token;
using Domain.Identity;

namespace Application.Abstraction.Services.Authentication;

public interface IAuthenticationService
{
    Task<Token> LoginAsync(string userNameOrEmail, string password, int accessTokenLifetime = 15);
    Task<Token> LoginAsync(string userNameOrEmail, string password, int accessTokenLifetime, string? ipAddress, string? userAgent);
    Task<AppUser?> LogoutAsync();
    Task<Token> RefreshTokenLoginAsync(string refreshToken);
    Task<Token> RefreshTokenLoginAsync(string refreshToken, string? ipAddress, string? userAgent);
}