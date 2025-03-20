namespace Application.Messages.EmailMessages;

public class SendActivationEmailMessage : BaseMessage
{
    public string Email { get; set; }
    public string UserId { get; set; }
    public string ActivationCode { get; set; }
}