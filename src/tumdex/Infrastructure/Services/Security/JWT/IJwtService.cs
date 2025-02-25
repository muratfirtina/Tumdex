namespace Infrastructure.Services.Security.JWT;

public interface IJwtService
{
    Task<JwtConfiguration> GetJwtConfigurationAsync();
}