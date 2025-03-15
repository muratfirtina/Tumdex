using Application.Abstraction.Services;
using Application.Abstraction.Services.Authentication;
using MediatR;

namespace Application.Features.Users.Commands.PasswordReset;

public class PasswordResetRequest : IRequest<PasswordResetResponse>
{
    public string Email { get; set; }
    

    public class PasswordResetCommandHandler : IRequestHandler<PasswordResetRequest, PasswordResetResponse>
    {
        private readonly IRegistrationAndPasswordService _registrationAndPasswordService;

        public PasswordResetCommandHandler(IRegistrationAndPasswordService registrationAndPasswordService)
        {
            _registrationAndPasswordService = registrationAndPasswordService;
        }

        public async Task<PasswordResetResponse> Handle(PasswordResetRequest request, CancellationToken cancellationToken)
        {
            await _registrationAndPasswordService.PasswordResetAsync(request.Email);
            return new ();
        }
    }
}