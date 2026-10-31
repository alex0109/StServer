using Domain.Entities;

namespace Application.Statistics;

// Same formula as the score on the results page (AverageScoreCalculator):
// answers weighted by difficulty, hard questions count three times
public static class AttemptScore
{
    public static double Weighted(IReadOnlyCollection<Result> results)
    {
        var totalWeight = results.Sum(r => r.Weight);
        if (results.Count == 0 || totalWeight <= 0) return 0;

        return Math.Clamp(results.Sum(r => r.Score * r.Weight) / totalWeight, 0, 100);
    }
}
