using System.Text.Json;
using Application.DTOs.RichTextDocument;
using Application.Library;
using Application.Mappers;
using Domain.Utility.Material;
using Domain.Utility.Note;
using Shouldly;

namespace UnitTests.Library;

public class LibrarySnapshotMapperTests
{
    private readonly Guid _author = Guid.NewGuid();
    private readonly Guid _reader = Guid.NewGuid();

    [Fact]
    public void Snapshot_KeepsContentNotesQuestionsAndWords()
    {
        var snapshot = LibrarySnapshotMapper.FromMaterial(LibraryTestData.Material(_author));

        snapshot.Title.ShouldBe("Photosynthesis");
        snapshot.Notes.Select(x => x.Title).ShouldBe(["Light phase", "Sketch"]);
        snapshot.Questions.Select(x => x.Title).ShouldBe(["What does it make?", "Products?", "chlorophyll"]);
        snapshot.Dictionaries.Single().Entries.Select(x => x.Term).ShouldBe(["chlorophyll", "stroma"]);
        snapshot.Content.ShouldNotBeNull();
    }

    [Fact]
    public void Snapshot_GivesTextInTheEditorShape()
    {
        var snapshot = LibrarySnapshotMapper.FromMaterial(LibraryTestData.Material(_author));

        var content = snapshot.Content!.Value;
        content.GetProperty("type").GetString().ShouldBe("doc");
        content.GetProperty("content")[0].GetProperty("attrs").GetProperty("level").GetInt32().ShouldBe(2);
        snapshot.Notes[0].TextContent!.Value.GetRawText().ShouldContain("\"text\":\"ATP\"");
        snapshot.Notes[0].TextContent!.Value.GetRawText().ShouldNotContain("\"Type\"", Case.Sensitive);
    }

    [Fact]
    public void OldSnapshotsWithDatabaseShapedText_AreFixedWhenRead()
    {
        var old = JsonDocument.Parse("""
            {"title":"Old","type":"article","content":{"Type":"doc","Content":[{"Type":"paragraph","Content":[{"Type":"text","Text":"Hello"}]}]},
             "notes":[{"key":1,"title":"N","type":"text","textContent":{"Type":"doc","Content":[]}}],"questions":[],"dictionaries":[]}
            """);

        var snapshot = LibrarySnapshotJson.FromDocument(old);

        snapshot.Content!.Value.GetRawText().ShouldContain("\"text\":\"Hello\"");
        snapshot.Notes[0].TextContent!.Value.GetProperty("type").GetString().ShouldBe("doc");
    }

    [Fact]
    public void ImportedCopy_OpensInStudytrackerLikeAnyMaterial()
    {
        var snapshot = LibrarySnapshotJson.FromDocument(
            LibrarySnapshotJson.ToDocument(LibrarySnapshotMapper.FromMaterial(LibraryTestData.Material(_author))));

        var copy = LibrarySnapshotMapper.ToMaterial(snapshot, Guid.NewGuid(), _reader, DateTime.UtcNow);
        var dto = MaterialMapper.ToDto(copy);

        dto.Content!.Content.Single().Content!.Single().Text.ShouldBe("Light");
        dto.Notes!.Single(x => x.Title == "Light phase").TextContent!.Content.Single().Content!.Single().Text.ShouldBe("ATP");
    }

    [Fact]
    public void MaterialsCopiedBeforeTheFix_StillOpen()
    {
        var material = LibraryTestData.Material(_author);
        material.Content = JsonDocument.Parse("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"camel"}]}]}""");

        MaterialMapper.ToDto(material).Content!.Content.Single().Content!.Single().Text.ShouldBe("camel");
    }

    [Fact]
    public void Snapshot_DropsUnsafeLinks()
    {
        LibrarySnapshotMapper.FromMaterial(LibraryTestData.Material(_author)).Link.ShouldBeNull();
    }

    [Fact]
    public void Counts_LeaveOutFlashcardQuestions()
    {
        var counts = LibrarySnapshotMapper.Count(LibrarySnapshotMapper.FromMaterial(LibraryTestData.Material(_author)));

        counts.ShouldBe(new LibraryCounts(Notes: 2, Questions: 2, Dictionaries: 1, Words: 2));
    }

    [Fact]
    public void Snapshot_SurvivesStorageRoundTrip()
    {
        var snapshot = LibrarySnapshotMapper.FromMaterial(LibraryTestData.Material(_author));

        var restored = LibrarySnapshotJson.FromDocument(LibrarySnapshotJson.ToDocument(snapshot));

        restored.Notes.Count.ShouldBe(2);
        restored.Questions[1].Options.Single(x => x.IsCorrect).Name.ShouldBe("Oxygen");
        restored.Dictionaries[0].Entries[1].Synonyms.ShouldBe(["matrix"]);
        restored.Notes[0].TextContent!.Value.GetRawText().ShouldContain("ATP");
    }

    [Fact]
    public void Import_CreatesReadersOwnMaterialWithLinksRemapped()
    {
        var itemId = Guid.NewGuid();
        var snapshot = LibrarySnapshotJson.FromDocument(
            LibrarySnapshotJson.ToDocument(LibrarySnapshotMapper.FromMaterial(LibraryTestData.Material(_author))));

        var copy = LibrarySnapshotMapper.ToMaterial(snapshot, itemId, _reader, DateTime.UtcNow);

        copy.UserId.ShouldBe(_reader);
        copy.ImportedFromLibraryItemId.ShouldBe(itemId);
        copy.ImportedAt.ShouldNotBeNull();
        copy.Status.ShouldBe(MaterialStatus.tolearn);
        copy.Assessments.Count.ShouldBe(1);
        copy.Notes.ShouldAllBe(x => x.UserId == _reader && x.MaterialId == copy.Id);
        copy.Notes.Single(x => x.Type == NoteType.drawing).DrawingContent.ShouldNotBeNull();

        var textNote = copy.Notes.Single(x => x.Title == "Light phase");
        copy.Questions.Single(x => x.Title == "What does it make?").NoteId.ShouldBe(textNote.Id);

        var word = copy.Dictionaries.Single().Entries.Single(x => x.Term == "chlorophyll");
        copy.Questions.Single(x => x.Title == "chlorophyll").DictionaryEntryId.ShouldBe(word.Id);
        copy.Questions.Single(x => x.Title == "Products?").Options.Count.ShouldBe(2);
    }
}
