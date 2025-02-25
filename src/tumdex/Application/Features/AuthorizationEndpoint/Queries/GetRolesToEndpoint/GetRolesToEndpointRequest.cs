using Application.Abstraction.Services;
using MediatR;

namespace Application.Features.AuthorizationEndpoint.Queries.GetRolesToEndpoint;

public class GetRolesToEndpointQuery: IRequest<GetRolesToEndpointQueryResponse>
{
    public string Code { get; set; }
    public string Menu { get; set; }
    
    public class GetRolesToEndpointRequestHandler : IRequestHandler<GetRolesToEndpointQuery, GetRolesToEndpointQueryResponse>
    {
        private readonly IAuthorizationEndpointService _authorizationEndpointService;

        public GetRolesToEndpointRequestHandler(IAuthorizationEndpointService authorizationEndpointService)
        {
            _authorizationEndpointService = authorizationEndpointService;
        }

        public async Task<GetRolesToEndpointQueryResponse> Handle(GetRolesToEndpointQuery request, CancellationToken cancellationToken)
        {
            var datas =await _authorizationEndpointService.GetRolesToEndpointAsync(request.Code, request.Menu);
            return new GetRolesToEndpointQueryResponse()
            {
                Roles = datas
            };
        }
    }
}