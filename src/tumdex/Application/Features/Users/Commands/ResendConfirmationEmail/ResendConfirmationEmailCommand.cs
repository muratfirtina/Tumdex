using Application.Abstraction.Services;
using Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Application.Features.Users.Commands.ResendConfirmationEmail
{
    public class ResendConfirmationEmailCommand : IRequest<bool>
    {
        public string Email { get; set; }

        public class ResendConfirmationEmailCommandHandler : IRequestHandler<ResendConfirmationEmailCommand, bool>
        {
            private readonly IAuthService _authService;
            private readonly ILogger<ResendConfirmationEmailCommandHandler> _logger;

            public ResendConfirmationEmailCommandHandler(
                IAuthService authService,
                ILogger<ResendConfirmationEmailCommandHandler> logger)
            {
                _authService = authService;
                _logger = logger;
            }

            public async Task<bool> Handle(ResendConfirmationEmailCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    await _authService.ResendConfirmationEmailAsync(request.Email);
                    _logger.LogInformation("Confirmation email resent request processed for {Email}", request.Email);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to resend confirmation email to {Email}", request.Email);
                    throw;
                }
            }
        }
    }
}