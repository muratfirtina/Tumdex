using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services.Security.JWT;

public class JwtConfiguration : IJwtConfiguration
{
    public SymmetricSecurityKey SecurityKey { get; set; }
    public SymmetricSecurityKey Issuer { get; set; }
    public SymmetricSecurityKey Audience { get; set; }
    public TokenValidationParameters TokenValidationParameters { get; set; }
}