namespace Application.Messages.EmailMessages;

public class SendPasswordResetEmailMessage
{
    public string Email { get; set; }
    public string UserId { get; set; }
    public string ResetToken { get; set; }
}