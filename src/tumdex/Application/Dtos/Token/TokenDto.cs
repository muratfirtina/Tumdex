namespace Application.Dtos.Token;

public class TokenDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiration { get; set; }
    public DateTime RefreshTokenExpiration { get; set; }
    public string UserId { get; set; } = string.Empty;

    // Conversion method to simplify Token/TokenDto conversion
    public Token ToToken()
    {
        return new Token
        {
            AccessToken = AccessToken,
            RefreshToken = RefreshToken,
            Expiration = AccessTokenExpiration,
            UserId = UserId
        };
    }
}