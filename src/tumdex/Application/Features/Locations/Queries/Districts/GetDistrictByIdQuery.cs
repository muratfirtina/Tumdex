using Application.Features.Locations.Dtos;
using Application.Repositories;
using AutoMapper;
using MediatR;

namespace Application.Features.Locations.Queries.Districts;

public class GetDistrictByIdQuery : IRequest<DistrictDto>
{
    public int Id { get; set; }

    public class GetDistrictByIdQueryHandler : IRequestHandler<GetDistrictByIdQuery, DistrictDto>
    {
        private readonly IDistrictRepository _districtRepository;
        private readonly IMapper _mapper;

        public GetDistrictByIdQueryHandler(IDistrictRepository districtRepository, IMapper mapper)
        {
            _districtRepository = districtRepository;
            _mapper = mapper;
        }

        public async Task<DistrictDto> Handle(GetDistrictByIdQuery request, CancellationToken cancellationToken)
        {
            var district = await _districtRepository.GetDistrictByIdAsync(request.Id);
            return _mapper.Map<DistrictDto>(district);
        }
    }
}