using Application.Features.UserAddresses.Dtos;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using MediatR;

namespace Application.Features.UserAddresses.Commands.Create;

public class CreateUserAddressCommand : IRequest<CreatedUserAddressCommandResponse>
{
    public string Name { get; set; }
    public string AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; }
    public bool IsDefault { get; set; }
    
    
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
