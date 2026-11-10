using System.Text.Json;
using Application.DTOs.Note;
using Application.DTOs.RichTextDocument;
using Application.Essays;
using Domain.Entities;
using Domain.Utility.Note;

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
        var review = EssayRules.FromDocument(entity.EssayReview);

        return new NoteResponseDto()
        {
            Id = entity.Id,
            MaterialId = entity.MaterialId,
            Title = entity.Title,
            Type = entity.Type,
            TextContent = RichTextJson.Parse(entity.TextContent),
            DrawingContent = entity.DrawingContent,
            EssayTask = entity.EssayTask,
            EssayReview = review is null ? null : EssayReviewMapper.ToDto(review),
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
            entity.TextContent = JsonSerializer.SerializeToDocument(dto.TextContent);
        
        // "drawingContent": null arrives as a JSON null document; stored, it would break the note type check
        if (dto.DrawingContent is { RootElement.ValueKind: not JsonValueKind.Null })
            entity.DrawingContent = JsonSerializer.SerializeToDocument(dto.DrawingContent);

        // Other notes have no task, the database refuses one
        if (dto.EssayTask is not null && entity.Type == NoteType.essay)
            entity.EssayTask = EssayRules.NormalizeTask(dto.EssayTask);
        
        entity.UpdatedAt = DateTime.UtcNow;
    }
}
