using Application.Dtos.Configuration;
using Core.Application.Responses;

namespace Application.Features.AuthorizationEndpoint.Queries.AuthorizeDefinition;

public class GetAuthorizeDefinitionEnpointsQueryResponse :IResponse
{
    public List<MenuDto>? AuthorizeMenu { get; set; }
}