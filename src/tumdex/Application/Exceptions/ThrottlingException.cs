namespace Application.Exceptions;
public class ThrottlingException : Exception
{
    public ThrottlingException(string message) : base(message)
    {
        
    }
}