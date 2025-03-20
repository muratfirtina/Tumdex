using Application.Features.Locations.Dtos;
using Application.Repositories;
using AutoMapper;
using Core.Application.Responses;
using MediatR;

namespace Application.Features.Locations.Queries.Countries;

public class GetListCountriesQuery : IRequest<GetListResponse<CountryDto>>
{
    public class GetListCountriesQueryHandler : IRequestHandler<GetListCountriesQuery, GetListResponse<CountryDto>>
    {
        private readonly ICountryRepository _countryRepository;
        private readonly IMapper _mapper;

        public GetListCountriesQueryHandler(ICountryRepository countryRepository, IMapper mapper)
        {
            _countryRepository = countryRepository;
            _mapper = mapper;
        }

        public async Task<GetListResponse<CountryDto>> Handle(GetListCountriesQuery request, CancellationToken cancellationToken)
        {
            var countries = await _countryRepository.GetAllCountriesAsync();
            var countryDtos = _mapper.Map<List<CountryDto>>(countries);
            
            return new GetListResponse<CountryDto>
            {
                Items = countryDtos
            };
        }
    }
}