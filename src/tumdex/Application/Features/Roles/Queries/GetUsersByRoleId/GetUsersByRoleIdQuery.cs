using Application.Abstraction.Services;
using AutoMapper;
using Core.Application.Responses;
using MediatR;

namespace Application.Features.Roles.Queries.GetUsersByRoleId;

public class GetUsersByRoleIdQuery : IRequest<GetListResponse<GetUsersByRoleIdQueryResponse>>
{
    public string RoleId { get; set; }

    public class
        GetUsersByRoleIdQueryHandler : IRequestHandler<GetUsersByRoleIdQuery, GetListResponse<GetUsersByRoleIdQueryResponse>>
    {
        private readonly IRoleService _roleService;
        private readonly IMapper _mapper;

        public GetUsersByRoleIdQueryHandler(IRoleService roleService, IMapper mapper)
        {
            _roleService = roleService;
            _mapper = mapper;
        }

        public async Task<GetListResponse<GetUsersByRoleIdQueryResponse>> Handle(GetUsersByRoleIdQuery request, CancellationToken cancellationToken)
        {
            var users = await _roleService.GetUsersByRoleIdAsync(request.RoleId);
    
            var response = _mapper.Map<GetListResponse<GetUsersByRoleIdQueryResponse>>(users);
    
            // Eğer kullanıcı listesi boşsa, özel bir mesaj ekleyelim
            if (response.Items.Count == 0)
            {
                response.Items = new List<GetUsersByRoleIdQueryResponse>();
                response.Items.Add(new GetUsersByRoleIdQueryResponse
                {
                    Id = "NoUsers",
                    NameSurname = "No Users"
                });
            }
    
            return response;
        }
        
    }
}