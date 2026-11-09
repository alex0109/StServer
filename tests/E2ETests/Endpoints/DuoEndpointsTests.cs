using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Entities;
using Domain.Utility.Subscription;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace E2ETests.Endpoints;

public class DuoEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public DuoEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client(Guid userId, string email)
    {
        var client = _factory.CreateAuthenticatedClient(userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email);
        return client;
    }

    private async Task PaysFor(Guid userId, SubscriptionTier tier)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserSubscriptions.Add(new UserSubscription
        {
            UserId = userId,
            Tier = tier,
            Provider = SubscriptionProvider.Stripe,
            ProviderCustomerId = $"cus_{userId:N}",
            ProviderSubscriptionId = $"sub_{userId:N}",
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(Ct);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<string> Tier(HttpClient client) =>
        (await Json(await client.GetAsync("/api/billing/status", Ct))).GetProperty("tier").GetString()!;

    [Fact]
    public async Task APayerInvites_TheFriendJoins_AndHasTheDuoForAiAndStatus()
    {
        var payerId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        await PaysFor(payerId, SubscriptionTier.Duo);
        var payer = Client(payerId, "payer@example.com");
        var friend = Client(friendId, "friend@example.com");

        var token = (await Json(await payer.PostAsync("/api/billing/duo/invite", null, Ct))).GetProperty("token").GetString()!;

        var preview = await Json(await friend.PostAsJsonAsync("/api/billing/duo/preview", new { token }, Ct));
        preview.GetProperty("ownerEmail").GetString().ShouldBe("payer@example.com");
        preview.GetProperty("isOwn").GetBoolean().ShouldBeFalse();

        (await Tier(friend)).ShouldBe("Free");
        (await friend.PostAsJsonAsync("/api/billing/duo/join", new { token }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Tier(friend)).ShouldBe("Duo");
        (await Json(await friend.GetAsync("/api/ai/usage", Ct))).GetProperty("tier").GetString().ShouldBe("Duo");

        var owner = await Json(await payer.GetAsync("/api/billing/duo", Ct));
        owner.GetProperty("role").GetString().ShouldBe("Owner");
        owner.GetProperty("memberEmail").GetString().ShouldBe("friend@example.com");
        owner.GetProperty("canInvite").GetBoolean().ShouldBeFalse();

        // The link is used up
        (await Client(Guid.NewGuid(), "third@example.com").PostAsJsonAsync("/api/billing/duo/join", new { token }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // The friend leaves and is back on Free
        (await friend.PostAsync("/api/billing/duo/leave", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Tier(friend)).ShouldBe("Free");
    }

    [Fact]
    public async Task OnPlus_ThereIsNoInvite_AndAnUnknownLinkIsNotFound()
    {
        var payerId = Guid.NewGuid();
        await PaysFor(payerId, SubscriptionTier.Plus);
        var payer = Client(payerId, "plus@example.com");

        (await payer.PostAsync("/api/billing/duo/invite", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await payer.PostAsJsonAsync("/api/billing/duo/preview", new { token = "made-up" }, Ct)).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
        (await _factory.CreateClientWithoutAuth().GetAsync("/api/billing/duo", Ct)).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletingTheMembersAccount_FreesThePayersPlace()
    {
        var payerId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        await PaysFor(payerId, SubscriptionTier.Duo);
        var payer = Client(payerId, "payer2@example.com");
        var token = (await Json(await payer.PostAsync("/api/billing/duo/invite", null, Ct))).GetProperty("token").GetString()!;
        (await Client(friendId, "friend2@example.com").PostAsJsonAsync("/api/billing/duo/join", new { token }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var rule in Infrastructure.Account.AccountDataPolicy.Rules.Where(x => x.Entity == typeof(DuoMembership)))
                await rule.Step!(db, friendId, Guid.NewGuid());

            var duo = await db.DuoMemberships.AsNoTracking().SingleAsync(x => x.UserId == payerId, Ct);
            duo.MemberUserId.ShouldBeNull();
            duo.MemberEmail.ShouldBeNull();
        }

        (await Json(await payer.GetAsync("/api/billing/duo", Ct))).GetProperty("canInvite").GetBoolean().ShouldBeTrue();
    }
}
