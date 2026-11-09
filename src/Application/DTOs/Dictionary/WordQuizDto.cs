using Domain.Utility.Dictionary;

namespace Application.DTOs.Dictionary;

// A practice check, not a grade: the right choice is sent along and nothing is saved
public class WordQuizDto
{
    public Guid EntryId { get; init; }
    public WordQuizKind Kind { get; init; }
    public required string Term { get; init; }
    public string? Article { get; init; }
    // In the order to show; empty when the user has no other words to mix in
    public List<WordQuizOptionDto> Options { get; init; } = [];
}

public class WordQuizOptionDto
{
    public required string Text { get; init; }
    public bool IsCorrect { get; init; }
}
