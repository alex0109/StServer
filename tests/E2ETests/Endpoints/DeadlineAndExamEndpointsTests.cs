using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.Assessment;
using Application.DTOs.Attempt;
using Application.DTOs.Statistics;
using Domain.Entities;
using Domain.Utility.Material;
using Domain.Utility.Question;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace E2ETests.Endpoints;

public class DeadlineAndExamEndpointsTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public DeadlineAndExamEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<(Guid MaterialId, Guid AssessmentId)> SeedMaterialAsync(Guid userId, string title, int questions = 2)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        var material = new Material
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn,
            IsActive = true,
            CreatedAt = now,
        };
        var assessment = new Assessment { Id = Guid.NewGuid(), UserId = userId, Material = material };
        db.Materials.Add(material);
        db.Assessments.Add(assessment);

        for (var i = 0; i < questions; i++)
        {
            db.Questions.Add(new Question
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Material = material,
                Title = $"Question {i}",
                Answer = "answer",
                QuestionType = QuestionType.Open,
                QuestionDifficulty = QuestionDifficulty.Easy,
                IsActive = true,
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(Ct);
        return (material.Id, assessment.Id);
    }

    [Fact]
    public async Task Deadline_IsSet_PlannedAndListedNearestFirst()
    {
        var userId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(userId);
        var (far, _) = await SeedMaterialAsync(userId, "Far");
        var (near, _) = await SeedMaterialAsync(userId, "Near");

        (await client.PutAsJsonAsync($"/api/materials/{far}/deadline", new { deadline = Today.AddDays(30) }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PutAsJsonAsync($"/api/materials/{near}/deadline", new { deadline = Today.AddDays(2) }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var plan = await client.GetFromJsonAsync<DeadlinePlanDto>($"/api/materials/{near}/deadline-plan", JsonOptions, Ct);
        plan.ShouldNotBeNull();
        plan.DaysLeft.ShouldBe(2);
        plan.NewQuestions.ShouldBe(2);
        plan.NewQuestionsToday.ShouldBe(1);
        plan.MinutesToday.ShouldBe(1);
        plan.AttemptRecommended.ShouldBeTrue();

        var list = await client.GetFromJsonAsync<List<DeadlinePlanDto>>("/api/review/deadlines", JsonOptions, Ct);
        list!.Select(x => x.Title).ShouldBe(["Near", "Far"]);
    }

    [Fact]
    public async Task Deadline_InThePast_IsRefused_AndCanBeRemoved()
    {
        var userId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(userId);
        var (material, _) = await SeedMaterialAsync(userId, "Material");

        (await client.PutAsJsonAsync($"/api/materials/{material}/deadline", new { deadline = Today.AddDays(-2) }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await client.PutAsJsonAsync($"/api/materials/{material}/deadline", new { deadline = Today.AddDays(5) }, Ct);
        await client.PutAsJsonAsync($"/api/materials/{material}/deadline", new { deadline = (DateOnly?)null }, Ct);

        var response = await client.GetAsync($"/api/materials/{material}/deadline-plan", Ct);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBeOneOf("", "null");
    }

    [Fact]
    public async Task Deadline_OfAnotherUser_IsNotFound()
    {
        var (material, _) = await SeedMaterialAsync(Guid.NewGuid(), "Foreign");
        var stranger = _factory.CreateAuthenticatedClient();

        (await stranger.PutAsJsonAsync($"/api/materials/{material}/deadline", new { deadline = Today.AddDays(5) }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Calendar_ShowsDeadlinesInTheRange()
    {
        var userId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(userId);
        var (material, _) = await SeedMaterialAsync(userId, "Exam");
        await client.PutAsJsonAsync($"/api/materials/{material}/deadline", new { deadline = Today.AddDays(3) }, Ct);

        var from = Today.ToString("yyyy-MM-dd");
        var to = Today.AddDays(10).ToString("yyyy-MM-dd");
        var calendar = await client.GetFromJsonAsync<StudyCalendarDto>($"/api/statistics/calendar?from={from}&to={to}", JsonOptions, Ct);

        calendar.ShouldNotBeNull();
        calendar.Today.ShouldBe(Today);
        calendar.StudyDays.ShouldBeEmpty();
        calendar.Deadlines.Single().Title.ShouldBe("Exam");

        (await client.GetAsync($"/api/statistics/calendar?from={from}&to={Today.AddYears(3):yyyy-MM-dd}", Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExamTimer_AppliesToMaterialAttempts_AndUnansweredCountAsWrong()
    {
        var userId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(userId);
        var (_, assessment) = await SeedMaterialAsync(userId, "Timed");

        (await client.PutAsJsonAsync($"/api/assessments/{assessment}/settings", new AssessmentSettingsDto { TimerMinutes = 0 }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync($"/api/assessments/{assessment}/settings", new AssessmentSettingsDto { TimerMinutes = 30 }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var settings = await client.GetFromJsonAsync<AssessmentResponseDto>($"/api/assessments/{assessment}", JsonOptions, Ct);
        settings!.TimerMinutes.ShouldBe(30);

        var start = await client.PostAsJsonAsync("/api/attempts/start", new StartAttemptDto { AssessmentId = assessment, UseTimer = true }, Ct);
        var timed = await start.Content.ReadFromJsonAsync<Guid>(JsonOptions, Ct);
        var fromNote = await client.PostAsJsonAsync("/api/attempts/start", new StartAttemptDto { AssessmentId = assessment, UseTimer = false }, Ct);
        var untimed = await fromNote.Content.ReadFromJsonAsync<Guid>(JsonOptions, Ct);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Attempts.Single(x => x.Id == timed).TimeLimitMinutes.ShouldBe(30);
            db.Attempts.Single(x => x.Id == untimed).TimeLimitMinutes.ShouldBeNull();
        }

        (await client.PostAsync($"/api/attempts/{timed}/finish", null, Ct)).IsSuccessStatusCode.ShouldBeTrue();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var results = db.Results.Where(x => x.AttemptId == timed).ToList();
            results.Count.ShouldBe(2);
            results.ShouldAllBe(x => !x.IsCorrect);
        }
    }

    [Fact]
    public async Task Library_IsOpenToGuestsForReading_ButNotForImport()
    {
        var guest = _factory.CreateClientWithoutAuth();

        var list = await guest.GetAsync("/api/library", Ct);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await guest.PostAsync($"/api/library/{Guid.NewGuid()}/import", null, Ct)).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
        (await guest.GetAsync("/api/library/requests", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
