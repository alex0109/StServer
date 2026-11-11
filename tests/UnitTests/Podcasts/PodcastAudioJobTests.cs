using Application.Interfaces;
using Application.Jobs;
using Application.Podcasts;
using Domain.Entities;
using Domain.Utility.Podcast;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace UnitTests.Podcasts;

public class PodcastAudioJobTests
{
    private readonly Mock<IPodcastRepository> _podcasts = new();
    private readonly Mock<ISpeechSynthesizer> _speech = new();
    private readonly Mock<IAudioStorage> _storage = new();
    private readonly Mock<TimeProvider> _time = new();
    private readonly MaterialPodcast _podcast;
    private readonly List<SpeechUsage> _usages = [];

    public PodcastAudioJobTests()
    {
        _time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        _podcast = new MaterialPodcast
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            Language = "uk",
            Status = PodcastStatus.Generating,
            AudioPath = "podcasts/old.mp3",
            CreatedAt = new DateTime(2026, 11, 11, 11, 59, 0, DateTimeKind.Utc),
            Script = new PodcastScript(
            [
                new PodcastChapter("Вступ", null, [new PodcastLine(PodcastSpeaker.Host, "Привіт"), new PodcastLine(PodcastSpeaker.Guest, "Привіт")]),
                new PodcastChapter("Нотатка", Guid.NewGuid(), [new PodcastLine(PodcastSpeaker.Host, "Далі")]),
            ]).ToDocument(),
        };
        _podcasts.Setup(x => x.GetByIdAsync(_podcast.Id)).ReturnsAsync(_podcast);
        _podcasts.Setup(x => x.AddSpeechUsageAsync(It.IsAny<SpeechUsage>())).Callback((SpeechUsage usage) => _usages.Add(usage));
    }

    private PodcastAudioJob Job() => new(_podcasts.Object, _speech.Object, _storage.Object, _time.Object, NullLogger<PodcastAudioJob>.Instance);

    [Fact]
    public async Task Run_RecordsEveryChapter_JoinsThem_AndRemembersTheTimeline()
    {
        // Each line is 1.2 seconds of audio
        _speech.Setup(x => x.SynthesizeAsync("uk", It.IsAny<IReadOnlyList<PodcastLine>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<PodcastLine> lines, CancellationToken _) => Mp3Fixtures.Speech(50 * lines.Count, withId3: true));
        byte[]? uploaded = null;
        _storage.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), "audio/mpeg", It.IsAny<CancellationToken>()))
            .Callback((string _, byte[] content, string _, CancellationToken _) => uploaded = content);

        await Job().Run(_podcast.Id);

        _podcast.Status.ShouldBe(PodcastStatus.Ready);
        _podcast.ChapterDurations.ShouldBe([2.4, 1.2], tolerance: 0.001);
        _podcast.DurationSeconds.ShouldBe(3.6, 0.001);
        _podcast.AudioPath.ShouldBe(PodcastRules.AudioPath(_podcast.UserId, _podcast.Id, _podcast.CreatedAt));
        uploaded!.Length.ShouldBe(150 * 144);
        _podcast.AudioSize.ShouldBe(uploaded.Length);
        // The replaced recording is removed only after the new one is stored
        _storage.Verify(x => x.DeleteAsync("podcasts/old.mp3", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_Failure_IsStored_NotRetried()
    {
        _speech.Setup(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<PodcastLine>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Speech is down"));

        await Job().Run(_podcast.Id);

        _podcast.Status.ShouldBe(PodcastStatus.Failed);
        _podcast.Error.ShouldBe("Speech is down");
        _podcast.AudioPath.ShouldBe("podcasts/old.mp3");
        _usages.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            usage => usage.Succeeded.ShouldBeFalse(),
            usage => usage.Characters.ShouldBe(0));
        _storage.Verify(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_LogsTheSpokenCharacters_ForTheCosts()
    {
        _speech.Setup(x => x.SynthesizeAsync("uk", It.IsAny<IReadOnlyList<PodcastLine>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mp3Fixtures.Speech(50));

        await Job().Run(_podcast.Id);

        var usage = _usages.ShouldHaveSingleItem();
        usage.Succeeded.ShouldBeTrue();
        usage.Characters.ShouldBe("Привіт".Length * 2 + "Далі".Length);
        usage.PodcastId.ShouldBe(_podcast.Id);
        usage.UserId.ShouldBe(_podcast.UserId);
        usage.Language.ShouldBe("uk");
        _podcasts.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Run_FailureHalfway_LogsThePartAlreadyVoiced()
    {
        // The first chapter is voiced and billed, the second one fails
        _speech.SetupSequence(x => x.SynthesizeAsync("uk", It.IsAny<IReadOnlyList<PodcastLine>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mp3Fixtures.Speech(50))
            .ThrowsAsync(new HttpRequestException("Speech is down"));

        await Job().Run(_podcast.Id);

        _podcast.Status.ShouldBe(PodcastStatus.Failed);
        var usage = _usages.ShouldHaveSingleItem();
        usage.Succeeded.ShouldBeFalse();
        usage.Characters.ShouldBe("Привіт".Length * 2);
    }

    [Fact]
    public async Task Run_NotRecording_DoesNothing()
    {
        _podcast.Status = PodcastStatus.Ready;

        await Job().Run(_podcast.Id);

        _speech.VerifyNoOtherCalls();
        _usages.ShouldBeEmpty();
    }

    [Fact]
    public void Batches_KeepRequestsSmall_AndNeverSplitALine()
    {
        var lines = Enumerable.Range(0, 70)
            .Select(i => new PodcastLine(PodcastSpeaker.Host, new string('a', 200)))
            .ToList();

        var batches = PodcastAudioJob.Batches(lines).ToList();

        batches.Sum(x => x.Count).ShouldBe(70);
        batches.ShouldAllBe(x => x.Count <= PodcastAudioJob.MaxLinesPerRequest && x.Sum(l => l.Text.Length) <= PodcastAudioJob.MaxCharactersPerRequest);
    }
}
