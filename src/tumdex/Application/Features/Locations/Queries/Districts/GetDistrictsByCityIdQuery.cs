using Application.Features.Locations.Dtos;
using Application.Repositories;
using AutoMapper;
using Core.Application.Responses;
using MediatR;

namespace Application.Features.Locations.Queries.Districts;

public class GetDistrictsByCityIdQuery : IRequest<GetListResponse<DistrictDto>>
{
    public int CityId { get; set; }

    public class GetDistrictsByCityIdQueryHandler : IRequestHandler<GetDistrictsByCityIdQuery, GetListResponse<DistrictDto>>
    {
        private readonly IDistrictRepository _districtRepository;
        private readonly IMapper _mapper;

        public GetDistrictsByCityIdQueryHandler(IDistrictRepository districtRepository, IMapper mapper)
        {
            _districtRepository = districtRepository;
            _mapper = mapper;
        }

        public async Task<GetListResponse<DistrictDto>> Handle(GetDistrictsByCityIdQuery request, CancellationToken cancellationToken)
        {
            var districts = await _districtRepository.GetDistrictsByCityIdAsync(request.CityId);
            var districtDtos = _mapper.Map<List<DistrictDto>>(districts);
            
            return new GetListResponse<DistrictDto>
            {
                Items = districtDtos
            };
        }
    }
}