using Application.Interfaces;
using Application.Podcasts;
using Domain.Entities;
using Domain.Utility.Podcast;
using Microsoft.Extensions.Logging;

namespace Application.Jobs;

// Records the saved script chapter by chapter, joins the parts into one MP3 and remembers
// where every chapter starts. A failure is stored instead of retried: voices cost money,
// and the user can start the recording again
public class PodcastAudioJob
{
    // One speech request stays well under the services' limits on length and voices
    public const int MaxCharactersPerRequest = 3500;
    public const int MaxLinesPerRequest = 30;

    private readonly IPodcastRepository _podcasts;
    private readonly ISpeechSynthesizer _speech;
    private readonly IAudioStorage _storage;
    private readonly TimeProvider _time;
    private readonly ILogger<PodcastAudioJob> _logger;

    public PodcastAudioJob(
        IPodcastRepository podcasts,
        ISpeechSynthesizer speech,
        IAudioStorage storage,
        TimeProvider time,
        ILogger<PodcastAudioJob> logger)
    {
        _podcasts = podcasts;
        _speech = speech;
        _storage = storage;
        _time = time;
        _logger = logger;
    }

    public async Task Run(Guid podcastId)
    {
        var podcast = await _podcasts.GetByIdAsync(podcastId);
        if (podcast is null || podcast.Status != PodcastStatus.Generating) return;

        var script = PodcastScript.FromDocument(podcast.Script);
        var previousPath = podcast.AudioPath;

        // Tracked now and saved with the podcast's final state, whichever way the recording ends
        var usage = new SpeechUsage
        {
            Id = Guid.NewGuid(),
            UserId = podcast.UserId,
            PodcastId = podcast.Id,
            Language = podcast.Language,
            CreatedAt = _time.GetUtcNow().UtcDateTime,
        };
        await _podcasts.AddSpeechUsageAsync(usage);

        try
        {
            var parts = new List<byte[]>();
            var durations = new List<double>();

            foreach (var chapter in script.Chapters)
            {
                double chapterSeconds = 0;
                foreach (var batch in Batches(chapter.Lines))
                {
                    var audio = await _speech.SynthesizeAsync(podcast.Language, batch);
                    usage.Characters += batch.Sum(line => line.Text.Length);
                    chapterSeconds += Mp3Audio.Duration(audio);
                    parts.Add(audio);
                }

                durations.Add(chapterSeconds);
            }

            var file = Mp3Audio.Join(parts);
            var path = PodcastRules.AudioPath(podcast.UserId, podcast.Id, podcast.CreatedAt);
            await _storage.UploadAsync(path, file, "audio/mpeg");

            podcast.AudioPath = path;
            podcast.AudioSize = file.Length;
            podcast.ChapterDurations = durations.ToArray();
            podcast.DurationSeconds = durations.Sum();
            podcast.Status = PodcastStatus.Ready;
            podcast.CompletedAt = _time.GetUtcNow().UtcDateTime;
            podcast.UpdatedAt = podcast.CompletedAt.Value;
            usage.Succeeded = true;
            await _podcasts.SaveChangesAsync();

            // The replaced recording goes only once the new one is safely stored
            if (previousPath is not null && previousPath != path)
                await DeleteQuietly(previousPath);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Podcast {PodcastId} could not be recorded", podcastId);
            usage.Succeeded = false;
            podcast.Status = PodcastStatus.Failed;
            podcast.Error = exception.Message.Length > 500 ? exception.Message[..500] : exception.Message;
            podcast.UpdatedAt = _time.GetUtcNow().UtcDateTime;
            await _podcasts.SaveChangesAsync();
        }
    }

    // Lines grouped into requests by size; a line is never split
    public static IEnumerable<IReadOnlyList<PodcastLine>> Batches(IReadOnlyList<PodcastLine> lines)
    {
        var batch = new List<PodcastLine>();
        var characters = 0;

        foreach (var line in lines)
        {
            if (batch.Count > 0 && (characters + line.Text.Length > MaxCharactersPerRequest || batch.Count == MaxLinesPerRequest))
            {
                yield return batch;
                batch = [];
                characters = 0;
            }

            batch.Add(line);
            characters += line.Text.Length;
        }

        if (batch.Count > 0) yield return batch;
    }

    private async Task DeleteQuietly(string path)
    {
        try
        {
            await _storage.DeleteAsync(path);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Old podcast audio {Path} was not deleted", path);
        }
    }
}
