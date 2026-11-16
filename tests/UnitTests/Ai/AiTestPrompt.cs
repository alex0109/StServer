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

// The same answer, with a video the model has to watch
internal class AiTestVideoPrompt : IAiPrompt<string>
{
    private readonly AiTestPrompt _text = new();

    public AiFeature Feature => AiFeature.VideoTranscript;

    public AiRequest BuildRequest() =>
        _text.BuildRequest() with { Feature = Feature, Video = new AiVideo("https://www.youtube.com/watch?v=dQw4w9WgXcQ", 1800, 0.5) };

    public string Parse(string json) => _text.Parse(json);
}

internal class FakeAiProvider : IAiProviderStub
{
    private readonly Func<Task<AiCompletion>> _answer;

    public FakeAiProvider(string name, Func<Task<AiCompletion>> answer, bool isConfigured = true, bool canWatchVideo = false)
    {
        Name = name;
        _answer = answer;
        IsConfigured = isConfigured;
        CanWatchVideo = canWatchVideo;
    }

    public string Name { get; }
    public bool IsConfigured { get; }
    public bool CanWatchVideo { get; }
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
