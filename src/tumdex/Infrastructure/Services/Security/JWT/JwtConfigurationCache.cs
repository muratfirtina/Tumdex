namespace Infrastructure.Services.Security.JWT;

public class JwtConfigurationCache
{
    public byte[] SecurityKeyBytes { get; set; }
    public byte[] IssuerBytes { get; set; }
    public byte[] AudienceBytes { get; set; }
}