using Application.Dtos.Token;
using Domain.Identity;

namespace Application.Abstraction.Services.Authentication;

public interface IInternalAuthentication
{
    Task<Token> LoginAsync(string email, string password, int accessTokenLifetime, string? ipAddress , string? userAgent);
    Task<AppUser?> LogoutAsync();
    Task<Token> RefreshTokenLoginAsync(string refreshToken, string? ipAddress, string? userAgent);
}