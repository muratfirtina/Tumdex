using Application.Repositories;
using Core.Application.Pipelines.Caching;
using MediatR;

namespace Application.Features.UserAddresses.Commands.Delete;

public class DeleteUserAddressCommand:IRequest<bool>,ICacheRemoverRequest
{
    public string Id { get; set; }
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => "UserAddresses";
    


    public class DelereUserAddressCommandHandler : IRequestHandler<DeleteUserAddressCommand, bool>
    {
        private readonly IUserAddressRepository _addressRepository;

        public DelereUserAddressCommandHandler(IUserAddressRepository addressRepository)
        {
            _addressRepository = addressRepository;
        }

        public Task<bool> Handle(DeleteUserAddressCommand request, CancellationToken cancellationToken)
        {
            return _addressRepository.DeleteAddressAsync(request.Id);
        }
    }

}