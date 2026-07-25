using StServer.Application.DTOs.Attempt;
using StServer.Application.Interfaces;
using StServer.Application.ScoreCalculators;

namespace StServer.Application.Services;

public class AttemptScoringService: IAttemptScoringService
{
    private readonly IAverageScoreCalculator _averageScoreCalculator;
    private readonly INonLinearDifficultyScoreCalculator _nonLinearDifficultyScoreCalculator;
    private readonly ISuccessBonusScoreCalculator _successBonusScoreCalculator;

    public AttemptScoringService(
        IAverageScoreCalculator averageScoreCalculator,
        INonLinearDifficultyScoreCalculator nonLinearDifficultyScoreCalculator,
        ISuccessBonusScoreCalculator successBonusScoreCalculator
        )
    {
        _averageScoreCalculator = averageScoreCalculator;
        _nonLinearDifficultyScoreCalculator = nonLinearDifficultyScoreCalculator;
        _successBonusScoreCalculator = successBonusScoreCalculator;
    }

    public void AverageScoreAttempt(AttemptResponseDto attempt)
    {
        attempt.Score = _averageScoreCalculator.Calculate(attempt.Results);
    }
    
    public void NonLinearScoreAttempt(AttemptResponseDto attempt)
    {
        attempt.Score = _nonLinearDifficultyScoreCalculator.Calculate(attempt.Results);
    }
    
    public void SuccessBonusScoreAttempt(AttemptResponseDto attempt)
    {
        attempt.Score = _successBonusScoreCalculator.Calculate(attempt.Results);
    }
}