using System.Text.Json;
using Domain.Entities;
using Domain.Utility.Material;
using Domain.Utility.Note;

namespace Application.Mappers;

// Deep copy of a shared material for another user: notes, dictionaries and questions get new ids,
// links between them are remapped. Tags, attempts and learning progress stay with the owner
public static class MaterialCopyMapper
{
    public static Material Copy(Material source, Guid userId, DateTime now)
    {
        var material = new Material
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = source.Title,
            Type = source.Type,
            Link = source.Link,
            Content = Clone(source.Content),
            Status = MaterialStatus.tolearn,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
            ImportedFromMaterialId = source.Id,
            ImportedAt = now,
        };

        material.Assessments.Add(new Assessment
        {
            Id = Guid.NewGuid(),
            MaterialId = material.Id,
            UserId = userId
        });

        var noteIds = new Dictionary<Guid, Guid>();

        // PDF notes depend on files in storage, those are not copied
        foreach (var note in source.Notes.Where(x => x.Type != NoteType.pdfAnnotation).OrderBy(x => x.Order))
        {
            var copy = new Note
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MaterialId = material.Id,
                Title = note.Title,
                Type = note.Type,
                TextContent = Clone(note.TextContent),
                DrawingContent = Clone(note.DrawingContent),
                Order = note.Order,
                CreatedAt = now,
                UpdatedAt = now,
            };

            material.Notes.Add(copy);
            noteIds[note.Id] = copy.Id;
        }

        var entryIds = new Dictionary<Guid, Guid>();

        foreach (var dictionary in source.Dictionaries.OrderBy(x => x.CreatedAt))
        {
            var copy = new Dictionary
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MaterialId = material.Id,
                Title = dictionary.Title,
                Description = dictionary.Description,
                CreatedAt = now,
                UpdatedAt = now,
            };

            foreach (var entry in dictionary.Entries.OrderBy(x => x.Position))
            {
                var entryCopy = new DictionaryEntry
                {
                    Id = Guid.NewGuid(),
                    DictionaryId = copy.Id,
                    Term = entry.Term,
                    Translation = entry.Translation,
                    Transcription = entry.Transcription,
                    PartOfSpeech = entry.PartOfSpeech,
                    GrammarInfo = entry.GrammarInfo,
                    Example = entry.Example,
                    ExampleTranslation = entry.ExampleTranslation,
                    Note = entry.Note,
                    Synonyms = [..entry.Synonyms],
                    Position = entry.Position,
                    IsArchived = false,
                    CreatedAt = now,
                    UpdatedAt = now,
                };

                copy.Entries.Add(entryCopy);
                entryIds[entry.Id] = entryCopy.Id;
            }

            material.Dictionaries.Add(copy);
        }

        foreach (var question in source.Questions.Where(x => x.IsActive && x.DeletedAt is null))
        {
            var questionId = Guid.NewGuid();

            material.Questions.Add(new Question
            {
                Id = questionId,
                UserId = userId,
                MaterialId = material.Id,
                NoteId = question.NoteId is Guid noteId && noteIds.TryGetValue(noteId, out var newNoteId)
                    ? newNoteId
                    : null,
                DictionaryEntryId = question.DictionaryEntryId is Guid entryId && entryIds.TryGetValue(entryId, out var newEntryId)
                    ? newEntryId
                    : null,
                Title = question.Title,
                Answer = question.Answer,
                QuestionType = question.QuestionType,
                QuestionDifficulty = question.QuestionDifficulty,
                Explanation = question.Explanation,
                Version = 1,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                Options = question.Options
                    .OrderBy(x => x.Position)
                    .Select(x => new Option
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = questionId,
                        Name = x.Name,
                        Position = x.Position,
                        IsCorrect = x.IsCorrect,
                    })
                    .ToList(),
            });
        }

        return material;
    }

    private static JsonDocument? Clone(JsonDocument? document) =>
        document is null ? null : JsonDocument.Parse(document.RootElement.GetRawText());
}
