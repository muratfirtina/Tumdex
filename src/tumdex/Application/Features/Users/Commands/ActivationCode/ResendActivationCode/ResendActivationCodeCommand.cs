using Application.Abstraction.Services;
using Application.Abstraction.Services.Authentication;
using Application.Abstraction.Services.Email;
using Application.Abstraction.Services.Tokens;
using MediatR;

namespace Application.Features.Users.Commands.ActivationCode.ResendActivationCode;

public class ResendActivationCodeCommand : IRequest<ResendActivationCodeResponse>
{
    public string Email { get; set; }
    
    
    public class ResendActivationCodeCommandHandler : IRequestHandler<ResendActivationCodeCommand, ResendActivationCodeResponse>
    {
        private readonly ITokenService _tokenService;
        private readonly IAccountEmailService _accountEmailService;
        private readonly IUserService _userService;

        public ResendActivationCodeCommandHandler(IAuthService authService, IUserService userService, ITokenService tokenService, IAccountEmailService accountEmailService)
        {
            _userService = userService;
            _tokenService = tokenService;
            _accountEmailService = accountEmailService;
        }

        public async Task<ResendActivationCodeResponse> Handle(ResendActivationCodeCommand request, CancellationToken cancellationToken)
        {
            var user = await _userService.GetUserByEmailAsync(request.Email);
    
            if (user != null)
            {
                var activationCode = await _tokenService.GenerateActivationCodeAsync(user.Id);
                await _accountEmailService.ResendEmailActivationCodeAsync(request.Email, activationCode);
        
                return new ResendActivationCodeResponse
                {
                    Success = true,
                    Message = "New activation code has been sent to your email address."
                };
            }
    
            // Güvenlik nedeniyle her durumda başarılı dönüyoruz
            return new ResendActivationCodeResponse
            {
                Success = true,
                Message = "New activation code has been sent to your email address."
            };
        }
    }
}