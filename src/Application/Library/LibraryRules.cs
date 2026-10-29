using System.Text.Json;
using System.Text.RegularExpressions;
using Application.DTOs.Library;
using Application.Exceptions;
using Domain.Entities;

namespace Application.Library;

public static partial class LibraryRules
{
    // Requests waiting for review per author, so the queue can't be flooded
    public const int MaxPendingRequests = 5;
    public const int MaxCommentLength = 500;
    // Content with pasted images can be large; the library is not file storage
    public const int MaxSnapshotLength = 5_000_000;

    // Letters (any alphabet), digits, spaces and _ . ' -, starting with a letter or digit, 2 to 40 long
    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N} _.'\-]{1,39}$")]
    private static partial Regex NicknamePattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    public static string NormalizeNickname(string? value)
    {
        var nickname = Spaces().Replace(value?.Trim() ?? string.Empty, " ");

        // A name, not a link: authors are shown to every reader
        if (!NicknamePattern().IsMatch(nickname)
            || nickname.Contains("http", StringComparison.OrdinalIgnoreCase)
            || nickname.Contains("www.", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Nickname must be 2 to 40 letters, digits, spaces or _ . ' -");
        }

        return nickname;
    }

    public static string? NormalizeComment(string? value)
    {
        var comment = value?.Trim();
        if (string.IsNullOrEmpty(comment)) return null;
        if (comment.Length > MaxCommentLength)
            throw new BadRequestException($"Comment must be at most {MaxCommentLength} characters long");

        return comment;
    }

    public static JsonDocument ToCheckedDocument(LibrarySnapshot snapshot)
    {
        var document = LibrarySnapshotJson.ToDocument(snapshot);
        if (document.RootElement.GetRawText().Length > MaxSnapshotLength)
        {
            document.Dispose();
            throw new BadRequestException("The material is too large for the library");
        }

        return document;
    }

    public static LibraryRequestDto ToDto(LibraryRequest request) => new()
    {
        Id = request.Id,
        Type = request.Type,
        Status = request.Status,
        MaterialId = request.MaterialId,
        LibraryItemId = request.LibraryItemId,
        Title = request.Title,
        MaterialType = request.MaterialType,
        AuthorNickname = request.AuthorNickname,
        Comment = request.Comment,
        RejectionReason = request.RejectionReason,
        ReviewComment = request.ReviewComment,
        CreatedAt = request.CreatedAt,
        ReviewedAt = request.ReviewedAt,
    };

    public static LibraryItemSummaryDto ToSummaryDto(LibraryItem item, Guid userId, bool isImported) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Type = item.Type,
        AuthorNickname = item.AuthorNickname,
        NotesCount = item.NotesCount,
        QuestionsCount = item.QuestionsCount,
        DictionariesCount = item.DictionariesCount,
        WordsCount = item.WordsCount,
        ImportsCount = item.ImportsCount,
        PublishedAt = item.PublishedAt,
        UpdatedAt = item.UpdatedAt,
        IsMine = item.AuthorUserId == userId,
        IsImported = isImported,
    };

    // Takes the snapshot's title, type and counts into the item
    public static void Apply(LibraryItem item, LibrarySnapshot snapshot, JsonDocument document)
    {
        var counts = LibrarySnapshotMapper.Count(snapshot);

        item.Title = snapshot.Title;
        item.Type = snapshot.Type;
        item.Snapshot = document;
        item.NotesCount = counts.Notes;
        item.QuestionsCount = counts.Questions;
        item.DictionariesCount = counts.Dictionaries;
        item.WordsCount = counts.Words;
    }
}
