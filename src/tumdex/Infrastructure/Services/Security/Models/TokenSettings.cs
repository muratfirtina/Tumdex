namespace Infrastructure.Services.Security.Models;

public class TokenSettings
{
    public string SecurityKey { get; set; }
    public string Issuer { get; set; }
    public string Audience { get; set; }
}