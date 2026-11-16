using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.Admin;
using Application.DTOs.Material;
using Application.Podcasts;
using Domain.Entities;
using Domain.Utility.Material;
using Domain.Utility.Podcast;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace E2ETests.Endpoints;

// The admin numbers are grouped by the database: these run every query on real Postgres,
// where a query EF can't translate fails (unit tests use a fake repository)
public class AdminStatsEndpointsTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminStatsEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/api/admin/stats/overview")]
    [InlineData("/api/admin/stats/ai")]
    public async Task Stats_AreCounted(string url)
    {
        (await _factory.CreateAdminClient().GetAsync(url, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Podcasts_AreCountedByStatusLanguageAndVoices()
    {
        var userId = Guid.NewGuid();
        var material = await (await _factory.CreateAuthenticatedClient(userId).PostAsJsonAsync("/api/materials", new MaterialCreateDto
        {
            Title = "Фотосинтез",
            Type = MaterialType.lecture,
            Status = MaterialStatus.inprocess,
        }, JsonOptions, Ct)).Content.ReadFromJsonAsync<MaterialResponseDto>(JsonOptions, Ct);

        var podcastId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            db.MaterialPodcasts.Add(new MaterialPodcast
            {
                Id = podcastId,
                UserId = userId,
                MaterialId = material!.Id,
                Language = "uk",
                Status = PodcastStatus.Ready,
                Script = new PodcastScript([]).ToDocument(),
                AudioPath = "podcasts/test.mp3",
                AudioSize = 2048,
                DurationSeconds = 120,
                CreatedAt = now,
                UpdatedAt = now,
                CompletedAt = now,
            });
            db.SpeechUsages.Add(new SpeechUsage
            {
                Id = Guid.NewGuid(), UserId = userId, PodcastId = podcastId, Language = "uk", Characters = 1000, Succeeded = true, CreatedAt = now,
            });
            await db.SaveChangesAsync(Ct);
        }

        var response = await _factory.CreateAdminClient().GetAsync("/api/admin/stats/podcasts", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<AdminPodcastStatsDto>(JsonOptions, Ct);
        stats!.Podcasts.Ready.ShouldBeGreaterThanOrEqualTo(1);
        stats.Languages.ShouldContain(x => x.Language == "uk");
        stats.Recent.ShouldContain(x => x.Id == podcastId);
        stats.Recordings.CharactersLast30Days.ShouldBeGreaterThanOrEqualTo(1000);
    }

    [Fact]
    public async Task Podcasts_NeedTheSecondFactor()
    {
        (await _factory.CreateAdminClient(secondFactor: false).GetAsync("/api/admin/stats/podcasts", Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
