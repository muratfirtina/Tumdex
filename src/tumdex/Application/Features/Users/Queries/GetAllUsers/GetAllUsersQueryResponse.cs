using Core.Application.Responses;

namespace Application.Features.Users.Queries.GetAllUsers;

public class GetAllUsersQueryResponse : IResponse
{
    public string? Id { get; set; }
    public string? UserName { get; set; }
    public string? NameSurname { get; set; }
    public string? Email { get; set; }
    
}