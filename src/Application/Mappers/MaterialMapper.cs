using System.Text.Json;
using Domain.Entities;
using Application.DTOs;
using Application.DTOs.Material;
using Application.DTOs.Tag;
using Domain.Utility.Material;

namespace Application.Mappers;

public static class MaterialMapper
{
    public static Material ToEntity(MaterialCreateDto dto, Guid userId)
    {
        return new Material
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = dto.Title,
            Type = dto.Type,
            Status = dto.Status,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static MaterialResponseDto ToDto(Material entity)
    {
        return new MaterialResponseDto
        {
            Id = entity.Id,
            AssessmentId = entity.Assessments.FirstOrDefault()?.Id,
            Title = entity.Title,
            Type = entity.Type,
            MaterialTags = entity.MaterialTags
                .Select(mt => new TagResponseDto
                {
                    Id = mt.Tag.Id,
                    Name = mt.Tag.Name,
                    Color = mt.Tag.Color,
                })
                .ToList(),
            Link = entity.Link,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Content = entity.Content == null
                ? null
                : JsonSerializer.Deserialize<RichTextDocument>(entity.Content),
            Version = entity.Version,
            IsActive =  entity.IsActive
        };
    }
    
    public static void ApplyUpdate(Material entity, MaterialUpdateDto dto)
    {
        if (dto.Title is not null)
            entity.Title = dto.Title;

        if (dto.Type is MaterialType type)
            entity.Type = type;

        if (dto.Link is not null)
            entity.Link = dto.Link;

        if (dto.Content is not null)
            entity.Content = JsonSerializer.SerializeToDocument(dto.Content);

        if (dto.Status is MaterialStatus status)
            entity.Status = status;

        entity.UpdatedAt = DateTime.UtcNow;
    }
}