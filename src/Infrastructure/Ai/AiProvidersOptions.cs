namespace Infrastructure.Ai;

// Models and endpoints of every provider. Keys are not here on purpose, see AiProviderKeys.
// Models have no defaults in code: the only place they are set is "AiProviders" in appsettings.json
public class AiProvidersOptions
{
    public const string SectionName = "AiProviders";

    public AiProviderSettings Anthropic { get; set; } = new();
    public AiProviderSettings OpenAi { get; set; } = new() { BaseUrl = "https://api.openai.com/v1" };
    public AiProviderSettings Gemini { get; set; } = new() { BaseUrl = "https://generativelanguage.googleapis.com/v1beta" };
}

public class AiProviderSettings
{
    public string? Model { get; set; }
    public string? BaseUrl { get; set; }

    // Keys are sent only over HTTPS, a misconfigured plain HTTP endpoint disables the provider
    internal bool HasSecureBaseUrl =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
