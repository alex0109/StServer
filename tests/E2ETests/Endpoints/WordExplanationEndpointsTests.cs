using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Ai;
using Application.DTOs.Ai;
using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Dictionary;
using Domain.Utility.Material;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace E2ETests.Endpoints;

public class WordExplanationEndpointsTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private const string Answer = """
        {"meaning":"A pet that barks.",
         "examples":[{"sentence":"Kan jeg klappe hunden din?","translation":"Can I pet your dog?","situation":"In the park"}],
         "collocations":["gå tur med hunden"],"tip":""}
        """;

    private readonly ApiFactory _factory;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WordExplanationEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithFakeAi(Guid userId)
    {
        var host = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAiProvider>();
            services.AddSingleton<IAiProvider, FakeProvider>();
        }));

        var client = host.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        return client;
    }

    private async Task<Guid> SeedWordAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var material = new Material
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Norsk",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn,
            IsActive = true,
            CreatedAt = now,
        };
        var dictionary = new Dictionary { Id = Guid.NewGuid(), UserId = userId, Material = material, Title = "Ord", CreatedAt = now };
        var entry = new DictionaryEntry
        {
            Id = Guid.NewGuid(),
            Dictionary = dictionary,
            Term = "hund",
            Translation = "dog",
            PartOfSpeech = PartOfSpeech.Noun,
            CreatedAt = now,
        };

        db.Materials.Add(material);
        db.Dictionaries.Add(dictionary);
        db.DictionaryEntries.Add(entry);
        await db.SaveChangesAsync(Ct);
        return entry.Id;
    }

    [Fact]
    public async Task Word_IsExplained_AndNothingIsStored()
    {
        var userId = Guid.NewGuid();
        var entryId = await SeedWordAsync(userId);

        var response = await ClientWithFakeAi(userId).PostAsync($"/api/ai/dictionaries/entries/{entryId}/explanation", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<WordExplanationDto>(JsonOptions, Ct);
        result!.Examples.Single().Translation.ShouldBe("Can I pet your dog?");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await db.DictionaryEntries.AsNoTracking().SingleAsync(x => x.Id == entryId, Ct);
        entry.Example.ShouldBeNull();
        // Counted against this month's word explanations
        (await db.AiUsages.AsNoTracking().CountAsync(x => x.UserId == userId && x.Feature == Domain.Utility.Ai.AiFeature.WordExplanation, Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task SomeoneElsesWord_IsNotFound()
    {
        var entryId = await SeedWordAsync(Guid.NewGuid());

        var response = await ClientWithFakeAi(Guid.NewGuid()).PostAsync($"/api/ai/dictionaries/entries/{entryId}/explanation", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private sealed class FakeProvider : IAiProvider
    {
        public string Name => "Anthropic";
        public bool IsConfigured => true;

        public Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AiCompletion { Text = Answer, Provider = Name, Model = "fake", InputTokens = 200, OutputTokens = 150 });
    }
}
