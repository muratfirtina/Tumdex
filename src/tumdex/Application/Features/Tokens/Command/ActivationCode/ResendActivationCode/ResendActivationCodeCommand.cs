using Application.Abstraction.Services;
using Application.Abstraction.Services.Messaging;
using Application.Abstraction.Services.Tokens;
using Application.Messages.EmailMessages;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Tokens.Command.ActivationCode.ResendActivationCode;

public class ResendActivationCodeCommand : IRequest<ResendActivationCodeResponse>
{
    public string Email { get; set; }
    
    public class ResendActivationCodeCommandHandler : IRequestHandler<ResendActivationCodeCommand, ResendActivationCodeResponse>
{
    private readonly ITokenService _tokenService;
    private readonly IUserService _userService;
    private readonly IMessageBroker _messageBroker;
    private readonly ILogger<ResendActivationCodeCommandHandler> _logger;

    public ResendActivationCodeCommandHandler(
        IUserService userService, 
        ITokenService tokenService, 
        IMessageBroker messageBroker,
        ILogger<ResendActivationCodeCommandHandler> logger)
    {
        _userService = userService;
        _tokenService = tokenService;
        _messageBroker = messageBroker;
        _logger = logger;
    }

    public async Task<ResendActivationCodeResponse> Handle(ResendActivationCodeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userService.GetUserByEmailAsync(request.Email);
    
            if (user != null)
            {
                // Aktivasyon kodu oluştur
                var activationCode = await _tokenService.GenerateActivationCodeAsync(user.Id);
        
                // RabbitMQ üzerinden gönder
                var sendCommand = new SendActivationEmailMessage
                {
                    Email = request.Email,
                    ActivationCode = activationCode
                };
        
                await _messageBroker.SendAsync(sendCommand, "email-resend-activation-code-queue");
                _logger.LogInformation("Resend activation code queued for {Email}", request.Email);
        
                return new ResendActivationCodeResponse
                {
                    Success = true,
                    Message = "New activation code has been generated.",
                    UserId = user.Id,
                    ActivationCode = activationCode
                };
            }
    
            // Güvenlik açısından aynı yanıtı dön
            return new ResendActivationCodeResponse
            {
                Success = true,
                Message = "If your email exists in our system, an activation code has been sent."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ResendActivationCodeCommandHandler for {Email}", request.Email);
            return new ResendActivationCodeResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }
}
}