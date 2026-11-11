using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Podcasts;

// The two voices of the podcast: the host explains, the guest asks what a student would ask
public enum PodcastSpeaker
{
    Host,
    Guest,
}

public record PodcastLine(PodcastSpeaker Speaker, string Text);

// One chapter of the timeline: the material itself (no note) or one of its notes
public record PodcastChapter(string Title, Guid? NoteId, IReadOnlyList<PodcastLine> Lines);

public record PodcastScript(IReadOnlyList<PodcastChapter> Chapters)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public int Characters => Chapters.Sum(chapter => chapter.Lines.Sum(line => line.Text.Length));

    public JsonDocument ToDocument() => JsonSerializer.SerializeToDocument(this, Json);

    public static PodcastScript FromDocument(JsonDocument document) =>
        document.Deserialize<PodcastScript>(Json) ?? new PodcastScript([]);
}
