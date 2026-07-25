using Application.DTOs.Option;
using Application.DTOs.Question;
using Application.Mappers;
using Domain.Entities;
using Domain.Utility.Question;
using Shouldly;

namespace UnitTests.Mappers;

public class QuestionMapperTests
{
    [Fact]
    public void ToEntityOpenQuestion_ShouldMapProperties()
    {
        var materialId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var dto = new OpenQuestionCreateDto
        {
            Title = "What is C#?",
            Answer = ".NET language",
            Explanation = "Explanation",
            QuestionDifficulty = QuestionDifficulty.Hard
        };

        var result = QuestionMapper.ToEntityOpenQuestion(
            dto,
            materialId,
            userId);

        result.Id.ShouldNotBe(Guid.Empty);
        result.MaterialId.ShouldBe(materialId);
        result.UserId.ShouldBe(userId);
        result.Title.ShouldBe(dto.Title);
        result.Answer.ShouldBe(dto.Answer);
        result.Explanation.ShouldBe(dto.Explanation);
        result.QuestionDifficulty.ShouldBe(QuestionDifficulty.Hard);
        result.QuestionType.ShouldBe(QuestionType.Open);

        result.CreatedAt.ShouldBeInRange(
            DateTime.UtcNow.AddSeconds(-2),
            DateTime.UtcNow);
    }


    [Fact]
    public void ToEntityOptionsQuestion_ShouldMapProperties()
    {
        var materialId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var dto = new OptionQuestionCreateDto
        {
            Title = "2 + 2 = ?",
            CorrectOptionId = Guid.NewGuid(),
            Explanation = "Math",
            QuestionDifficulty = QuestionDifficulty.Medium,
            Options =
            [
                new OptionUpdateDto
                {
                    Name = "4"
                }
            ]
        };

        var result = QuestionMapper.ToEntityOptionsQuestion(
            dto,
            materialId,
            userId);

        result.Id.ShouldNotBe(Guid.Empty);
        result.MaterialId.ShouldBe(materialId);
        result.UserId.ShouldBe(userId);
        result.Title.ShouldBe(dto.Title);
        result.QuestionType.ShouldBe(QuestionType.Options);
        result.Explanation.ShouldBe(dto.Explanation);
        result.QuestionDifficulty.ShouldBe(QuestionDifficulty.Medium);
    }


    [Fact]
    public void ToDto_ShouldMapPropertiesAndOptions()
    {
        var correctOptionId = Guid.NewGuid();

        var entity = new Question
        {
            Id = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            Title = "Question",
            Answer = "Answer",
            QuestionType = QuestionType.Options,
            CorrectOptionId = correctOptionId,
            QuestionDifficulty = QuestionDifficulty.Easy,
            Explanation = "Explain",
            Version = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow,
            Options =
            [
                new Option
                {
                    Id = correctOptionId,
                    Name = "Correct"
                },
                new Option
                {
                    Id = Guid.NewGuid(),
                    Name = "Wrong"
                }
            ]
        };

        var result = QuestionMapper.ToDto(entity);

        result.Id.ShouldBe(entity.Id);
        result.MaterialId.ShouldBe(entity.MaterialId);
        result.Title.ShouldBe(entity.Title);
        result.Answer.ShouldBe(entity.Answer);
        result.QuestionType.ShouldBe(QuestionType.Options);
        result.QuestionDifficulty.ShouldBe(QuestionDifficulty.Easy);

        result.Options.ShouldNotBeNull();
        result.Options.Count.ShouldBe(2);

        result.Options[0].IsCorrect.ShouldBeTrue();
        result.Options[1].IsCorrect.ShouldBeFalse();
    }


    [Fact]
    public void ToReducedDto_ShouldMapOnlyRequiredFields()
    {
        var entity = new Question
        {
            Id = Guid.NewGuid(),
            Title = "Reduced question",
            QuestionType = QuestionType.Open,
            CreatedAt =  DateTime.UtcNow,
        };

        var result = QuestionMapper.ToReducedDto(entity);

        result.Id.ShouldBe(entity.Id);
        result.Title.ShouldBe(entity.Title);
        result.QuestionType.ShouldBe(QuestionType.Open);
    }


    [Fact]
    public void ApplyUpdate_ShouldUpdateProvidedFields()
    {
        var entity = new Question
        {
            Id = Guid.NewGuid(),
            Title = "Old title",
            Answer = "Old answer",
            QuestionDifficulty = QuestionDifficulty.Easy,
            Explanation = "Old explanation",
            CreatedAt =  DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow
        };

        var dto = new QuestionUpdateDto
        {
            Title = "New title",
            Answer = "New answer",
            CorrectOptionId = Guid.NewGuid(),
            QuestionDifficulty = QuestionDifficulty.Hard,
            Explanation = "New explanation",
            Options = []
        };

        var oldUpdatedAt = entity.UpdatedAt;

        QuestionMapper.ApplyUpdate(entity, dto);

        entity.Title.ShouldBe("New title");
        entity.Answer.ShouldBe("New answer");
        entity.CorrectOptionId.ShouldBe(dto.CorrectOptionId);
        entity.QuestionDifficulty.ShouldBe(QuestionDifficulty.Hard);
        entity.Explanation.ShouldBe("New explanation");

        entity.UpdatedAt.ShouldBeGreaterThan(oldUpdatedAt);
    }


    [Fact]
    public void ApplyUpdate_NullValues_ShouldKeepOldValues()
    {
        var entity = new Question
        {
            Id = Guid.NewGuid(),
            Title = "Old title",
            Answer = "Old answer",
            QuestionDifficulty = QuestionDifficulty.Easy,
            Explanation = "Old explanation",
            CreatedAt =  DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow
        };

        var dto = new QuestionUpdateDto
        {
            Options = []
        };

        QuestionMapper.ApplyUpdate(entity, dto);

        entity.Title.ShouldBe("Old title");
        entity.Answer.ShouldBe("Old answer");
        entity.QuestionDifficulty.ShouldBe(QuestionDifficulty.Easy);
        entity.Explanation.ShouldBe("Old explanation");
    }
}