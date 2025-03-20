using Application.Exceptions;
using Application.Features.PhoneNumbers.Dtos;
using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;
using Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class PhoneNumberRepository : EfRepositoryBase<PhoneNumber, string, TumdexDbContext>, IPhoneNumberRepository
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    public PhoneNumberRepository(TumdexDbContext dbContext, UserManager<AppUser> userManager, IHttpContextAccessor httpContextAccessor) : base(dbContext)
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

    public async Task<PhoneNumber> AddPhoneAsync(CreatePhoneNumberDto phoneDto)
    {
        var user = await GetCurrentUserAsync();

        if (phoneDto.IsDefault)
            await UnsetCurrentDefaultPhone(user.Id);

        var phone = new PhoneNumber(phoneDto.Name)
        {
            UserId = user.Id,
            Name = phoneDto.Name,
            Number = phoneDto.Number,
            IsDefault = phoneDto.IsDefault
        };

        await AddAsync(phone);
        return phone;
    }

    public async Task<PhoneNumber> UpdatePhoneAsync(UpdatePhoneNumberDto phoneDto)
    {
        var user = await GetCurrentUserAsync();

        if (phoneDto.IsDefault)
            await UnsetCurrentDefaultPhone(user.Id);

        var phone = await GetAsync(p => p.Id == phoneDto.Id && p.UserId == user.Id);
        if (phone == null)
            throw new Exception("Phone number not found.");

        phone.Name = phoneDto.Name;
        phone.Number = phoneDto.Number;
        phone.IsDefault = phoneDto.IsDefault;

        await UpdateAsync(phone);
        return phone;
    }

    public async Task<bool> DeletePhoneAsync(string id)
    {
        var user = await GetCurrentUserAsync();
        var phone = await GetAsync(p => p.Id == id && p.UserId == user.Id);
        if (phone == null)
            return false;

        await DeleteAsync(phone);
        return true;
    }

    public async Task<IList<PhoneNumber>> GetUserPhonesAsync()
    {
        var user = await GetCurrentUserAsync();
        return await GetAllAsync(p => p.UserId == user.Id);
    }

    public async Task<bool> SetDefaultPhoneAsync(string id)
    {
        var user = await GetCurrentUserAsync();
        
        // First, find the phone to set as default
        var phoneToSetDefault = await GetAsync(p => p.Id == id && p.UserId == user.Id);
        if (phoneToSetDefault == null)
            return false;

        // Unset all default phones for this user
        await UnsetCurrentDefaultPhone(user.Id);
        
        // Set the selected phone as default
        phoneToSetDefault.IsDefault = true;
        await UpdateAsync(phoneToSetDefault);
        
        return true;
    }

    private async Task UnsetCurrentDefaultPhone(string userId)
    {
        // Get all default phones for this user
        var defaultPhones = await GetAllAsync(p => p.UserId == userId && p.IsDefault == true);
        
        // Update each phone to not be default
        foreach (var phone in defaultPhones)
        {
            phone.IsDefault = false;
            await UpdateAsync(phone);
        }
    }
}