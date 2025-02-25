using Application.Abstraction.Services;
using Application.Dtos.Token;
using MediatR;

namespace Application.Features.Users.Commands.RefreshTokenLogin;

public class RefreshTokenLoginRequest: IRequest<RefreshTokenLoginResponse>
{
    public string RefreshToken { get; set; }
    
    public class RefreshTokenLoginHandler : IRequestHandler<RefreshTokenLoginRequest, RefreshTokenLoginResponse>
    {
        readonly IAuthService _authService;

        public RefreshTokenLoginHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<RefreshTokenLoginResponse> Handle(RefreshTokenLoginRequest request, CancellationToken cancellationToken)
        {
            Token token = await _authService.RefreshTokenLoginAsync(request.RefreshToken);
            return new RefreshTokenLoginResponse
            {
                Token = token
            };
        }
    }
}