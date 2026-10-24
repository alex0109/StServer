using System.Text.Json;
using Application.Ai;
using Application.Ai.Prompts;
using Domain.Utility.Ai;

namespace UnitTests.Ai;

internal class AiTestPrompt : IAiPrompt<string>
{
    public AiFeature Feature => AiFeature.QuestionGeneration;

    public AiRequest BuildRequest() => new()
    {
        Feature = Feature,
        SystemPrompt = "system",
        UserPrompt = "user",
        OutputSchema = JsonSerializer.SerializeToElement(new { type = "object" }),
        MaxOutputTokens = 100,
    };

    public string Parse(string json) => AiJson.Read<Answer>(json).Value ?? string.Empty;

    private record Answer(string? Value);
}

internal class FakeAiProvider : IAiProviderStub
{
    private readonly Func<Task<AiCompletion>> _answer;

    public FakeAiProvider(string name, Func<Task<AiCompletion>> answer, bool isConfigured = true)
    {
        Name = name;
        _answer = answer;
        IsConfigured = isConfigured;
    }

    public string Name { get; }
    public bool IsConfigured { get; }
    public int Calls { get; private set; }

    public Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
    {
        Calls++;
        cancellationToken.ThrowIfCancellationRequested();
        return _answer();
    }

    public static AiCompletion Completion(string provider, string text) => new()
    {
        Text = text,
        Provider = provider,
        Model = "model",
        InputTokens = 10,
        OutputTokens = 5,
    };
}

internal interface IAiProviderStub : Application.Interfaces.IAiProvider;
