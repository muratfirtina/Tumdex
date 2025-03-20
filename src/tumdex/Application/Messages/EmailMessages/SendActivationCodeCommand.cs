namespace Application.Messages.EmailMessages;

public class SendActivationCodeCommand
{
    public string Email { get; set; }
    public string UserId { get; set; }
    public string ActivationCode { get; set; }
    public DateTime RequestTime { get; set; } = DateTime.UtcNow;
}