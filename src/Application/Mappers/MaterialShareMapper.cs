using Application.DTOs.Share;
using Domain.Entities;
using Domain.Utility.Note;

namespace Application.Mappers;

public static class MaterialShareMapper
{
    public static MaterialShare ToEntity(Guid materialId, Guid userId, string token, DateTime now)
    {
        return new MaterialShare
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            UserId = userId,
            Token = token,
            CreatedAt = now,
        };
    }

    public static MaterialShareResponseDto ToDto(MaterialShare entity)
    {
        return new MaterialShareResponseDto
        {
            MaterialId = entity.MaterialId,
            Token = entity.Token,
            ImportsCount = entity.ImportsCount,
            CreatedAt = entity.CreatedAt,
        };
    }

    public static SharedMaterialPreviewDto ToPreviewDto(MaterialShare entity, bool isOwner, bool isAlreadyImported)
    {
        var material = entity.Material;

        return new SharedMaterialPreviewDto
        {
            Token = entity.Token,
            Title = material.Title,
            Type = material.Type,
            NotesCount = material.Notes.Count(x => x.Type != NoteType.pdfAnnotation),
            QuestionsCount = material.Questions.Count(x => x.IsActive && x.DeletedAt is null),
            DictionariesCount = material.Dictionaries.Count,
            WordsCount = material.Dictionaries.Sum(x => x.Entries.Count),
            IsOwner = isOwner,
            IsAlreadyImported = isAlreadyImported,
            SharedAt = entity.CreatedAt,
        };
    }
}
