using Application.Abstraction.Services;
using AutoMapper;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain.Identity;
using MediatR;

namespace Application.Features.Users.Queries.GetAllUsers;

public class GetAllUsersQuery: IRequest<GetListResponse<GetAllUsersQueryResponse>>
{
    public PageRequest PageRequest { get; set; }
    
    public class GetAllUsersQueryHandler:IRequestHandler<GetAllUsersQuery,GetListResponse<GetAllUsersQueryResponse>>
    {
        private readonly IUserService _userService;
        private readonly IMapper _mapper;
        
        public GetAllUsersQueryHandler(IUserService userService,IMapper mapper)
        {
            _userService = userService;
            _mapper = mapper;
        }
        public async Task<GetListResponse<GetAllUsersQueryResponse>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
        {
            List<AppUser> users = await _userService.GetAllUsersAsync(request.PageRequest);
            GetListResponse<GetAllUsersQueryResponse> response = _mapper.Map<GetListResponse<GetAllUsersQueryResponse>>(users);
            return response;
        }
    }
}