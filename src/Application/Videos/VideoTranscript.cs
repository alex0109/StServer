using System.Text;
using System.Text.Json;

namespace Application.Videos;

// One line of the subtitles and the second it starts at
public record VideoSegment(double Start, string Text);

public record VideoTranscript(IReadOnlyList<VideoSegment> Segments)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public JsonDocument ToDocument() => JsonSerializer.SerializeToDocument(this, Json);

    public static VideoTranscript FromDocument(JsonDocument document) =>
        document.Deserialize<VideoTranscript>(Json) ?? new VideoTranscript([]);

    // "[03:12] text" per line: the source questions are written from, the times let the AI
    // point every question to its moment in the video
    public string ToSourceText()
    {
        var text = new StringBuilder();
        foreach (var segment in Segments)
            text.Append('[').Append(VideoTime.Format(segment.Start)).Append("] ").AppendLine(segment.Text);
        return text.ToString();
    }
}
