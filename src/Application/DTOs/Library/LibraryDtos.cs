using Application.Library;
using Domain.Utility.Library;
using Domain.Utility.Material;

namespace Application.DTOs.Library;

public class LibraryItemSummaryDto
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public MaterialType Type { get; set; }
    public required string AuthorNickname { get; set; }
    public int NotesCount { get; set; }
    public int QuestionsCount { get; set; }
    public int DictionariesCount { get; set; }
    public int WordsCount { get; set; }
    public int ImportsCount { get; set; }
    public DateTime PublishedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    // The current user is its author
    public bool IsMine { get; set; }
    // The current user already has a copy
    public bool IsImported { get; set; }
}

public class LibraryItemDto : LibraryItemSummaryDto
{
    public required LibrarySnapshot Content { get; set; }
    // The author's request about this item waiting for review, if any
    public LibraryRequestType? PendingRequest { get; set; }
}

public class LibraryPageDto
{
    public List<LibraryItemSummaryDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class LibraryRequestDto
{
    public Guid Id { get; set; }
    public LibraryRequestType Type { get; set; }
    public LibraryRequestStatus Status { get; set; }
    public Guid? MaterialId { get; set; }
    public Guid? LibraryItemId { get; set; }
    public required string Title { get; set; }
    public MaterialType MaterialType { get; set; }
    public required string AuthorNickname { get; set; }
    public string? Comment { get; set; }
    public LibraryRejectionReason? RejectionReason { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

// For the reviewer: who sent it and exactly what would be published
public class LibraryRequestDetailsDto : LibraryRequestDto
{
    public Guid UserId { get; set; }
    public LibrarySnapshot? Content { get; set; }
}

public class LibraryRequestCreateDto
{
    public LibraryRequestType Type { get; set; }
    // Publish and update: the author's material to copy
    public Guid? MaterialId { get; set; }
    // Update and remove: the library material it is about
    public Guid? LibraryItemId { get; set; }
    // Shown as the author; taken from the library material for removals
    public string? AuthorNickname { get; set; }
    public string? Comment { get; set; }
}

public class LibraryRejectDto
{
    public LibraryRejectionReason Reason { get; set; } = LibraryRejectionReason.Other;
    public string? Comment { get; set; }
}

public class LibraryAdminPublishDto
{
    public Guid MaterialId { get; set; }
    public string? AuthorNickname { get; set; }
}
