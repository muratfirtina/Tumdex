using Application.Dtos.Role;
using Core.Application.Responses;

namespace Application.Features.AuthorizationEndpoint.Queries.GetRolesToEndpoint;

public class GetRolesToEndpointQueryResponse:IResponse
{
    public List<RoleDto> Roles { get; set; }
}