using Application.Abstraction.Services;
using Application.Abstraction.Services.Authentication;
using Application.Abstraction.Services.Email;
using Application.Abstraction.Services.Messaging;
using Application.Abstraction.Services.Tokens;
using Application.Abstraction.Services.Utilities;
using Application.Features.Events.User.UserRegister;
using Application.Messages.EmailMessages;
using Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
    private readonly ITokenService _tokenService;
    private readonly IMessageBroker _messageBroker;
    private readonly IAccountEmailService _accountEmailService;
    private readonly IRegistrationAndPasswordService _registrationAndPasswordService;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateUserCommandHandler> _logger;
    private readonly UserManager<AppUser> _userManager;
    private readonly IBackgroundTaskQueue _backgroundTaskQueue;
    private readonly IDistributedCache _cache;

    public CreateUserCommandHandler(
        IAuthService authService,
        IMediator mediator,
        ILogger<CreateUserCommandHandler> logger,
        UserManager<AppUser> userManager,
        IBackgroundTaskQueue backgroundTaskQueue, 
        IRegistrationAndPasswordService registrationAndPasswordService, 
        ITokenService tokenService, 
        IAccountEmailService accountEmailService, 
        IMessageBroker messageBroker,
        IDistributedCache cache)
    {
        _authService = authService;
        _mediator = mediator;
        _logger = logger;
        _userManager = userManager;
        _backgroundTaskQueue = backgroundTaskQueue;
        _registrationAndPasswordService = registrationAndPasswordService;
        _tokenService = tokenService;
        _accountEmailService = accountEmailService;
        _messageBroker = messageBroker;
        _cache = cache;
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
                    
                    // Mevcut aktivasyon kodu kontrolü
                    var cacheKey = $"email_activation_code_{existingUser.Id}";
                    var codeCacheJson = await _cache.GetStringAsync(cacheKey);
                    string existingActivationCode;
                    
                    if (!string.IsNullOrEmpty(codeCacheJson))
                    {
                        try
                        {
                            // Önbellekteki kodu kullan
                            var codeData = JsonSerializer.Deserialize<JsonElement>(codeCacheJson);
                            existingActivationCode = codeData.GetProperty("Code").GetString();
                            
                            _logger.LogInformation("Using existing activation code from Redis: {Code} for user {UserId}", 
                                existingActivationCode, existingUser.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error retrieving cached activation code, generating new one");
                            // Hata durumunda yeni kod oluştur
                            existingActivationCode = await _tokenService.GenerateActivationCodeAsync(existingUser.Id);
                        }
                    }
                    else
                    {
                        // Redis'te kod yoksa yeni oluştur
                        existingActivationCode = await _tokenService.GenerateActivationCodeAsync(existingUser.Id);
                    }
                    
                    // Queue activation code resend in background
                    _backgroundTaskQueue.QueueBackgroundWorkItem(async token =>
                    {
                        try
                        {
                            await _accountEmailService.ResendEmailActivationCodeAsync(existingUser.Email, existingActivationCode);
                            _logger.LogInformation("Activation email resent in background for {Email} with code {Code}", 
                                existingUser.Email, existingActivationCode);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to resend activation email in background for {Email}", 
                                existingUser.Email);
                        }
                    });
                    
                    return response;
                }
                
                // User already exists and is confirmed
                response.IsSuccess = false;
                response.Message = "An account with this email already exists.";
                return response;
            }

            // No existing user found, proceed with registration
            var (result, user) = await _registrationAndPasswordService.RegisterUserAsync(request);

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
            var activationToken = await _tokenService.GenerateSecureActivationTokenAsync(user.Id, user.Email);
            response.ActivationToken = activationToken;
            
            // Aktivasyon kodu oluştur
            var activationCode = await _tokenService.GenerateActivationCodeAsync(user.Id);
            
            // Log aktivasyon kodunu (yalnızca geliştirme ortamında)
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (environment == "Development")
            {
                _logger.LogInformation("DEBUG: Activation code for user {Email}: {Code}", user.Email, activationCode);
                response.DebugActivationCode = activationCode; // Response'da da saklayalım
            }
            
            // Redis'te kodun doğru kaydedildiğinden emin ol
            await EnsureActivationCodeInRedis(user.Id, activationCode);
            
            // E-posta gönderme mesajını oluştur
            var sendCommand = new SendActivationEmailMessage 
            {
                Email = user.Email,
                ActivationCode = activationCode,
                UserId = user.Id
            };

            // RabbitMQ kuyruğuna ekle
            await _messageBroker.SendAsync(sendCommand, "email-activation-code-queue");
            _logger.LogInformation("Activation code email request queued for {Email} with code {Code}", 
                user.Email, activationCode);
    
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
    
    private async Task EnsureActivationCodeInRedis(string userId, string activationCode)
    {
        try
        {
            var cacheKey = $"email_activation_code_{userId}";
            
            // Aktivasyon kodu verisini hazırla
            var codeData = new
            {
                Code = activationCode,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                AttemptCount = 0,
                EmailStatus = "pending"
            };
            
            // Redis'e kaydet
            await _cache.SetStringAsync(cacheKey, 
                JsonSerializer.Serialize(codeData),
                new DistributedCacheEntryOptions 
                { 
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) 
                });
            
            _logger.LogInformation("Saved activation code to Redis: {Code} for user {UserId}", 
                activationCode, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving activation code to Redis for user {UserId}", userId);
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