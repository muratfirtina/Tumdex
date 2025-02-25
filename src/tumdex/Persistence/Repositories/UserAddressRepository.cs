using Application.Exceptions;
using Application.Features.UserAddresses.Dtos;
using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class UserAddressRepository : EfRepositoryBase<UserAddress, string, TumdexDbContext>, IUserAddressRepository
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    public UserAddressRepository(TumdexDbContext context, UserManager<AppUser> userManager, IHttpContextAccessor httpContextAccessor) : base(context)
    {
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
    }
    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userName))
        {
            AppUser? user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.UserName == userName);

            if (user == null)
                throw new NotFoundUserExceptions();

            return user;
        }
        throw new Exception("Unexpected error occurred.");
    }

    public async Task<UserAddress> AddAddressAsync(CreateUserAddressDto addressDto)
    {
        var user = await GetCurrentUserAsync();

        if (addressDto.IsDefault)
            await UnsetCurrentDefaultAddress(user.Id);

        var address = new UserAddress(addressDto.Name)
        {
            UserId = user.Id,
            Name = addressDto.Name,
            AddressLine1 = addressDto.AddressLine1,
            AddressLine2 = addressDto.AddressLine2,
            City = addressDto.City,
            State = addressDto.State,
            PostalCode = addressDto.PostalCode,
            Country = addressDto.Country,
            IsDefault = addressDto.IsDefault
        };

        await AddAsync(address);
        return address;
    }

    public async Task<UserAddress> UpdateAddressAsync(UpdateUserAddressDto addressDto)
    {
        var user = await GetCurrentUserAsync();

        if (addressDto.IsDefault)
            await UnsetCurrentDefaultAddress(user.Id);

        var address = await GetAsync(a => a.Id == addressDto.Id && a.UserId == user.Id);
        if (address == null)
            throw new ("User address not found.");

        address.Name = addressDto.Name;
        address.AddressLine1 = addressDto.AddressLine1;
        address.AddressLine2 = addressDto.AddressLine2;
        address.City = addressDto.City;
        address.State = addressDto.State;
        address.PostalCode = addressDto.PostalCode;
        address.Country = addressDto.Country;
        address.IsDefault = addressDto.IsDefault;

        await UpdateAsync(address);
        return address;
    }

    public async Task<bool> DeleteAddressAsync(string id)
    {
        var user = await GetCurrentUserAsync();
        var address = await GetAsync(a => a.Id == id && a.UserId == user.Id);
        if (address == null)
            return false;

        await DeleteAsync(address);
        return true;
    }

    public async Task<IList<UserAddress>> GetUserAddressesAsync()
    {
        var user = await GetCurrentUserAsync();
        return await GetAllAsync(a => a.UserId == user.Id);
    }

    public async Task<bool> SetDefaultAddressAsync(string id)
    {
        var user = await GetCurrentUserAsync();
        var address = await GetAsync(a => a.Id == id && a.UserId == user.Id);
        if (address == null)
            return false;

        await UnsetCurrentDefaultAddress(user.Id);
        address.IsDefault = true;
        await UpdateAsync(address);
        return true;
    }

    private async Task UnsetCurrentDefaultAddress(string id)
    {
        var user = await GetCurrentUserAsync();
        var addresses = await GetAllAsync(a => a.UserId == user.Id);
        foreach (var address in addresses)
        {
            address.IsDefault = false;
            await UpdateAsync(address);
        }
    }
}