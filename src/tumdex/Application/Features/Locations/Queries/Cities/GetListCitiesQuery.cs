using Application.Features.Locations.Dtos;
using Application.Repositories;
using AutoMapper;
using Core.Application.Responses;
using MediatR;

namespace Application.Features.Locations.Queries.Cities;

public class GetListCitiesQuery : IRequest<GetListResponse<CityDto>>
{
    public class GetListCitiesQueryHandler : IRequestHandler<GetListCitiesQuery, GetListResponse<CityDto>>
    {
        private readonly ICityRepository _cityRepository;
        private readonly IMapper _mapper;

        public GetListCitiesQueryHandler(ICityRepository cityRepository, IMapper mapper)
        {
            _cityRepository = cityRepository;
            _mapper = mapper;
        }

        public async Task<GetListResponse<CityDto>> Handle(GetListCitiesQuery request, CancellationToken cancellationToken)
        {
            var cities = await _cityRepository.GetAllCitiesAsync();
            var cityDtos = _mapper.Map<List<CityDto>>(cities);
            
            return new GetListResponse<CityDto>
            {
                Items = cityDtos
            };
        }
    }
}