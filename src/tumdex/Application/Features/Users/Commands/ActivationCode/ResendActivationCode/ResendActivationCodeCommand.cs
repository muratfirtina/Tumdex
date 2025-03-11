using Application.Abstraction.Services;
using MediatR;

namespace Application.Features.Users.Commands.ActivationCode.ResendActivationCode;

public class ResendActivationCodeCommand : IRequest<ResendActivationCodeResponse>
{
    public string Email { get; set; }
    
    
    public class ResendActivationCodeCommandHandler : IRequestHandler<ResendActivationCodeCommand, ResendActivationCodeResponse>
    {
        private readonly IAuthService _authService;
        private readonly IUserService _userService;

        public ResendActivationCodeCommandHandler(IAuthService authService, IUserService userService)
        {
            _authService = authService;
            _userService = userService;
        }

        public async Task<ResendActivationCodeResponse> Handle(ResendActivationCodeCommand request, CancellationToken cancellationToken)
        {
            var user = await _userService.GetUserByEmailAsync(request.Email);
            
            if (user != null)
            {
                var activationCode = await _authService.GenerateActivationCodeAsync(user.Id);
                await _authService.ResendActivationEmailAsync(request.Email, activationCode);
                
                return new ResendActivationCodeResponse
                {
                    Success = true
                };
            }
            
            // Güvenlik nedeniyle her durumda başarılı dönüyoruz
            return new ResendActivationCodeResponse
            {
                Success = true
            };
        }
    }
}