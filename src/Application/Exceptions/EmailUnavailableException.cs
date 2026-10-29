namespace Application.Exceptions;

public class EmailUnavailableException : Exception
{
    public EmailUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
