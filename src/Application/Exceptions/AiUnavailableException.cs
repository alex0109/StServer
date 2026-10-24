namespace Application.Exceptions;

// Every configured provider failed or AI is switched off
public class AiUnavailableException : Exception
{
    public AiUnavailableException(string message)
        : base(message)
    {
    }
}
