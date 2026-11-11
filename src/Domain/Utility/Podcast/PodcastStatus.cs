namespace Domain.Utility.Podcast;

public enum PodcastStatus
{
    // The script is written, the voices are being recorded in the background
    Generating = 0,
    Ready = 1,
    Failed = 2,
}
