using Core.Application.Responses;

namespace Application.Features.Roles.Queries.GetRoleById;

public class GetRoleByIdQueryResponse :IResponse
{
    public string RoleId { get; set; }
    public string? RoleName { get; set; }
    //public string? Message { get; set; }
}