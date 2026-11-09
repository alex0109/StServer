using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.Dictionary;
using Domain.Entities;
using Domain.Utility.Dictionary;
using Domain.Utility.Material;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace E2ETests.Endpoints;

public class WordQuizEndpointsTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WordQuizEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    // One material with a dictionary of the given words (term, translation); returns their ids in order
    private async Task<List<Guid>> SeedWordsAsync(Guid userId, params (string Term, string Translation)[] words)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        var material = new Material
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "German",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn,
            IsActive = true,
            CreatedAt = now,
        };
        var dictionary = new Dictionary { Id = Guid.NewGuid(), UserId = userId, Material = material, Title = "Words", CreatedAt = now };
        var entries = words.Select((word, position) => new DictionaryEntry
        {
            Id = Guid.NewGuid(),
            Dictionary = dictionary,
            Term = word.Term,
            Translation = word.Translation,
            PartOfSpeech = PartOfSpeech.Noun,
            Position = position,
            CreatedAt = now,
        }).ToList();

        db.Materials.Add(material);
        db.Dictionaries.Add(dictionary);
        db.DictionaryEntries.AddRange(entries);
        await db.SaveChangesAsync(Ct);

        return entries.Select(e => e.Id).ToList();
    }

    [Fact]
    public async Task Quiz_OffersTheTranslationAmongTheUsersOtherWords()
    {
        var userId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(userId);
        var ids = await SeedWordsAsync(userId, ("Hund", "собака"), ("Katze", "кішка"), ("Haus", "будинок"));

        var quiz = await client.GetFromJsonAsync<WordQuizDto>($"/api/dictionaries/entries/{ids[0]}/quiz", JsonOptions, Ct);

        quiz.ShouldNotBeNull();
        quiz.Kind.ShouldBe(WordQuizKind.Translation);
        quiz.Term.ShouldBe("Hund");
        quiz.Options.Select(o => o.Text).ShouldBe(["собака", "кішка", "будинок"], ignoreOrder: true);
        quiz.Options.Single(o => o.IsCorrect).Text.ShouldBe("собака");
    }

    [Fact]
    public async Task Quiz_OnlyWord_HasNoChoices()
    {
        var userId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(userId);
        var ids = await SeedWordsAsync(userId, ("Hund", "собака"));

        var quiz = await client.GetFromJsonAsync<WordQuizDto>($"/api/dictionaries/entries/{ids[0]}/quiz", JsonOptions, Ct);

        quiz!.Options.ShouldBeEmpty();
    }

    [Fact]
    public async Task Quiz_SomeoneElsesWord_IsNotFound_AndTheirWordsAreNotUsed()
    {
        var owner = Guid.NewGuid();
        var ownerIds = await SeedWordsAsync(owner, ("Hund", "собака"), ("Katze", "кішка"));
        var stranger = Guid.NewGuid();
        var strangerIds = await SeedWordsAsync(stranger, ("Baum", "дерево"));

        var foreign = await _factory.CreateAuthenticatedClient(stranger).GetAsync($"/api/dictionaries/entries/{ownerIds[0]}/quiz", Ct);
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // The stranger's only word gets no choices from the owner's dictionary
        var own = await _factory.CreateAuthenticatedClient(stranger)
            .GetFromJsonAsync<WordQuizDto>($"/api/dictionaries/entries/{strangerIds[0]}/quiz", JsonOptions, Ct);
        own!.Options.ShouldBeEmpty();
    }
}
