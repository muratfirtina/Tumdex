using Application.Features.Locations.Dtos;
using Application.Repositories;
using AutoMapper;
using Core.Application.Responses;
using MediatR;

namespace Application.Features.Locations.Queries.Districts;

public class GetListDistrictsQuery : IRequest<GetListResponse<DistrictDto>>
{
    public class GetListDistrictsQueryHandler : IRequestHandler<GetListDistrictsQuery, GetListResponse<DistrictDto>>
    {
        private readonly IDistrictRepository _districtRepository;
        private readonly IMapper _mapper;

        public GetListDistrictsQueryHandler(IDistrictRepository districtRepository, IMapper mapper)
        {
            _districtRepository = districtRepository;
            _mapper = mapper;
        }

        public async Task<GetListResponse<DistrictDto>> Handle(GetListDistrictsQuery request, CancellationToken cancellationToken)
        {
            var districts = await _districtRepository.GetAllDistrictsAsync();
            var districtDtos = _mapper.Map<List<DistrictDto>>(districts);
            
            return new GetListResponse<DistrictDto>
            {
                Items = districtDtos
            };
        }
    }
}