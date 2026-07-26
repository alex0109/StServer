using Application.DTOs.Assessment;
using Application.Mappers;
using Domain.Entities;
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
}