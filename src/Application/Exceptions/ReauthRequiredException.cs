namespace Application.Exceptions;

// The action needs a sign in from the last few minutes, not just a valid token
public class ReauthRequiredException : Exception
{
    public ReauthRequiredException(string message)
        : base(message)
    {
    }
}
