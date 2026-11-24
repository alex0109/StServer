using Application.Ai;
using Application.Ai.Prompts;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services.Ai;
using Domain.Utility.Ai;
using Moq;
using Shouldly;

namespace UnitTests.Ai;

public class AiServiceTests
{
    private readonly Mock<IAiClient> _client = new();
    private readonly Mock<IAiUsageService> _usage = new();
    private readonly Mock<IUserContext> _user = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _usageId = Guid.NewGuid();
    private readonly AiService _sut;

    public AiServiceTests()
    {
        _user.Setup(x => x.UserId).Returns(_userId);
        _usage.Setup(x => x.ReserveAsync(_userId, AiFeature.QuestionGeneration, 1, true)).ReturnsAsync(new AiReservation(_usageId, AiUsageSource.Quota, 1, 0));
        _sut = new AiService(_client.Object, _usage.Object, _user.Object);
    }

    [Fact]
    public async Task RunAsync_Success_CompletesUsage()
    {
        var completion = FakeAiProvider.Completion("A", "{}");
        _client.Setup(x => x.CompleteAsync(It.IsAny<IAiPrompt<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResult<string>("value", completion));

        var result = await _sut.RunAsync(new AiTestPrompt(), TestContext.Current.CancellationToken);

        result.Value.ShouldBe("value");
        _usage.Verify(x => x.CommitAsync(_usageId, completion, null, null), Times.Once);
        _usage.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<AiCompletion?>()), Times.Never);
    }

    // "No new words": the model answered and billed it, the user got nothing; the gate decides whether it counts
    [Fact]
    public async Task RunAsync_UnusableAnswer_IsSettledByTheGate()
    {
        var completion = FakeAiProvider.Completion("A", "{}");
        _client.Setup(x => x.CompleteAsync(It.IsAny<IAiPrompt<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResult<string>("", completion));

        var error = await Should.ThrowAsync<AiOutputException>(() => _sut.RunAsync(
            new AiTestPrompt(), x => x.Length > 0, "AI found no new words in this text", TestContext.Current.CancellationToken));

        error.Message.ShouldBe("AI found no new words in this text");
        _usage.Verify(x => x.SettleUnusableAsync(_usageId, completion), Times.Once);
        _usage.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<AiCompletion?>()), Times.Never);
        _usage.Verify(x => x.CommitAsync(It.IsAny<Guid>(), It.IsAny<AiCompletion>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    // An answer in the wrong shape was still paid for: settled like an empty one, not released
    [Fact]
    public async Task RunAsync_UnreadableAnswer_IsSettledByTheGate()
    {
        var completion = FakeAiProvider.Completion("A", "not json");
        _client.Setup(x => x.CompleteAsync(It.IsAny<IAiPrompt<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiOutputException("answer is not valid JSON", completion));

        await Should.ThrowAsync<AiOutputException>(() => _sut.RunAsync(new AiTestPrompt(), TestContext.Current.CancellationToken));

        _usage.Verify(x => x.SettleUnusableAsync(_usageId, completion), Times.Once);
        _usage.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<AiCompletion?>()), Times.Never);
    }

    // Leaving the page doesn't cancel a request a provider already has: it would be billed anyway
    [Fact]
    public async Task RunAsync_ClientLeaves_ProviderCallIsNotCancelled()
    {
        using var client = new CancellationTokenSource();
        CancellationToken passed = default;
        _client.Setup(x => x.CompleteAsync(It.IsAny<IAiPrompt<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IAiPrompt<string>, CancellationToken>((_, token) =>
            {
                passed = token;
                client.Cancel();
            })
            .ReturnsAsync(new AiResult<string>("value", FakeAiProvider.Completion("A", "{}")));

        await _sut.RunAsync(new AiTestPrompt(), client.Token);

        passed.CanBeCanceled.ShouldBeFalse();
        _usage.Verify(x => x.CommitAsync(_usageId, It.IsAny<AiCompletion>(), null, null), Times.Once);
    }

    // A suggestion made while typing is the exception: it stops with the client
    [Fact]
    public async Task RunAsync_SuggestionWhileTyping_StopsWithTheClient()
    {
        using var client = new CancellationTokenSource();
        CancellationToken passed = default;
        _usage.Setup(x => x.ReserveAsync(_userId, AiFeature.QuestionGeneration, 1, false))
            .ReturnsAsync(new AiReservation(_usageId, AiUsageSource.Quota, 1, 0));
        _client.Setup(x => x.CompleteAsync(It.IsAny<IAiPrompt<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IAiPrompt<string>, CancellationToken>((_, token) => passed = token)
            .ReturnsAsync(new AiResult<string>("value", FakeAiProvider.Completion("A", "{}")));

        await _sut.RunAsync(new AiTestPrompt(), new AiUnits<string>(1, AllowCredits: false, StopsWithClient: true), _ => true, "", client.Token);

        passed.ShouldBe(client.Token);
    }

    // Anonymous sessions cost nothing to make: no AI, and no quota is even looked at
    [Fact]
    public async Task RunAsync_AnonymousSession_IsDeniedBeforeReserving()
    {
        _user.Setup(x => x.Claim(AiService.AnonymousClaim)).Returns("true");

        var denied = await Should.ThrowAsync<AiUsageDeniedException>(() => _sut.RunAsync(new AiTestPrompt()));

        denied.Code.ShouldBe(AiDenialCode.AccountRequired);
        _usage.Verify(x => x.ReserveAsync(It.IsAny<Guid>(), It.IsAny<AiFeature>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        _client.Verify(x => x.CompleteAsync(It.IsAny<IAiPrompt<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_AllProvidersDown_ReturnsQuotaAndRethrows()
    {
        _client.Setup(x => x.CompleteAsync(It.IsAny<IAiPrompt<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiUnavailableException("down"));

        await Should.ThrowAsync<AiUnavailableException>(() => _sut.RunAsync(new AiTestPrompt()));

        _usage.Verify(x => x.ReleaseAsync(_usageId, null), Times.Once);
    }

    [Fact]
    public async Task RunAsync_QuotaExceeded_DoesNotCallAi()
    {
        _usage.Setup(x => x.ReserveAsync(_userId, AiFeature.QuestionGeneration, 1, true))
            .ThrowsAsync(new AiUsageDeniedException(AiDenialCode.QuotaExhausted, AiFeature.QuestionGeneration, "limit"));

        await Should.ThrowAsync<AiUsageDeniedException>(() => _sut.RunAsync(new AiTestPrompt()));

        _client.Verify(x => x.CompleteAsync(It.IsAny<IAiPrompt<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryRunAsync_Unavailable_ReturnsNull()
    {
        _client.Setup(x => x.CompleteAsync(It.IsAny<IAiPrompt<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiUnavailableException("down"));

        (await _sut.TryRunAsync(new AiTestPrompt(), TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}
