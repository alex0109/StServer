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

// Units an AI request reserves, and how to read from its answer what it really took
// AllowCredits false: a background extra that must never spend the user's credits
// StopsWithClient true: a suggestion made while typing, dropped (and not counted) when the client leaves.
// Everything else runs to the end once a provider has it: the provider bills it either way
public record AiUnits<T>(
    int Reserved,
    Func<T, int>? Used = null,
    Func<T, int?>? VideoSeconds = null,
    bool AllowCredits = true,
    bool StopsWithClient = false);
