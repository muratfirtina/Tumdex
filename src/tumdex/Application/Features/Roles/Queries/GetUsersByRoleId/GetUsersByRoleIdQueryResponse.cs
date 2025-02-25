using Core.Application.Responses;

namespace Application.Features.Roles.Queries.GetUsersByRoleId;

public class GetUsersByRoleIdQueryResponse :IResponse
{
    public string Id { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public string NameSurname { get; set; }
}