using Application.Dtos.Token;
using Application.Tokens;
using MediatR;

namespace Application.Features.Users.Commands.RefreshTokenLogin;

public class RefreshTokenLoginRequest: IRequest<RefreshTokenLoginResponse>
{
    public string RefreshToken { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    
    public class RefreshTokenLoginHandler : IRequestHandler<RefreshTokenLoginRequest, RefreshTokenLoginResponse>
    {
        private readonly ITokenHandler _tokenHandler;

        public RefreshTokenLoginHandler(ITokenHandler tokenHandler)
        {
            _tokenHandler = tokenHandler;
        }

        public async Task<RefreshTokenLoginResponse> Handle(RefreshTokenLoginRequest request, CancellationToken cancellationToken)
        {
            var tokenDto = await _tokenHandler.RefreshAccessTokenAsync(
                request.RefreshToken,
                request.IpAddress,
                request.UserAgent);
                
            // TokenDto'yu Token'a dönüştür
            var token = new Token
            {
                AccessToken = tokenDto.AccessToken,
                RefreshToken = tokenDto.RefreshToken,
                Expiration = tokenDto.AccessTokenExpiration,
                UserId = tokenDto.UserId // TokenDto'dan UserId alınıyor
            };
                
            return new RefreshTokenLoginResponse
            {
                Token = token
            };
        }
    }
}