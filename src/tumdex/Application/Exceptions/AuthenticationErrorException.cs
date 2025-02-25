namespace Application.Exceptions;

public class AuthenticationErrorException: Exception
{
    public AuthenticationErrorException(): base("Authentication Error")
    {
        
    }
    
    public AuthenticationErrorException(string message): base(message)
    {
        
    }
    public AuthenticationErrorException(string message, Exception inner): base(message, inner)
    {
        
    }
}