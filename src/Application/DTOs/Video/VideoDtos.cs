namespace Application.DTOs.Video;

public record VideoSegmentDto(double Start, string Text);

// Subtitles of the material's YouTube video
public record MaterialVideoDto(
    string VideoId,
    string Language,
    IReadOnlyList<VideoSegmentDto> Segments,
    bool IsClipped,
    DateTime UpdatedAt);
