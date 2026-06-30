using StServer.Application.DTOs.Tag;
using StServer.Domain.Entities;

namespace StServer.Application.Mappers;

public class TagMapper
{
    public static Tag ToEntity(TagCreateDto dto, Guid userId)
    {
        return new Tag
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name,
        };
    }

    public static TagResponseDto ToDto(Tag entity)
    {
        return new TagResponseDto
        {
            Id = entity.Id,
            Name = entity.Name
        };
    }

    public static void ApplyUpdate(Tag entity, TagUpdateDto dto)
    {
        if (dto.Name is not null)
            entity.Name = dto.Name;
    }
}
