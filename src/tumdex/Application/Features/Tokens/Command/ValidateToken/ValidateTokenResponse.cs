namespace Application.Features.Tokens.Command.ValidateToken;

public class ValidateTokenResponse
{
    public bool IsValid { get; set; }
    public string UserId { get; set; }
    public string Error { get; set; }
}