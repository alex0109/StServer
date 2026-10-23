namespace Application.Schedulers.Fsrs;

public static class FsrsDefaults
{
    // FSRS-5 default weights, w[0..18]
    public static readonly double[] Parameters =
    [
        0.40255, 1.18385, 3.173, 15.69105,
        7.1949, 0.5345, 1.4604, 0.0046,
        1.54575, 0.1192, 1.01925,
        1.9395, 0.11, 0.29605, 2.2698,
        0.2315, 2.9898,
        0.51655, 0.6621
    ];

    public static readonly int[] LearningStepsMinutes = [1, 10];
    public static readonly int[] RelearningStepsMinutes = [10];
}
