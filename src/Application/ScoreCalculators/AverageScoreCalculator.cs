using Application.DTOs.Result;
using Application.Interfaces;

namespace Application.ScoreCalculators;

public interface IAverageScoreCalculator : IScoreCalculator { }

public class AverageScoreCalculator : IAverageScoreCalculator
{
    public double MaxScore => 100;

    public double Calculate(IEnumerable<ResultResponseDto> results)
    {
        var list = results?.ToList() ?? new List<ResultResponseDto>();
        if (list.Count == 0)
            return 0;

        double totalWeight = list.Sum(r => r.Weight);
        if (totalWeight <= 0)
            return 0;

        double weightedSum = list.Sum(r => r.Score * r.Weight);
        double weightedAverage = weightedSum / totalWeight;

        return Math.Round(Math.Clamp(weightedAverage, 0, MaxScore), 2);
    }
}