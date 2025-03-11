using System;
using System.Threading.Tasks;
using Application.Dtos.Token;
using Application.Features.Users.Commands.ActivationCode.ActivationUrlToken;
using Application.Features.Users.Commands.CreateUser;
using Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Application.Abstraction.Services;

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
    
    #region User Registration and Management
    
    /// <summary>
    /// Registers a new user
    /// </summary>
    Task<(IdentityResult result, AppUser user)> RegisterUserAsync(CreateUserCommand model);
    
    /// <summary>
    /// Initiates a password reset for a user
    /// </summary>
    Task PasswordResetAsync(string email);
    
    /// <summary>
    /// Verifies a password reset token
    /// </summary>
    Task<bool> VerifyResetPasswordTokenAsync(string userId, string resetToken);
    
    /// <summary>
    /// Confirms a user's email address
    /// </summary>
    Task<IdentityResult> ConfirmEmailAsync(string userId, string token);
    
    /// <summary>
    /// Resends a confirmation email to a user
    /// </summary>
    Task ResendConfirmationEmailAsync(string email);
    
    /// <summary>
    /// Gets a user by their email address
    /// </summary>
    Task<AppUser> GetUserByEmailAsync(string email);
    
    #endregion
    
    #region Token Generation and Verification
    
    /// <summary>
    /// Generates a secure token for user authentication or verification purposes
    /// </summary>
    Task<string> GenerateSecureTokenAsync(string userId, string email, string purpose, int expireHours = 24);
    
    /// <summary>
    /// Verifies a secure token for the specified purpose
    /// </summary>
    Task<bool> VerifySecureTokenAsync(string userId, string token, string purpose);
    /// <summary>
    /// Verifies a token and returns validation details
    /// </summary>
    Task<TokenValidationResult> VerifyActivationTokenAsync(string token, string purpose, string expectedUserId = null);
    
    /// <summary>
    /// Generates a secure activation token for a user
    /// </summary>
    Task<string> GenerateSecureActivationTokenAsync(string userId, string email);
    
    /// <summary>
    /// Generates a secure activation URL for email verification
    /// </summary>
    Task<string> GenerateActivationUrlAsync(string userId, string email);
    
    #endregion
    
    #region Email Activation Methods
    
    /// <summary>
    /// Generates an email activation code for a user
    /// </summary>
    Task<string> GenerateActivationCodeAsync(string userId);
    
    /// <summary>
    /// Resends activation email to a user
    /// </summary>
    Task ResendActivationEmailAsync(string email, string activationCode);
    
    /// <summary>
    /// Verifies an email activation code (alias for UserService method)
    /// </summary>
    Task<bool> VerifyActivationCodeAsync(string userId, string code);
    
    #endregion
}