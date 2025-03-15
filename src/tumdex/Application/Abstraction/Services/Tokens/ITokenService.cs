using Application.Features.Users.Commands.ActivationCode.ActivationUrlToken;

namespace Application.Abstraction.Services.Tokens;

public interface ITokenService
{
    Task<string> GenerateSecureTokenAsync(string userId, string email, string purpose, int expireHours = 24);
    Task<bool> VerifySecureTokenAsync(string userId, string token, string purpose);
    Task<TokenValidationResult> VerifyActivationTokenAsync(string token, string purpose, string expectedUserId = null);
    Task<string> GenerateSecureActivationTokenAsync(string userId, string email);
    Task<bool> VerifyResetPasswordTokenAsync(string userId, string resetToken);
    Task<string> GenerateActivationCodeAsync(string userId);
    Task<bool> VerifyActivationCodeAsync(string userId, string code);
    Task<string> GenerateActivationUrlAsync(string userId, string email);
}