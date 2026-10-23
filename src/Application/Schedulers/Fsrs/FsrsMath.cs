using Domain.Utility.Review;

namespace Application.Schedulers.Fsrs;

// FSRS-5 memory model: stability S (days until recall drops to 90%),
// difficulty D (1..10) and retrievability R (probability of recall now)
public static class FsrsMath
{
    public const int ParametersCount = 19;
    public const double Decay = -0.5;
    public const double MinStability = 0.001;
    public const double MinDifficulty = 1;
    public const double MaxDifficulty = 10;

    // Chosen so that R(S, S) = 0.9
    public static readonly double Factor = Math.Pow(0.9, 1 / Decay) - 1;

    public static double Retrievability(double elapsedDays, double stability) =>
        Math.Pow(1 + Factor * elapsedDays / stability, Decay);

    public static double InitialStability(double[] w, ReviewRating rating) =>
        Math.Max(w[(int)rating - 1], MinStability);

    public static double InitialDifficulty(double[] w, ReviewRating rating) =>
        ClampDifficulty(w[4] - Math.Exp(w[5] * ((int)rating - 1)) + 1);

    public static double NextDifficulty(double[] w, double difficulty, ReviewRating rating)
    {
        var delta = -w[6] * ((int)rating - 3);
        // Linear damping: changes get smaller near the maximum difficulty
        var damped = difficulty + delta * (MaxDifficulty - difficulty) / 9;
        // Mean reversion towards the difficulty of an easy first answer
        var reverted = w[7] * InitialDifficulty(w, ReviewRating.Easy) + (1 - w[7]) * damped;

        return ClampDifficulty(reverted);
    }

    // Reviews on the same day
    public static double ShortTermStability(double[] w, double stability, ReviewRating rating) =>
        Math.Max(stability * Math.Exp(w[17] * ((int)rating - 3 + w[18])), MinStability);

    public static double NextStability(
        double[] w, double difficulty, double stability, double retrievability, ReviewRating rating) =>
        rating == ReviewRating.Again
            ? NextForgetStability(w, difficulty, stability, retrievability)
            : NextRecallStability(w, difficulty, stability, retrievability, rating);

    public static double NextRecallStability(
        double[] w, double difficulty, double stability, double retrievability, ReviewRating rating)
    {
        var hardPenalty = rating == ReviewRating.Hard ? w[15] : 1;
        var easyBonus = rating == ReviewRating.Easy ? w[16] : 1;

        var growth = Math.Exp(w[8])
                     * (11 - difficulty)
                     * Math.Pow(stability, -w[9])
                     * (Math.Exp((1 - retrievability) * w[10]) - 1)
                     * hardPenalty
                     * easyBonus;

        return Math.Max(stability * (1 + growth), MinStability);
    }

    public static double NextForgetStability(
        double[] w, double difficulty, double stability, double retrievability)
    {
        var longTerm = w[11]
                       * Math.Pow(difficulty, -w[12])
                       * (Math.Pow(stability + 1, w[13]) - 1)
                       * Math.Exp((1 - retrievability) * w[14]);
        // A lapse never leaves the card more stable than it was
        var shortTerm = stability / Math.Exp(w[17] * w[18]);

        return Math.Max(Math.Min(longTerm, shortTerm), MinStability);
    }

    public static int NextIntervalDays(double stability, double desiredRetention, int maximumIntervalDays)
    {
        var interval = stability / Factor * (Math.Pow(desiredRetention, 1 / Decay) - 1);

        return Math.Clamp((int)Math.Round(interval), 1, maximumIntervalDays);
    }

    private static double ClampDifficulty(double difficulty) =>
        Math.Clamp(difficulty, MinDifficulty, MaxDifficulty);
}
