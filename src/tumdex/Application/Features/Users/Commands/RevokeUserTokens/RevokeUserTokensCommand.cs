using Application.Tokens;
using MediatR;

namespace Application.Features.Users.Commands.RevokeUserTokens;

public class RevokeUserTokensCommand : IRequest<bool>
{
    public string UserId { get; set; }
    public string AdminId { get; set; }
    public string IpAddress { get; set; }
    
    public class RevokeUserTokensCommandHandler : IRequestHandler<RevokeUserTokensCommand, bool>
    {
        private readonly ITokenHandler _tokenHandler;
        
        public RevokeUserTokensCommandHandler(ITokenHandler tokenHandler)
        {
            _tokenHandler = tokenHandler;
        }
        
        public async Task<bool> Handle(RevokeUserTokensCommand request, CancellationToken cancellationToken)
        {
            await _tokenHandler.RevokeAllUserRefreshTokensAsync(
                request.UserId,
                request.IpAddress,
                $"Admin action by {request.AdminId}");
                
            return true;
        }
    }
}