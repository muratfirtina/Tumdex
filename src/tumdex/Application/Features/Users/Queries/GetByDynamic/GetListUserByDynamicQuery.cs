using Application.Abstraction.Services;
using AutoMapper;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Dynamic;
using Core.Persistence.Paging;
using Domain.Identity;
using MediatR;

namespace Application.Features.Users.Queries.GetByDynamic;

public class GetListUserByDynamicQuery : IRequest<GetListResponse<GetListUserByDynamicQueryResponse>>
{
    public DynamicQuery DynamicQuery { get; set; }
    public PageRequest PageRequest { get; set; }
    
    public class GetUserByDynamicQueryHandler : IRequestHandler<GetListUserByDynamicQuery, GetListResponse<GetListUserByDynamicQueryResponse>>
    {
        private readonly IUserService _userService;
        private readonly IMapper _mapper;
        
        public GetUserByDynamicQueryHandler(IUserService userService, IMapper mapper)
        {
            _userService = userService;
            _mapper = mapper;
        }
        
        public async Task<GetListResponse<GetListUserByDynamicQueryResponse>> Handle(GetListUserByDynamicQuery request, CancellationToken cancellationToken)
        {
            if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
            {
                var allUsers = await _userService.GetAllByDynamicAsync(
                    request.DynamicQuery,
                    index: request.PageRequest.PageIndex,
                    size: request.PageRequest.PageSize,
                    cancellationToken: cancellationToken);
                
                var usersDtos = _mapper.Map<GetListResponse<GetListUserByDynamicQueryResponse>>(allUsers);
                
                return usersDtos;
            }
            else
            {
                IPaginate<AppUser> users = await _userService.GetListByDynamicAsync(
                    request.DynamicQuery,
                    index: request.PageRequest.PageIndex,
                    size: request.PageRequest.PageSize,
                    cancellationToken: cancellationToken);
                
                var usersDtos = _mapper.Map<GetListResponse<GetListUserByDynamicQueryResponse>>(users);
                
                return usersDtos;
            }
        }
    }
}