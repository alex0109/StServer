using Application.DTOs.Podcast;
using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Podcast;

namespace Application.Podcasts;

public static class PodcastMapper
{
    public static PodcastDto ToDto(MaterialPodcast podcast, IAudioStorage storage, DateTime now, bool isContentTruncated = false)
    {
        var script = PodcastScript.FromDocument(podcast.Script);
        var status = StatusAt(podcast, now);

        double start = 0;
        var chapters = script.Chapters.Select((chapter, index) =>
        {
            var duration = index < podcast.ChapterDurations.Length ? podcast.ChapterDurations[index] : 0;
            var dto = new PodcastChapterDto
            {
                Title = chapter.Title,
                NoteId = chapter.NoteId,
                StartSeconds = Math.Round(start, 2),
                DurationSeconds = Math.Round(duration, 2),
                Lines = chapter.Lines.Select(line => new PodcastLineDto { Speaker = line.Speaker, Text = line.Text }).ToList(),
            };
            start += duration;
            return dto;
        }).ToList();

        return new PodcastDto
        {
            Id = podcast.Id,
            Status = status,
            Language = podcast.Language,
            Chapters = chapters,
            DurationSeconds = Math.Round(podcast.DurationSeconds, 2),
            AudioUrl = status == PodcastStatus.Ready && podcast.AudioPath is not null
                ? storage.GetReadUrl(podcast.AudioPath, PodcastRules.AudioLinkLifetime).ToString()
                : null,
            IsContentTruncated = isContentTruncated,
            CreatedAt = podcast.CreatedAt,
            CompletedAt = podcast.CompletedAt,
        };
    }

    // A recording that never finished is shown as failed, so it can be started again
    public static PodcastStatus StatusAt(MaterialPodcast podcast, DateTime now) =>
        podcast.Status == PodcastStatus.Generating && now - podcast.UpdatedAt > PodcastRules.StaleAfter
            ? PodcastStatus.Failed
            : podcast.Status;
}
