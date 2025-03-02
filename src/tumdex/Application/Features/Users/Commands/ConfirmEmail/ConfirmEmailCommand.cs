using Application.Abstraction.Services;
using Application.Exceptions;
using Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Application.Features.Users.Commands.ConfirmEmail
{
    public class ConfirmEmailCommand : IRequest<IdentityResult>
    {
        public string UserId { get; set; }
        public string Token { get; set; }

        public class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand, IdentityResult>
        {
            private readonly IAuthService _authService;
            private readonly ILogger<ConfirmEmailCommandHandler> _logger;

            public ConfirmEmailCommandHandler(
                IAuthService authService,
                ILogger<ConfirmEmailCommandHandler> logger)
            {
                _authService = authService;
                _logger = logger;
            }

            public async Task<IdentityResult> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    var result = await _authService.ConfirmEmailAsync(request.UserId, request.Token);
            
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("Email confirmed successfully for user {UserId}", request.UserId);
                    }
                    else
                    {
                        _logger.LogWarning("Email confirmation failed for user {UserId}", request.UserId);
                    }
            
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error confirming email for user {UserId}", request.UserId);
            
                    if (ex is NotFoundUserExceptions)
                    {
                        return IdentityResult.Failed(new IdentityError { Description = "User not found." });
                    }
            
                    throw;
                }
            }
        }
    }
}