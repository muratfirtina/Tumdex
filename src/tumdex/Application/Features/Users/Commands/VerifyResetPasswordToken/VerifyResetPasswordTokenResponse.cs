using Core.Application.Responses;

namespace Application.Features.Users.Commands.VerifyResetPasswordToken;

public class VerifyResetPasswordTokenResponse : IResponse
{
    public bool State { get; set; }
}