using Domain.Identity;

namespace Application.Services;

public interface ICurrentUserService
{
    Task<AppUser> GetCurrentUserAsync();
    Task<string> GetCurrentUserIdAsync();
    Task<string> GetCurrentUserNameAsync();
}