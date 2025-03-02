using Application.Abstraction.Services;
using Application.Features.Events.User.UserRegister;
using Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Application.Features.Users.Commands.CreateUser;

public class CreateUserCommand : IRequest<CreatedUserResponse>
{
    public string NameSurname { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public string ConfirmPassword { get; set; }
    
    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, CreatedUserResponse>
{
    private readonly IAuthService _authService;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        IAuthService authService,
        IMediator mediator,
        ILogger<CreateUserCommandHandler> logger)
    {
        _authService = authService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<CreatedUserResponse> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var response = new CreatedUserResponse();

        try
        {
            // AuthService üzerinden kullanıcı kaydı
            var result = await _authService.RegisterUserAsync(request);
        
            if (!result.Succeeded)
            {
                response.IsSuccess = false;
                response.Message = string.Join("\n", result.Errors.Select(e => $"{e.Code} - {e.Description}"));
                return response;
            }

            // Kullanıcı bilgilerini al (newsletter için)
            var user = await _authService.GetUserByEmailAsync(request.Email);
        
            // Newsletter kaydını asenkron olarak başlat - ana işlemi bekletmeyecek
            _ = ProcessNewsletterSubscriptionAsync(user, cancellationToken);

            response.IsSuccess = true;
            response.Message = "User created successfully. Please check your email to confirm your account.";
            return response;
        }
        catch (Exception ex)
        {
            response.IsSuccess = false;
            response.Message = $"An unexpected error occurred: {ex.Message}";
            return response;
        }
    }

// Yeni eklenen asenkron newsletter işleme metodu 
    private async Task ProcessNewsletterSubscriptionAsync(AppUser user, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Publish(new UserRegisteredEvent(user), cancellationToken);
            _logger.LogInformation("Newsletter subscription processed for user {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Newsletter subscription failed for user {Email}", user.Email);
            // Burada hataları işleyebilir veya bir kuyruk sistemine ekleyerek daha sonra yeniden deneyebilirsiniz
        }
    }
}
}