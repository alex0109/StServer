using System.Net.Http.Headers;
using System.Text;
using Application.Exceptions;
using Application.Interfaces;
using Application.Podcasts;

namespace Infrastructure.Podcasts;

public class AzureSpeechSettings
{
    public string? Key { get; init; }
    public string? Region { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Key) && !string.IsNullOrWhiteSpace(Region);

    public static AzureSpeechSettings FromEnvironment() => new()
    {
        Key = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY"),
        Region = Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION"),
    };
}

// Azure AI Speech, text to speech over REST. Mono 24 kHz MP3 at 48 kbit/s: clear speech at
// about 360 KB a minute, and every answer has the same format, so parts join into one file
public class AzureSpeechSynthesizer : ISpeechSynthesizer
{
    public const string HttpClientName = "azure-speech";
    public const string OutputFormat = "audio-24khz-48kbitrate-mono-mp3";

    private readonly IHttpClientFactory _http;
    private readonly AzureSpeechSettings _settings;

    public AzureSpeechSynthesizer(IHttpClientFactory http, AzureSpeechSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public bool IsConfigured => _settings.IsConfigured;

    public async Task<byte[]> SynthesizeAsync(string language, IReadOnlyList<PodcastLine> lines, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) throw new AiUnavailableException("Speech is not set up");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://{_settings.Region}.tts.speech.microsoft.com/cognitiveservices/v1")
        {
            Content = new StringContent(SpeechSsml.Build(language, lines), Encoding.UTF8),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/ssml+xml") { CharSet = "utf-8" };
        request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.Key);
        request.Headers.Add("X-Microsoft-OutputFormat", OutputFormat);
        request.Headers.UserAgent.ParseAdd("Studytracker");

        using var response = await _http.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new AiUnavailableException($"Speech failed: {(int)response.StatusCode} {detail}".Trim());
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
