using Application.Abstraction.Services;
using MediatR;

namespace Application.Features.Users.Queries.GetRolesToUser;

public class GetRolesToUserQuery : IRequest<GetRolesToUserQueryResponse>
{
    public string UserId { get; set; }

    public class GetRolesToUserRequestHandler : IRequestHandler<GetRolesToUserQuery, GetRolesToUserQueryResponse>
    {
        private readonly IUserService _userService;

        public GetRolesToUserRequestHandler(IUserService userService)
        {
            _userService = userService;
        }

        public async Task<GetRolesToUserQueryResponse> Handle(GetRolesToUserQuery request,
            CancellationToken cancellationToken)
        {
            var userRoles = await _userService.GetRolesToUserAsync(request.UserId);
            return new GetRolesToUserQueryResponse
            {
                UserRoles = userRoles
            };
        }
    }
}