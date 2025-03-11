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
}
    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, CreatedUserResponse>
{
    private readonly IAuthService _authService;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateUserCommandHandler> _logger;
    private readonly UserManager<AppUser> _userManager;
    private readonly IBackgroundTaskQueue _backgroundTaskQueue;

    public CreateUserCommandHandler(
        IAuthService authService,
        IMediator mediator,
        ILogger<CreateUserCommandHandler> logger,
        UserManager<AppUser> userManager,
        IBackgroundTaskQueue backgroundTaskQueue)
    {
        _authService = authService;
        _mediator = mediator;
        _logger = logger;
        _userManager = userManager;
        _backgroundTaskQueue = backgroundTaskQueue;
    }

    public async Task<CreatedUserResponse> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var response = new CreatedUserResponse();

        try
        {
            // Check for existing user before attempting registration
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                // User already exists, determine appropriate response
                if (!existingUser.EmailConfirmed)
                {
                    // User exists but hasn't confirmed email - resend activation
                    response.IsSuccess = true;
                    response.Message = "An activation email has been sent to your address. Please check your email.";
                    response.UserId = existingUser.Id;
                    
                    // Queue activation code resend in background
                    _backgroundTaskQueue.QueueBackgroundWorkItem(async token =>
                    {
                        var activationCode = await _authService.GenerateActivationCodeAsync(existingUser.Id);
                        await _authService.ResendActivationEmailAsync(existingUser.Email, activationCode);
                    });
                    
                    return response;
                }
                
                // User already exists and is confirmed
                response.IsSuccess = false;
                response.Message = "An account with this email already exists.";
                return response;
            }

            // No existing user found, proceed with registration
            var (result, user) = await _authService.RegisterUserAsync(request);

            if (!result.Succeeded)
            {
                response.IsSuccess = false;
                response.Message = string.Join("\n", result.Errors.Select(e => $"{e.Code} - {e.Description}"));
                return response;
            }
    
            // Successfully created user
            response.IsSuccess = true;
            response.Message = "User created successfully. Please check your email to confirm your account.";
            response.UserId = user.Id;
            
            // Get activation token asynchronously and include in response
            var activationToken = await _authService.GenerateSecureActivationTokenAsync(user.Id, user.Email);
            response.ActivationToken = activationToken;
    
            // Queue newsletter subscription processing asynchronously
            _ = ProcessNewsletterSubscriptionAsync(user, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while creating user");
            response.IsSuccess = false;
            response.Message = "An unexpected error occurred. Please try again later.";
            return response;
        }
    }
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
        }
    }
}