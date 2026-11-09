using Application.Interfaces;
using Application.Services;
using Application.WordQuiz;
using Domain.Entities;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class WordQuizServiceTests
{
    private readonly Mock<IDictionaryRepository> _dictionaries = new();
    private readonly Mock<IUserContext> _user = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly WordQuizService _sut;

    public WordQuizServiceTests()
    {
        _user.Setup(x => x.UserId).Returns(_userId);
        _sut = new WordQuizService(_dictionaries.Object, _user.Object, new Random(3));
    }

    [Fact]
    public async Task SomeoneElsesWord_IsNotFound()
    {
        var result = await _sut.GetQuizAsync(Guid.NewGuid());

        result.ShouldBeNull();
        _dictionaries.Verify(
            x => x.GetQuizCandidatesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task OwnWord_IsQuizzedWithTheUsersOtherWords()
    {
        var entry = new DictionaryEntry
        {
            Id = Guid.NewGuid(),
            DictionaryId = Guid.NewGuid(),
            Term = "Hund",
            Translation = "собака",
            CreatedAt = DateTime.UtcNow,
        };
        _dictionaries.Setup(x => x.GetEntryByIdAsync(entry.Id, _userId)).ReturnsAsync(entry);
        _dictionaries.Setup(x => x.GetQuizCandidatesAsync(entry.Id, entry.DictionaryId, _userId, It.IsAny<int>()))
            .ReturnsAsync([new WordQuizCandidate("Katze", "кішка")]);

        var result = await _sut.GetQuizAsync(entry.Id);

        result.ShouldNotBeNull();
        result.EntryId.ShouldBe(entry.Id);
        result.Options.Select(o => o.Text).ShouldBe(["собака", "кішка"], ignoreOrder: true);
    }
}
