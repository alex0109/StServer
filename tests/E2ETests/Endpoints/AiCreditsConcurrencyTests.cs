using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Ai;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace E2ETests.Endpoints;

// The usage gate against real Postgres locks: parallel requests can't spend credits twice
public class AiCreditsConcurrencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AiCreditsConcurrencyTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreditsForTenActions_TwentyFiveAtOnce_ExactlyTenRun_AndTheBalanceNeverGoesBelowZero()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Free's 5 mistake explanations are used; each more costs 2 credits, 20 credits pay for 10
            for (var i = 0; i < 5; i++)
            {
                db.AiUsages.Add(new AiUsage
                {
                    Id = Guid.NewGuid(), UserId = userId, Feature = AiFeature.MistakeExplanation,
                    Status = AiUsageStatus.Succeeded, CreatedAt = now,
                });
            }

            db.CreditWallets.Add(new CreditWallet { UserId = userId, Balance = 20, AutoUseForStudy = true, CreatedAt = now });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 25).Select(async _ =>
        {
            using var scope = _factory.Services.CreateScope();
            var gate = scope.ServiceProvider.GetRequiredService<IAiUsageService>();
            try
            {
                await gate.ReserveAsync(userId, AiFeature.MistakeExplanation);
                return true;
            }
            catch (AiUsageDeniedException denied) when (denied.Code == AiDenialCode.InsufficientCredits)
            {
                return false;
            }
        }));

        outcomes.Count(x => x).ShouldBe(10);

        using var check = _factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.CreditWallets.SingleAsync(x => x.UserId == userId, TestContext.Current.CancellationToken)).Balance.ShouldBe(0);
        (await context.CreditTransactions.CountAsync(x => x.UserId == userId, TestContext.Current.CancellationToken)).ShouldBe(10);
    }
}
