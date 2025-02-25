using Application.Abstraction.Services;
using MediatR;

namespace Application.Features.Users.Commands.PasswordReset;

public class PasswordResetRequest : IRequest<PasswordResetResponse>
{
    public string Email { get; set; }
    

    public class PasswordResetCommandHandler : IRequestHandler<PasswordResetRequest, PasswordResetResponse>
    {
        private readonly IAuthService _authService;

        public PasswordResetCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<PasswordResetResponse> Handle(PasswordResetRequest request, CancellationToken cancellationToken)
        {
            await _authService.PasswordResetAsync(request.Email);
            return new ();
        }
    }
}