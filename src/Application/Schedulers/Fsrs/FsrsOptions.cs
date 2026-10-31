namespace Application.Schedulers.Fsrs;

public class FsrsOptions
{
    // Probability of recall the intervals aim for
    public double DesiredRetention { get; set; } = 0.9;
    public int MaximumIntervalDays { get; set; } = 36500;
    public bool EnableFuzzing { get; set; } = true;

    // Null means defaults. Arrays are nullable on purpose: the configuration binder
    // appends to collections that already have items instead of replacing them
    public double[]? Parameters { get; set; }
    public int[]? LearningStepsMinutes { get; set; }
    public int[]? RelearningStepsMinutes { get; set; }
}
