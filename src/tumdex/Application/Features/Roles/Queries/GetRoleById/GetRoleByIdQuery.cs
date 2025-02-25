using Application.Abstraction.Services;
using MediatR;

namespace Application.Features.Roles.Queries.GetRoleById;

public class GetRoleByIdQuery:IRequest<GetRoleByIdQueryResponse>
{
    public string? RoleId { get; set; }
    
    public class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, GetRoleByIdQueryResponse>
    {
        private readonly IRoleService _roleService;

        public GetRoleByIdQueryHandler(IRoleService roleService)
        {
            _roleService = roleService;
        }

        public async Task<GetRoleByIdQueryResponse> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
        {
            var (roleId, roleName) = await _roleService.GetRoleByIdAsync(request.RoleId);
            return new GetRoleByIdQueryResponse()
            {
                RoleId = roleId,
                RoleName = roleName
            };

        }
    }
}