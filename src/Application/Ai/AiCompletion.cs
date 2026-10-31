namespace Application.Ai;

public record AiCompletion
{
    // Raw JSON text produced by the model
    public required string Text { get; init; }
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
}

public record AiResult<T>(T Value, AiCompletion Completion);
