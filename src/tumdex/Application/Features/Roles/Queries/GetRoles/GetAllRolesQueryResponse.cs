using Core.Application.Responses;

namespace Application.Features.Roles.Queries.GetRoles;

public class GetAllRolesQueryResponse :IResponse
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}