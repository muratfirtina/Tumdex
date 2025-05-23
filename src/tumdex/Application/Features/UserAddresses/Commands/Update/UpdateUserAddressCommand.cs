using Application.Consts;
using Application.Features.UserAddresses.Dtos;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using MediatR;

namespace Application.Features.UserAddresses.Commands.Update;

public class UpdateUserAddressCommand:IRequest<UpdatedUserAddressCommandResponse>,ICacheRemoverRequest
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public int? CityId { get; set; }
    public int? DistrictId { get; set; }
    public string? PostalCode { get; set; }
    public int? CountryId { get; set; }
    public bool IsDefault { get; set; }

    public string CacheKey => $"UserAddress-{Id}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.UserAddresses;
    
    public class UpdateUserAddressCommandHandler : IRequestHandler<UpdateUserAddressCommand, UpdatedUserAddressCommandResponse>
    {
        private readonly IUserAddressRepository _userAddressRepository;
        private readonly IMapper _mapper;

        public UpdateUserAddressCommandHandler(IUserAddressRepository userAddressRepository, IMapper mapper)
        {
            _userAddressRepository = userAddressRepository;
            _mapper = mapper;
        }

        public async Task<UpdatedUserAddressCommandResponse> Handle(UpdateUserAddressCommand request, CancellationToken cancellationToken)
        {
            var addressDto = _mapper.Map<UpdateUserAddressDto>(request);
            var address = await _userAddressRepository.UpdateAddressAsync(addressDto);
            return _mapper.Map<UpdatedUserAddressCommandResponse>(address);
        }
    }
}
