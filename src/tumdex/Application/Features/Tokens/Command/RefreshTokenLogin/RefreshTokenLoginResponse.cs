using Application.Dtos.Token;
using Core.Application.Responses;

namespace Application.Features.Tokens.Command.RefreshTokenLogin;

public class RefreshTokenLoginResponse : IResponse
{
    public Token Token { get; set; }
}