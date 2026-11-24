using Domain.Utility.Ai;

namespace Application.Ai.Pricing;

// Study AI (text) and Media (podcasts, video) are shown and confirmed apart: media cost far more
public enum AiBucket
{
    Study = 1,
    Media = 2,
}

public static class AiBuckets
{
    public static AiBucket Of(AiFeature feature) =>
        feature is AiFeature.PodcastGeneration or AiFeature.VideoTranscript ? AiBucket.Media : AiBucket.Study;
}
