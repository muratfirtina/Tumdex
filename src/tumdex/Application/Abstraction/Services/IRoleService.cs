using Application.Dtos.Role;
using Core.Application.Requests;
using Domain.Identity;

namespace Application.Abstraction.Services;

public interface IRoleService
{
    Task<bool> CreateRoleAsync(string roleName);
    Task<bool> DeleteRoleAsync(string roleId);
    Task<bool> UpdateRoleAsync(string roleId, string roleName);
    Task<List<AppRole>> GetAllRolesAsync(PageRequest pageRequest);
    Task<(string roleId,string roleName)> GetRoleByIdAsync(string roleId);
    Task<List<AppUser>> GetUsersByRoleIdAsync(string roleId);

}