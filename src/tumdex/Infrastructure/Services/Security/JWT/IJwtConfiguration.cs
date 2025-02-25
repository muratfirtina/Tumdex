using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services.Security.JWT;

public interface IJwtConfiguration
{
    SymmetricSecurityKey SecurityKey { get; }
    SymmetricSecurityKey Issuer { get; }
    SymmetricSecurityKey Audience { get; }
    TokenValidationParameters TokenValidationParameters { get; }
}