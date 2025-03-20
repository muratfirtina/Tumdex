namespace Application.Features.Tokens.Command.ActivationCode.ResendActivationCode;

public class ResendActivationCodeResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string UserId { get; set; }
    public string ActivationCode { get; set; }
}