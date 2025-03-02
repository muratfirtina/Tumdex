using Application.Abstraction.Services.Authentication;
using Application.Dtos.Token;
using Application.Features.Users.Commands.CreateUser;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Application.Abstraction.Services;

public interface IAuthService : IExternalAuthentication, IInternalAuthentication
{
    
    // Şifre sıfırlama
    Task PasswordResetAsync(string email);
    Task<bool> VerifyResetPasswordTokenAsync(string userId, string resetToken);
    
    // Kullanıcı kaydı ve yönetimi
    Task<IdentityResult> RegisterUserAsync(CreateUserCommand model);
    Task<IdentityResult> ConfirmEmailAsync(string userId, string token);
    Task ResendConfirmationEmailAsync(string email);
    Task<AppUser> GetUserByEmailAsync(string email);
}