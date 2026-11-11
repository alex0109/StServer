using System.Text.Json;
using Application.Ai;
using Application.Ai.Prompts;
using Application.DTOs.Podcast;
using Application.DTOs.RichTextDocument;
using Application.Exceptions;
using Application.Interfaces;
using Application.Podcasts;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Material;
using Domain.Utility.Note;
using Domain.Utility.Podcast;
using Moq;
using Shouldly;
using UnitTests.Ai;

namespace UnitTests.Podcasts;

public class PodcastServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
    private const string Answer = """
        {"chapters":[
          {"section":0,"title":"Вступ","lines":[{"speaker":"host","text":"Привіт! Сьогодні про фотосинтез."}]},
          {"section":1,"title":"Світлові реакції","lines":[{"speaker":"guest","text":"А де вони відбуваються?"}]}
        ]}
        """;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<IPodcastRepository> _podcasts = new();
    private readonly Mock<IMaterialRepository> _materials = new();
    private readonly Mock<IAiService> _ai = new();
    private readonly Mock<ISpeechSynthesizer> _speech = new();
    private readonly Mock<IAudioStorage> _storage = new();
    private readonly Mock<IPodcastJobs> _jobs = new();
    private readonly Mock<IUserContext> _user = new();
    private readonly Mock<TimeProvider> _time = new();
    private readonly Material _material;
    private readonly Note _note;

    public PodcastServiceTests()
    {
        _user.Setup(x => x.UserId).Returns(_userId);
        _time.Setup(x => x.GetUtcNow()).Returns(Now);
        _speech.Setup(x => x.IsConfigured).Returns(true);
        _storage.Setup(x => x.IsConfigured).Returns(true);
        _storage.Setup(x => x.GetReadUrl(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns((string path, TimeSpan _) => new Uri($"https://audio.test/{path}"));

        _note = new Note
        {
            Id = Guid.NewGuid(),
            Title = "Світлові реакції",
            Type = NoteType.text,
            TextContent = Text(new string('n', 400)),
            CreatedAt = Now.UtcDateTime,
        };
        _material = new Material
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Title = "Фотосинтез",
            Type = MaterialType.lecture,
            Status = MaterialStatus.inprocess,
            IsActive = true,
            CreatedAt = Now.UtcDateTime,
            Content = Text("Photosynthesis turns light into chemical energy."),
            Notes =
            [
                _note,
                new Note { Id = Guid.NewGuid(), Title = "Ескіз", Type = NoteType.drawing, CreatedAt = Now.UtcDateTime },
            ],
        };
        _materials.Setup(x => x.GetByIdAsync(_material.Id, _userId)).ReturnsAsync(_material);
    }

    private static JsonDocument Text(string text) => JsonSerializer.SerializeToDocument(new RichTextDocument
    {
        Content = [new Node { Type = "paragraph", Content = [new Node { Type = "text", Text = text }] }],
    });

    private PodcastService Service() =>
        new(_podcasts.Object, _materials.Object, _ai.Object, _speech.Object, _storage.Object, _jobs.Object, _user.Object, _time.Object);

    private void AiWrites() =>
        _ai.Setup(x => x.RunAsync(It.IsAny<IAiPrompt<PodcastScript>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IAiPrompt<PodcastScript> prompt, CancellationToken _) =>
                new AiResult<PodcastScript>(prompt.Parse(Answer), FakeAiProvider.Completion("A", Answer)));

    [Fact]
    public async Task Generate_WritesTheScript_SavesIt_AndStartsRecording()
    {
        AiWrites();
        MaterialPodcast? saved = null;
        _podcasts.Setup(x => x.AddAsync(It.IsAny<MaterialPodcast>())).Callback<MaterialPodcast>(x => saved = x);

        var result = await Service().GenerateAsync(_material.Id, new PodcastGenerateDto { Language = "uk" }, TestContext.Current.CancellationToken);

        result!.Status.ShouldBe(PodcastStatus.Generating);
        result.Language.ShouldBe("uk");
        result.AudioUrl.ShouldBeNull();
        // The material, then the note; the drawing has no words to speak
        result.Chapters.Select(x => x.NoteId).ShouldBe([null, _note.Id]);
        saved!.UserId.ShouldBe(_userId);
        _podcasts.Verify(x => x.SaveChangesAsync(), Times.Once);
        _jobs.Verify(x => x.RecordAudio(saved.Id), Times.Once);
    }

    [Fact]
    public async Task Generate_Again_ReplacesTheOldPodcast()
    {
        AiWrites();
        var old = new MaterialPodcast
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MaterialId = _material.Id,
            Language = "en-GB",
            Status = PodcastStatus.Ready,
            Script = new PodcastScript([]).ToDocument(),
            AudioPath = "podcasts/old.mp3",
            ChapterDurations = [12],
            CreatedAt = Now.UtcDateTime.AddDays(-2),
            UpdatedAt = Now.UtcDateTime.AddDays(-2),
        };
        _podcasts.Setup(x => x.GetByMaterialAsync(_material.Id, _userId)).ReturnsAsync(old);

        await Service().GenerateAsync(_material.Id, new PodcastGenerateDto { Language = "nb" }, TestContext.Current.CancellationToken);

        old.Language.ShouldBe("nb");
        old.Status.ShouldBe(PodcastStatus.Generating);
        old.ChapterDurations.ShouldBeEmpty();
        // The old file stays until the new one is recorded
        old.AudioPath.ShouldBe("podcasts/old.mp3");
        _podcasts.Verify(x => x.AddAsync(It.IsAny<MaterialPodcast>()), Times.Never);
        _jobs.Verify(x => x.RecordAudio(old.Id), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("klingon")]
    [InlineData("question")]
    public async Task Generate_UnknownLanguage_IsRefusedForFree(string? language)
    {
        await Should.ThrowAsync<BadRequestException>(() => Service().GenerateAsync(_material.Id, new PodcastGenerateDto { Language = language }));

        _ai.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Generate_WithoutVoicesSetUp_CostsNothing()
    {
        _speech.Setup(x => x.IsConfigured).Returns(false);

        await Should.ThrowAsync<AiUnavailableException>(() => Service().GenerateAsync(_material.Id, new PodcastGenerateDto { Language = "uk" }));

        _ai.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Generate_TooLittleText_CostsNothing()
    {
        _material.Content = Text("Short.");
        _material.Notes.Clear();

        await Should.ThrowAsync<BadRequestException>(() => Service().GenerateAsync(_material.Id, new PodcastGenerateDto { Language = "uk" }));

        _ai.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Generate_WhileRecording_IsRefused_ButAStuckRecordingCanBeReplaced()
    {
        var recording = new MaterialPodcast
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MaterialId = _material.Id,
            Language = "uk",
            Status = PodcastStatus.Generating,
            Script = new PodcastScript([]).ToDocument(),
            UpdatedAt = Now.UtcDateTime.AddMinutes(-5),
        };
        _podcasts.Setup(x => x.GetByMaterialAsync(_material.Id, _userId)).ReturnsAsync(recording);

        await Should.ThrowAsync<ConflictException>(() => Service().GenerateAsync(_material.Id, new PodcastGenerateDto { Language = "uk" }));
        _ai.VerifyNoOtherCalls();

        recording.UpdatedAt = Now.UtcDateTime - PodcastRules.StaleAfter - TimeSpan.FromMinutes(1);
        (await Service().GetAsync(_material.Id))!.Status.ShouldBe(PodcastStatus.Failed);
    }

    [Fact]
    public async Task Retry_RecordsTheSavedScriptAgain_WithoutAi()
    {
        var failed = new MaterialPodcast
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MaterialId = _material.Id,
            Language = "uk",
            Status = PodcastStatus.Failed,
            Error = "Speech failed",
            Script = new PodcastScript([]).ToDocument(),
            UpdatedAt = Now.UtcDateTime,
        };
        _podcasts.Setup(x => x.GetByMaterialAsync(_material.Id, _userId)).ReturnsAsync(failed);

        var result = await Service().RetryAsync(_material.Id);

        result!.Status.ShouldBe(PodcastStatus.Generating);
        failed.Error.ShouldBeNull();
        _jobs.Verify(x => x.RecordAudio(failed.Id), Times.Once);
        _ai.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Get_Ready_GivesALinkAndWhereEveryChapterStarts()
    {
        var script = new PodcastScript(
        [
            new PodcastChapter("Вступ", null, [new PodcastLine(PodcastSpeaker.Host, "a")]),
            new PodcastChapter("Нотатка", _note.Id, [new PodcastLine(PodcastSpeaker.Guest, "b")]),
        ]);
        _podcasts.Setup(x => x.GetByMaterialAsync(_material.Id, _userId)).ReturnsAsync(new MaterialPodcast
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MaterialId = _material.Id,
            Language = "uk",
            Status = PodcastStatus.Ready,
            Script = script.ToDocument(),
            ChapterDurations = [61.5, 90],
            DurationSeconds = 151.5,
            AudioPath = "podcasts/a.mp3",
            UpdatedAt = Now.UtcDateTime,
        });

        var result = await Service().GetAsync(_material.Id);

        result!.AudioUrl.ShouldBe("https://audio.test/podcasts/a.mp3");
        result.Chapters.Select(x => x.StartSeconds).ShouldBe([0, 61.5]);
        result.Chapters[1].Lines.Single().Speaker.ShouldBe(PodcastSpeaker.Guest);
    }
}
