using StServer.Application.DTOs.Result;
using StServer.Application.Interfaces;

namespace StServer.Application.ScoreCalculators;

public interface ISuccessBonusScoreCalculator : IScoreCalculator { }

public class SuccessBonusScoreCalculator : ISuccessBonusScoreCalculator
{
    private readonly double _bonusFactor;

    public double MaxScore => 100;

    public SuccessBonusScoreCalculator(double bonusFactor = 0.5)
    {
        _bonusFactor = bonusFactor;
    }

    public double Calculate(IEnumerable<ResultResponseDto> results)
    {
        var list = results?.ToList() ?? new List<ResultResponseDto>();
        int n = list.Count;
        if (n == 0)
            return 0;

        double basePointsPerQuestion = MaxScore / n;

        double totalEarned = 0;
        double totalPossible = 0;

        foreach (var r in list)
        {
            double bonus = basePointsPerQuestion * (r.Weight - 1) * _bonusFactor;
            double maxForQuestion = basePointsPerQuestion + bonus;

            totalPossible += maxForQuestion;

            if (r.IsCorrect)
            {
                totalEarned += maxForQuestion;
            }
        }

        if (totalPossible <= 0)
            return 0;

        double normalized = totalEarned / totalPossible;

        return Math.Round(Math.Clamp(normalized * MaxScore, 0, MaxScore), 2);
    }
}