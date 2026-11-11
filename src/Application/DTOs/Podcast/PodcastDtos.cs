using Application.Podcasts;
using Domain.Utility.Podcast;

namespace Application.DTOs.Podcast;

public class PodcastGenerateDto
{
    // Language code from the explanation languages, e.g. "uk"
    public string? Language { get; set; }
}

public class PodcastDto
{
    public Guid Id { get; init; }
    public PodcastStatus Status { get; init; }
    public required string Language { get; init; }
    public required List<PodcastChapterDto> Chapters { get; init; }
    public double DurationSeconds { get; init; }
    // A short-lived link to the MP3; only when it is ready
    public string? AudioUrl { get; init; }
    public bool IsContentTruncated { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public class PodcastChapterDto
{
    public required string Title { get; init; }
    public Guid? NoteId { get; init; }
    public double StartSeconds { get; init; }
    public double DurationSeconds { get; init; }
    public required List<PodcastLineDto> Lines { get; init; }
}

public class PodcastLineDto
{
    public PodcastSpeaker Speaker { get; init; }
    public required string Text { get; init; }
}
