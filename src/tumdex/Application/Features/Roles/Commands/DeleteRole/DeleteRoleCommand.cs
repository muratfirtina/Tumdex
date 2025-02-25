using Application.Abstraction.Services;
using MediatR;

namespace Application.Features.Roles.Commands.DeleteRole;

public class DeleteRoleCommand: IRequest<DeletedRoleResponse>
{
    public string RoleId { get; set; }
    
    public class DeleteRoleRequestHandler: IRequestHandler<DeleteRoleCommand, DeletedRoleResponse>
    {
        private readonly IRoleService _roleService;

        public DeleteRoleRequestHandler(IRoleService roleService)
        {
            _roleService = roleService;
        }

        public async Task<DeletedRoleResponse> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
        {
            var result = await _roleService.DeleteRoleAsync(request.RoleId);
            return new DeletedRoleResponse()
            {
                Succeeded = result
            };
        }
    }
}