using Application.Abstraction.Services;
using Application.Abstraction.Services.Email;
using Application.Abstraction.Services.Tokens;
using Application.Abstraction.Services.Utilities;
using Application.Exceptions;
using Application.Features.Users.Commands.CreateUser;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Persistence.Services;

public class RegistrationAndPasswordService : IRegistrationAndPasswordService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IBackgroundTaskQueue _backgroundTaskQueue;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IAccountEmailService _accountEmailService;
    private readonly ILogger<RegistrationAndPasswordService> _logger;
    private readonly ITokenService _tokenService;

    public RegistrationAndPasswordService(UserManager<AppUser> userManager, IBackgroundTaskQueue backgroundTaskQueue, IServiceScopeFactory serviceScopeFactory, IAccountEmailService accountEmailService, ILogger<RegistrationAndPasswordService> logger, ITokenService tokenService)
    {
        _userManager = userManager;
        _backgroundTaskQueue = backgroundTaskQueue;
        _serviceScopeFactory = serviceScopeFactory;
        _accountEmailService = accountEmailService;
        _logger = logger;
        _tokenService = tokenService;
    }

    public async Task<(IdentityResult result, AppUser user)> RegisterUserAsync(CreateUserCommand model)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = model.UserName,
            Email = model.Email,
            NameSurname = model.NameSurname,
            EmailConfirmed = false, // Email confirmation required
            IsActive = true, // User is active by default
            CreatedDate = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            // Add user to "User" role
            await _userManager.AddToRoleAsync(user, "User");

            // Generate activation code
            var activationCode = await _tokenService.GenerateActivationCodeAsync(user.Id);

            // Queue activation email asynchronously
            _backgroundTaskQueue.QueueBackgroundWorkItem(async token =>
            {
                using var scope = _serviceScopeFactory.CreateScope();

                try
                {
                    var emailService = scope.ServiceProvider.GetRequiredService<IAccountEmailService>();
                    await emailService.SendEmailActivationCodeAsync(user.Email, user.Id, activationCode);
                    _logger.LogInformation("Activation email sent to {Email}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send activation email to {Email}", user.Email);
                }
            });
        }

        return (result, user);
    }

    public async Task PasswordResetAsync(string email)
    {
        AppUser? user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
            // Generate token for password reset
            string resetToken = await _tokenService.GenerateSecureTokenAsync(user.Id, user.Email, "PasswordReset");

            // Queue password reset email in background
            _backgroundTaskQueue.QueueBackgroundWorkItem(async cancellationToken =>
            {
                try
                {
                    await _accountEmailService.SendPasswordResetEmailAsync(user.Email, user.Id, resetToken);
                    _logger.LogInformation("Password reset email sent to {Email}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
                }
            });
        }
        else
        {
            // Add delay to prevent user enumeration
            await Task.Delay(TimeSpan.FromMilliseconds(new Random().Next(200, 500)));
            _logger.LogInformation("Password reset requested for non-existent email: {Email}", email);
        }
    }
    

    public async Task<AppUser> GetUserByEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            throw new NotFoundUserExceptions();
        }

        return user;
    }
}