using Application.Dtos.Token;
using Core.Application.Responses;

namespace Application.Features.Users.Commands.RefreshTokenLogin;

public class RefreshTokenLoginResponse : IResponse
{
    public Token? Token { get; set; }
}