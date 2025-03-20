using Application.Features.Locations.Dtos;
using Application.Repositories;
using AutoMapper;
using MediatR;

namespace Application.Features.Locations.Queries.Cities;

public class GetCityByIdQuery : IRequest<CityDto>
{
    public int Id { get; set; }

    public class GetCityByIdQueryHandler : IRequestHandler<GetCityByIdQuery, CityDto>
    {
        private readonly ICityRepository _cityRepository;
        private readonly IMapper _mapper;

        public GetCityByIdQueryHandler(ICityRepository cityRepository, IMapper mapper)
        {
            _cityRepository = cityRepository;
            _mapper = mapper;
        }

        public async Task<CityDto> Handle(GetCityByIdQuery request, CancellationToken cancellationToken)
        {
            var city = await _cityRepository.GetCityByIdAsync(request.Id);
            return _mapper.Map<CityDto>(city);
        }
    }
}