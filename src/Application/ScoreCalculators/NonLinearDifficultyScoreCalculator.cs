using Application.DTOs.Result;
using Application.Interfaces;
using Domain.Utility.Question;

namespace Application.ScoreCalculators;

public interface IDifficultyWeightProvider
{
    double GetWeight(QuestionDifficulty difficulty);
}

public class ExponentialDifficultyWeightProvider : IDifficultyWeightProvider
{
    public double GetWeight(QuestionDifficulty difficulty)
        => Math.Pow(2, (int)difficulty - 1);
}

public interface INonLinearDifficultyScoreCalculator : IScoreCalculator { }

public class NonLinearDifficultyScoreCalculator : INonLinearDifficultyScoreCalculator
{
    private readonly IDifficultyWeightProvider _weightProvider;

    public double MaxScore => 200;

    public NonLinearDifficultyScoreCalculator(IDifficultyWeightProvider weightProvider)
    {
        _weightProvider = weightProvider;
    }

    public double Calculate(IEnumerable<ResultResponseDto> results)
    {
        var list = results?.ToList() ?? new List<ResultResponseDto>();
        if (list.Count == 0)
            return 0;

        double totalWeight = 0;
        double weightedSum = 0;

        foreach (var r in list)
        {
            var difficulty = (QuestionDifficulty)r.Weight;
            double w = _weightProvider.GetWeight(difficulty);

            totalWeight += w;
            weightedSum += r.Score * w;
        }

        if (totalWeight <= 0)
            return 0;

        double weightedAveragePercent = weightedSum / totalWeight;
        double normalized = weightedAveragePercent / 100.0;

        return Math.Round(Math.Clamp(normalized * MaxScore, 0, MaxScore), 2);
    }
}