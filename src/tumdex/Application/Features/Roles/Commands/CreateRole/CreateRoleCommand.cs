using Application.Abstraction.Services;
using MediatR;

namespace Application.Features.Roles.Commands.CreateRole;

public class CreateRoleCommand: IRequest<CreatedRoleResponse>
{
    public string Name { get; set; }
    
    public class CreateRoleRequestHandler: IRequestHandler<CreateRoleCommand, CreatedRoleResponse>
    {
        private readonly IRoleService _roleService;

        public CreateRoleRequestHandler(IRoleService roleService)
        {
            _roleService = roleService;
        }

        public async Task<CreatedRoleResponse> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
        {
            var result= await _roleService.CreateRoleAsync(command.Name);
            return new CreatedRoleResponse()
            {
                Succeeded = result
            };
        }
    }
}