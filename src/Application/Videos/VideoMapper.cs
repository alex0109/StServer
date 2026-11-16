using Application.DTOs.Video;
using Domain.Entities;

namespace Application.Videos;

public static class VideoMapper
{
    public static MaterialVideoDto ToDto(MaterialVideo video) => new(
        video.VideoId,
        video.Language,
        VideoTranscript.FromDocument(video.Transcript).Segments.Select(x => new VideoSegmentDto(x.Start, x.Text)).ToList(),
        video.IsClipped,
        video.UpdatedAt);
}
