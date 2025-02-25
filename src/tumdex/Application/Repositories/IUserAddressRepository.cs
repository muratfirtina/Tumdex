using Application.Dtos.User;
using Application.Features.UserAddresses.Dtos;
using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface IUserAddressRepository : IAsyncRepository<UserAddress, string>, IRepository<UserAddress, string>
{
    Task<UserAddress> AddAddressAsync(CreateUserAddressDto addressDto);
    Task<UserAddress> UpdateAddressAsync(UpdateUserAddressDto addressDto);
    Task<bool> DeleteAddressAsync(string id);
    Task<IList<UserAddress>> GetUserAddressesAsync();
    Task<bool> SetDefaultAddressAsync(string id);
}