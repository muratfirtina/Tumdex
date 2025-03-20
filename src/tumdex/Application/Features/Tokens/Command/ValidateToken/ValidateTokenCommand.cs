using MediatR;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstraction.Services.Tokens;
using Microsoft.Extensions.Caching.Distributed;

namespace Application.Features.Tokens.Command.ValidateToken
{
    public class ValidateTokenCommand : IRequest<ValidateTokenResponse>
    {
        public string UserId { get; set; }
        public string Token { get; set; }
        
        public class ValidateTokenCommandHandler : IRequestHandler<ValidateTokenCommand, ValidateTokenResponse>
        {
            private readonly ITokenHandler _tokenHandler;
            private readonly IDistributedCache _cache;
            
            public ValidateTokenCommandHandler(
                ITokenHandler tokenHandler,
                IDistributedCache cache)
            {
                _tokenHandler = tokenHandler;
                _cache = cache;
            }
            
            public async Task<ValidateTokenResponse> Handle(ValidateTokenCommand request, CancellationToken cancellationToken)
            {
                // Check if user is blocked
                bool isBlocked = await _tokenHandler.IsUserBlockedAsync(request.UserId);
                if (isBlocked)
                {
                    return new ValidateTokenResponse
                    {
                        IsValid = false,
                        Error = "User is blocked"
                    };
                }
                
                // Check if tokens have been revoked for this user
                string revokeKey = $"UserTokensRevoked:{request.UserId}";
                string? revokedTimeString = await _cache.GetStringAsync(revokeKey);
                
                if (!string.IsNullOrEmpty(revokedTimeString))
                {
                    var jwtTokenHandler = new JwtSecurityTokenHandler();
                    var jwtToken = jwtTokenHandler.ReadJwtToken(request.Token);
                    
                    if (System.DateTime.TryParse(revokedTimeString, out System.DateTime revokedTime) && 
                        jwtToken.IssuedAt < revokedTime)
                    {
                        return new ValidateTokenResponse
                        {
                            IsValid = false,
                            Error = "Token has been revoked"
                        };
                    }
                }
                
                // Validate the token itself
                var validationResult = await _tokenHandler.ValidateAccessTokenAsync(request.Token);
                
                return new ValidateTokenResponse
                {
                    IsValid = validationResult.isValid,
                    UserId = validationResult.userId,
                    Error = validationResult.error
                };
            }
        }
    }
}