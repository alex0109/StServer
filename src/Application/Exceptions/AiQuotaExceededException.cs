namespace Application.Exceptions;

public class AiQuotaExceededException : Exception
{
    public AiQuotaExceededException(string message)
        : base(message)
    {
    }
}
