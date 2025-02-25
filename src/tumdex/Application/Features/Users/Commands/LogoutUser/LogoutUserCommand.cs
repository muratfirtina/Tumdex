using Application.Abstraction.Services;
using MediatR;

namespace Application.Features.Users.Commands.LogoutUser;

public class LogoutUserCommand : IRequest<bool>
{
    public class LogoutUserCommandHandler : IRequestHandler<LogoutUserCommand, bool>
    {
        private readonly IAuthService _authService;

        public LogoutUserCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<bool> Handle(LogoutUserCommand request, CancellationToken cancellationToken)
        {
            try
            {
                await _authService.LogoutAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}