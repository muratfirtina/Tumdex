using Application.Dtos.Role;

namespace Application.Abstraction.Services;

public interface IAuthorizationEndpointService
{
    public Task AssignRoleToEndpointAsync(List<RoleDto> roles, string menu , string code, Type type);
    public Task<List<RoleDto>?> GetRolesToEndpointAsync(string code, string menu);
}