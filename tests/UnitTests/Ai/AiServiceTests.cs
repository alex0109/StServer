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
        _usage.Setup(x => x.ReserveAsync(_userId, AiFeature.QuestionGeneration)).ReturnsAsync(_usageId);
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
        _usage.Verify(x => x.CompleteAsync(_usageId, completion), Times.Once);
        _usage.Verify(x => x.FailAsync(It.IsAny<Guid>()), Times.Never);
    }

    // "No new words": the model answered, the user got nothing, so the try doesn't count
    [Fact]
    public async Task RunAsync_UnusableAnswer_GivesTheQuotaBack()
    {
        var completion = FakeAiProvider.Completion("A", "{}");
        _client.Setup(x => x.CompleteAsync(It.IsAny<IAiPrompt<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResult<string>("", completion));

        var error = await Should.ThrowAsync<AiOutputException>(() => _sut.RunAsync(
            new AiTestPrompt(), x => x.Length > 0, "AI found no new words in this text", TestContext.Current.CancellationToken));

        error.Message.ShouldBe("AI found no new words in this text");
        _usage.Verify(x => x.RefundAsync(_usageId, completion), Times.Once);
        _usage.Verify(x => x.CompleteAsync(It.IsAny<Guid>(), It.IsAny<AiCompletion>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_AllProvidersDown_ReturnsQuotaAndRethrows()
    {
        _client.Setup(x => x.CompleteAsync(It.IsAny<IAiPrompt<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiUnavailableException("down"));

        await Should.ThrowAsync<AiUnavailableException>(() => _sut.RunAsync(new AiTestPrompt()));

        _usage.Verify(x => x.FailAsync(_usageId), Times.Once);
    }

    [Fact]
    public async Task RunAsync_QuotaExceeded_DoesNotCallAi()
    {
        _usage.Setup(x => x.ReserveAsync(_userId, AiFeature.QuestionGeneration))
            .ThrowsAsync(new AiQuotaExceededException("limit"));

        await Should.ThrowAsync<AiQuotaExceededException>(() => _sut.RunAsync(new AiTestPrompt()));

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
