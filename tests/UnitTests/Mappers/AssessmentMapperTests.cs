using Application.DTOs.Assessment;
using Application.Mappers;
using Domain.Entities;
using Domain.Utility.Question;
using Shouldly;

namespace UnitTests.Mappers;

public class AssessmentMapperTests
{
    [Fact]
    public void ToEntity_ShouldMapProperties()
    {
        var materialId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = AssessmentMapper.ToEntity(materialId, userId);

        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        result.MaterialId.ShouldBe(materialId);
        result.UserId.ShouldBe(userId);
    }

    [Fact]
    public void ToDto_ShouldMapProperties()
    {
        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        var result = AssessmentMapper.ToDto(assessment);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(assessment.Id);
        result.MaterialId.ShouldBe(assessment.MaterialId);
    }

    [Fact]
    public void ToDto_NoLevelsChosen_ListsEveryLevel()
    {
        var result = AssessmentMapper.ToDto(new Assessment { Id = Guid.NewGuid(), MaterialId = Guid.NewGuid() });

        result.Difficulties.ShouldBe([QuestionDifficulty.Easy, QuestionDifficulty.Medium, QuestionDifficulty.Hard]);
        result.QuestionCount.ShouldBeNull();
    }

    [Fact]
    public void ToDto_ChosenLevelsAndCount_AreMapped()
    {
        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            TimerMinutes = 20,
            Difficulties = [QuestionDifficulty.Easy, QuestionDifficulty.Hard],
            QuestionCount = 25
        };

        var result = AssessmentMapper.ToDto(assessment);

        result.TimerMinutes.ShouldBe(20);
        result.Difficulties.ShouldBe([QuestionDifficulty.Easy, QuestionDifficulty.Hard]);
        result.QuestionCount.ShouldBe(25);
    }
}
