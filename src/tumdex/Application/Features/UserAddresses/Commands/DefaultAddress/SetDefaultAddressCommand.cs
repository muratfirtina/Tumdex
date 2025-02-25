using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using MediatR;

namespace Application.Features.UserAddresses.Commands.DefaultAddress;

public class SetDefaultAddressCommand : IRequest<SetDefaultAddressCommandResponse>,ICacheRemoverRequest
{
    public string Id { get; set; }
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => "UserAddresses";
    
    public class SetDefaultAddressCommandHandler : IRequestHandler<SetDefaultAddressCommand, SetDefaultAddressCommandResponse>
    {
        private readonly IUserAddressRepository _userAddressRepository;
        private readonly IMapper _mapper;

        public SetDefaultAddressCommandHandler(IUserAddressRepository userAddressRepository, IMapper mapper)
        {
            _userAddressRepository = userAddressRepository;
            _mapper = mapper;
        }

        public async Task<SetDefaultAddressCommandResponse> Handle(SetDefaultAddressCommand request, CancellationToken cancellationToken)
        {
            var result = await _userAddressRepository.SetDefaultAddressAsync(request.Id);
            return new SetDefaultAddressCommandResponse { Success = result };
        }
    }
}