using Application.Dtos.Token;
using Core.Application.Responses;

namespace Application.Features.Users.Commands.LoginUser;

public class LoginUserResponse : IResponse
{
    
    
}

public class LoginUserSuccessResponse : LoginUserResponse
{
    public Token Token { get; set; }
}
public class LoginUserErrorResponse : LoginUserResponse
{
    public string Message { get; set; }
}