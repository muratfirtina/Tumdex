using Application.Abstraction.Services;
using MediatR;

namespace Application.Features.Users.Commands.VerifyResetPasswordToken;

public class VerifyResetPasswordTokenRequest : IRequest<VerifyResetPasswordTokenResponse>
{
    public string UserId { get; set; }
    public string ResetToken { get; set; }
    
    public class VerifyResetPasswordTokenCommandHandler : IRequestHandler<VerifyResetPasswordTokenRequest, VerifyResetPasswordTokenResponse>
    {
        readonly IAuthService _authService;

        public VerifyResetPasswordTokenCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<VerifyResetPasswordTokenResponse> Handle(VerifyResetPasswordTokenRequest request, CancellationToken cancellationToken)
        {
            bool state = await _authService.VerifyResetPasswordTokenAsync(request.UserId, request.ResetToken);
            return new VerifyResetPasswordTokenResponse()
            {
                State = state
            };
        }
    }
}