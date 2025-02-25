using Application.Dtos.Token;
using Domain.Identity;

namespace Application.Tokens;

public interface ITokenHandler
{
    // Belirli bir süre için access token oluşturur
    Task<Token> CreateAccessTokenAsync(int second, AppUser appUser);
    
    // Varsayılan süre (120 dakika) için access token oluşturur
    Task<Token> CreateAccessTokenAsync(AppUser appUser);
    
    // Refresh token oluşturur - Bu senkron kalabilir çünkü
    // sadece güvenli rastgele sayı üretiyor
    string CreateRefreshToken();
}