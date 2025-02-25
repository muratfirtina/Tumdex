using Application.Abstraction.Services.Authentication;

namespace Application.Abstraction.Services;

public interface IAuthService : IExternalAuthentication, IInternalAuthentication
{
    Task PasswordResetAsync(string email);
    Task<bool> VerifyResetPasswordTokenAsync(string userId, string resetToken);
}