using Application.Admin;
using Application.DTOs.Admin;
using Application.Interfaces;
using Application.Services;
using Domain.Utility.Ai;
using Moq;
using Shouldly;

namespace UnitTests.Admin;

public class AdminStatsServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);
    private readonly Guid _ann = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly FakeRepo _repo = new();

    private readonly AiPricingOptions _pricing = new()
    {
        CheckedOn = "2026-10-26",
        Models =
        {
            ["claude-haiku-5-5"] = new AiModelPrice { InputPerMillion = 0.10m, OutputPerMillion = 0.50m },
            ["gpt-5.4-mini"] = new AiModelPrice { InputPerMillion = 0.75m, OutputPerMillion = 4.50m },
        },
    };

    private AdminStatsService Service()
    {
        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now));
        return new AdminStatsService(_repo, _pricing, time.Object);
    }

    private AiUsageGroupRow Row(int daysAgo, Guid user, string model, int requests, long input = 1_000_000, long output = 1_000_000,
        AiUsageStatus status = AiUsageStatus.Succeeded, AiFeature feature = AiFeature.QuestionGeneration) =>
        new(Today.AddDays(-daysAgo), feature, "Anthropic", model, user, status, requests, input, output);

    [Fact]
    public async Task Ai_CountsWindowsAndEstimatesCost()
    {
        _repo.Ai =
        [
            Row(0, _ann, "claude-haiku-5-5", 2),                     // today: 0.10 + 0.50 = 0.60
            Row(3, _bob, "gpt-5.4-mini", 1),                         // 7 days: 0.75 + 4.50 = 5.25
            Row(20, _ann, "claude-haiku-5-5", 1),                    // 30 days
            Row(20, _ann, "claude-haiku-5-5", 1, 0, 0, AiUsageStatus.Failed),
            Row(1, _bob, "claude-haiku-5-5", 5, 0, 0, AiUsageStatus.Pending), // still running: ignored
        ];

        var stats = await Service().GetAiAsync();

        stats.Totals.Today.ShouldBe(2);
        stats.Totals.Last7Days.ShouldBe(3);
        stats.Totals.Last30Days.ShouldBe(5);
        stats.Totals.FailedLast30Days.ShouldBe(1);
        stats.Totals.AiUsers30.ShouldBe(2);
        stats.Totals.AveragePerActiveUser30.ShouldBe(2.5);
        stats.Cost.Today.ShouldBe(0.60m);
        stats.Cost.Last7Days.ShouldBe(5.85m);
        stats.Cost.Last30Days.ShouldBe(6.45m);
        stats.Days.Count.ShouldBe(30);
        stats.Days[^1].Requests.ShouldBe(2);
        stats.TopUsers[0].UserId.ShouldBe(_ann);
    }

    [Fact]
    public async Task Ai_ModelsWithoutPrice_AreCountedButNotPriced()
    {
        _repo.Ai = [Row(0, _ann, "some-new-model", 4)];

        var stats = await Service().GetAiAsync();

        stats.Totals.Today.ShouldBe(4);
        stats.Cost.Today.ShouldBe(0);
        stats.Cost.UnpricedRequests30.ShouldBe(4);
        stats.ByModel.Single().Cost.ShouldBeNull();
    }

    [Fact]
    public async Task Ai_AllTimeIncludesRolledUpMonths()
    {
        _repo.AiTotals = [Row(0, Guid.Empty, "claude-haiku-5-5", 3)];
        _repo.Monthly = [new AiMonthlyRow("Anthropic", "claude-haiku-5-5", 10, 2, 2_000_000, 0)];

        var stats = await Service().GetAiAsync();

        stats.Totals.AllTime.ShouldBe(15);
        stats.Cost.AllTime.ShouldBe(0.60m + 0.20m);
    }

    [Fact]
    public async Task Overview_CountsActiveAndNewUsersByDay()
    {
        _repo.Overview = Raw(
            firstSeen: new() { [_ann] = Now.AddDays(-40), [_bob] = Now.AddDays(-2) },
            active: [(_ann, Today), (_ann, Today.AddDays(-10)), (_bob, Today.AddDays(-2))]);

        var overview = await Service().GetOverviewAsync();

        overview.Users.Total.ShouldBe(2);
        overview.Users.ActiveToday.ShouldBe(1);
        overview.Users.Active7Days.ShouldBe(2);
        overview.Users.Active30Days.ShouldBe(2);
        overview.Users.New7Days.ShouldBe(1);
        overview.Days.Count.ShouldBe(30);
        overview.Days.Single(x => x.Date == Today.AddDays(-2)).NewUsers.ShouldBe(1);
        overview.Days.Single(x => x.Date == Today).ActiveUsers.ShouldBe(1);
    }

    private static AdminOverviewRaw Raw(Dictionary<Guid, DateTime> firstSeen, List<(Guid, DateOnly)> active) => new(
        firstSeen, active,
        new AdminContentDto(0, 0, 0, 0, 0, 0, 0, 0, 0),
        new AdminLibraryDto(0, 0, 0, 0),
        new AdminFeedbackDto(0, 0),
        [], [], [], []);

    private sealed class FakeRepo : IAdminStatsRepository
    {
        public List<AiUsageGroupRow> Ai { get; set; } = [];
        public List<AiUsageGroupRow> AiTotals { get; set; } = [];
        public List<AiMonthlyRow> Monthly { get; set; } = [];
        public AdminOverviewRaw? Overview { get; set; }

        public Task<AdminOverviewRaw> GetOverviewAsync(DateTime since, DateTime monthStart, DateTime previousMonthStart) =>
            Task.FromResult(Overview!);

        public Task<List<AiUsageGroupRow>> GetAiUsageAsync(DateTime since) =>
            Task.FromResult(Ai.Where(x => x.Date.ToDateTime(TimeOnly.MinValue) >= since).ToList());

        public Task<List<AiUsageGroupRow>> GetAiUsageTotalsAsync() => Task.FromResult(AiTotals);

        public Task<List<AiMonthlyRow>> GetAiMonthlyAsync() => Task.FromResult(Monthly);

        public Task<int> CountManualAcceptsAsync(DateTime since) => Task.FromResult(0);
    }
}
