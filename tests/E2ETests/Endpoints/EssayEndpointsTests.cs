using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Ai;
using Application.DTOs.Ai;
using Application.DTOs.Material;
using Application.DTOs.Note;
using Application.DTOs.RichTextDocument;
using Application.Essays;
using Application.Interfaces;
using Domain.Utility.Material;
using Domain.Utility.Note;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace E2ETests.Endpoints;

public class EssayEndpointsTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private const string Review = """
        {"taskResponse":{"score":70,"comment":"Both views, one briefly."},
         "coherence":{"score":80,"comment":"Clear paragraphs."},
         "vocabulary":{"score":60,"comment":"Repetition."},
         "grammar":{"score":75,"comment":"Mostly accurate."},
         "strengths":["Clear position"],"improvements":["Give examples"],
         "corrections":[{"original":"peoples thinks","correction":"people think","explanation":"Plural subject.","category":"grammar"}]}
        """;

    private static readonly string Essay = string.Join(' ', Enumerable.Repeat("Many peoples thinks that cities are better.", 6));

    private readonly ApiFactory _factory;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public EssayEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    // Every provider is replaced by one that answers with the review above
    private HttpClient ClientWithFakeAi(Guid userId)
    {
        var host = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAiProvider>();
            services.AddSingleton<IAiProvider, FakeEssayProvider>();
        }));

        var client = host.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        return client;
    }

    private static async Task<(Guid MaterialId, NoteResponseDto Note)> CreateNoteAsync(HttpClient client, NoteType type)
    {
        var material = await (await client.PostAsJsonAsync("/api/materials", new MaterialCreateDto
        {
            Title = "English writing",
            Type = MaterialType.practice,
            Status = MaterialStatus.inprocess,
        }, JsonOptions, Ct)).Content.ReadFromJsonAsync<MaterialResponseDto>(JsonOptions, Ct);

        var created = await client.PostAsJsonAsync($"/api/materials/{material!.Id}/notes",
            new NoteCreateDto { Title = "City life", Type = type }, JsonOptions, Ct);
        created.EnsureSuccessStatusCode();

        return (material.Id, (await created.Content.ReadFromJsonAsync<NoteResponseDto>(JsonOptions, Ct))!);
    }

    private static Task<HttpResponseMessage> WriteAsync(HttpClient client, Guid materialId, Guid noteId, string text, string? task = null) =>
        client.PatchAsJsonAsync($"/api/materials/{materialId}/notes/{noteId}", new NoteUpdateDto
        {
            EssayTask = task,
            TextContent = new RichTextDocument
            {
                Type = "doc",
                Content = [new Node { Type = "paragraph", Content = [new Node { Type = "text", Text = text }] }],
            },
        }, JsonOptions, Ct);

    [Fact]
    public async Task Essay_IsCheckedAndTheReviewIsKept()
    {
        var userId = Guid.NewGuid();
        var client = ClientWithFakeAi(userId);
        var (materialId, note) = await CreateNoteAsync(client, NoteType.essay);
        (await WriteAsync(client, materialId, note.Id, Essay, "Are cities better places to live?")).EnsureSuccessStatusCode();

        var response = await client.PostAsync($"/api/ai/materials/{materialId}/notes/{note.Id}/essay-review", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<EssayReviewResponseDto>(JsonOptions, Ct);
        result!.Note.EssayReview!.Score.ShouldBe(71);

        // After a reload the note still has its task and the last review
        var reloaded = await client.GetFromJsonAsync<NoteResponseDto>($"/api/materials/{materialId}/notes/{note.Id}", JsonOptions, Ct);
        reloaded!.EssayTask.ShouldBe("Are cities better places to live?");
        reloaded.EssayReview!.WordCount.ShouldBe(42);
        reloaded.EssayReview.Criteria.Single(x => x.Criterion == EssayCriterion.Coherence).Score.ShouldBe(80);
        reloaded.EssayReview.Corrections.Single().Correction.ShouldBe("people think");

        // Counted as an essay check in this month's AI usage
        var usage = await client.GetStringAsync("/api/ai/usage", Ct);
        usage.ShouldContain("EssayReview");
    }

    [Fact]
    public async Task TooShortEssay_IsRefusedWithoutUsingAi()
    {
        var client = ClientWithFakeAi(Guid.NewGuid());
        var (materialId, note) = await CreateNoteAsync(client, NoteType.essay);
        (await WriteAsync(client, materialId, note.Id, "Cities are big.")).EnsureSuccessStatusCode();

        var response = await client.PostAsync($"/api/ai/materials/{materialId}/notes/{note.Id}/essay-review", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        FakeEssayProvider.Prompts.ShouldNotContain(x => x.Contains("Cities are big."));
    }

    [Fact]
    public async Task OnlyEssays_HaveATaskAndCanBeChecked()
    {
        var client = ClientWithFakeAi(Guid.NewGuid());
        var (materialId, note) = await CreateNoteAsync(client, NoteType.text);

        // The task of another note is ignored, the database would refuse it
        (await WriteAsync(client, materialId, note.Id, Essay, "Task")).EnsureSuccessStatusCode();
        var reloaded = await client.GetFromJsonAsync<NoteResponseDto>($"/api/materials/{materialId}/notes/{note.Id}", JsonOptions, Ct);
        reloaded!.EssayTask.ShouldBeNull();

        var response = await client.PostAsync($"/api/ai/materials/{materialId}/notes/{note.Id}/essay-review", null, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TooLongTask_IsRefused()
    {
        var client = _factory.CreateAuthenticatedClient();
        var (materialId, note) = await CreateNoteAsync(client, NoteType.essay);

        var response = await WriteAsync(client, materialId, note.Id, Essay, new string('a', EssayRules.MaxTaskLength + 1));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SomeoneElsesEssay_IsNotFound()
    {
        var owner = _factory.CreateAuthenticatedClient();
        var (materialId, note) = await CreateNoteAsync(owner, NoteType.essay);

        var response = await ClientWithFakeAi(Guid.NewGuid())
            .PostAsync($"/api/ai/materials/{materialId}/notes/{note.Id}/essay-review", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private sealed class FakeEssayProvider : IAiProvider
    {
        // Every prompt sent from any host of this class
        public static readonly System.Collections.Concurrent.ConcurrentBag<string> Prompts = [];

        public string Name => "Anthropic";
        public bool IsConfigured => true;

        public Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
        {
            Prompts.Add(request.UserPrompt);
            return Task.FromResult(new AiCompletion
            {
                Text = Review,
                Provider = Name,
                Model = "fake",
                InputTokens = 1000,
                OutputTokens = 500,
            });
        }
    }
}
