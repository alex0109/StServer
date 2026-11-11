using System.Security;
using System.Text;
using Application.Podcasts;

namespace Infrastructure.Podcasts;

// One SSML document for a run of conversation lines, each in its speaker's voice
public static class SpeechSsml
{
    // A short breath between speakers sounds like a conversation, not a list
    private const string Pause = "<break time=\"250ms\"/>";

    public static string Build(string language, IReadOnlyList<PodcastLine> lines)
    {
        var voices = SpeechVoices.For(language);
        var builder = new StringBuilder();
        builder.Append($"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"{voices.Locale}\">");

        foreach (var line in lines)
        {
            var voice = line.Speaker == PodcastSpeaker.Guest ? voices.Guest : voices.Host;
            // Text from the model is data: escaped, it can't add SSML tags of its own
            builder.Append($"<voice name=\"{voice}\">{SecurityElement.Escape(line.Text)}{Pause}</voice>");
        }

        builder.Append("</speak>");
        return builder.ToString();
    }
}
