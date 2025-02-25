using Core.Application.Responses;

namespace Application.Features.Users.Queries.GetCurrentUser;

public class GetCurrentUserQueryResponse : IResponse
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public string NameSurname { get; set; }
}