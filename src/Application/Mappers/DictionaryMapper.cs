using Application.DTOs.Dictionary;
using Domain.Entities;
using Domain.Utility.Dictionary;

namespace Application.Mappers;

public static class DictionaryMapper
{
    public static Dictionary ToEntity(DictionaryCreateDto dto, Guid materialId, Guid userId)
    {
        return new Dictionary
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MaterialId = materialId,
            Title = dto.Title,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static DictionaryResponseDto ToDto(Dictionary entity)
    {
        return new DictionaryResponseDto
        {
            Id = entity.Id,
            MaterialId = entity.MaterialId,
            Title = entity.Title,
            Description = entity.Description,
            Entries = entity.Entries
                .OrderBy(e => e.Position)
                .Select(ToEntryDto)
                .ToList(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    public static void ApplyUpdate(Dictionary entity, DictionaryUpdateDto dto)
    {
        if (dto.Title is not null)
            entity.Title = dto.Title;

        if (dto.Description is not null)
            entity.Description = dto.Description;

        entity.UpdatedAt = DateTime.UtcNow;
    }

    public static DictionaryEntry ToEntryEntity(DictionaryEntryCreateDto dto, Guid dictionaryId, int position)
    {
        return new DictionaryEntry
        {
            Id = Guid.NewGuid(),
            DictionaryId = dictionaryId,
            Term = dto.Term,
            Translation = dto.Translation,
            Transcription = dto.Transcription,
            PartOfSpeech = dto.PartOfSpeech,
            GrammarInfo = dto.GrammarInfo,
            Example = dto.Example,
            ExampleTranslation = dto.ExampleTranslation,
            Note = dto.Note,
            Synonyms = NormalizeSynonyms(dto.Synonyms),
            Position = position,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static DictionaryEntryResponseDto ToEntryDto(DictionaryEntry entity)
    {
        return new DictionaryEntryResponseDto
        {
            Id = entity.Id,
            DictionaryId = entity.DictionaryId,
            Term = entity.Term,
            Translation = entity.Translation,
            Transcription = entity.Transcription,
            PartOfSpeech = entity.PartOfSpeech,
            GrammarInfo = entity.GrammarInfo,
            Example = entity.Example,
            ExampleTranslation = entity.ExampleTranslation,
            Note = entity.Note,
            Synonyms = entity.Synonyms,
            Position = entity.Position,
            IsArchived = entity.IsArchived,
            Progress = entity.Progress is null ? null : FlashcardMapper.ToProgressDto(entity.Progress),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    public static void ApplyEntryUpdate(DictionaryEntry entity, DictionaryEntryUpdateDto dto)
    {
        if (dto.Term is not null)
            entity.Term = dto.Term;

        if (dto.Translation is not null)
            entity.Translation = dto.Translation;

        if (dto.Transcription is not null)
            entity.Transcription = dto.Transcription;

        if (dto.PartOfSpeech is PartOfSpeech partOfSpeech)
            entity.PartOfSpeech = partOfSpeech;

        if (dto.GrammarInfo is not null)
            entity.GrammarInfo = dto.GrammarInfo;

        if (dto.Example is not null)
            entity.Example = dto.Example;

        if (dto.ExampleTranslation is not null)
            entity.ExampleTranslation = dto.ExampleTranslation;

        if (dto.Note is not null)
            entity.Note = dto.Note;

        if (dto.Synonyms is not null)
            entity.Synonyms = NormalizeSynonyms(dto.Synonyms);

        if (dto.IsArchived is bool isArchived)
            entity.IsArchived = isArchived;

        entity.UpdatedAt = DateTime.UtcNow;
    }

    private static List<string> NormalizeSynonyms(List<string>? synonyms)
    {
        if (synonyms is null) return [];

        return synonyms
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}