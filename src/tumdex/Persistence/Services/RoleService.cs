using Application.Abstraction.Services;
using Application.Dtos.Role;
using Core.Application.Requests;
using Core.Persistence.Paging;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Services;

public class RoleService : IRoleService
{
    private readonly RoleManager<AppRole> _roleManager;
    private readonly UserManager<AppUser> _userManager;

    public RoleService(RoleManager<AppRole> roleManager, UserManager<AppUser> userManager)
    {
        _roleManager = roleManager;
        _userManager = userManager;
    }

    public async Task<bool> CreateRoleAsync(string roleName)
    {
        IdentityResult result = await _roleManager.CreateAsync(new AppRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = roleName
        });
        return result.Succeeded;
    }

    public async Task<bool> DeleteRoleAsync(string roleId)
    {
        AppRole? appRole = await _roleManager.FindByIdAsync(roleId);
        IdentityResult result = await _roleManager.DeleteAsync(appRole);
        return result.Succeeded;
    }

    public async Task<bool> UpdateRoleAsync(string roleId, string roleName)
    {
        AppRole? role = await _roleManager.FindByIdAsync(roleId);
        role.Name = roleName;
        IdentityResult result = await _roleManager.UpdateAsync(role);
        return result.Succeeded;
    }

    public async Task<List<AppRole>> GetAllRolesAsync(PageRequest pageRequest)
    {
        IQueryable<AppRole>? roleQuery = _roleManager.Roles;

        if (pageRequest.PageIndex == -1 && pageRequest.PageSize == -1)
        {
            // Tüm rolleri getir
            var allRoles = await roleQuery.ToListAsync();
            return allRoles;
        }
        else
        {
            // Pagination yap
            IPaginate<AppRole> roles = await roleQuery.ToPaginateAsync(pageRequest.PageIndex, pageRequest.PageSize);
            return roles.Items.ToList();
        }
       
    }

    public async Task<(string roleId, string roleName)> GetRoleByIdAsync(string roleId)
    {
        AppRole? role = await _roleManager.FindByIdAsync(roleId);
        if (role == null)
        {
            // Rol bulunamadığında uygun bir şekilde işleyin
            // Örneğin, özel bir exception fırlatabilir veya default değerler dönebilirsiniz
            throw new ($"Role with ID {roleId} not found.");
            // Veya: return (string.Empty, string.Empty);
        }
        return (role.Id, role.Name);
    }
    
    public async Task<List<AppUser>> GetUsersByRoleIdAsync(string roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role == null)
        {
            // Rol bulunamadığında null dönmek yerine boş liste dönelim
            return new List<AppUser>();
        }

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
        return usersInRole.ToList();
    }
}