using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Domain.Utility.Library;
using Domain.Utility.Material;

namespace Domain.Entities;

// What an author asks the library owner: publish a material, replace a published one
// with a new snapshot, or take it down. The snapshot is taken when the request is sent
public class LibraryRequest
{
    public Guid Id { get; set; }
    public required Guid UserId { get; set; }
    public required LibraryRequestType Type { get; set; }
    public LibraryRequestStatus Status { get; set; } = LibraryRequestStatus.Pending;

    public Guid? MaterialId { get; set; }
    public Guid? LibraryItemId { get; set; }

    [MaxLength(70)]
    public required string Title { get; set; }
    public required MaterialType MaterialType { get; set; }

    [MaxLength(40)]
    public required string AuthorNickname { get; set; }

    // Note from the author to the reviewer
    [MaxLength(500)]
    public string? Comment { get; set; }

    public LibraryRejectionReason? RejectionReason { get; set; }

    // Optional words from the reviewer on top of the reason
    [MaxLength(500)]
    public string? ReviewComment { get; set; }

    // Publish and update requests carry the content to put into the library
    public JsonDocument? Snapshot { get; set; }

    public required DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
}
