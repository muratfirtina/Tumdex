using Infrastructure.Services.Security.Models;

namespace Infrastructure.Services.Token;

public interface ITokenSettingsService
{
    Task<TokenSettings> GetTokenSettingsAsync();
}