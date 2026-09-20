using System.Text.Json;
using Application.DTOs.Note;
using Domain.Entities;

namespace Application.Mappers;

public static class NoteMapper
{
    public static Note ToEntity(NoteCreateDto dto, Guid materialId, Guid userId)
    {
        return new Note
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MaterialId = materialId,
            Title = dto.Title,
            Type = dto.Type,
            Order = 0,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static NoteResponseDto ToDto(Note entity)
    {
        return new NoteResponseDto()
        {
            Id = entity.Id,
            MaterialId = entity.MaterialId,
            Title = entity.Title,
            Type = entity.Type,
            TextContent = entity.TextContent,
            DrawingContent = entity.DrawingContent,
            Order = entity.Order,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
    
    public static void ApplyUpdate(Note entity, NoteUpdateDto dto)
    {
        if (dto.Title is not null)
            entity.Title = dto.Title;
            
        if (dto.TextContent is not null)
            entity.TextContent = dto.TextContent;
        
        if (dto.DrawingContent is not null)
            entity.DrawingContent = JsonSerializer.SerializeToDocument(dto.DrawingContent);
        
        entity.UpdatedAt = DateTime.UtcNow;
    }
}