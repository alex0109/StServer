using Application.Schedulers;
using Application.Schedulers.Fsrs;
using Domain.Utility.Flashcard;
using Shouldly;

namespace UnitTests.Schedulers;

public class FlashcardSchedulerProviderTests
{
    private static readonly FlashcardSchedulerOptions FsrsConfig = new() { Algorithm = FlashcardAlgorithm.Fsrs };

    [Fact]
    public void Current_IsTheConfiguredAlgorithm()
    {
        var provider = new FlashcardSchedulerProvider(
            [new BasicFlashcardScheduler(), new FsrsFlashcardScheduler(FsrsConfig)],
            FsrsConfig);

        provider.Current.ShouldBeOfType<FsrsFlashcardScheduler>();
        provider.Get(FlashcardAlgorithm.Basic).ShouldBeOfType<BasicFlashcardScheduler>();
    }

    [Fact]
    public void NotRegisteredAlgorithm_FailsOnStart()
    {
        Should.Throw<InvalidOperationException>(() =>
            new FlashcardSchedulerProvider([new BasicFlashcardScheduler()], FsrsConfig));
    }
}
