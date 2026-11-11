using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Ai;
using Application.DTOs.Material;
using Application.DTOs.Note;
using Application.DTOs.Podcast;
using Application.DTOs.RichTextDocument;
using Application.Interfaces;
using Application.Jobs;
using Application.Podcasts;
using Domain.Utility.Material;
using Domain.Utility.Note;
using Domain.Utility.Podcast;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace E2ETests.Endpoints;

public class PodcastEndpointsTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private const string Script = """
        {"chapters":[
          {"section":0,"title":"Вступ","lines":[{"speaker":"host","text":"Привіт! Сьогодні про фотосинтез."},{"speaker":"guest","text":"Чудово."}]},
          {"section":1,"title":"Світлові реакції","lines":[{"speaker":"guest","text":"Де вони відбуваються?"}]}
        ]}
        """;

    private readonly ApiFactory _factory;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PodcastEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    // AI, voices and storage are fakes; the database and the endpoints are real
    private (HttpClient Client, WebApplicationFactory<Program> Host) Host(Guid userId)
    {
        var host = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAiProvider>();
            services.AddSingleton<IAiProvider, FakeScriptProvider>();
            services.RemoveAll<ISpeechSynthesizer>();
            services.AddSingleton<ISpeechSynthesizer, FakeSpeech>();
            services.RemoveAll<IAudioStorage>();
            services.AddSingleton<IAudioStorage, FakeStorage>();
        }));

        var client = host.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        return (client, host);
    }

    private static async Task<Guid> CreateMaterialWithNoteAsync(HttpClient client)
    {
        var material = await (await client.PostAsJsonAsync("/api/materials", new MaterialCreateDto
        {
            Title = "Фотосинтез",
            Type = MaterialType.lecture,
            Status = MaterialStatus.inprocess,
        }, JsonOptions, Ct)).Content.ReadFromJsonAsync<MaterialResponseDto>(JsonOptions, Ct);

        (await client.PatchAsJsonAsync($"/api/materials/{material!.Id}", new MaterialUpdateDto { Content = Doc(new string('m', 400)) }, JsonOptions, Ct))
            .EnsureSuccessStatusCode();

        var note = await (await client.PostAsJsonAsync($"/api/materials/{material.Id}/notes",
            new NoteCreateDto { Title = "Світлові реакції", Type = NoteType.text }, JsonOptions, Ct))
            .Content.ReadFromJsonAsync<NoteResponseDto>(JsonOptions, Ct);
        (await client.PatchAsJsonAsync($"/api/materials/{material.Id}/notes/{note!.Id}",
            new NoteUpdateDto { TextContent = Doc("They happen in the thylakoids.") }, JsonOptions, Ct)).EnsureSuccessStatusCode();

        return material.Id;
    }

    private static RichTextDocument Doc(string text) => new()
    {
        Type = "doc",
        Content = [new Node { Type = "paragraph", Content = [new Node { Type = "text", Text = text }] }],
    };

    [Fact]
    public async Task Podcast_IsWritten_Recorded_AndPlayedByChapters()
    {
        var userId = Guid.NewGuid();
        var (client, host) = Host(userId);
        var materialId = await CreateMaterialWithNoteAsync(client);

        (await client.GetAsync($"/api/materials/{materialId}/podcast", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var started = await client.PostAsJsonAsync($"/api/ai/materials/{materialId}/podcast", new PodcastGenerateDto { Language = "uk" }, JsonOptions, Ct);
        started.StatusCode.ShouldBe(HttpStatusCode.OK);
        var generating = await started.Content.ReadFromJsonAsync<PodcastDto>(JsonOptions, Ct);
        generating!.Status.ShouldBe(PodcastStatus.Generating);

        // Hangfire is off in tests: the job runs here, the way the server would run it
        using (var scope = host.Services.CreateScope())
        {
            await ActivatorUtilities.CreateInstance<PodcastAudioJob>(scope.ServiceProvider).Run(generating.Id);

            // The voices went into the cost log, every spoken character of the script
            var usage = await scope.ServiceProvider.GetRequiredService<AppDbContext>().SpeechUsages
                .SingleAsync(x => x.PodcastId == generating.Id, Ct);
            usage.Succeeded.ShouldBeTrue();
            usage.Characters.ShouldBe("Привіт! Сьогодні про фотосинтез.".Length + "Чудово.".Length + "Де вони відбуваються?".Length);
        }

        var ready = await client.GetFromJsonAsync<PodcastDto>($"/api/materials/{materialId}/podcast", JsonOptions, Ct);
        ready!.Status.ShouldBe(PodcastStatus.Ready);
        ready.AudioUrl.ShouldStartWith("https://audio.test/podcasts/");
        ready.Chapters.Count.ShouldBe(2);
        ready.Chapters[1].NoteId.ShouldNotBeNull();
        ready.Chapters[1].StartSeconds.ShouldBe(2 * 0.24, 0.01);
        ready.DurationSeconds.ShouldBe(3 * 0.24, 0.01);
        FakeStorage.Files.Keys.ShouldContain(x => x.StartsWith($"podcasts/{userId}/"));

        // Spent one podcast of this month's quota
        (await client.GetStringAsync("/api/ai/usage", Ct)).ShouldContain("PodcastGeneration");
    }

    [Fact]
    public async Task UnknownLanguage_IsRefused()
    {
        var (client, _) = Host(Guid.NewGuid());
        var materialId = await CreateMaterialWithNoteAsync(client);

        var response = await client.PostAsJsonAsync($"/api/ai/materials/{materialId}/podcast", new PodcastGenerateDto { Language = "xx" }, JsonOptions, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WithoutVoicesSetUp_PodcastsAreUnavailable()
    {
        var client = _factory.CreateAuthenticatedClient();
        var materialId = await CreateMaterialWithNoteAsync(client);

        var response = await client.PostAsJsonAsync($"/api/ai/materials/{materialId}/podcast", new PodcastGenerateDto { Language = "uk" }, JsonOptions, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task SomeoneElsesMaterial_IsNotFound()
    {
        var (owner, _) = Host(Guid.NewGuid());
        var materialId = await CreateMaterialWithNoteAsync(owner);
        var (stranger, _) = Host(Guid.NewGuid());

        (await stranger.PostAsJsonAsync($"/api/ai/materials/{materialId}/podcast", new PodcastGenerateDto { Language = "uk" }, JsonOptions, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.GetAsync($"/api/materials/{materialId}/podcast", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private sealed class FakeScriptProvider : IAiProvider
    {
        public string Name => "Anthropic";
        public bool IsConfigured => true;

        public Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AiCompletion { Text = Script, Provider = Name, Model = "fake", InputTokens = 3000, OutputTokens = 900 });
    }

    // Ten silent frames a line: 0.24 seconds
    private sealed class FakeSpeech : ISpeechSynthesizer
    {
        public bool IsConfigured => true;

        public Task<byte[]> SynthesizeAsync(string language, IReadOnlyList<PodcastLine> lines, CancellationToken cancellationToken = default)
        {
            var frame = new byte[144];
            frame[0] = 0xFF;
            frame[1] = 0xF3;
            frame[2] = 0x64;
            frame[3] = 0xC4;
            return Task.FromResult(Enumerable.Range(0, 10 * lines.Count).SelectMany(_ => frame).ToArray());
        }
    }

    private sealed class FakeStorage : IAudioStorage
    {
        public static readonly ConcurrentDictionary<string, byte[]> Files = new();

        public bool IsConfigured => true;

        public Task UploadAsync(string path, byte[] content, string contentType, CancellationToken cancellationToken = default)
        {
            Files[path] = content;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            Files.TryRemove(path, out _);
            return Task.CompletedTask;
        }

        public Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Uri GetReadUrl(string path, TimeSpan validFor) => new($"https://audio.test/{path}");
    }
}
