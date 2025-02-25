using System.Linq.Expressions;
using Application.Dtos.Role;
using Application.Dtos.User;
using Core.Application.Requests;
using Core.Persistence.Dynamic;
using Core.Persistence.Paging;
using Domain.Identity;
using Microsoft.EntityFrameworkCore.Query;

namespace Application.Abstraction.Services;

public interface IUserService
{
    Task<AppUser?> GetCurrentUserAsync();
    Task<AppUser> GetUserByUsernameAsync(string userName);
    Task UpdateRefreshTokenAsync(string refreshToken, AppUser user, DateTime accessTokenDateTime, int refreshTokenLifetime);
    Task UpdateForgotPasswordAsync(string userId, string resetToken, string newPassword);
    Task<List<AppUser>>  GetAllUsersAsync(PageRequest pageRequest);
    Task<bool> IsAdminAsync();
    Task AssignRoleToUserAsync(string userId, List<RoleDto> roles);
    public Task<List<RoleDto>> GetRolesToUserAsync(string userIdOrName);
    Task<bool>HasRolePermissionToEndpointAsync(string name, string code);
    Task<List<AppUser>> GetAllByDynamicAsync(DynamicQuery dynamic,
        Expression<Func<AppUser, bool>>? predicate = null,
        Func<IQueryable<AppUser>, IIncludableQueryable<AppUser, object>>? include = null,
        int index = -1,
        int size = -1,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default
    );
    Task<IPaginate<AppUser>> GetListByDynamicAsync(
        DynamicQuery dynamic,
        Expression<Func<AppUser, bool>>? predicate = null,
        Func<IQueryable<AppUser>, IIncludableQueryable<AppUser, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default
    );
}