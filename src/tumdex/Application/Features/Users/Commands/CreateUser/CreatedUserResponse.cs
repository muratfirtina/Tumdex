using Core.Application.Responses;

namespace Application.Features.Users.Commands.CreateUser;

public class CreatedUserResponse : IResponse
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; }
    public string UserId { get; set; }
    public string ActivationToken { get; set; } 
}