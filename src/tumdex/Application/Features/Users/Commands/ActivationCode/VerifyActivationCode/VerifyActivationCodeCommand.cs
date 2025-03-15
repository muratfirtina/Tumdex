using Application.Abstraction.Services.Authentication;
using Application.Abstraction.Services.Tokens;
using MediatR;

namespace Application.Features.Users.Commands.ActivationCode.VerifyActivationCode;

public class VerifyActivationCodeCommand : IRequest<VerifyActivationCodeResponse>
{
    public string UserId { get; set; }
    public string Code { get; set; }
    
    public class VerifyActivationCodeCommandHandler : IRequestHandler<VerifyActivationCodeCommand, VerifyActivationCodeResponse>
    {
        private readonly ITokenService _tokenService;

        public VerifyActivationCodeCommandHandler(IAuthService authService, ITokenService tokenService)
        {
            _tokenService = tokenService;
        }

        public async Task<VerifyActivationCodeResponse> Handle(VerifyActivationCodeCommand request, CancellationToken cancellationToken)
        {
            var result = await _tokenService.VerifyActivationCodeAsync(request.UserId, request.Code);
            
            return new VerifyActivationCodeResponse
            {
                Verified = result
            };
        }
    }
}