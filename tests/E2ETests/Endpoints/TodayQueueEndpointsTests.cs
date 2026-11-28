using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.Flashcard;
using Application.DTOs.Review;
using Domain.Entities;
using Domain.Utility.Flashcard;
using Domain.Utility.Material;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace E2ETests.Endpoints;

// "Today" holds only what was studied before and is due, once a day
public class TodayQueueEndpointsTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public TodayQueueEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    // A never studied word, one studied two days ago and due now, one already answered a minute ago;
    // returns the id of the one that belongs in "Today"
    private async Task<Guid> SeedAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        var material = new Material
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "From the library",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn,
            IsActive = true,
            CreatedAt = now,
        };
        var dictionary = new Dictionary { Id = Guid.NewGuid(), UserId = userId, Material = material, Title = "Words", CreatedAt = now };

        DictionaryEntry Word(string term, int position) => new()
        {
            Id = Guid.NewGuid(),
            Dictionary = dictionary,
            Term = term,
            Translation = term,
            Position = position,
            CreatedAt = now,
        };

        var neverStudied = Word("neu", 0);
        var dueAgain = Word("fällig", 1);
        var answeredToday = Word("heute", 2);

        dueAgain.Progress = new DictionaryEntryProgress
        {
            UserId = userId,
            Algorithm = FlashcardAlgorithm.Fsrs,
            State = FlashcardState.Review,
            Due = now.AddMinutes(-30),
            LastReviewAt = now.AddDays(-2),
            Reps = 1,
            CreatedAt = now.AddDays(-2),
        };
        // Answered today and scheduled again within minutes: it waits for tomorrow
        answeredToday.Progress = new DictionaryEntryProgress
        {
            UserId = userId,
            Algorithm = FlashcardAlgorithm.Fsrs,
            State = FlashcardState.Learning,
            Due = now.AddMinutes(-1),
            LastReviewAt = now.AddSeconds(-30),
            Reps = 1,
            CreatedAt = now,
        };

        db.Materials.Add(material);
        db.Dictionaries.Add(dictionary);
        db.DictionaryEntries.AddRange(neverStudied, dueAgain, answeredToday);
        await db.SaveChangesAsync(Ct);

        return dueAgain.Id;
    }

    [Fact]
    public async Task Today_HoldsOnlyWordsStudiedBefore_AndNotAnsweredToday()
    {
        var userId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(userId);
        var expected = await SeedAsync(userId);

        var today = await client.GetFromJsonAsync<ReviewTodayDto>("/api/review/today", JsonOptions, Ct);

        today.ShouldNotBeNull();
        today.DueWords.ShouldBe(1);
        today.WordsInSession.ShouldBe(1);

        var response = await client.PostAsJsonAsync(
            "/api/review/today/words", new FlashcardSessionCreateDto { Mode = FlashcardSessionMode.Due }, JsonOptions, Ct);
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<FlashcardSessionResponseDto>(JsonOptions, Ct);

        session.ShouldNotBeNull();
        session.Cards.Select(card => card.Id).ShouldBe([expected]);
    }
}
