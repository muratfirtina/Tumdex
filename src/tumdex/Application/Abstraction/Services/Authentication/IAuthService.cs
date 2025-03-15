using Application.Dtos.Token;
using Application.Features.Users.Commands.ActivationCode.ActivationUrlToken;
using Application.Features.Users.Commands.CreateUser;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Application.Abstraction.Services.Authentication;

public interface IAuthService
{
    #region Authentication Methods
    
    /// <summary>
    /// Authenticates a user and generates access and refresh tokens
    /// </summary>
    Task<Token> LoginAsync(string userNameOrEmail, string password, int accessTokenLifetime);
    
    /// <summary>
    /// Authenticates a user with additional context information
    /// </summary>
    Task<Token> LoginAsync(string userNameOrEmail, string password, int accessTokenLifetime, string? ipAddress, string? userAgent);
    
    /// <summary>
    /// Logs out the current user
    /// </summary>
    Task<AppUser?> LogoutAsync();
    
    /// <summary>
    /// Refreshes the access token using a refresh token
    /// </summary>
    Task<Token> RefreshTokenLoginAsync(string refreshToken);
    
    /// <summary>
    /// Refreshes the access token with additional context information
    /// </summary>
    Task<Token> RefreshTokenLoginAsync(string refreshToken, string? ipAddress, string? userAgent);
    
    #endregion
}