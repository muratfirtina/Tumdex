using Application.Features.Newsletters.Dtos;
using Application.Repositories;
using AutoMapper;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Paging;
using MediatR;

namespace Application.Features.Newsletters.Queries.GetList;

public class GetListNewsletterQuery : IRequest<GetListResponse<NewsletterListDto>>
{
    public PageRequest PageRequest { get; set; }

    public class GetListNewsletterQueryHandler : IRequestHandler<GetListNewsletterQuery, GetListResponse<NewsletterListDto>>
    {
        private readonly INewsletterRepository _newsletterRepository;
        private readonly IMapper _mapper;

        public GetListNewsletterQueryHandler(INewsletterRepository newsletterRepository, IMapper mapper)
        {
            _newsletterRepository = newsletterRepository;
            _mapper = mapper;
        }

        public async Task<GetListResponse<NewsletterListDto>> Handle(GetListNewsletterQuery request, CancellationToken cancellationToken)
        {
            IPaginate<Domain.Newsletter> newsletters = await _newsletterRepository.GetListAsync(
                index: request.PageRequest.PageIndex,
                size: request.PageRequest.PageSize,
                cancellationToken: cancellationToken
            );

            var mappedNewsletters = _mapper.Map<List<NewsletterListDto>>(newsletters.Items);
            return new GetListResponse<NewsletterListDto>
            {
                Items = mappedNewsletters,
                Size = newsletters.Size,
                Index = newsletters.Index,
                Count = newsletters.Count,
                Pages = newsletters.Pages,
                HasNext = newsletters.HasNext,
                HasPrevious = newsletters.HasPrevious
            };
        }
    }
}