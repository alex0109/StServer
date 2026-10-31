namespace Application.Exceptions;

// The model answered with something that does not match the expected format
public class AiOutputException : Exception
{
    public AiOutputException(string message)
        : base(message)
    {
    }
}
