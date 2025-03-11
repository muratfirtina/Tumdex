using Application.Abstraction.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Users.Commands.LoginUser;

public class LoginUserRequest: IRequest<LoginUserResponse>
{
    public string UsernameOrEmail { get; set; }
    public string Password { get; set; }
    
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public class LoginUserCommandHandler : IRequestHandler<LoginUserRequest, LoginUserResponse>
    {
        private readonly IAuthService _authService;
        private readonly ILogger<LoginUserCommandHandler> _logger;

        public LoginUserCommandHandler(
            IAuthService authService,
            ILogger<LoginUserCommandHandler> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        public async Task<LoginUserResponse> Handle(LoginUserRequest request, CancellationToken cancellationToken)
        {
            try
            {
                // Validate request
                if (string.IsNullOrWhiteSpace(request.UsernameOrEmail))
                    throw new ArgumentException("Username or E-mail required.");
                
                if (string.IsNullOrWhiteSpace(request.Password))
                    throw new ArgumentException("Password required.");
                
                // Call auth service with client information
                var token = await _authService.LoginAsync(
                    request.UsernameOrEmail, 
                    request.Password, 
                    900, // 15 minutes
                    request.IpAddress,
                    request.UserAgent);
                
                // Return successful response with token
                return new LoginUserSuccessResponse() { Token = token };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for user: {Username} from IP: {IpAddress}", 
                    request.UsernameOrEmail, request.IpAddress);
                
                // Rethrow to be handled by the controller
                throw;
            }
        }
    }
}