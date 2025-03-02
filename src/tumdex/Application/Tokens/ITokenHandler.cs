using Application.Dtos.Token;
using Domain.Identity;
using Microsoft.AspNetCore.Http;

namespace Application.Tokens;

public interface ITokenHandler : IDisposable
{
    // Access token oluşturma
    Task<TokenDto> CreateAccessTokenAsync(int seconds, AppUser appUser, HttpContext httpContext);
    Task<TokenDto> CreateAccessTokenAsync(AppUser user, HttpContext httpContext);
        
    // Refresh token işlemleri
    Task<TokenDto> RefreshAccessTokenAsync(string refreshToken, string? ipAddress, string? userAgent);
    Task RevokeRefreshTokenAsync(string refreshToken, string? ipAddress, string reasonRevoked);
    Task RevokeAllUserRefreshTokensAsync(string userId, string? ipAddress, string reasonRevoked);
        
    // Tokenları doğrulama
    Task<(bool isValid, string userId, string error)> ValidateAccessTokenAsync(string token);
    Task<(bool isValid, AppUser user, RefreshToken refreshToken, string error)> ValidateRefreshTokenAsync(
        string refreshToken, string ipAddress, string userAgent);
        
    // Yardımcı metotlar
    Task InvalidateUserClaimsCache(string userId);
    Task<bool> IsUserBlockedAsync(string userId);
}