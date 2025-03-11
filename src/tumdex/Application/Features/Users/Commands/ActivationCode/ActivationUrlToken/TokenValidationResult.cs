namespace Application.Features.Users.Commands.ActivationCode.ActivationUrlToken;

public class TokenValidationResult
{
    public bool IsValid { get; set; }
    public string UserId { get; set; }
    public string Email { get; set; }
    public string Message { get; set; }
}