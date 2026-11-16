using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Application.Videos;
using Application.Ai.Prompts;
using Infrastructure.Ai;
using Microsoft.Extensions.Options;
using Shouldly;

namespace UnitTests.Videos;

// What Gemini receives for a video: the link as file data, clipped, before the text, small frames
public class GeminiVideoRequestTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public JsonNode? Body { get; private set; }
        public string? Url { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.ToString();
            Body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            const string answer = """
                {"candidates":[{"finishReason":"STOP","content":{"parts":[{"text":"{\"language\":\"en\",\"lines\":[]}"}]}}],
                 "modelVersion":"gemini-test","usageMetadata":{"promptTokenCount":120000,"candidatesTokenCount":900}}
                """;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    [Fact]
    public async Task Video_IsSentAsAClippedYouTubeLink()
    {
        var previous = Environment.GetEnvironmentVariable(AiProviderKeys.GeminiVariable);
        Environment.SetEnvironmentVariable(AiProviderKeys.GeminiVariable, "test-key");
        try
        {
            var handler = new CapturingHandler();
            var options = Options.Create(new AiProvidersOptions { Gemini = { Model = "gemini-flash-latest" } });
            var provider = new GeminiAiProvider(new Factory(handler), options, AiProviderKeys.FromEnvironment());

            provider.CanWatchVideo.ShouldBeTrue();
            var completion = await provider.CompleteAsync(
                new VideoTranscriptPrompt("dQw4w9WgXcQ").BuildRequest(), TestContext.Current.CancellationToken);

            var parts = handler.Body!["contents"]![0]!["parts"]!.AsArray();
            parts.Count.ShouldBe(2);
            parts[0]!["fileData"]!["fileUri"]!.GetValue<string>().ShouldBe("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
            parts[0]!["videoMetadata"]!["startOffset"]!.GetValue<string>().ShouldBe("0s");
            parts[0]!["videoMetadata"]!["endOffset"]!.GetValue<string>().ShouldBe($"{VideoRules.MaxSeconds}s");
            parts[0]!["videoMetadata"]!["fps"]!.GetValue<double>().ShouldBe(VideoRules.FramesPerSecond);
            parts[1]!["text"].ShouldNotBeNull();
            handler.Body["generationConfig"]!["mediaResolution"]!.GetValue<string>().ShouldBe("MEDIA_RESOLUTION_LOW");
            handler.Url.ShouldEndWith("/models/gemini-flash-latest:generateContent");
            completion.InputTokens.ShouldBe(120000);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AiProviderKeys.GeminiVariable, previous);
        }
    }
}
