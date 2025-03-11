using Application.Dtos.Token;
using Domain.Identity;
using Microsoft.AspNetCore.Http;

namespace Application.Tokens;

public interface ITokenHandler : IDisposable
{
    // Access token creation methods
    Task<TokenDto> CreateAccessTokenAsync(AppUser user, int accessTokenLifetime = 15);
    Task<TokenDto> CreateAccessTokenAsync(AppUser user, HttpContext? httpContext = null, int accessTokenLifetime = 15);
        
    // Refresh token operations
    Task<TokenDto> RefreshAccessTokenAsync(string refreshToken, string? ipAddress = null, string? userAgent = null);
    Task RevokeRefreshTokenAsync(string refreshToken, string? ipAddress = null, string? reasonRevoked = null);
    Task RevokeAllUserRefreshTokensAsync(string userId, string? ipAddress = null, string? reasonRevoked = null);
        
    // Token validation
    Task<(bool isValid, string userId, string error)> ValidateAccessTokenAsync(string token);
    Task<(bool isValid, AppUser user, RefreshToken refreshToken, string error)> ValidateRefreshTokenAsync(
        string refreshToken, string ipAddress, string userAgent);
        
    // Helper methods
    Task InvalidateUserClaimsCache(string userId);
    Task<bool> IsUserBlockedAsync(string userId);
}