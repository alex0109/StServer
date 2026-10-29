using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Domain.Utility.Library;
using Domain.Utility.Material;

namespace Domain.Entities;

// A material in the global library. Its content is a frozen snapshot: nobody edits it,
// a change comes only as a new snapshot from an approved request
public class LibraryItem
{
    public Guid Id { get; set; }

    [MaxLength(70)]
    public required string Title { get; set; }
    public required MaterialType Type { get; set; }

    [MaxLength(40)]
    public required string AuthorNickname { get; set; }
    public required Guid AuthorUserId { get; set; }

    // The author's material it was copied from, no FK: the material may be deleted later
    public Guid? SourceMaterialId { get; set; }

    public required JsonDocument Snapshot { get; set; }

    public int NotesCount { get; set; }
    public int QuestionsCount { get; set; }
    public int DictionariesCount { get; set; }
    public int WordsCount { get; set; }
    public int ImportsCount { get; set; }

    public LibraryItemStatus Status { get; set; } = LibraryItemStatus.Published;
    public int Version { get; set; } = 1;

    public required DateTime PublishedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
    public DateTime? RemovedAt { get; set; }
}
