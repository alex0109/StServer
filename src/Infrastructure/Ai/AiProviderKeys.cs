namespace Infrastructure.Ai;

// API keys come only from environment variables (or a secret store that fills them),
// never from appsettings, so they cannot end up in the repository or in a config dump
public sealed class AiProviderKeys
{
    public const string AnthropicVariable = "ANTHROPIC_API_KEY";
    public const string OpenAiVariable = "OPENAI_API_KEY";
    public const string GeminiVariable = "GEMINI_API_KEY";

    private readonly string? _anthropic;
    private readonly string? _openAi;
    private readonly string? _gemini;

    private AiProviderKeys(string? anthropic, string? openAi, string? gemini)
    {
        _anthropic = anthropic;
        _openAi = openAi;
        _gemini = gemini;
    }

    public static AiProviderKeys FromEnvironment() => new(
        Read(AnthropicVariable),
        Read(OpenAiVariable),
        Read(GeminiVariable));

    internal string? Anthropic => _anthropic;
    internal string? OpenAi => _openAi;
    internal string? Gemini => _gemini;

    // Keeps keys out of logs and debugger views if the object is ever printed
    public override string ToString() => "AiProviderKeys(***)";

    private static string? Read(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable)?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
