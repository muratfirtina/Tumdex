using Application.Features.Locations.Dtos;
using Application.Repositories;
using AutoMapper;
using MediatR;

namespace Application.Features.Locations.Queries.Countries;

public class GetCountryByIdQuery : IRequest<CountryDto>
{
    public int Id { get; set; }

    public class GetCountryByIdQueryHandler : IRequestHandler<GetCountryByIdQuery, CountryDto>
    {
        private readonly ICountryRepository _countryRepository;
        private readonly IMapper _mapper;

        public GetCountryByIdQueryHandler(ICountryRepository countryRepository, IMapper mapper)
        {
            _countryRepository = countryRepository;
            _mapper = mapper;
        }

        public async Task<CountryDto> Handle(GetCountryByIdQuery request, CancellationToken cancellationToken)
        {
            var country = await _countryRepository.GetCountryByIdAsync(request.Id);
            return _mapper.Map<CountryDto>(country);
        }
    }
}