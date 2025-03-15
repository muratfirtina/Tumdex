using Application.Features.Users.Commands.CreateUser;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Application.Abstraction.Services;

public interface IRegistrationAndPasswordService
{
    /// <summary>
    /// Registers a new user
    /// </summary>
    Task<(IdentityResult result, AppUser user)> RegisterUserAsync(CreateUserCommand model);
    
    /// <summary>
    /// Initiates a password reset for a user
    /// </summary>
    Task PasswordResetAsync(string email);
    
    /// <summary>
    /// Gets a user by their email address
    /// </summary>
    Task<AppUser> GetUserByEmailAsync(string email);
    
}