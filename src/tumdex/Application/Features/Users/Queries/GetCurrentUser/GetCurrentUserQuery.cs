using Application.Abstraction.Services;
using Application.Exceptions;
using AutoMapper;
using MediatR;

namespace Application.Features.Users.Queries.GetCurrentUser;

public class GetCurrentUserQuery :IRequest<GetCurrentUserQueryResponse>
{
    public string UserName { get; set; }
    
    public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, GetCurrentUserQueryResponse>
    {
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        public GetCurrentUserQueryHandler(IUserService userService, IMapper mapper)
        {
            _userService = userService;
            _mapper = mapper;
        }

        public async Task<GetCurrentUserQueryResponse> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
        {
            var user = await _userService.GetUserByUsernameAsync(request.UserName);
            if (user == null)
            {
                throw new NotFoundUserExceptions();  // Eğer kullanıcı bulunamazsa hata fırlatıyoruz
            }

            return _mapper.Map<GetCurrentUserQueryResponse>(user);
        }
    }
}