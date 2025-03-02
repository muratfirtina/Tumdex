using Application.Services;
using Core.Application.Pipelines.Transaction;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Newsletters.Commands.SendMonthly;

public class SendMonthlyNewsletterCommand : IRequest<Unit>, ITransactionalRequest
{
    public bool IsTest { get; set; } = false;  // Parameter for test mode

    public class SendMonthlyNewsletterCommandHandler : IRequestHandler<SendMonthlyNewsletterCommand, Unit>
    {
        private readonly INewsletterService _newsletterService;
        private readonly ILogger<SendMonthlyNewsletterCommandHandler> _logger;

        public SendMonthlyNewsletterCommandHandler(
            INewsletterService newsletterService,
            ILogger<SendMonthlyNewsletterCommandHandler> logger)
        {
            _newsletterService = newsletterService;
            _logger = logger;
        }

        public async Task<Unit> Handle(SendMonthlyNewsletterCommand request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting to send {Mode} newsletter via API request", 
                    request.IsTest ? "test" : "monthly");
                
                // Queue the newsletter to be sent in the background
                await _newsletterService.QueueSendMonthlyNewsletterAsync(request.IsTest);
                
                _logger.LogInformation("Newsletter sending task has been queued successfully");
                return Unit.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while queueing newsletter");
                throw;
            }
        }
    }
}