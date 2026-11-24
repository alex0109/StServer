namespace Application.Videos;

public static class VideoRules
{
    // Only this much of a video is processed: it bounds what one video costs
    public const int MaxSeconds = 30 * 60;
    // Frames per second the model looks at: the subtitles come from the sound, a frame every
    // two seconds is enough to tell who speaks and halves the cost of the picture
    public const double FramesPerSecond = 0.5;
    public const int MaxSegmentLength = 500;
    public const int MaxSegments = 1500;

    // Video quota and credits count started minutes of what is processed
    public static int Minutes(int seconds) => Math.Max(1, (Math.Min(seconds, MaxSeconds) + 59) / 60);
}
