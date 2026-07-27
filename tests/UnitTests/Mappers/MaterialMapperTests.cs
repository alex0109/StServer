using System.Text.Json;
using Application.DTOs.RichTextDocument;
using Application.DTOs.Material;
using Application.Mappers;
using Domain.Entities;
using Domain.Utility.Material;
using Shouldly;

namespace UnitTests.Mappers;

public class MaterialMapperTests
{
    [Fact]
    public void ToEntity_ShouldCreateNewEntityWithGeneratedId()
    {
        var dto = new MaterialCreateDto
        {
            Title = "Material",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn
        };

        var result = MaterialMapper.ToEntity(dto, Guid.NewGuid());

        result.Id.ShouldNotBe(Guid.Empty);
    }
    
    [Fact]
    public void ToEntity_ShouldSetActiveByDefault()
    {
        var dto = new MaterialCreateDto
        {
            Title = "Material",
            Type = MaterialType.video,
            Status = MaterialStatus.finished
        };

        var result = MaterialMapper.ToEntity(dto, Guid.NewGuid());

        result.IsActive.ShouldBeTrue();
    }
    
    [Fact]
    public void ToDto_WhenNoAssessments_ShouldHaveNullAssessmentId()
    {
        var entity = CreateMaterial();

        var result = MaterialMapper.ToDto(entity);

        result.AssessmentId.ShouldBeNull();
    }
    
    [Fact]
    public void ToDto_WithAssessment_ShouldMapFirstAssessmentId()
    {
        var assessmentId = Guid.NewGuid();

        var entity = CreateMaterial();
        entity.Assessments =
        [
            new Assessment
            {
                Id = assessmentId
            }
        ];

        var result = MaterialMapper.ToDto(entity);

        result.AssessmentId.ShouldBe(assessmentId);
    }
    
    [Fact]
    public void ToDto_WithMultipleAssessments_ShouldTakeFirstOne()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        var entity = CreateMaterial();

        entity.Assessments =
        [
            new Assessment
            {
                Id = firstId
            },
            new Assessment
            {
                Id = secondId
            }
        ];

        var result = MaterialMapper.ToDto(entity);

        result.AssessmentId.ShouldBe(firstId);
    }
    
    [Fact]
    public void ToDto_WithoutTags_ShouldReturnEmptyList()
    {
        var entity = CreateMaterial();

        var result = MaterialMapper.ToDto(entity);

        result.MaterialTags.ShouldNotBeNull();
        result.MaterialTags.ShouldBeEmpty();
    }
    
    [Fact]
    public void ToDto_WithNullContent_ShouldReturnNull()
    {
        var entity = CreateMaterial();

        entity.Content = null;

        var result = MaterialMapper.ToDto(entity);

        result.Content.ShouldBeNull();
    }
    
    [Fact]
    public void ToDto_WithComplexContent_ShouldDeserializeAllNodes()
    {
        var document = new RichTextDocument
        {
            Type = "doc",
            Content =
            [
                new Node
                {
                    Type = "heading",
                    Text = "Title",
                }
            ]
        };

        var entity = CreateMaterial();

        entity.Content = JsonDocument.Parse(
            JsonSerializer.Serialize(document));

        var result = MaterialMapper.ToDto(entity);

        result.Content.ShouldNotBeNull();

        result.Content.Content[0].Type
            .ShouldBe("heading");
    }
    
    [Fact]
    public void ApplyUpdate_ShouldUpdateOnlyTitle()
    {
        var entity = CreateMaterial();

        var dto = new MaterialUpdateDto
        {
            Title = "New title"
        };

        MaterialMapper.ApplyUpdate(entity, dto);

        entity.Title.ShouldBe("New title");
        entity.Type.ShouldBe(MaterialType.article);
        entity.Link.ShouldBe("old-link");
    }
    
    [Fact]
    public void ApplyUpdate_ShouldUpdateOnlyType()
    {
        var entity = CreateMaterial();

        var dto = new MaterialUpdateDto
        {
            Type = MaterialType.video
        };

        MaterialMapper.ApplyUpdate(entity, dto);

        entity.Type.ShouldBe(MaterialType.video);
    }
    
    [Fact]
    public void ApplyUpdate_ShouldUpdateOnlyLink()
    {
        var entity = CreateMaterial();

        var dto = new MaterialUpdateDto
        {
            Link = "new-link"
        };

        MaterialMapper.ApplyUpdate(entity, dto);

        entity.Link.ShouldBe("new-link");
    }
    
    [Fact]
    public void ApplyUpdate_ShouldSerializeContent()
    {
        var entity = CreateMaterial();

        var content = new RichTextDocument
        {
            Type = "doc"
        };

        var dto = new MaterialUpdateDto
        {
            Content = content
        };


        MaterialMapper.ApplyUpdate(entity, dto);


        entity.Content.ShouldNotBeNull();

        var restored =
            JsonSerializer.Deserialize<RichTextDocument>(
                entity.Content);

        restored.ShouldNotBeNull();
        restored.Type.ShouldBe("doc");
    }
    
    [Fact]
    public void ApplyUpdate_ShouldUpdateStatus()
    {
        var entity = CreateMaterial();

        var dto = new MaterialUpdateDto
        {
            Status = MaterialStatus.finished
        };

        MaterialMapper.ApplyUpdate(entity, dto);

        entity.Status.ShouldBe(MaterialStatus.finished);
    }
    
    [Fact]
    public void ApplyUpdate_ShouldAlwaysUpdateUpdatedAt()
    {
        var entity = CreateMaterial();

        var old = entity.UpdatedAt;

        MaterialMapper.ApplyUpdate(
            entity,
            new MaterialUpdateDto());

        entity.UpdatedAt.ShouldBeGreaterThan(old);
    }
    
    private static Material CreateMaterial()
    {
        return new Material
        {
            Id = Guid.NewGuid(),
            Title = "Original",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn,
            Link = "old-link",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-1),
            Version = 1
        };
    }
}