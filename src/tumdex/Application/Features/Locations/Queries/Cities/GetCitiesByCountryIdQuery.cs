using Application.Features.Locations.Dtos;
using Application.Repositories;
using AutoMapper;
using Core.Application.Responses;
using MediatR;

namespace Application.Features.Locations.Queries.Cities;

public class GetCitiesByCountryIdQuery : IRequest<GetListResponse<CityDto>>
{
    public int CountryId { get; set; }

    public class GetCitiesByCountryIdQueryHandler : IRequestHandler<GetCitiesByCountryIdQuery, GetListResponse<CityDto>>
    {
        private readonly ICityRepository _cityRepository;
        private readonly IMapper _mapper;

        public GetCitiesByCountryIdQueryHandler(ICityRepository cityRepository, IMapper mapper)
        {
            _cityRepository = cityRepository;
            _mapper = mapper;
        }

        public async Task<GetListResponse<CityDto>> Handle(GetCitiesByCountryIdQuery request, CancellationToken cancellationToken)
        {
            var cities = await _cityRepository.GetCitiesByCountryIdAsync(request.CountryId);
            var cityDtos = _mapper.Map<List<CityDto>>(cities);
            
            return new GetListResponse<CityDto>
            {
                Items = cityDtos
            };
        }
    }
}