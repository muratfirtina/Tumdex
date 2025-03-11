namespace Application.Queue.Email;

public enum EmailType
{
    Generic,
    PasswordReset,
    EmailConfirmation,
    Newsletter,
    OrderConfirmation,
    OrderUpdate,
    EmailActivation
}