using Application.DTOs.Tag;
using Application.Mappers;
using Domain.Entities;
using Shouldly;

namespace UnitTests.Mappers;

public class TagMapperTests
{
    [Fact]
    public void ToEntity_ShouldMapProperties()
    {
        var userId = Guid.NewGuid();

        var dto = new TagCreateDto
        {
            Name = "Programming",
            Color = "#FF0000"
        };

        var result = TagMapper.ToEntity(dto, userId);

        result.ShouldNotBeNull();

        result.Id.ShouldNotBe(Guid.Empty);
        result.UserId.ShouldBe(userId);
        result.Name.ShouldBe("Programming");
        result.Color.ShouldBe("#FF0000");
    }


    [Fact]
    public void ToDto_ShouldMapProperties()
    {
        var entity = new Tag
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Backend",
            Color = "#000000"
        };

        var result = TagMapper.ToDto(entity);

        result.ShouldNotBeNull();

        result.Id.ShouldBe(entity.Id);
        result.Name.ShouldBe(entity.Name);
        result.Color.ShouldBe(entity.Color);
    }


    [Fact]
    public void ApplyUpdate_ShouldUpdateProvidedFields()
    {
        var entity = new Tag
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Old name",
            Color = "#FFFFFF"
        };

        var dto = new TagUpdateDto
        {
            Name = "New name",
            Color = "#000000"
        };

        TagMapper.ApplyUpdate(entity, dto);

        entity.Name.ShouldBe("New name");
        entity.Color.ShouldBe("#000000");
    }


    [Fact]
    public void ApplyUpdate_NullValues_ShouldKeepOldValues()
    {
        var entity = new Tag
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Name",
            Color = "#FFFFFF"
        };

        var dto = new TagUpdateDto();

        TagMapper.ApplyUpdate(entity, dto);

        entity.Name.ShouldBe("Name");
        entity.Color.ShouldBe("#FFFFFF");
    }
}