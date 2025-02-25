using Application.Abstraction.Services;
using MediatR;

namespace Application.Features.Roles.Commands.UpdateRole;

public class UpdateRoleCommand:IRequest<UpdatedRoleResponse>
{
    public string RoleId { get; set; }
    public string Name { get; set; }
    
    public class UpdateRoleRequestHandler: IRequestHandler<UpdateRoleCommand, UpdatedRoleResponse>
    {
        private readonly IRoleService _roleService;

        public UpdateRoleRequestHandler(IRoleService roleService)
        {
            _roleService = roleService;
        }

        public async Task<UpdatedRoleResponse> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
        {  
            var result = await _roleService.UpdateRoleAsync(command.RoleId, command.Name);
            return new UpdatedRoleResponse
            {
                Succeeded = result
            };
            
        }
    }
}