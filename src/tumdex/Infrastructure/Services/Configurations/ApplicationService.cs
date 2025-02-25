using System.Reflection;
using Application.Abstraction.Services.Configurations;
using Application.CustomAttributes;
using Application.Dtos.Configuration;
using Application.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Infrastructure.Services.Configurations;

public class ApplicationService : IApplicationService
{
    public List<MenuDto>? GetAuthorizeDefinitionEnpoints(Type type)
    {
        Assembly? assembly = Assembly.GetAssembly(type);
        var controllers = assembly?.GetTypes().Where(t => t.IsAssignableTo(typeof(ControllerBase)));
        List<MenuDto>? menus = new();
        if (controllers == null) return menus;
        foreach (var controller in controllers)
        {
            var methodInfos = controller.GetMethods().Where(m =>m.IsDefined(typeof(AuthorizeDefinitionAttribute)));
            var actions = methodInfos.ToList();
            if (!actions.Any()) continue;
            MenuDto? menu = new()
            {
                Name = controller.Name.Replace("Controller", "")
            };
            List<ActionDto>? actionDtos = new();
            foreach (var action in actions)
            {
                ActionDto? actionDto = new();
                var authorizeDefinitionAttribute = action.GetCustomAttribute<AuthorizeDefinitionAttribute>();
                var httpMethodAttribute = action.GetCustomAttribute<HttpMethodAttribute>();
                var httpMethodName = httpMethodAttribute?.HttpMethods.FirstOrDefault();
                if (authorizeDefinitionAttribute != null)
                {
                    actionDto.ActionType =
                        Enum.GetName(typeof(ActionType), authorizeDefinitionAttribute.ActionType);
                    actionDto.HttpType = httpMethodName;
                    actionDto.Definition = authorizeDefinitionAttribute.Definition;
                    actionDto.Code = $"{actionDto.HttpType}.{actionDto.ActionType}.{actionDto.Definition.Replace(" ", "")}";
                }

                actionDtos.Add(actionDto);
            }
            menu.Actions = actionDtos;
            menus.Add(menu);
        }
        return menus;
    }
}