using System.Text.Json;
using StServer.Domain.Entities;
using StServer.Application.DTOs;
using StServer.Application.DTOs.Material;

namespace StServer.Application.Mappers;

public static class MaterialMapper
{
    public static Material ToEntity(MaterialCreateDto dto)
    {
        return new Material
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Type = dto.Type,
            Tags = dto.Tags,
            Link = dto.Link,
            Status = dto.Status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Description = dto.Description == null
                ? null
                : JsonSerializer.SerializeToDocument(dto.Description)
        };
    }

    public static MaterialResponseDto ToDto(Material entity)
    {
        return new MaterialResponseDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Type = entity.Type,
            Tags = entity.Tags,
            Link = entity.Link,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Description = entity.Description == null
                ? null
                : JsonSerializer.Deserialize<RichTextDocument>(entity.Description)
        };
    }
    
    public static void ApplyUpdate(Material entity, MaterialUpdateDto dto)
    {
        if (dto.Title is not null)
            entity.Title = dto.Title;

        if (dto.Type is not null)
            entity.Type = dto.Type;

        if (dto.Tags is not null)
            entity.Tags = dto.Tags;

        if (dto.Link is not null)
            entity.Link = dto.Link;

        if (dto.Description is not null)
            entity.Description = dto.Description == null
                ? null
                : JsonSerializer.SerializeToDocument(dto.Description);

        if (dto.Status is not null)
            entity.Status = dto.Status;

        entity.UpdatedAt = DateTime.UtcNow;
    }
}