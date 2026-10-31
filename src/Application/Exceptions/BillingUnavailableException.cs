namespace Application.Exceptions;

// No payment provider is configured, or it doesn't answer
public class BillingUnavailableException : Exception
{
    public BillingUnavailableException(string message)
        : base(message)
    {
    }
}
