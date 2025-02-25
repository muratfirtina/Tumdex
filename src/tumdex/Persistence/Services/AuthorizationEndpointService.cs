using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Configurations;
using Application.Dtos.Configuration;
using Application.Dtos.Role;
using Application.Repositories;
using Domain;
using Domain.Identity;

namespace Persistence.Services;

public class AuthorizationEndpointService : IAuthorizationEndpointService
{
    readonly IApplicationService _applicationService;
    readonly IEndpointRepository _endpointRepository;
    readonly IACMenuRepository _acMenuRepository;
    readonly RoleManager<AppRole> _roleManager;

    public AuthorizationEndpointService(
        IApplicationService applicationService,
        IEndpointRepository endpointRepository,
        IACMenuRepository acMenuRepository,
        RoleManager<AppRole> roleManager)
    {
        _applicationService = applicationService;
        _endpointRepository = endpointRepository;
        _acMenuRepository = acMenuRepository;
        _roleManager = roleManager;
    }

    public async Task AssignRoleToEndpointAsync(List<RoleDto> roles, string menu, string code, Type type)
    {
        var acMenu = await _acMenuRepository.GetAsync(x => x.Name == menu);
        if (acMenu == null)
        {
            acMenu = new ACMenu
            {
                Id = Guid.NewGuid().ToString(),
                Name = menu
            };
            await _acMenuRepository.AddAsync(acMenu);
        }

        var endpoint = await _endpointRepository.GetAsync(x => x.AcMenu.Id == acMenu.Id && x.Code == code);
        if (endpoint == null)
        {
            var action = _applicationService.GetAuthorizeDefinitionEnpoints(type)
                .FirstOrDefault(m => m.Name == menu)?.Actions.FirstOrDefault(a => a.Code == code);

            endpoint = new Endpoint
            {
                Id = Guid.NewGuid().ToString(),
                Code = code,
                ActionType = action.ActionType,
                HttpType = action.HttpType,
                Definition = action.Definition,
                AcMenu = acMenu
            };
            await _endpointRepository.AddAsync(endpoint);
        }

        endpoint = await _endpointRepository.GetAsync(
            predicate: e => e.Code == code && e.AcMenu.Name == menu,
            include: x => x.Include(e => e.AcMenu).Include(e => e.Roles)
        );

        endpoint.Roles.Clear();

        foreach (var roleDto in roles)
        {
            var role = await _roleManager.FindByNameAsync(roleDto.Name);
            if (role != null)
            {
                endpoint.Roles.Add(role);
            }
        }

        await _endpointRepository.UpdateAsync(endpoint);
    }

    public async Task<List<RoleDto>> GetRolesToEndpointAsync(string code, string menu)
    {
        var endpoint = await _endpointRepository.GetAsync(
            predicate: e => e.Code == code && e.AcMenu.Name == menu,
            include: x => x.Include(e => e.AcMenu).Include(e => e.Roles)
        );

        if (endpoint != null)
        {
            return endpoint.Roles.Select(x => new RoleDto
            {
                Name = x.Name,
                Id = x.Id
            }).ToList();
        }

        return new List<RoleDto>();
    }
}