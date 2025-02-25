using Application.Dtos.Role;
using Core.Application.Responses;

namespace Application.Features.Users.Queries.GetRolesToUser;

public class GetRolesToUserQueryResponse:IResponse
{
    public List<RoleDto> UserRoles { get; set; }
}