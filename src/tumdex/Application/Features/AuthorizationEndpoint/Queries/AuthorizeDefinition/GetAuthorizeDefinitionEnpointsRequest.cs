using Application.Abstraction.Services.Configurations;
using Application.Dtos.Configuration;
using MediatR;

namespace Application.Features.AuthorizationEndpoint.Queries.AuthorizeDefinition;

public class GetAuthorizeDefinitionEnpointsQuery: IRequest<GetAuthorizeDefinitionEnpointsQueryResponse>
{
    public class GetAuthorizeDefinitionEnpointsRequestHandler : IRequestHandler<GetAuthorizeDefinitionEnpointsQuery, GetAuthorizeDefinitionEnpointsQueryResponse>
    {
        private readonly IApplicationService _applicationService;

        public GetAuthorizeDefinitionEnpointsRequestHandler(IApplicationService applicationService)
        {
            _applicationService = applicationService;
        }

        public Task<GetAuthorizeDefinitionEnpointsQueryResponse> Handle(GetAuthorizeDefinitionEnpointsQuery request, CancellationToken cancellationToken)
        {
            var result = _applicationService.GetAuthorizeDefinitionEnpoints(typeof(MenuDto));
            return Task.FromResult(new GetAuthorizeDefinitionEnpointsQueryResponse
            {
                AuthorizeMenu = result
            });
        }
    }
}