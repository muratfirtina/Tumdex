namespace Application.Exceptions;

public class NotFoundUserExceptions: Exception
{
    public NotFoundUserExceptions(): base("Username or password is wrong! Or email is wrong!")
    {
        
    }
    
    public NotFoundUserExceptions(string message): base(message)
    {
        
    }
    public NotFoundUserExceptions(string message, Exception inner): base(message, inner)
    {
        
    }
}