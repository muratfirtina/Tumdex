using Application.Abstraction.Services;
using MediatR;

namespace Application.Features.Users.Commands.ActivationCode.VerifyActivationCode;

public class VerifyActivationCodeCommand : IRequest<VerifyActivationCodeResponse>
{
    public string UserId { get; set; }
    public string Code { get; set; }
    
    public class VerifyActivationCodeCommandHandler : IRequestHandler<VerifyActivationCodeCommand, VerifyActivationCodeResponse>
    {
        private readonly IAuthService _authService;

        public VerifyActivationCodeCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<VerifyActivationCodeResponse> Handle(VerifyActivationCodeCommand request, CancellationToken cancellationToken)
        {
            var result = await _authService.VerifyActivationCodeAsync(request.UserId, request.Code);
            
            return new VerifyActivationCodeResponse
            {
                Verified = result
            };
        }
    }
}