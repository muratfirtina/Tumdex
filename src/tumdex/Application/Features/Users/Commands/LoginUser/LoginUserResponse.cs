using Application.Dtos.Token;
using Application.Enums;
using Core.Application.Responses;

namespace Application.Features.Users.Commands.LoginUser;

public class LoginUserResponse : IResponse
{
}

public class LoginUserSuccessResponse : LoginUserResponse
{
    public Token? Token { get; set; }
    public string? UserName { get; set; }
}

public class LoginUserErrorResponse : LoginUserResponse
{
    public string? Message { get; set; }
    public int FailedAttempts { get; set; }
    public bool IsLockedOut { get; set; }
    public int? LockoutSeconds { get; set; }
    
    // Login denemesinin durumunu belirten bir enum
    public LoginErrorType ErrorType { get; set; } = LoginErrorType.InvalidCredentials;
}
