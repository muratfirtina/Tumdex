using Application.Abstraction.Services;
using Application.Dtos.Role;
using MediatR;

namespace Application.Features.Users.Commands.AssignRoleToUser;

public class AssignRoleToUserRequest: IRequest<AssignRoleToUserResponse>
{
    public string UserId { get; set; }
    public List<RoleDto> Roles { get; set; }
    

    public class AssignRoleToUserHandler : IRequestHandler<AssignRoleToUserRequest, AssignRoleToUserResponse>
    {
        private readonly IUserService _userService;
        
        public AssignRoleToUserHandler(IUserService userService, IRoleService roleService)
        {
            _userService = userService;
        }

        public async Task<AssignRoleToUserResponse> Handle(AssignRoleToUserRequest request, CancellationToken cancellationToken)
        {
            await _userService.AssignRoleToUserAsync(request.UserId, request.Roles);
            return new AssignRoleToUserResponse();
        }
    }
}