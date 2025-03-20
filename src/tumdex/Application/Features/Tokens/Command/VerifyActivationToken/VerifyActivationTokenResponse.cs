namespace Application.Features.Tokens.Command.VerifyActivationToken;

public class VerifyActivationTokenResponse
{
    public bool Success { get; set; }
    public string UserId { get; set; }
    public string Email { get; set; }
    public string Message { get; set; }
}