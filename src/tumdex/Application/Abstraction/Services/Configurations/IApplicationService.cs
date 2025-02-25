using Application.Dtos.Configuration;

namespace Application.Abstraction.Services.Configurations;

public interface IApplicationService //endpoint-authorize service
{
    List<MenuDto>? GetAuthorizeDefinitionEnpoints(Type type);
}