namespace Application.Features.Users.Exceptions;

public class PasswordChangeFailedException : Exception
{
    public List<string> Errors { get; }

    public PasswordChangeFailedException(List<string> errors) : base("Failed to change password")
    {
        Errors = errors;
    }
}