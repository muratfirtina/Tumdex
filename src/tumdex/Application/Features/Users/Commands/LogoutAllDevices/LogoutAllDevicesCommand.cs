using Application.Abstraction.Services.Tokens;
using MediatR;

namespace Application.Features.Users.Commands.LogoutAllDevices;

public class LogoutAllDevicesCommand : IRequest<bool>
{
    public string UserId { get; set; }
    public string IpAddress { get; set; }
    public string Reason { get; set; }
    
    public class LogoutAllDevicesCommandHandler : IRequestHandler<LogoutAllDevicesCommand, bool>
    {
        private readonly ITokenHandler _tokenHandler;
        
        public LogoutAllDevicesCommandHandler(ITokenHandler tokenHandler)
        {
            _tokenHandler = tokenHandler;
        }
        
        public async Task<bool> Handle(LogoutAllDevicesCommand request, CancellationToken cancellationToken)
        {
            await _tokenHandler.RevokeAllUserRefreshTokensAsync(
                request.UserId,
                request.IpAddress,
                request.Reason);
                
            return true;
        }
    }
}