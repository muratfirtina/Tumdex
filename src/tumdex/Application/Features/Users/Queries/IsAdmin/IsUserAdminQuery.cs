using Application.Abstraction.Services;
using MediatR;

namespace Application.Features.Users.Queries.IsAdmin;

public class IsUserAdminQuery : IRequest<bool>
{
    
    public class UserIsAdminHandler : IRequestHandler<IsUserAdminQuery, bool>
    {
        private readonly IUserService _userService;

        public UserIsAdminHandler(IUserService userService)
        {
            _userService = userService;
        }

        public async Task<bool> Handle(IsUserAdminQuery request, CancellationToken cancellationToken)
        {
            return await _userService.IsAdminAsync();
        }
    }
    
}