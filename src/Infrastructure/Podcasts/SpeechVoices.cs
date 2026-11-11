namespace Infrastructure.Podcasts;

// Two voices per language, a woman hosts and a man asks. Names are Azure neural voices
public static class SpeechVoices
{
    public record Pair(string Locale, string Host, string Guest);

    private static readonly IReadOnlyDictionary<string, Pair> Pairs = new Dictionary<string, Pair>
    {
        ["uk"] = new("uk-UA", "uk-UA-PolinaNeural", "uk-UA-OstapNeural"),
        ["en-GB"] = new("en-GB", "en-GB-SoniaNeural", "en-GB-RyanNeural"),
        ["nb"] = new("nb-NO", "nb-NO-PernilleNeural", "nb-NO-FinnNeural"),
        ["de"] = new("de-DE", "de-DE-KatjaNeural", "de-DE-ConradNeural"),
        ["fr"] = new("fr-FR", "fr-FR-DeniseNeural", "fr-FR-HenriNeural"),
        ["es"] = new("es-ES", "es-ES-ElviraNeural", "es-ES-AlvaroNeural"),
        ["it"] = new("it-IT", "it-IT-ElsaNeural", "it-IT-DiegoNeural"),
        ["pl"] = new("pl-PL", "pl-PL-ZofiaNeural", "pl-PL-MarekNeural"),
        ["pt"] = new("pt-BR", "pt-BR-FranciscaNeural", "pt-BR-AntonioNeural"),
        ["sv"] = new("sv-SE", "sv-SE-SofieNeural", "sv-SE-MattiasNeural"),
        ["da"] = new("da-DK", "da-DK-ChristelNeural", "da-DK-JeppeNeural"),
    };

    public static Pair For(string language) =>
        Pairs.TryGetValue(language, out var pair) ? pair : throw new ArgumentException($"No voices for {language}");

    public static IEnumerable<string> Languages => Pairs.Keys;
}
