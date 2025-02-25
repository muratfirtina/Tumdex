using Application.Services;
using Core.Application.Pipelines.Transaction;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Newsletters.Commands.SendMonthly;

public class SendMonthlyNewsletterCommand : IRequest<Unit>,ITransactionalRequest
{
    public bool IsTest { get; set; } = false;  // Test modu için parametre

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
                _logger.LogInformation("Starting to send {Mode} newsletter", request.IsTest ? "test" : "monthly");
                await _newsletterService.SendMonthlyNewsletterAsync();
                _logger.LogInformation("Newsletter sent successfully");
                return Unit.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending newsletter");
                throw;
            }
        }
    }
}