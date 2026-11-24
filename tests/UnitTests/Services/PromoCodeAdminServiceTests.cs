using Application.Billing;
using Application.DTOs.Billing;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Utility.Admin;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class PromoCodeAdminServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Mock<IBillingPromotions> _promotions = new();
    private readonly Mock<IAdminAudit> _audit = new();
    private readonly Mock<IAdminAlerts> _alerts = new();
    private readonly List<PromoBatch> _discounts = [];
    private readonly List<string> _sent = [];
    private readonly PromoCodeAdminService _sut;

    public PromoCodeAdminServiceTests()
    {
        _promotions.Setup(x => x.IsConfigured).Returns(true);
        _promotions.Setup(x => x.CreateDiscountAsync(It.IsAny<PromoBatch>(), It.IsAny<CancellationToken>()))
            .Callback<PromoBatch, CancellationToken>((batch, _) => _discounts.Add(batch))
            .ReturnsAsync("co_1");
        // Stripe takes every code unless a test says otherwise
        _promotions.Setup(x => x.AddCodesAsync("co_1", It.IsAny<IReadOnlyList<string>>(), It.IsAny<PromoCodeLimits>(), It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<string>, PromoCodeLimits, CancellationToken>((_, codes, _, _) => _sent.AddRange(codes))
            .ReturnsAsync((string _, IReadOnlyList<string> codes, PromoCodeLimits _, CancellationToken _) => codes);

        var user = new Mock<IUserContext>();
        user.Setup(x => x.UserId).Returns(_adminId);
        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now));

        _sut = new PromoCodeAdminService(_promotions.Object, _audit.Object, _alerts.Object, user.Object, time.Object);
    }

    private static PromoBatchCreateDto FreePlus(int count = 1, string? code = null) => new()
    {
        Name = "Deichman Bjørvika",
        Kind = PromoKind.FreeMonths,
        Plan = BillingPlan.Plus,
        Months = 3,
        Count = count,
        Code = code,
    };

    [Fact]
    public async Task ManyCodes_GetThePrefix_AndARandomReadableTail()
    {
        var result = await _sut.CreateAsync(FreePlus(count: 50, code: "deichman"), TestContext.Current.CancellationToken);

        result.Codes.Count.ShouldBe(50);
        result.Codes.Distinct().Count().ShouldBe(50);
        result.Codes.ShouldAllBe(code => System.Text.RegularExpressions.Regex.IsMatch(code, "^DEICHMAN-[A-HJKMNP-Z2-9]{6}$"));

        var discount = _discounts.Single();
        discount.Months.ShouldBe(3);
        discount.Duration.ShouldBe(PromoDuration.Months);
        discount.TotalUses.ShouldBe(50);
        discount.CreatedBy.ShouldBe(_adminId);
        _audit.Verify(x => x.RecordAsync(AdminAction.CreatePromoCodes, null, It.Is<string>(d => d.Contains("Deichman Bjørvika"))), Times.Once);
        _alerts.Verify(x => x.SubscriptionChangedAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task OneCode_IsTheChosenOne_InUpperCase()
    {
        var result = await _sut.CreateAsync(FreePlus(code: " welcome-2026 "), TestContext.Current.CancellationToken);

        result.Codes.ShouldBe(["WELCOME-2026"]);
    }

    [Fact]
    public async Task RandomCodesThatClash_AreMadeAgain_WithNewOnes()
    {
        var calls = 0;
        _promotions.Setup(x => x.AddCodesAsync("co_1", It.IsAny<IReadOnlyList<string>>(), It.IsAny<PromoCodeLimits>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<string> codes, PromoCodeLimits _, CancellationToken _) =>
                ++calls == 1 ? codes.Skip(2).ToList() : codes);

        var result = await _sut.CreateAsync(FreePlus(count: 5), TestContext.Current.CancellationToken);

        result.Codes.Count.ShouldBe(5);
        result.Codes.Distinct().Count().ShouldBe(5);
        calls.ShouldBe(2);
    }

    [Fact]
    public async Task AChosenCodeStripeRefuses_IsABadRequest_AndNotAudited()
    {
        _promotions.Setup(x => x.AddCodesAsync("co_1", It.IsAny<IReadOnlyList<string>>(), It.IsAny<PromoCodeLimits>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await Should.ThrowAsync<BadRequestException>(() => _sut.CreateAsync(FreePlus(code: "TAKEN"), TestContext.Current.CancellationToken));

        _audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PercentOff_KeepsItsDuration_AndMayBeForBothPlans()
    {
        await _sut.CreateAsync(new PromoBatchCreateDto
        {
            Name = "Autumn",
            Kind = PromoKind.PercentOff,
            PercentOff = 20,
            Duration = PromoDuration.Forever,
            Months = 6,
            Count = 2,
            UsesPerCode = 10,
            NewCustomersOnly = true,
        }, TestContext.Current.CancellationToken);

        var discount = _discounts.Single();
        discount.Plan.ShouldBeNull();
        discount.PercentOff.ShouldBe(20);
        discount.Months.ShouldBeNull();
        discount.TotalUses.ShouldBe(20);
        _promotions.Verify(x => x.AddCodesAsync("co_1", It.IsAny<IReadOnlyList<string>>(), new PromoCodeLimits(10, true), It.IsAny<CancellationToken>()));
    }

    public static TheoryData<string, Action<PromoBatchCreateDto>> Invalid => new()
    {
        { "short name", dto => dto.Name = "ab" },
        { "no codes", dto => dto.Count = 0 },
        { "too many codes", dto => dto.Count = PromoCodeAdminService.MaxCodes + 1 },
        { "no uses", dto => dto.UsesPerCode = 0 },
        { "free months without a plan", dto => dto.Plan = null },
        { "no months", dto => dto.Months = 0 },
        { "too many months", dto => dto.Months = PromoCodeAdminService.MaxMonths + 1 },
        { "bad characters", dto => dto.Code = "AB_C" },
        { "long prefix", dto => { dto.Count = 2; dto.Code = "ABCDEFGHIJKLM"; } },
        { "ended already", dto => dto.ExpiresAt = Now.AddDays(-1) },
        { "too far ahead", dto => dto.ExpiresAt = Now.AddYears(6) },
        { "no percent", dto => { dto.Kind = PromoKind.PercentOff; dto.PercentOff = 0; } },
        { "over 100 percent", dto => { dto.Kind = PromoKind.PercentOff; dto.PercentOff = 101; } },
        { "unknown plan", dto => dto.Plan = (BillingPlan)9 },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public async Task InvalidBatches_AreRefused_BeforeStripe(string _, Action<PromoBatchCreateDto> spoil)
    {
        var dto = FreePlus();
        spoil(dto);

        await Should.ThrowAsync<BadRequestException>(() => _sut.CreateAsync(dto, TestContext.Current.CancellationToken));

        _discounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task WithoutStripe_NothingIsListedOrMade()
    {
        _promotions.Setup(x => x.IsConfigured).Returns(false);

        (await _sut.ListAsync(TestContext.Current.CancellationToken)).Configured.ShouldBeFalse();
        await Should.ThrowAsync<BillingUnavailableException>(() => _sut.CreateAsync(FreePlus(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Deactivate_IsAudited_AndChecksTheId()
    {
        _promotions.Setup(x => x.DeactivateAsync("co_1", It.IsAny<CancellationToken>())).ReturnsAsync(4);

        (await _sut.DeactivateAsync("co_1", TestContext.Current.CancellationToken)).ShouldBe(4);
        _audit.Verify(x => x.RecordAsync(AdminAction.DeactivatePromoCodes, null, It.IsAny<string>()), Times.Once);

        await Should.ThrowAsync<BadRequestException>(() => _sut.DeactivateAsync("co/../x", TestContext.Current.CancellationToken));
    }
}
