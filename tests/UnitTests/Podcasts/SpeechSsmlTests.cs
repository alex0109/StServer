using Application.Podcasts;
using Infrastructure.Podcasts;
using Shouldly;

namespace UnitTests.Podcasts;

public class SpeechSsmlTests
{
    [Fact]
    public void Build_GivesEachSpeakerTheirVoice_AndEscapesText()
    {
        var ssml = SpeechSsml.Build("uk",
        [
            new PodcastLine(PodcastSpeaker.Host, "Привіт <voice name=\"x\">!"),
            new PodcastLine(PodcastSpeaker.Guest, "A & B"),
        ]);

        ssml.ShouldContain("xml:lang=\"uk-UA\"");
        ssml.ShouldContain("<voice name=\"uk-UA-PolinaNeural\">Привіт &lt;voice name=&quot;x&quot;&gt;!");
        ssml.ShouldContain("<voice name=\"uk-UA-OstapNeural\">A &amp; B");
    }

    [Fact]
    public void EveryPodcastLanguage_HasVoices()
    {
        foreach (var language in Application.Profile.ProfileLanguages.Explanation.Keys)
        {
            PodcastRules.IsLanguage(language).ShouldBeTrue();
            SpeechVoices.For(language).Host.ShouldNotBeNullOrWhiteSpace();
        }
    }
}
