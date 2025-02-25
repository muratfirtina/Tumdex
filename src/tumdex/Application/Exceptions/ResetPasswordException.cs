namespace Application.Exceptions;

public class ResetPasswordException : Exception
{
    public ResetPasswordException() : base("Reset password is wrong!")
    {
    }
    
    public ResetPasswordException(string message) : base(message)
    {
    }
    
    public ResetPasswordException(string message, Exception innerException) : base(message, innerException)
    {
    }
}