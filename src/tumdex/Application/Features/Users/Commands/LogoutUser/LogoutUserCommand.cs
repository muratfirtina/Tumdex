using Application.Abstraction.Services;
using Application.Abstraction.Services.Authentication;
using Application.Abstraction.Services.Tokens;
using MediatR;

namespace Application.Features.Users.Commands.LogoutUser;

public class LogoutUserCommand : IRequest<bool>
{
    public string? RefreshToken { get; set; }
    public string? IpAddress { get; set; }
    
    public class LogoutUserCommandHandler : IRequestHandler<LogoutUserCommand, bool>
    {
        private readonly ITokenHandler _tokenHandler;
        private readonly IAuthService _authService;


        public LogoutUserCommandHandler(
            ITokenHandler tokenHandler,
            IAuthService authService)
        {
            _tokenHandler = tokenHandler;
            _authService = authService;
        }

        public async Task<bool> Handle(LogoutUserCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // Use AuthService's LogoutAsync for complete logout process
                // This handles both token revocation and cookie removal
                var user = await _authService.LogoutAsync();
                
                // If we have a specific refresh token to revoke (from request body)
                if (!string.IsNullOrEmpty(request.RefreshToken))
                {
                    await _tokenHandler.RevokeRefreshTokenAsync(
                        request.RefreshToken,
                        request.IpAddress,
                        "User logout");
                }
                
                return user != null || !string.IsNullOrEmpty(request.RefreshToken);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}