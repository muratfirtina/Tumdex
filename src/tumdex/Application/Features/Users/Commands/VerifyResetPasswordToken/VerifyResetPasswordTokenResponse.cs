using Core.Application.Responses;

namespace Application.Features.Users.Commands.VerifyResetPasswordToken;

public class VerifyResetPasswordTokenResponse : IResponse
{
    public bool TokenValid { get; set; } // Add this property
    public string UserId { get; set; }
    public string Email { get; set; }
}