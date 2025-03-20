using Application.Consts;
using Application.Features.UserAddresses.Dtos;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using MediatR;

namespace Application.Features.UserAddresses.Commands.Create;

public class CreateUserAddressCommand : IRequest<CreatedUserAddressCommandResponse>,ICacheRemoverRequest,ITransactionalRequest
{
    public string Name { get; set; }
    public string AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public int? CityId { get; set; }
    public int? DistrictId { get; set; }
    public string? PostalCode { get; set; }
    public int? CountryId { get; set; }
    public bool IsDefault { get; set; }

    public string CacheKey => "";
    public bool BypassCache => false;
    public string CacheGroupKey => CacheGroups.UserAddress;
    
    
    public class CreateUserAddressCommandHandler : IRequestHandler<CreateUserAddressCommand, CreatedUserAddressCommandResponse>
    {
        private readonly IUserAddressRepository _userAddressRepository;
        private readonly IMapper _mapper;

        public CreateUserAddressCommandHandler(IUserAddressRepository userAddressRepository, IMapper mapper)
        {
            _userAddressRepository = userAddressRepository;
            _mapper = mapper;
        }

        public async Task<CreatedUserAddressCommandResponse> Handle(CreateUserAddressCommand request, CancellationToken cancellationToken)
        {
            var addressDto = _mapper.Map<CreateUserAddressDto>(request);
            var address = await _userAddressRepository.AddAddressAsync(addressDto);
            return _mapper.Map<CreatedUserAddressCommandResponse>(address);
        }
    }
}
