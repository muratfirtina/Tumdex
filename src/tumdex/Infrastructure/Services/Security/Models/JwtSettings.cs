using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services.Security.Models;

public class JwtSettings
{
    public bool ValidateIssuerSigningKey { get; set; } = true;
    public bool ValidateIssuer { get; set; } = true;
    public bool ValidateAudience { get; set; } = true;
    public bool ValidateLifetime { get; set; } = true;
    public int ClockSkewMinutes { get; set; } = 0;
    public int AccessTokenLifetimeMinutes { get; set; } = 120;
    public int RefreshTokenLifetimeMinutes { get; set; } = 1440;
}

