using Application.Services;
using AutoMapper;
using Core.Application.Pipelines.Transaction;
using MediatR;

namespace Application.Features.Newsletters.Commands.Unsubscribe;

public class UnsubscribeCommand : IRequest<UnsubscribeResponse>,ITransactionalRequest
{
    public string Email { get; set; }
    
    public class UnsubscribeCommandHandler : IRequestHandler<UnsubscribeCommand, UnsubscribeResponse>
    {
        private readonly INewsletterService _newsletterService;
        private readonly IMapper _mapper;

        public UnsubscribeCommandHandler(INewsletterService newsletterService, IMapper mapper)
        {
            _newsletterService = newsletterService;
            _mapper = mapper;
        }

        public async Task<UnsubscribeResponse> Handle(UnsubscribeCommand request, CancellationToken cancellationToken)
        {
            var newsletter = await _newsletterService.UnsubscribeAsync(request.Email);
            return _mapper.Map<UnsubscribeResponse>(newsletter);
        }
    }
}