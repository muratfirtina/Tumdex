using System;
using System.Threading.Tasks;
using Application.Abstraction.Services.Email;
using Application.Messages.EmailMessages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Consumers.EmailConsumers
{
    public class SendPasswordResetEmailConsumer : IConsumer<SendPasswordResetEmailMessage>
    {
        private readonly IAccountEmailService _accountEmailService;
        private readonly ILogger<SendPasswordResetEmailConsumer> _logger;

        public SendPasswordResetEmailConsumer(
            IAccountEmailService accountEmailService,
            ILogger<SendPasswordResetEmailConsumer> logger)
        {
            _accountEmailService = accountEmailService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SendPasswordResetEmailMessage> context)
        {
            var message = context.Message;
            
            try
            {
                _logger.LogInformation("Processing password reset email for {Email}", message.Email);
                
                await _accountEmailService.SendPasswordResetEmailAsync(
                    message.Email, 
                    message.UserId, 
                    message.ResetToken);
                
                _logger.LogInformation("Password reset email sent to {Email}", message.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset email to {Email}", message.Email);
                
                // Hata oluşursa, mesajı tekrar işlenmek üzere kuyruğa ekle
                throw;
            }
        }
    }
}