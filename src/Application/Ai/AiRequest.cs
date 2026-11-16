using System.Text.Json;
using Domain.Utility.Ai;

namespace Application.Ai;

// Provider neutral request: rules and content are prepared by a prompt, providers only transport them
public record AiRequest
{
    public required AiFeature Feature { get; init; }
    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }
    // JSON schema the answer must follow, providers use their structured output mode with it
    public required JsonElement OutputSchema { get; init; }
    public required int MaxOutputTokens { get; init; }
    public AiEffort Effort { get; init; } = AiEffort.Medium;
    // A video the model watches before it answers; only providers that can watch one are used
    public AiVideo? Video { get; init; }
}

// A public YouTube video, processed from the start up to EndSeconds at a few frames per second
public record AiVideo(string Url, int EndSeconds, double FramesPerSecond);
