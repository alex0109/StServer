using Application.Podcasts;

namespace Application.Interfaces;

// Text to speech. Every call returns MP3 of the same format, so recordings can be joined
public interface ISpeechSynthesizer
{
    // False without a key: podcasts are then switched off
    bool IsConfigured { get; }

    // Lines of a conversation in one language, each in its speaker's voice
    Task<byte[]> SynthesizeAsync(string language, IReadOnlyList<PodcastLine> lines, CancellationToken cancellationToken = default);
}
