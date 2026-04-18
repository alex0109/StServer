using System.Text.Json;
using StServer.Entities;
using StServer.DTOs;
using StServer.Models;

namespace StServer.Mappers;

public static class MaterialMapper
{
    public static Material ToEntity(MaterialItemDto dto)
    {
        return new Material
        {
            Id = dto.Id,
            Title = dto.Title,
            Type = dto.Type,
            Tags = dto.Tags,
            Link = dto.Link,
            Status = dto.Status,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,

            Description = dto.Description == null
                ? null
                : JsonSerializer.Serialize(dto.Description)
        };
    }

    public static MaterialItemDto ToDto(Material entity)
    {
        return new MaterialItemDto
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
}