using Application.Abstraction.Services;
using Application.Dtos.Role;
using MediatR;

namespace Application.Features.AuthorizationEndpoint.Commands.AssignRoleToEndpoint;

public class AssignRoleToEndpointRequest: IRequest<AssignRoleToEndpointResponse>
{
    public List<RoleDto> Roles { get; set; }
    public string Code { get; set; }
    public string Menu { get; set; }
    public Type? Type { get; set; }
    
    public class AssignRoleToEndpointCommandRequestHandler : IRequestHandler<AssignRoleToEndpointRequest, AssignRoleToEndpointResponse>
    {
        private readonly IAuthorizationEndpointService _authorizationEndpointService;

        public AssignRoleToEndpointCommandRequestHandler(IAuthorizationEndpointService authorizationEndpointService)
        {
            _authorizationEndpointService = authorizationEndpointService;
        }

        public async Task<AssignRoleToEndpointResponse> Handle(AssignRoleToEndpointRequest request, CancellationToken cancellationToken)
        {
            await _authorizationEndpointService.AssignRoleToEndpointAsync(request.Roles ,request.Menu, request.Code, request.Type);
            return new AssignRoleToEndpointResponse()
            {

            };
        }
    }
}