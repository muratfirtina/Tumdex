using Application.Abstraction.Services;
using AutoMapper;
using Core.Application.Requests;
using Core.Application.Responses;
using Domain.Identity;
using MediatR;

namespace Application.Features.Roles.Queries.GetRoles;

public class GetAllRolesQuery: IRequest<GetListResponse<GetAllRolesQueryResponse>>
{
    public PageRequest PageRequest { get; set; }
    
    public class GetRolesQueryHandler:IRequestHandler<GetAllRolesQuery,GetListResponse<GetAllRolesQueryResponse>>
    {
        private readonly IRoleService _roleService;
        
        private readonly IMapper _mapper;
        public GetRolesQueryHandler(IRoleService roleService,IMapper mapper)
        {
            _roleService = roleService;
            _mapper = mapper;
        }
        public async Task<GetListResponse<GetAllRolesQueryResponse>> Handle(GetAllRolesQuery request, CancellationToken cancellationToken)
        {
            List<AppRole> roles = await _roleService.GetAllRolesAsync(request.PageRequest);
            GetListResponse<GetAllRolesQueryResponse> response = _mapper.Map<GetListResponse<GetAllRolesQueryResponse>>(roles);
            return response;
        }
    }
}