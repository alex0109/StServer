using System.Text.Json;
using Application.Mappers;
using Domain.Entities;
using Domain.Utility.Dictionary;
using Domain.Utility.Material;
using Domain.Utility.Note;
using Domain.Utility.Question;
using Shouldly;

namespace UnitTests.Mappers;

public class MaterialCopyMapperTests
{
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _recipientId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    private Material CreateSource()
    {
        var material = new Material
        {
            Id = Guid.NewGuid(),
            UserId = _ownerId,
            Title = "Photosynthesis",
            Type = MaterialType.article,
            Link = "https://example.com",
            Content = JsonDocument.Parse("""{"type":"doc"}"""),
            Status = MaterialStatus.finished,
            IsActive = true,
            CreatedAt = _now.AddDays(-10),
        };
        material.MaterialTags.Add(new MaterialTag { MaterialId = material.Id, TagId = Guid.NewGuid() });

        var textNote = new Note
        {
            Id = Guid.NewGuid(), UserId = _ownerId, MaterialId = material.Id, Title = "Light",
            Type = NoteType.text, TextContent = JsonDocument.Parse("""{"type":"doc"}"""), Order = 1
        };
        var pdfNote = new Note
        {
            Id = Guid.NewGuid(), UserId = _ownerId, MaterialId = material.Id, Title = "Scan",
            Type = NoteType.pdfAnnotation, FileId = Guid.NewGuid(), Order = 2
        };
        material.Notes.Add(textNote);
        material.Notes.Add(pdfNote);

        var dictionary = new Dictionary
        {
            Id = Guid.NewGuid(), UserId = _ownerId, MaterialId = material.Id, Title = "Terms", CreatedAt = _now
        };
        var entry = new DictionaryEntry
        {
            Id = Guid.NewGuid(), DictionaryId = dictionary.Id, Term = "chlorophyll", Translation = "хлорофіл",
            PartOfSpeech = PartOfSpeech.Noun, Synonyms = ["pigment"], Position = 0, IsArchived = true,
            CreatedAt = _now,
            Progress = new DictionaryEntryProgress { UserId = _ownerId, CreatedAt = _now }
        };
        dictionary.Entries.Add(entry);
        material.Dictionaries.Add(dictionary);

        var optionsQuestion = new Question
        {
            Id = Guid.NewGuid(), UserId = _ownerId, MaterialId = material.Id, NoteId = textNote.Id,
            Title = "Products?", QuestionType = QuestionType.Options, Version = 4, IsActive = true, CreatedAt = _now,
        };
        optionsQuestion.Options.Add(new Option { Id = Guid.NewGuid(), QuestionId = optionsQuestion.Id, Name = "Oxygen", Position = 1, IsCorrect = true });
        optionsQuestion.Options.Add(new Option { Id = Guid.NewGuid(), QuestionId = optionsQuestion.Id, Name = "Glucose", Position = 0, IsCorrect = true });

        var pdfQuestion = new Question
        {
            Id = Guid.NewGuid(), UserId = _ownerId, MaterialId = material.Id, NoteId = pdfNote.Id,
            Title = "From scan", Answer = "a", QuestionType = QuestionType.Open, IsActive = true, CreatedAt = _now,
        };
        var deletedQuestion = new Question
        {
            Id = Guid.NewGuid(), UserId = _ownerId, MaterialId = material.Id,
            Title = "Old", Answer = "b", QuestionType = QuestionType.Open, IsActive = false,
            DeletedAt = _now, CreatedAt = _now,
        };
        material.Questions.Add(optionsQuestion);
        material.Questions.Add(pdfQuestion);
        material.Questions.Add(deletedQuestion);

        return material;
    }

    [Fact]
    public void Copy_BelongsToRecipientAndIsMarkedImported()
    {
        var source = CreateSource();

        var copy = MaterialCopyMapper.Copy(source, _recipientId, _now);

        copy.Id.ShouldNotBe(source.Id);
        copy.UserId.ShouldBe(_recipientId);
        copy.Title.ShouldBe(source.Title);
        copy.Link.ShouldBe(source.Link);
        copy.Content!.RootElement.GetRawText().ShouldBe(source.Content!.RootElement.GetRawText());
        copy.Content.ShouldNotBeSameAs(source.Content);
        copy.Status.ShouldBe(MaterialStatus.tolearn);
        copy.ImportedFromMaterialId.ShouldBe(source.Id);
        copy.ImportedAt.ShouldBe(_now);
        copy.Assessments.Single().UserId.ShouldBe(_recipientId);
    }

    [Fact]
    public void Copy_DoesNotTakeTags()
    {
        var copy = MaterialCopyMapper.Copy(CreateSource(), _recipientId, _now);

        copy.MaterialTags.ShouldBeEmpty();
    }

    [Fact]
    public void Copy_TakesNotesExceptPdfOnes()
    {
        var source = CreateSource();

        var copy = MaterialCopyMapper.Copy(source, _recipientId, _now);

        var note = copy.Notes.Single();
        note.Title.ShouldBe("Light");
        note.UserId.ShouldBe(_recipientId);
        note.MaterialId.ShouldBe(copy.Id);
        note.Id.ShouldNotBe(source.Notes.First().Id);
    }

    [Fact]
    public void Copy_TakesDictionariesWithoutLearningState()
    {
        var copy = MaterialCopyMapper.Copy(CreateSource(), _recipientId, _now);

        var dictionary = copy.Dictionaries.Single();
        dictionary.UserId.ShouldBe(_recipientId);
        var entry = dictionary.Entries.Single();
        entry.DictionaryId.ShouldBe(dictionary.Id);
        entry.Term.ShouldBe("chlorophyll");
        entry.Synonyms.ShouldBe(["pigment"]);
        entry.IsArchived.ShouldBeFalse();
        entry.Progress.ShouldBeNull();
    }

    [Fact]
    public void Copy_TakesActiveQuestionsAndRemapsNotes()
    {
        var copy = MaterialCopyMapper.Copy(CreateSource(), _recipientId, _now);

        copy.Questions.Count.ShouldBe(2);

        var options = copy.Questions.Single(x => x.QuestionType == QuestionType.Options);
        options.NoteId.ShouldBe(copy.Notes.Single().Id);
        options.Version.ShouldBe(1);
        options.Options.Select(x => x.Name).ShouldBe(["Glucose", "Oxygen"]);
        options.Options.ShouldAllBe(x => x.IsCorrect && x.QuestionId == options.Id);

        // Its note was a PDF one and is not copied
        copy.Questions.Single(x => x.Title == "From scan").NoteId.ShouldBeNull();
    }
}
