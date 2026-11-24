using Application.Ai;
using Application.Ai.Prompts;
using Application.DTOs.Ai;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services.Ai;
using Domain.Entities;
using Domain.Utility.Ai;
using Domain.Utility.Subscription;
using Moq;
using Shouldly;

namespace UnitTests.Ai;

public class WordTranslationServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<IAiService> _ai = new();
    private readonly Mock<ISubscriptionTierProvider> _tiers = new();
    private readonly Mock<IUserProfileRepository> _profiles = new();
    private readonly WordTranslationService _sut;

    public WordTranslationServiceTests()
    {
        var user = new Mock<IUserContext>();
        user.Setup(x => x.UserId).Returns(_userId);
        _tiers.Setup(x => x.GetTierAsync(_userId)).ReturnsAsync(SubscriptionTier.Plus);
        _profiles.Setup(x => x.GetAsync(_userId)).ReturnsAsync(Profile(autoTranslate: null));
        _sut = new WordTranslationService(_ai.Object, user.Object, _tiers.Object, _profiles.Object);
    }

    private UserProfile Profile(bool? autoTranslate) => new()
    {
        UserId = _userId, InterfaceLanguage = "uk", AutoTranslate = autoTranslate, CreatedAt = DateTime.UtcNow,
    };

    private void AiAnswers(string translation) =>
        _ai.Setup(x => x.RunAsync(It.IsAny<IAiPrompt<string>>(), It.IsAny<AiUnits<string>>(), It.IsAny<Func<string, bool>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResult<string>(translation, new AiCompletion { Text = "{}", Provider = "Anthropic", Model = "m" }));

    [Fact]
    public async Task OnPlus_ItTranslates_AsAWordFromText_WithoutCredits()
    {
        AiAnswers("собака");

        var result = await _sut.TranslateAsync(new WordTranslateDto { Term = "hund" }, TestContext.Current.CancellationToken);

        result.Translation.ShouldBe("собака");
        result.Skipped.ShouldBeNull();
        _ai.Verify(x => x.RunAsync(
            It.Is<IAiPrompt<string>>(p => p.Feature == AiFeature.WordLookup),
            It.Is<AiUnits<string>>(u => u.Reserved == 1 && !u.AllowCredits),
            It.IsAny<Func<string, bool>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task SwitchedOff_OrOnFree_AskNoAi()
    {
        _profiles.Setup(x => x.GetAsync(_userId)).ReturnsAsync(Profile(autoTranslate: false));
        (await _sut.TranslateAsync(new WordTranslateDto { Term = "hund" })).Skipped.ShouldBe(WordTranslationSkip.Off);

        _profiles.Setup(x => x.GetAsync(_userId)).ReturnsAsync(Profile(autoTranslate: null));
        _tiers.Setup(x => x.GetTierAsync(_userId)).ReturnsAsync(SubscriptionTier.Free);
        (await _sut.TranslateAsync(new WordTranslateDto { Term = "hund" })).Skipped.ShouldBe(WordTranslationSkip.Plan);

        _ai.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AUsedUpQuota_IsAQuietAnswer_NotAnError()
    {
        _ai.Setup(x => x.RunAsync(It.IsAny<IAiPrompt<string>>(), It.IsAny<AiUnits<string>>(), It.IsAny<Func<string, bool>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiUsageDeniedException(AiDenialCode.QuotaExhausted, AiFeature.WordLookup, "limit"));

        (await _sut.TranslateAsync(new WordTranslateDto { Term = "hund" })).Skipped.ShouldBe(WordTranslationSkip.Quota);
    }

    [Fact]
    public async Task AnEmptyTerm_IsABadRequest()
    {
        await Should.ThrowAsync<BadRequestException>(() => _sut.TranslateAsync(new WordTranslateDto { Term = "  " }));
    }
}
