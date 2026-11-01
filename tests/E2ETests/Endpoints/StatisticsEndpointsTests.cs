using System.Net;
using System.Net.Http.Json;
using Application.DTOs.Statistics;
using Domain.Entities;
using Domain.Utility.Material;
using Domain.Utility.Review;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace E2ETests.Endpoints;

public class StatisticsEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public StatisticsEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    // A material with one dictionary and the given words, each reviewed with the given ratings just now
    private async Task SeedAsync(Guid userId, params (string Term, ReviewRating[] Ratings)[] words)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        var material = new Material
        {
            UserId = userId,
            Title = "Words",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn,
            IsActive = true,
            CreatedAt = now,
        };
        var dictionary = new Dictionary { UserId = userId, Material = material, Title = "Words", CreatedAt = now };

        foreach (var (term, ratings) in words)
        {
            var entry = new DictionaryEntry { Dictionary = dictionary, Term = term, Translation = $"{term} (uk)", CreatedAt = now };
            db.DictionaryEntries.Add(entry);
            db.FlashcardReviews.AddRange(ratings.Select(rating => new FlashcardReview
            {
                // The service gives reviews their ids, the database doesn't
                Id = Guid.NewGuid(),
                UserId = userId,
                Entry = entry,
                Rating = rating,
                ReviewedAt = now.AddMinutes(-1),
                PrevDue = now,
            }));
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Flashcards_CountsAnswers_AndNamesTheHardestWords()
    {
        var userId = Guid.NewGuid();
        await SeedAsync(userId,
            ("mouse", [ReviewRating.Again, ReviewRating.Again, ReviewRating.Good]),
            ("apple", [ReviewRating.Hard, ReviewRating.Good]));

        var response = await _factory.CreateAuthenticatedClient(userId).GetAsync("/api/statistics/flashcards?days=7", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<FlashcardAnswersDto>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Know.ShouldBe(2);
        result.Unsure.ShouldBe(1);
        result.DontKnow.ShouldBe(2);
        result.Days.Count.ShouldBe(7);
        result.HardestWords.Select(x => x.Term).ShouldBe(["mouse"]);
        result.HardestWords[0].Translation.ShouldBe("mouse (uk)");
    }

    [Fact]
    public async Task Flashcards_ShowsOnlyTheUsersOwnReviews()
    {
        var someoneElse = Guid.NewGuid();
        await SeedAsync(someoneElse, ("secret", [ReviewRating.Again]));

        var response = await _factory.CreateAuthenticatedClient().GetAsync("/api/statistics/flashcards", TestContext.Current.CancellationToken);

        var result = await response.Content.ReadFromJsonAsync<FlashcardAnswersDto>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.DontKnow.ShouldBe(0);
        result.HardestWords.ShouldBeEmpty();
    }
}
