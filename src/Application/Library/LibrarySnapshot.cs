using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.RichTextDocument;
using Domain.Utility.Dictionary;
using Domain.Utility.Material;
using Domain.Utility.Note;
using Domain.Utility.Question;

namespace Application.Library;

// Everything a library material shows and an import recreates, frozen at the moment it was sent.
// Content keeps the editor JSON as it was; notes and dictionary words get small local keys,
// so questions can point at them without revealing the author's ids
public class LibrarySnapshot
{
    public required string Title { get; set; }
    public required MaterialType Type { get; set; }
    public string? Link { get; set; }
    public JsonElement? Content { get; set; }
    public List<LibraryNoteSnapshot> Notes { get; set; } = [];
    public List<LibraryQuestionSnapshot> Questions { get; set; } = [];
    public List<LibraryDictionarySnapshot> Dictionaries { get; set; } = [];
}

public class LibraryNoteSnapshot
{
    public int Key { get; set; }
    public required string Title { get; set; }
    public NoteType Type { get; set; }
    public JsonElement? TextContent { get; set; }
    public JsonElement? DrawingContent { get; set; }
}

public class LibraryQuestionSnapshot
{
    public required string Title { get; set; }
    public string? Answer { get; set; }
    public QuestionType QuestionType { get; set; }
    public QuestionDifficulty QuestionDifficulty { get; set; }
    public string? Explanation { get; set; }
    public int? NoteKey { get; set; }
    public int? EntryKey { get; set; }
    public List<LibraryOptionSnapshot> Options { get; set; } = [];
}

public class LibraryOptionSnapshot
{
    public required string Name { get; set; }
    public int Position { get; set; }
    public bool IsCorrect { get; set; }
}

public class LibraryDictionarySnapshot
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public List<LibraryEntrySnapshot> Entries { get; set; } = [];
}

public class LibraryEntrySnapshot
{
    public int Key { get; set; }
    public required string Term { get; set; }
    public required string Translation { get; set; }
    public string? Transcription { get; set; }
    public PartOfSpeech? PartOfSpeech { get; set; }
    public string? GrammarInfo { get; set; }
    public string? Example { get; set; }
    public string? ExampleTranslation { get; set; }
    public string? Note { get; set; }
    public List<string> Synonyms { get; set; } = [];
    public int Position { get; set; }
}

public static class LibrarySnapshotJson
{
    // Same shape as the API answers: camelCase and enum names
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static JsonDocument ToDocument(LibrarySnapshot snapshot) =>
        JsonSerializer.SerializeToDocument(snapshot, Options);

    // Snapshots sent before the shapes were aligned hold database-shaped text: it's turned into
    // the editor's shape here, so every reader gets text the editor can open
    public static LibrarySnapshot FromDocument(JsonDocument document)
    {
        var snapshot = document.Deserialize<LibrarySnapshot>(Options)
                       ?? throw new InvalidOperationException("Library snapshot is empty");

        snapshot.Content = ToClient(snapshot.Content);
        foreach (var note in snapshot.Notes)
            note.TextContent = ToClient(note.TextContent);

        return snapshot;
    }

    private static JsonElement? ToClient(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } value ? RichTextJson.ToClient(value) : null;
}
