using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Ai;
using Application.DTOs.Ai;
using Application.DTOs.Material;
using Application.DTOs.Question;
using Application.DTOs.Video;
using Application.Interfaces;
using Domain.Utility.Material;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace E2ETests.Endpoints;

public class VideoEndpointsTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public VideoEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    // The AI is a fake that "watches" videos; the database and the endpoints are real
    private HttpClient Client(Guid userId)
    {
        var host = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAiProvider>();
            services.AddSingleton<IAiProvider, FakeGemini>();
        }));

        var client = host.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        return client;
    }

    private static async Task<Guid> CreateMaterialAsync(HttpClient client, string link)
    {
        var material = await (await client.PostAsJsonAsync("/api/materials", new MaterialCreateDto
        {
            Title = "Photosynthesis explained",
            Type = MaterialType.video,
            Status = MaterialStatus.inprocess,
        }, JsonOptions, Ct)).Content.ReadFromJsonAsync<MaterialResponseDto>(JsonOptions, Ct);

        // The link is set the way the page sets it: an edit of the material
        (await client.PatchAsJsonAsync($"/api/materials/{material!.Id}", new MaterialUpdateDto { Link = link }, JsonOptions, Ct))
            .EnsureSuccessStatusCode();
        return material.Id;
    }

    [Fact]
    public async Task Video_GetsSubtitles_ThenAQuizThatLeadsBackToTheVideo()
    {
        var client = Client(Guid.NewGuid());
        var materialId = await CreateMaterialAsync(client, "https://youtu.be/dQw4w9WgXcQ");

        (await client.GetAsync($"/api/materials/{materialId}/video", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var processed = await client.PostAsync($"/api/ai/materials/{materialId}/video", null, Ct);
        processed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var video = await processed.Content.ReadFromJsonAsync<MaterialVideoDto>(JsonOptions, Ct);
        video!.VideoId.ShouldBe("dQw4w9WgXcQ");
        video.Segments.Select(x => x.Start).ShouldBe([2d, 75d]);

        var saved = await client.GetFromJsonAsync<MaterialVideoDto>($"/api/materials/{materialId}/video", JsonOptions, Ct);
        saved!.Language.ShouldBe("en");

        var quiz = await client.PostAsJsonAsync($"/api/ai/materials/{materialId}/questions",
            new QuestionGenerateDto { Count = 1, FromVideo = true }, JsonOptions, Ct);
        quiz.StatusCode.ShouldBe(HttpStatusCode.OK);
        var questions = await quiz.Content.ReadFromJsonAsync<List<QuestionResponseDto>>(JsonOptions, Ct);
        questions!.Single().VideoSeconds.ShouldBe(75);

        // Spent one video of this month's quota
        (await client.GetStringAsync("/api/ai/usage", Ct)).ShouldContain("VideoTranscript");
    }

    [Fact]
    public async Task MaterialWithoutAYouTubeLink_IsRefused()
    {
        var client = Client(Guid.NewGuid());
        var materialId = await CreateMaterialAsync(client, "https://example.com/lecture");

        (await client.PostAsync($"/api/ai/materials/{materialId}/video", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SomeoneElsesMaterial_IsNotFound()
    {
        var materialId = await CreateMaterialAsync(Client(Guid.NewGuid()), "https://youtu.be/dQw4w9WgXcQ");
        var stranger = Client(Guid.NewGuid());

        (await stranger.PostAsync($"/api/ai/materials/{materialId}/video", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.GetAsync($"/api/materials/{materialId}/video", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // Subtitles for a video request, one question for a text one
    private sealed class FakeGemini : IAiProvider
    {
        public string Name => "Gemini";
        public bool IsConfigured => true;
        public bool CanWatchVideo => true;

        public Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
        {
            var text = request.Video is not null
                ? """{"language":"en","lines":[{"start":"00:02","text":"Plants turn light into sugar."},{"start":"01:15","text":"This happens in the chloroplasts."}]}"""
                : """{"questions":[{"type":"open","title":"Where does photosynthesis happen?","difficulty":"easy","answer":"In the chloroplasts","options":[],"isTrue":false,"explanation":"","videoAt":"01:15"}]}""";

            return Task.FromResult(new AiCompletion { Text = text, Provider = Name, Model = "fake", InputTokens = 1000, OutputTokens = 200 });
        }
    }
}
