using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using MediatR;

namespace Application.Features.PhoneNumbers.Queries.GetList;

public class GetListPhoneNumberQuery : IRequest<GetListResponse<GetListPhoneNumberQueryResponse>>,ICachableRequest
{
    
    public string CacheKey => "GetListPhoneNumberQuery()";
    public bool BypassCache => false;
    public string? CacheGroupKey => "PhoneNumbers";
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(5);
    public class GetListPhoneNumberQueryHandler : IRequestHandler<GetListPhoneNumberQuery, GetListResponse<GetListPhoneNumberQueryResponse>>
    {
        private readonly IPhoneNumberRepository _phoneNumberRepository;
        private readonly IMapper _mapper;

        public GetListPhoneNumberQueryHandler(IPhoneNumberRepository phoneNumberRepository, IMapper mapper)
        {
            _phoneNumberRepository = phoneNumberRepository;
            _mapper = mapper;
        }

        public async Task<GetListResponse<GetListPhoneNumberQueryResponse>> Handle(GetListPhoneNumberQuery request, CancellationToken cancellationToken)
        {
            var phoneNumbers = await _phoneNumberRepository.GetUserPhonesAsync();
            var response = _mapper.Map<GetListResponse<GetListPhoneNumberQueryResponse>>(phoneNumbers);
            return response;
        }
        
    }
}