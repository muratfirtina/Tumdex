using Application.Services;
using AutoMapper;
using Core.Application.Pipelines.Transaction;
using MediatR;

namespace Application.Features.Newsletters.Commands.Subscribe;

public class SubscribeCommand : IRequest<SubscribeResponse>,ITransactionalRequest
{
    public string Email { get; set; }
    
    public class SubscribeCommandHandler : IRequestHandler<SubscribeCommand, SubscribeResponse>
    {
        private readonly INewsletterService _newsletterService;
        private readonly IMapper _mapper;

        public SubscribeCommandHandler(INewsletterService newsletterService, IMapper mapper)
        {
            _newsletterService = newsletterService;
            _mapper = mapper;
        }

        public async Task<SubscribeResponse> Handle(SubscribeCommand request, CancellationToken cancellationToken)
        {
            var newsletter = await _newsletterService.SubscribeAsync(request.Email, "Manual");
            return _mapper.Map<SubscribeResponse>(newsletter);
        }
    }
}