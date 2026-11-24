using Application.Ai;

namespace Application.Exceptions;

// The model answered with something that does not match the expected format
public class AiOutputException : Exception
{
    public AiOutputException(string message)
        : base(message)
    {
    }

    // The provider's answer that couldn't be read: it was paid for, so the usage gate settles it
    public AiOutputException(string message, AiCompletion completion)
        : base(message)
    {
        Completion = completion;
    }

    public AiCompletion? Completion { get; }
}
