using Application.DTOs.Tag;
using Domain.Entities;

namespace Application.Mappers;

public class TagMapper
{
    public static Tag ToEntity(TagCreateDto dto, Guid userId)
    {
        return new Tag
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name,
            Color = dto.Color,
        };
    }

    public static TagResponseDto ToDto(Tag entity)
    {
        return new TagResponseDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Color = entity.Color
        };
    }

    public static void ApplyUpdate(Tag entity, TagUpdateDto dto)
    {
        if (dto.Name is not null)
            entity.Name = dto.Name;
        
        if (dto.Color is not null)
            entity.Name = dto.Color;
    }
}
