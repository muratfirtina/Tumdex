using System.Linq.Expressions;
using System.Text.Json;
using Application.Abstraction.Helpers;
using Application.Abstraction.Services;
using Application.Dtos.Role;
using Application.Exceptions;
using Application.Repositories;
using Core.Application.Requests;
using Core.Persistence.Dynamic;
using Core.Persistence.Paging;
using Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Persistence.Services;

public class UserService : IUserService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly IEndpointRepository _endpointRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDistributedCache _cache;
    private readonly ILogger<UserService> _logger;

    public UserService(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager,
        IEndpointRepository endpointRepository, IHttpContextAccessor httpContextAccessor, IDistributedCache cache,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _endpointRepository = endpointRepository;
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AppUser> GetUserByUsernameAsync(string userName)
    {
        var user = await _userManager.FindByNameAsync(userName);
        if (user == null)
        {
            throw new NotFoundUserExceptions();
        }

        return user;
    }

    public async Task<AppUser> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Kullanıcı bulunamadı: ID={UserId}", userId);
            throw new NotFoundUserExceptions();
        }

        return user;
    }

    public async Task UpdateRefreshTokenAsync(string refreshToken, AppUser? user, DateTime accessTokenDateTime,
        int refreshTokenLifetime)
    {
        if (user != null)
        {
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = accessTokenDateTime.AddSeconds(refreshTokenLifetime);
            await _userManager.UpdateAsync(user);
        }
        else
            throw new NotFoundUserExceptions();
    }

    public async Task UpdateForgotPasswordAsync(string userId, string resetToken, string newPassword)
    {
        AppUser? user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            resetToken = resetToken.UrlDecode();

            IdentityResult result = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);
            if (result.Succeeded)
                await _userManager.UpdateSecurityStampAsync(user);
            else
                throw new ResetPasswordException();
        }
    }

    public async Task<List<AppUser>> GetAllUsersAsync(PageRequest pageRequest)
    {
        IQueryable<AppUser> userQuery = _userManager.Users;

        if (pageRequest.PageIndex == -1 && pageRequest.PageSize == -1)
        {
            // Tüm kullanıcıları getir
            var allUsers = await userQuery.ToListAsync();

            return allUsers;
        }
        else
        {
            // Pagination yap
            IPaginate<AppUser> users = await userQuery.ToPaginateAsync(pageRequest.PageIndex, pageRequest.PageSize);
            return users.Items.ToList();
        }
    }


    public async Task AssignRoleToUserAsync(string userId, List<RoleDto>? roles)
    {
        AppUser? user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, userRoles);
            await _userManager.AddToRolesAsync(user, roles.Select(x => x.Name).ToList());
        }
    }

    public async Task<List<RoleDto>> GetRolesToUserAsync(string userIdOrName)
    {
        AppUser? user = await _userManager.FindByIdAsync(userIdOrName);
        if (user == null)
            user = await _userManager.FindByNameAsync(userIdOrName);
        if (user != null)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            return await _roleManager.Roles.Where(x => userRoles.Contains(x.Name)).Select(x => new RoleDto()
            {
                Name = x.Name,
                Id = x.Id
            }).ToListAsync();
        }

        return new List<RoleDto>();
    }

    public async Task<AppUser?> GetCurrentUserAsync()
    {
        var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userName))
        {
            AppUser? user = await _userManager.Users.FirstOrDefaultAsync(u => u.UserName == userName);
            if (user != null)
            {
                return user;
            }
        }

        // Kimliği doğrulanmamış kullanıcılar için null dönebilirsiniz
        return null;
        // Veya istisna fırlatmak yerine:
        // throw new UnauthorizedAccessException("Kullanıcı kimliği doğrulanmamış.");
    }

    public async Task<bool> IsAdminAsync()
    {
        AppUser? user = await GetCurrentUserAsync();
        if (user != null)
        {
            var roles = await _userManager.GetRolesAsync(user);
            return roles.Select(x => x.ToLower()).Contains("admin");
        }

        return false;
    }

    public async Task<bool> HasRolePermissionToEndpointAsync(string name, string code)
    {
        var userRoles = await GetRolesToUserAsync(name);
        if (!userRoles.Any())
            return false;

        var endpoint = await _endpointRepository.GetAsync(
            predicate: e => e.Code == code,
            include: e => e.Include(x => x.Roles),
            enableTracking: false
        );

        if (endpoint == null)
            return false;

        var endpointRoles = endpoint.Roles.Select(r => r.Name).ToList();

        return userRoles.Any(userRole => endpointRoles.Contains(userRole.Name));
    }

    public async Task<List<AppUser>> GetAllByDynamicAsync(DynamicQuery dynamic,
        Expression<Func<AppUser, bool>>? predicate = null,
        Func<IQueryable<AppUser>, IIncludableQueryable<AppUser, object>>? include = null, int index = -1,
        int size = -1, bool withDeleted = false, bool enableTracking = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<AppUser> queryable = Query().ToDynamic(dynamic);
        if (!enableTracking)
            queryable = queryable.AsNoTracking();
        if (include != null)
            queryable = include(queryable);
        if (withDeleted)
            queryable = queryable.IgnoreQueryFilters();
        if (predicate != null)
            queryable = queryable.Where(predicate);
        return await queryable.ToListAsync();
    }

    public async Task<IPaginate<AppUser>> GetListByDynamicAsync(DynamicQuery dynamic,
        Expression<Func<AppUser, bool>>? predicate = null,
        Func<IQueryable<AppUser>, IIncludableQueryable<AppUser, object>>? include = null, int index = 0,
        int size = 10, bool withDeleted = false, bool enableTracking = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<AppUser> queryable = Query().ToDynamic(dynamic);
        if (!enableTracking)
            queryable = queryable.AsNoTracking();
        if (include != null)
            queryable = include(queryable);
        if (withDeleted)
            queryable = queryable.IgnoreQueryFilters();
        if (predicate != null)
            queryable = queryable.Where(predicate);
        return await queryable.ToPaginateAsync(index, size, from: 0, cancellationToken);
    }

    public IQueryable<AppUser> Query() => _userManager.Users.AsQueryable();


    public async Task<AppUser> GetUserByEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            throw new NotFoundUserExceptions();
        }

        return user;
    }
}