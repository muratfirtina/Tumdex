using Application.Abstraction.Services;
using Application.Abstraction.Services.Authentication;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed; // Redis için gerekli

namespace Application.Features.Users.Commands.LoginUser;

public class LoginUserRequest: IRequest<LoginUserResponse>
{
    public string UsernameOrEmail { get; set; }
    public string Password { get; set; }
    
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceFingerprint { get; set; }

    public class LoginUserCommandHandler : IRequestHandler<LoginUserRequest, LoginUserResponse>
    {
        private readonly IAuthService _authService;
        private readonly ILogger<LoginUserCommandHandler> _logger;
        private readonly IDistributedCache _cache; // Redis cache eklendi

        public LoginUserCommandHandler(
            IAuthService authService,
            ILogger<LoginUserCommandHandler> logger,
            IDistributedCache cache) // Redis cache enjekte edildi
        {
            _authService = authService;
            _logger = logger;
            _cache = cache;
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
                
                // Başarılı login sonrası Redis'teki token iptal kaydını temizle
                if (token != null && !string.IsNullOrEmpty(token.UserId))
                {
                    try
                    {
                        // Kullanıcıya ait eski token iptal kaydını sil
                        await _cache.RemoveAsync($"UserTokensRevoked:{token.UserId}");
                        _logger.LogInformation("Kullanıcı başarıyla giriş yaptı, token iptal kaydı temizlendi: {UserId}", token.UserId);
                    }
                    catch (Exception ex)
                    {
                        // Redis hatası login işlemini etkilememeli, sadece log
                        _logger.LogWarning(ex, "Kullanıcı giriş yaptı ancak Redis temizleme hatası: {UserId}", token.UserId);
                    }
                }
                
                // Return successful response with token
                return new LoginUserSuccessResponse { 
                    Token = token,
                    UserName = request.UsernameOrEmail
                };
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