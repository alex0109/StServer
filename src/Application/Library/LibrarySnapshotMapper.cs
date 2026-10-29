using System.Text.Json;
using Application.DTOs.RichTextDocument;
using Domain.Entities;
using Domain.Utility.Material;
using Domain.Utility.Note;
using Domain.Utility.Question;

namespace Application.Library;

public static class LibrarySnapshotMapper
{
    // The material with its notes, questions (with options) and dictionaries (with words) loaded
    public static LibrarySnapshot FromMaterial(Material material)
    {
        var noteKeys = new Dictionary<Guid, int>();
        var entryKeys = new Dictionary<Guid, int>();

        // PDF notes depend on files in storage, those don't go to the library
        var notes = material.Notes
            .Where(x => x.Type != NoteType.pdfAnnotation)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(note =>
            {
                var key = noteKeys.Count + 1;
                noteKeys[note.Id] = key;

                return new LibraryNoteSnapshot
                {
                    Key = key,
                    Title = note.Title,
                    Type = note.Type,
                    // The library shows text in the editor's shape, not in the database one
                    TextContent = note.TextContent is null ? null : RichTextJson.ToClient(note.TextContent.RootElement),
                    DrawingContent = note.DrawingContent?.RootElement.Clone(),
                };
            })
            .ToList();

        var dictionaries = material.Dictionaries
            .OrderBy(x => x.CreatedAt)
            .Select(dictionary => new LibraryDictionarySnapshot
            {
                Title = dictionary.Title,
                Description = dictionary.Description,
                Entries = dictionary.Entries
                    .OrderBy(x => x.Position)
                    .Select(entry =>
                    {
                        var key = entryKeys.Count + 1;
                        entryKeys[entry.Id] = key;

                        return new LibraryEntrySnapshot
                        {
                            Key = key,
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
                        };
                    })
                    .ToList(),
            })
            .ToList();

        var questions = material.Questions
            .Where(x => x.IsActive && x.DeletedAt is null)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(question => new LibraryQuestionSnapshot
            {
                Title = question.Title,
                Answer = question.Answer,
                QuestionType = question.QuestionType,
                QuestionDifficulty = question.QuestionDifficulty,
                Explanation = question.Explanation,
                NoteKey = question.NoteId is Guid noteId && noteKeys.TryGetValue(noteId, out var noteKey) ? noteKey : null,
                EntryKey = question.DictionaryEntryId is Guid entryId && entryKeys.TryGetValue(entryId, out var entryKey) ? entryKey : null,
                Options = question.Options
                    .OrderBy(x => x.Position)
                    .Select(x => new LibraryOptionSnapshot { Name = x.Name, Position = x.Position, IsCorrect = x.IsCorrect })
                    .ToList(),
            })
            .ToList();

        return new LibrarySnapshot
        {
            Title = material.Title,
            Type = material.Type,
            // Readers get it as a link: only web addresses
            Link = IsWebLink(material.Link) ? material.Link : null,
            Content = material.Content is null ? null : RichTextJson.ToClient(material.Content.RootElement),
            Notes = notes,
            Questions = questions,
            Dictionaries = dictionaries,
        };
    }

    // A new material of the user, marked as imported from the library
    public static Material ToMaterial(LibrarySnapshot snapshot, Guid libraryItemId, Guid userId, DateTime now)
    {
        var material = new Material
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = snapshot.Title,
            Type = snapshot.Type,
            Link = snapshot.Link,
            // Stored like every other material, so Studytracker reads the copy as its own
            Content = ToStoredText(snapshot.Content),
            Status = MaterialStatus.tolearn,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
            ImportedFromLibraryItemId = libraryItemId,
            ImportedAt = now,
        };

        material.Assessments.Add(new Assessment { Id = Guid.NewGuid(), MaterialId = material.Id, UserId = userId });

        var noteIds = new Dictionary<int, Guid>();
        var order = 0;
        foreach (var note in snapshot.Notes)
        {
            var copy = new Note
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MaterialId = material.Id,
                Title = note.Title,
                Type = note.Type,
                TextContent = ToStoredText(note.TextContent),
                DrawingContent = ToDocument(note.DrawingContent),
                Order = order++,
                // A millisecond apart (the database keeps microseconds), so notes keep the library order
                CreatedAt = now.AddMilliseconds(order),
                UpdatedAt = now,
            };

            material.Notes.Add(copy);
            noteIds[note.Key] = copy.Id;
        }

        var entryIds = new Dictionary<int, Guid>();
        var dictionaryIndex = 0;
        foreach (var dictionary in snapshot.Dictionaries)
        {
            var copy = new Dictionary
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MaterialId = material.Id,
                Title = dictionary.Title,
                Description = dictionary.Description,
                CreatedAt = now.AddMilliseconds(++dictionaryIndex),
                UpdatedAt = now,
            };

            foreach (var entry in dictionary.Entries)
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
                entryIds[entry.Key] = entryCopy.Id;
            }

            material.Dictionaries.Add(copy);
        }

        var questionIndex = 0;
        foreach (var question in snapshot.Questions)
        {
            var questionId = Guid.NewGuid();

            material.Questions.Add(new Question
            {
                Id = questionId,
                UserId = userId,
                MaterialId = material.Id,
                NoteId = question.NoteKey is int noteKey && noteIds.TryGetValue(noteKey, out var noteId) ? noteId : null,
                DictionaryEntryId = question.EntryKey is int entryKey && entryIds.TryGetValue(entryKey, out var entryId) ? entryId : null,
                Title = question.Title,
                Answer = question.Answer,
                QuestionType = question.QuestionType,
                QuestionDifficulty = question.QuestionDifficulty,
                Explanation = question.Explanation,
                Version = 1,
                IsActive = true,
                CreatedAt = now.AddMilliseconds(++questionIndex),
                UpdatedAt = now,
                Options = question.Options
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

    public static LibraryCounts Count(LibrarySnapshot snapshot) => new(
        Notes: snapshot.Notes.Count,
        // Flashcard questions mirror dictionary words, the library shows the words instead
        Questions: snapshot.Questions.Count(x => x.QuestionType != QuestionType.Flashcard),
        Dictionaries: snapshot.Dictionaries.Count,
        Words: snapshot.Dictionaries.Sum(x => x.Entries.Count));

    public static bool IsWebLink(string? link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static JsonDocument? ToStoredText(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } value ? RichTextJson.ToStored(value) : null;

    private static JsonDocument? ToDocument(JsonElement? element) =>
        element is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } value
            ? JsonDocument.Parse(value.GetRawText())
            : null;
}

public record LibraryCounts(int Notes, int Questions, int Dictionaries, int Words);
