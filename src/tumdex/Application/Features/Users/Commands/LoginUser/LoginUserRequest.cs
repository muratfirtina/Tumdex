using Application.Abstraction.Services;
using MediatR;

namespace Application.Features.Users.Commands.LoginUser;

public class LoginUserRequest: IRequest<LoginUserResponse>
{
    public string UsernameOrEmail { get; set; }
    public string Password { get; set; }
    

    public class LoginUserCommandHandler : IRequestHandler<LoginUserRequest, LoginUserResponse>
    {
        private readonly IAuthService _authService;

        public LoginUserCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<LoginUserResponse> Handle(LoginUserRequest request, CancellationToken cancellationToken)
        {
            var token = await _authService.LoginAsync(request.UsernameOrEmail, request.Password, 900);
            return new LoginUserSuccessResponse() { Token = token };
        }
    }
}