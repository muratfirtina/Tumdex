namespace Application.Dtos.Token;

public class Token
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    
    // Create an empty constructor to ensure the class can be instantiated
    public Token() { }
    
    // Create a constructor from TokenDto for easier conversion
    public Token(TokenDto tokenDto)
    {
        AccessToken = tokenDto.AccessToken;
        RefreshToken = tokenDto.RefreshToken;
        Expiration = tokenDto.AccessTokenExpiration;
        UserId = tokenDto.UserId;
    }
}