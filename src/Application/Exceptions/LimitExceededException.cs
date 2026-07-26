namespace Application.Exceptions;

public class LimitExceededException : Exception
{
    public LimitExceededException(string message)
        : base(message)
    {
    }
}