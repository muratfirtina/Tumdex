using Application.Abstraction.Services;
using Application.Services;
using Domain.Identity;

namespace Persistence.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IUserService _userService;

    public CurrentUserService(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<AppUser?> GetCurrentUserAsync()
    {
        return await _userService.GetCurrentUserAsync();
    }

    public async Task<string> GetCurrentUserIdAsync()
    {
        var user = await GetCurrentUserAsync();
        return user?.Id ?? "anonymous"; // Kullanıcı null ise "anonymous" dön
    }

    public async Task<string> GetCurrentUserNameAsync()
    {
        var user = await GetCurrentUserAsync();
        return user?.UserName ?? "anonymous"; // Kullanıcı null ise "anonymous" dön
    }
}