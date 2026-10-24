namespace Application.Exceptions;

// One provider could not answer, the next provider is tried.
// Messages must never contain keys or user content, they go to logs
public class AiProviderException : Exception
{
    public AiProviderException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
