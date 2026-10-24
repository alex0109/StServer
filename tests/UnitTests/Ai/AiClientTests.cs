using Application.Ai;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace UnitTests.Ai;

public class AiClientTests
{
    public AiClientTests()
    {
        AiClient.ResetHealth();
    }

    private static AiClient Create(AiOptions options, params IAiProvider[] providers) =>
        new(providers, options, NullLogger<AiClient>.Instance);

    private static AiOptions Order(params string[] names) => new() { ProviderOrder = names };

    [Fact]
    public async Task CompleteAsync_FirstProviderAnswers_UsesIt()
    {
        var first = new FakeAiProvider("A", () => Task.FromResult(FakeAiProvider.Completion("A", """{"value":"a"}""")));
        var second = new FakeAiProvider("B", () => Task.FromResult(FakeAiProvider.Completion("B", """{"value":"b"}""")));

        var result = await Create(Order("A", "B"), first, second).CompleteAsync(new AiTestPrompt());

        result.Value.ShouldBe("a");
        second.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task CompleteAsync_ProviderOrderFromOptions_IsRespected()
    {
        var first = new FakeAiProvider("A", () => Task.FromResult(FakeAiProvider.Completion("A", """{"value":"a"}""")));
        var second = new FakeAiProvider("B", () => Task.FromResult(FakeAiProvider.Completion("B", """{"value":"b"}""")));

        var result = await Create(Order("B", "A"), first, second).CompleteAsync(new AiTestPrompt());

        result.Value.ShouldBe("b");
        first.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task CompleteAsync_ProviderFails_FallsBackToNext()
    {
        var failing = new FakeAiProvider("A", () => throw new AiProviderException("A API error 529"));
        var working = new FakeAiProvider("B", () => Task.FromResult(FakeAiProvider.Completion("B", """{"value":"b"}""")));

        var result = await Create(Order("A", "B"), failing, working).CompleteAsync(new AiTestPrompt());

        result.Value.ShouldBe("b");
        result.Completion.Provider.ShouldBe("B");
    }

    [Fact]
    public async Task CompleteAsync_InvalidJson_FallsBackToNext()
    {
        var broken = new FakeAiProvider("A", () => Task.FromResult(FakeAiProvider.Completion("A", "not json")));
        var working = new FakeAiProvider("B", () => Task.FromResult(FakeAiProvider.Completion("B", """{"value":"b"}""")));

        var result = await Create(Order("A", "B"), broken, working).CompleteAsync(new AiTestPrompt());

        result.Value.ShouldBe("b");
    }

    [Fact]
    public async Task CompleteAsync_UnexpectedProviderError_FallsBackToNext()
    {
        var broken = new FakeAiProvider("A", () => throw new InvalidOperationException("sdk bug"));
        var working = new FakeAiProvider("B", () => Task.FromResult(FakeAiProvider.Completion("B", """{"value":"b"}""")));

        var result = await Create(Order("A", "B"), broken, working).CompleteAsync(new AiTestPrompt());

        result.Value.ShouldBe("b");
    }

    [Fact]
    public async Task CompleteAsync_AllProvidersFail_ThrowsUnavailable()
    {
        var first = new FakeAiProvider("A", () => throw new AiProviderException("down"));
        var second = new FakeAiProvider("B", () => throw new HttpRequestException("down"));

        await Should.ThrowAsync<AiUnavailableException>(
            () => Create(Order("A", "B"), first, second).CompleteAsync(new AiTestPrompt()));

        first.Calls.ShouldBe(1);
        second.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task CompleteAsync_ProviderTimesOut_FallsBackToNext()
    {
        var slow = new FakeAiProvider("A", async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            return FakeAiProvider.Completion("A", """{"value":"a"}""");
        });
        var working = new FakeAiProvider("B", () => Task.FromResult(FakeAiProvider.Completion("B", """{"value":"b"}""")));
        var options = new AiOptions { ProviderOrder = ["A", "B"], RequestTimeoutSeconds = 0 };

        var result = await Create(options, slow, working).CompleteAsync(new AiTestPrompt());

        result.Value.ShouldBe("b");
    }

    [Fact]
    public async Task CompleteAsync_NotConfiguredProvider_IsSkipped()
    {
        var notConfigured = new FakeAiProvider("A", () => throw new AiProviderException("no key"), isConfigured: false);
        var working = new FakeAiProvider("B", () => Task.FromResult(FakeAiProvider.Completion("B", """{"value":"b"}""")));

        await Create(Order("A", "B"), notConfigured, working).CompleteAsync(new AiTestPrompt());

        notConfigured.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task CompleteAsync_ProviderMissingFromOrder_IsNotUsed()
    {
        var excluded = new FakeAiProvider("A", () => Task.FromResult(FakeAiProvider.Completion("A", """{"value":"a"}""")));

        await Should.ThrowAsync<AiUnavailableException>(
            () => Create(Order("B"), excluded).CompleteAsync(new AiTestPrompt()));

        excluded.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task CompleteAsync_Disabled_ThrowsWithoutCallingProviders()
    {
        var provider = new FakeAiProvider("A", () => Task.FromResult(FakeAiProvider.Completion("A", """{"value":"a"}""")));
        var options = new AiOptions { Enabled = false, ProviderOrder = ["A"] };

        await Should.ThrowAsync<AiUnavailableException>(
            () => Create(options, provider).CompleteAsync(new AiTestPrompt()));

        provider.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task CompleteAsync_CallerCancels_DoesNotTryOtherProviders()
    {
        using var cancellation = new CancellationTokenSource();
        var first = new FakeAiProvider("A", () =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });
        var second = new FakeAiProvider("B", () => Task.FromResult(FakeAiProvider.Completion("B", """{"value":"b"}""")));

        await Should.ThrowAsync<OperationCanceledException>(
            () => Create(Order("A", "B"), first, second).CompleteAsync(new AiTestPrompt(), cancellation.Token));

        second.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task CompleteAsync_ProviderKeepsFailing_IsTriedLast()
    {
        var failing = new FakeAiProvider("A", () => throw new AiProviderException("down"));
        var working = new FakeAiProvider("B", () => Task.FromResult(FakeAiProvider.Completion("B", """{"value":"b"}""")));
        var client = Create(Order("A", "B"), failing, working);

        for (var i = 0; i < 3; i++)
        {
            await client.CompleteAsync(new AiTestPrompt());
        }

        // Circuit is open: the next request goes straight to the working provider
        await client.CompleteAsync(new AiTestPrompt());

        failing.Calls.ShouldBe(3);
        working.Calls.ShouldBe(4);
    }
}
