using Application.Abstraction.Services;
using Application.Tokens;
using MediatR;

namespace Application.Features.Users.Commands.LogoutUser;

public class LogoutUserCommand : IRequest<bool>
{
    public string RefreshToken { get; set; }
    public string IpAddress { get; set; }
    public class LogoutUserCommandHandler : IRequestHandler<LogoutUserCommand, bool>
    {
        private readonly ITokenHandler _tokenHandler;

        public LogoutUserCommandHandler(ITokenHandler tokenHandler)
        {
            _tokenHandler = tokenHandler;
        }

        public async Task<bool> Handle(LogoutUserCommand request, CancellationToken cancellationToken)
        {
            try
            {
                await _tokenHandler.RevokeRefreshTokenAsync(
                    request.RefreshToken,
                    request.IpAddress,
                    "User logout");
                
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}