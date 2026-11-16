using Application.Ai;
using Application.Ai.Prompts;
using Application.Constants;
using Application.DTOs.Ai;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Application.Services.Ai;
using Application.Videos;
using Domain.Entities;
using Domain.Utility.Material;
using Domain.Utility.Question;
using Moq;
using Shouldly;
using UnitTests.Ai;

namespace UnitTests.Videos;

public class VideoServiceTests
{
    private const string VideoId = "dQw4w9WgXcQ";

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<IUserContext> _user = new();
    private readonly Mock<IAiService> _ai = new();
    private readonly Mock<IMaterialRepository> _materials = new();
    private readonly Mock<IMaterialVideoRepository> _videos = new();
    private readonly Material _material;

    public VideoServiceTests()
    {
        _user.Setup(x => x.UserId).Returns(_userId);
        _material = new Material
        {
            Id = Guid.NewGuid(), UserId = _userId, Title = "Photosynthesis", Type = MaterialType.video,
            Status = MaterialStatus.inprocess, IsActive = true, CreatedAt = DateTime.UtcNow,
            Link = $"https://youtu.be/{VideoId}",
        };
        _materials.Setup(x => x.GetByIdAsync(_material.Id, _userId)).ReturnsAsync(_material);
    }

    private VideoService Service() => new(_videos.Object, _materials.Object, _ai.Object, _user.Object);

    private void AiWrites(params VideoSegment[] segments) =>
        _ai.Setup(x => x.RunAsync(
                It.IsAny<IAiPrompt<VideoTranscriptResult>>(),
                It.IsAny<Func<VideoTranscriptResult, bool>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResult<VideoTranscriptResult>(
                new VideoTranscriptResult("en", segments, false),
                FakeAiProvider.Completion("Gemini", "{}")));

    private MaterialVideo Stored(string videoId) => new()
    {
        Id = Guid.NewGuid(), UserId = _userId, MaterialId = _material.Id, VideoId = videoId, Language = "en",
        Transcript = new VideoTranscript([new VideoSegment(1, "Old line.")]).ToDocument(),
    };

    [Fact]
    public async Task Process_WritesAndSavesTheSubtitles()
    {
        AiWrites(new VideoSegment(2, "Plants make sugar."), new VideoSegment(9, "They need light."));
        MaterialVideo? added = null;
        _videos.Setup(x => x.AddAsync(It.IsAny<MaterialVideo>())).Callback((MaterialVideo video) => added = video);

        var result = await Service().ProcessAsync(_material.Id);

        result!.VideoId.ShouldBe(VideoId);
        result.Segments.Select(x => x.Text).ShouldBe(["Plants make sugar.", "They need light."]);
        added!.UserId.ShouldBe(_userId);
        added.MaterialId.ShouldBe(_material.Id);
        _videos.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Process_Again_ReplacesTheSubtitles()
    {
        var stored = Stored("aaaaaaaaaaa");
        _videos.Setup(x => x.GetByMaterialAsync(_material.Id, _userId)).ReturnsAsync(stored);
        AiWrites(new VideoSegment(3, "New line."));

        await Service().ProcessAsync(_material.Id);

        stored.VideoId.ShouldBe(VideoId);
        VideoTranscript.FromDocument(stored.Transcript).Segments.Single().Text.ShouldBe("New line.");
        _videos.Verify(x => x.AddAsync(It.IsAny<MaterialVideo>()), Times.Never);
    }

    [Fact]
    public async Task Process_WithoutAYouTubeLink_IsRefusedBeforeAi()
    {
        _material.Link = "https://example.com/lecture";

        await Should.ThrowAsync<BadRequestException>(() => Service().ProcessAsync(_material.Id));

        _ai.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Process_SomeoneElsesMaterial_IsNotFound()
    {
        (await Service().ProcessAsync(Guid.NewGuid())).ShouldBeNull();

        _ai.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Get_SubtitlesOfAnotherVideo_AreNotShown()
    {
        _videos.Setup(x => x.GetByMaterialAsync(_material.Id, _userId)).ReturnsAsync(Stored("bbbbbbbbbbb"));

        (await Service().GetAsync(_material.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Get_SubtitlesOfTheLinkedVideo_AreShown()
    {
        _videos.Setup(x => x.GetByMaterialAsync(_material.Id, _userId)).ReturnsAsync(Stored(VideoId));

        (await Service().GetAsync(_material.Id))!.Segments.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Questions_FromVideo_ComeFromTheSubtitles_AndKeepTheirMoment()
    {
        _videos.Setup(x => x.GetByMaterialAsync(_material.Id, _userId)).ReturnsAsync(Stored(VideoId));
        var limitLock = new Mock<ILimitLock>();
        limitLock.Setup(x => x.AcquireAsync(It.IsAny<LimitScope>(), It.IsAny<Guid>())).ReturnsAsync(Mock.Of<ILimitLockScope>());
        var questions = new Mock<IQuestionRepository>();
        questions.Setup(x => x.GetActiveQuestionsAsync(_material.Id, _userId)).ReturnsAsync([]);
        var saved = new List<Question>();
        questions.Setup(x => x.AddQuestionAsync(It.IsAny<Question>())).Callback((Question q) => saved.Add(q));

        IAiPrompt<IReadOnlyList<GeneratedQuestion>>? asked = null;
        _ai.Setup(x => x.RunAsync(
                It.IsAny<IAiPrompt<IReadOnlyList<GeneratedQuestion>>>(),
                It.IsAny<Func<IReadOnlyList<GeneratedQuestion>, bool>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback((IAiPrompt<IReadOnlyList<GeneratedQuestion>> prompt, Func<IReadOnlyList<GeneratedQuestion>, bool> _, string _, CancellationToken _) => asked = prompt)
            .ReturnsAsync(new AiResult<IReadOnlyList<GeneratedQuestion>>(
                [new GeneratedQuestion(QuestionType.Open, "What is said first?", QuestionDifficulty.Easy, "Old line", [], false, null, 1)],
                FakeAiProvider.Completion("A", "{}")));

        var sut = new QuestionGenerationService(
            questions.Object, _materials.Object, Mock.Of<INoteRepository>(), Mock.Of<IOptionService>(),
            _ai.Object, _user.Object, limitLock.Object, videos: _videos.Object);

        var result = await sut.GenerateAsync(_material.Id, new QuestionGenerateDto { Count = 1, FromVideo = true, NoteId = Guid.NewGuid() });

        asked!.BuildRequest().UserPrompt.ShouldContain("[00:01] Old line.");
        result!.Single().VideoSeconds.ShouldBe(1);
        saved.Single().NoteId.ShouldBeNull();
    }

    [Fact]
    public async Task Questions_FromVideoWithoutSubtitles_AreRefusedBeforeAi()
    {
        var sut = new QuestionGenerationService(
            Mock.Of<IQuestionRepository>(), _materials.Object, Mock.Of<INoteRepository>(), Mock.Of<IOptionService>(),
            _ai.Object, _user.Object, Mock.Of<ILimitLock>(), videos: _videos.Object);

        await Should.ThrowAsync<BadRequestException>(
            () => sut.GenerateAsync(_material.Id, new QuestionGenerateDto { Count = 3, FromVideo = true }));

        _ai.VerifyNoOtherCalls();
    }
}
