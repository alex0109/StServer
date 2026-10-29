using System.Text.Json;
using Domain.Entities;
using Domain.Utility.Material;
using Domain.Utility.Note;
using Domain.Utility.Question;

namespace UnitTests.Library;

internal static class LibraryTestData
{
    // A material with a text, a drawing and a PDF note, a dictionary with two words,
    // an open question about the text note, an options question, a flashcard question
    // for the first word and a deleted question
    public static Material Material(Guid userId)
    {
        var materialId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var text = new Note
        {
            Id = Guid.NewGuid(), UserId = userId, MaterialId = materialId, Title = "Light phase", Type = NoteType.text,
            TextContent = JsonDocument.Parse("""{"Type":"doc","Content":[{"Type":"paragraph","Content":[{"Type":"text","Text":"ATP","Marks":[{"Type":"bold"}]}]}]}"""),
            CreatedAt = now,
        };
        var drawing = new Note
        {
            Id = Guid.NewGuid(), UserId = userId, MaterialId = materialId, Title = "Sketch", Type = NoteType.drawing,
            DrawingContent = JsonDocument.Parse("""{"strokes":[]}"""), CreatedAt = now.AddMinutes(1),
        };
        var pdf = new Note
        {
            Id = Guid.NewGuid(), UserId = userId, MaterialId = materialId, Title = "Scan", Type = NoteType.pdfAnnotation,
            CreatedAt = now.AddMinutes(2),
        };

        var dictionary = new Dictionary { Id = Guid.NewGuid(), UserId = userId, MaterialId = materialId, Title = "Terms", CreatedAt = now };
        var first = new DictionaryEntry { Id = Guid.NewGuid(), DictionaryId = dictionary.Id, Term = "chlorophyll", Translation = "хлорофіл", Position = 0, CreatedAt = now };
        var second = new DictionaryEntry { Id = Guid.NewGuid(), DictionaryId = dictionary.Id, Term = "stroma", Translation = "строма", Position = 1, CreatedAt = now, Synonyms = ["matrix"] };
        dictionary.Entries.Add(first);
        dictionary.Entries.Add(second);

        var material = new Material
        {
            Id = materialId,
            UserId = userId,
            Title = "Photosynthesis",
            Type = MaterialType.article,
            Link = "javascript:alert(1)",
            Content = JsonDocument.Parse("""{"Type":"doc","Content":[{"Type":"heading","Attrs":{"level":2},"Content":[{"Type":"text","Text":"Light"}]}]}"""),
            Status = MaterialStatus.inprocess,
            IsActive = true,
            CreatedAt = now,
            Notes = [text, drawing, pdf],
            Dictionaries = [dictionary],
        };

        material.Questions.Add(new Question
        {
            Id = Guid.NewGuid(), UserId = userId, MaterialId = materialId, NoteId = text.Id, Title = "What does it make?",
            Answer = "ATP", QuestionType = QuestionType.Open, CreatedAt = now,
        });
        var optionsId = Guid.NewGuid();
        material.Questions.Add(new Question
        {
            Id = optionsId, UserId = userId, MaterialId = materialId, Title = "Products?", QuestionType = QuestionType.Options,
            CreatedAt = now.AddSeconds(1),
            Options =
            [
                new Option { Id = Guid.NewGuid(), QuestionId = optionsId, Name = "Oxygen", Position = 0, IsCorrect = true },
                new Option { Id = Guid.NewGuid(), QuestionId = optionsId, Name = "Nitrogen", Position = 1 },
            ],
        });
        material.Questions.Add(new Question
        {
            Id = Guid.NewGuid(), UserId = userId, MaterialId = materialId, DictionaryEntryId = first.Id, Title = "chlorophyll",
            QuestionType = QuestionType.Flashcard, CreatedAt = now.AddSeconds(2),
        });
        material.Questions.Add(new Question
        {
            Id = Guid.NewGuid(), UserId = userId, MaterialId = materialId, Title = "Old one", QuestionType = QuestionType.Open,
            IsActive = false, DeletedAt = now, CreatedAt = now.AddSeconds(3),
        });

        return material;
    }
}
